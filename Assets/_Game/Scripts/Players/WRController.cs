using System;
using UnityEngine;

namespace Gridiron
{
    public enum WRState { LinedUp, RunningRoute, BallPursuit, RunAfterCatch, Done }

    /// <summary>
    /// Wide receiver AI (PRD §8.2). Kinematic, script-driven transform.
    /// Audit pass: persistent-velocity locomotion (C2/H1), damped animator floats (C1/C3),
    /// real ball tracking + catch-in-stride (H4), dodge hysteresis in RAC (H6),
    /// HandsOnBall-gated catch (H7), tunables in MatchRules (L2), unsubscribes (L1).
    /// </summary>
    public class WRController : MonoBehaviour
    {
        public RouteRunner routeRunner;
        public CatchZone catchZone;
        public BallSocket socket;
        public CharacterAnimatorDriver animDriver;

        [Header("Scene refs (wired by MatchManager/scene builder)")]
        public Transform cbTransform;
        public Transform qbTransform;
        public Football ball;
        public FieldBounds field;
        [Header("WR catch (in-stride)")]
        [Tooltip("WR-only socket the caught ball attaches to (child of the hand bone). NOT the shared BallSocket.")]
        public Transform catchSocket;
        [Tooltip("Local position of the ball inside catchSocket (tune so it sits in the hands).")]
        public Vector3 catchGripOffset;
        [Tooltip("Contact frame in Football Catch where the ball meets the hands (authored HandsOnBall ≈ 37.5).")]
        [SerializeField] float catchContactFrame = 11f; // Goalkeeper Catch hand-contact frame (30fps clip)
        [Tooltip("Seconds for the ball to home into the catch socket on a confirmed catch (final approach into the hands, no teleport).")]
        [SerializeField] float catchApproachTime = 0.12f;
        [SerializeField] float catchClipFrameRate = 30f;
        [Tooltip("Faster SmoothDamp time used while turning to face the incoming ball during the catch.")]
        [SerializeField] float faceCatchSmoothTime = 0.08f;
        float catchTriggerAt = -1f;
        bool catchTriggered;
        bool catchFacing;

        public WRState State { get; private set; }
        public bool HasBall { get; private set; }

        /// <summary>(waypointIndex, isBreak) — forwarded from the route for CB reaction (§8.4).</summary>
        public event Action<int, bool> OnRouteWaypoint;

        readonly KinematicLocomotion loco = new KinematicLocomotion();

        ThrowData activeThrow;
        bool hasThrowData;
        bool catchAttempted;
        float lastBreakTime = -99f;
        float currentBaseSpeed = 7.5f;

        // RAC dodge state (audit H6)
        float dodgeSide;
        float dodgeRetimer;
        float dodgeLateral;

        // Two-zone catch flow (Catch PRD R3/R4): zone entry only ARMS the logic; the roll happens
        // when the ball is genuinely reachable, securing happens at hand contact.
        bool anticipationArmed;
        bool uncatchableThisThrow;
        Football anticipatedBall;

        // HandsOnBall catch gating (audit H7 + Catch PRD R5)
        Football pendingCatchBall;
        float handsFallbackTimer;
        bool handsEventFired;
        float lastSecureGap;

        static MatchRules fallbackRules;
        /// <summary>Null-safe: per-frame anim code must survive domain reloads / missing MatchManager.</summary>
        MatchRules Rules
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.rules != null) return mm.rules;
                if (fallbackRules == null) fallbackRules = ScriptableObject.CreateInstance<MatchRules>();
                return fallbackRules;
            }
        }
        public float CurrentBaseSpeed => currentBaseSpeed;
        public Vector3 Velocity => State == WRState.RunningRoute && routeRunner.Path != null
            ? routeRunner.Path.Direction * routeRunner.Path.CurrentSpeed
            : loco.Velocity;

        void Start()
        {
            if (routeRunner != null) routeRunner.OnWaypointReached += HandleWaypoint;
            if (catchZone != null) catchZone.OnBallEnter += HandleBallEnter;
            if (animDriver != null) animDriver.HandsOnBallEvent += HandleHandsOnBall;
        }

        void OnDestroy() // audit L1
        {
            if (routeRunner != null) routeRunner.OnWaypointReached -= HandleWaypoint;
            if (catchZone != null) catchZone.OnBallEnter -= HandleBallEnter;
            if (animDriver != null) animDriver.HandsOnBallEvent -= HandleHandsOnBall;
        }

        public void ResetForPlay(Vector3 position, RouteDefinition route)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(Vector3.forward);
            faceYawVel = 0f; // reset damped-yaw integrator so a stale swing can't carry into the new play
            catchTriggered = false; catchTriggerAt = -1f; catchFacing = false; // PART B/C: clear catch sync + face-ball override
            State = WRState.LinedUp;
            HasBall = false;
            hasThrowData = false;
            catchAttempted = false;
            anticipationArmed = false;
            uncatchableThisThrow = false;
            anticipatedBall = null;
            pendingCatchBall = null;
            handsEventFired = false;
            lastBreakTime = -99f;
            dodgeSide = 0f;
            dodgeRetimer = 0f;
            dodgeLateral = 0f;
            currentBaseSpeed = route != null ? route.baseSpeed : 7.5f;
            loco.Reset();
            routeRunner.StopRoute();
            if (catchZone != null)
            {
                catchZone.SetRadius(Rules.catchRadius);
                catchZone.Arm(); // audit M5: explicit one-shot latch per play
            }
            if (animDriver != null)
            {
                animDriver.floatDampTime = Rules.animSpeedDamp;
                animDriver.SetSpeed(0f);
                animDriver.ResetCatchOverlay(); // upper-body catch layer back to weight 0 for the new play
            }
        }

        public void OnSnapped(RouteDefinition route, bool mirror)
        {
            routeRunner.Begin(route, mirror, field);
            State = WRState.RunningRoute;
        }

        public void OnThrowNotify(ThrowData data)
        {
            activeThrow = data;
            hasThrowData = true;
            if (State == WRState.RunningRoute)
            {
                // Hand the route's momentum to the locomotion model — no velocity pop (audit C2).
                var p = routeRunner.Path;
                if (p != null) loco.ForceVelocity(p.Direction * p.CurrentSpeed);
                routeRunner.StopRoute();
                State = WRState.BallPursuit;
            }
            // PART B7 sync (method a): fire the catch animation early so its contact frame lands at ball arrival.
            float contactTime = catchContactFrame / Mathf.Max(1f, catchClipFrameRate);
            catchTriggerAt = data.launchTime + data.flightTime - contactTime;
            catchTriggered = false;
        }

        public void BeginRunAfterCatch()
        {
            // Play ENDS at the catch — do NOT run after the catch. Freeze the WR holding the ball
            // (in the pose/facing he caught it) instead of turning back into a run that fights the catch anim.
            State = WRState.Done;
            animDriver?.SetSpeed(0f);
        }

        public void EndPlay()
        {
            State = WRState.Done;
            animDriver?.SetSpeed(0f);
        }

        /// <summary>Audit M2: symmetric soft push — routed into the path while route-running so it isn't overwritten.</summary>
        public void ApplyPush(Vector3 offset)
        {
            offset.y = 0f;
            if (State == WRState.RunningRoute) routeRunner.Nudge(offset);
            else transform.position += offset;
        }

        /// <summary>Lead-solver hook (PRD §7.3): route prediction while running, current position otherwise.</summary>
        public Vector3 PredictFuturePosition(float tFuture)
        {
            if (State == WRState.RunningRoute && routeRunner.Path != null)
                return routeRunner.PredictPosition(tFuture);
            return transform.position;
        }

        /// <summary>Pre-cut throws are riskier (§7.3): approaching a break, or just made one.</summary>
        public bool IsMidBreak
        {
            get
            {
                if (Time.time - lastBreakTime < 0.3f) return true;
                var p = routeRunner.Path;
                return State == WRState.RunningRoute && p != null && p.NextWaypointIsBreak && p.DistanceToNextWaypoint < 1.5f;
            }
        }

        void HandleWaypoint(int index, bool isBreak)
        {
            if (isBreak)
            {
                lastBreakTime = Time.time;
                animDriver?.Trigger("Plant");
            }
            OnRouteWaypoint?.Invoke(index, isBreak);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.04f); // clamp dt spikes — kinematic movement must stay smooth
            TickCatchability(dt);
            TickPendingCatch(dt);
            // PART B7: trigger the catch reach early so the clip reaches its contact frame as the ball arrives.
            if (hasThrowData && !catchTriggered && catchTriggerAt >= 0f && Time.time >= catchTriggerAt
                && (State == WRState.BallPursuit || State == WRState.RunningRoute))
            {
                catchTriggered = true;
                animDriver?.Trigger("Catch");
                animDriver?.SetCatchOverlay(true);
                catchFacing = true; // PART C: start turning to face the incoming ball
            }


            switch (State)
            {
                case WRState.RunningRoute:
                {
                    routeRunner.Tick(dt);
                    var path = routeRunner.Path;
                    float speed = path != null ? path.CurrentSpeed : 0f;
                    UpdateAnim(speed, dt);
                    if (path != null && path.Holding && qbTransform != null)
                        FaceFlat(qbTransform.position - transform.position, speed, dt); // Curl/Comeback: face the QB (§6.2)
                    break;
                }

                case WRState.BallPursuit:
                    TickPursuit(dt);
                    break;

                case WRState.RunAfterCatch:
                    TickRunAfterCatch(dt);
                    break;

                case WRState.Done:
                    UpdateAnim(0f, dt);
                    break;
            }
        }

        // ---------- Ball pursuit (audit C2 + H4) ----------

        void TickPursuit(float dt)
        {
            if (!hasThrowData) return;
            var rules = Rules;

            // Catch PRD R2: the intent point is only a SEED for the first 20 % of flight — after
            // that the receiver converges on the live-projected arrival point A.
            Vector3 target = activeThrow.targetPoint;
            float ballEta = (activeThrow.launchTime + activeThrow.flightTime) - Time.time;
            if (ball != null && ball.State == BallState.InFlight && activeThrow.flightTime > 0.05f)
            {
                float seedWindow = 0.2f * activeThrow.flightTime;
                float t01 = Mathf.Clamp01((Time.time - activeThrow.launchTime) / Mathf.Max(0.01f, seedWindow));
                target = Vector3.Lerp(activeThrow.targetPoint, ProjectBallLanding(rules), t01);
            }
            target.y = 0f;

            Vector3 to = target - transform.position; to.y = 0f;
            float dist = to.magnitude;
            float maxSpeed = currentBaseSpeed * rules.wrBurstMultiplier; // burst (§8.2)

            Vector3 desired;
            if (dist > rules.pursuitStopDistance)
            {
                Vector3 dir = to / dist;
                // Catch-in-stride (H4): if the ball arrives when we do, run through the point;
                // only settle (arrive-brake) when we'd beat the ball there comfortably.
                float myEta = dist / Mathf.Max(0.1f, maxSpeed);
                float speed = (ballEta > 0f && myEta >= ballEta - 0.15f)
                    ? maxSpeed
                    : KinematicLocomotion.ArriveSpeed(dist - rules.pursuitStopDistance * 0.5f, maxSpeed, rules.brake);
                speed *= KinematicLocomotion.TurnSpeedFactor(loco.Velocity, dir, rules.minTurnSpeedFactor); // H1
                desired = dir * speed;
            }
            else
            {
                desired = Vector3.zero;
            }

            transform.position += loco.Tick(desired, rules.accel, rules.brake, dt);
            Vector3 p = field.ClampToField(transform.position, routeRunner.fieldMargin);
            p.y = 0f;
            transform.position = p;

            // Velocity-driven facing only while genuinely moving; below faceMinSpeed HOLD the last yaw
            // (a near-zero velocity has no meaningful direction, and chasing the moving ball twitches the body).
            if (catchFacing && ball != null)
                FaceFlat(ball.transform.position - transform.position, 999f, dt, faceCatchSmoothTime); // PART C: look back at the ball
            else if (loco.Speed > faceMinSpeed)
                FaceFlat(loco.Velocity, loco.Speed, dt);
            UpdateAnim(loco.Speed, dt);
        }

        /// <summary>Seconds until the ball crosses catch height on its descent. −1 if it never will (Catch PRD §3).</summary>
        static float TimeToCatchHeight(Vector3 ballPos, Vector3 ballVel, float catchHeight, float gravityScale)
        {
            float g = 9.81f * gravityScale;
            float disc = ballVel.y * ballVel.y + 2f * g * (ballPos.y - catchHeight);
            if (disc < 0f) return -1f;
            float t = (ballVel.y + Mathf.Sqrt(disc)) / g; // descending root
            return (t < 0f || t > 5f) ? -1f : t;
        }

        /// <summary>Project the in-flight ball forward to catch height under effective gravity (audit H4 / Catch PRD R2).</summary>
        Vector3 ProjectBallLanding(MatchRules rules)
        {
            Vector3 bp = ball.transform.position;
            Vector3 bv = ball.Velocity;
            float t = TimeToCatchHeight(bp, bv, rules.chestHeight, rules.gravityScale);
            if (t < 0f) return activeThrow.targetPoint;
            Vector3 land = bp + new Vector3(bv.x, 0f, bv.z) * t;
            land.y = 0f;
            return land;
        }

        // ---------- Run after catch (audit C2 + H6) ----------

        void TickRunAfterCatch(float dt)
        {
            var rules = Rules;

            // H6: dodge side is chosen on an interval with hysteresis, never per-frame sign flips.
            dodgeRetimer -= dt;
            if (dodgeRetimer <= 0f)
            {
                dodgeRetimer = rules.racDodgeInterval;
                if (cbTransform != null)
                {
                    float dx = transform.position.x - cbTransform.position.x;
                    if (Mathf.Abs(dx) > 0.2f) dodgeSide = Mathf.Sign(dx);
                    else if (dodgeSide == 0f) dodgeSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                }
            }

            float targetLateral = 0f;
            if (cbTransform != null && Mathf.Abs(transform.position.x - cbTransform.position.x) < rules.racDodgeWindow)
                targetLateral = dodgeSide * rules.racDodgeOffset;
            dodgeLateral = Mathf.MoveTowards(dodgeLateral, targetLateral, 3f * dt); // eased in/out (H6)

            Vector3 dir = new Vector3(dodgeLateral, 0f, 1f).normalized;
            float speed = currentBaseSpeed * KinematicLocomotion.TurnSpeedFactor(loco.Velocity, dir, rules.minTurnSpeedFactor);
            transform.position += loco.Tick(dir * speed, rules.accel, rules.brake, dt);
            Vector3 p = field.ClampToField(transform.position, routeRunner.fieldMargin);
            p.y = 0f;
            transform.position = p;

            FaceFlat(dir, loco.Speed, dt);
            UpdateAnim(loco.Speed, dt);
        }

        // ---------- Animation (audit C1 + C3) ----------

        void UpdateAnim(float speed, float dt)
        {
            if (animDriver == null) return;
            animDriver.SetSpeed(speed, dt);
            var rules = Rules;
            float mult = speed < 0.5f
                ? 1f
                : Mathf.Clamp(speed / Mathf.Max(0.5f, rules.runClipNaturalSpeed), 1f, rules.maxAnimSpeedMult);
            animDriver.SetSpeedMult(mult, dt);
        }

        // Yaw-only damped facing toward movement direction — same SmoothDampAngle style as QBFacing,
        // applied to the WR ROOT (never the Model) so it never fights the in-place humanoid clips.
        // A single forward Running clip + this eased rotation handles route cuts (no turn clips needed).
        [SerializeField] float faceSmoothTime = 0.11f;
        [SerializeField] float faceMaxTurnSpeed = 900f;
        [Tooltip("Below this planar speed the velocity-driven facing holds its last yaw (no chasing noise).")]
        [SerializeField] float faceMinSpeed = 0.5f;
        [Tooltip("Hold yaw when already within this many degrees of target — kills micro-jitter/ringing.")]
        [SerializeField] float faceDeadzoneDeg = 2.5f;
        float faceYawVel;

        void FaceFlat(Vector3 dir, float speed, float dt) => FaceFlat(dir, speed, dt, faceSmoothTime);
        void FaceFlat(Vector3 dir, float speed, float dt, float smoothTime)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float current = transform.eulerAngles.y;
            // Deadzone: already pointing where we want — hold rather than continuously correcting.
            if (Mathf.Abs(Mathf.DeltaAngle(current, target)) < faceDeadzoneDeg) { faceYawVel = 0f; return; }
            float yaw = Mathf.SmoothDampAngle(current, target, ref faceYawVel, smoothTime, faceMaxTurnSpeed, dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---------- Catch resolution (PRD §8.2 + Catch PRD R3-R6) ----------

        /// <summary>
        /// Catch PRD R4: zone entry is ANTICIPATION only — arms go up and the catchability
        /// logic arms. No roll, no attach. The 1.1 m sphere never secures a ball anymore.
        /// </summary>
        void HandleBallEnter(Football fb)
        {
            if (anticipationArmed || catchAttempted || fb == null || !fb.IsLive) return;
            if (State != WRState.RunningRoute && State != WRState.BallPursuit) return;
            var mm = MatchManager.Instance;
            if (mm == null || mm.State != GameState.BallInAir || mm.resolver.ResolvedThisPlay) return;
            anticipationArmed = true;
            anticipatedBall = fb;
            // Fallback: if the synced early trigger hasn't fired yet, start the catch now (zone entry).
            if (!catchTriggered) { catchTriggered = true; animDriver?.Trigger("Catch"); animDriver?.SetCatchOverlay(true); catchFacing = true; }
        }

        /// <summary>
        /// Catch PRD R3: per-frame catchability. The roll runs only when the ball is inside the
        /// reach envelope, or one step from crossing catch height with arrival point A within
        /// reach (+ in-stride allowance). Out of reach at the crossing → uncatchable: no roll,
        /// no Drop, ball sails and resolves INCOMPLETE on the ground.
        /// </summary>
        void TickCatchability(float dt)
        {
            if (!anticipationArmed || catchAttempted || uncatchableThisThrow) return;
            var fb = anticipatedBall;
            if (fb == null || !fb.IsLive) return;
            if (State != WRState.RunningRoute && State != WRState.BallPursuit) return;
            var mm = MatchManager.Instance;
            if (mm == null || mm.State != GameState.BallInAir || mm.resolver.ResolvedThisPlay) return;

            var rules = Rules;
            Vector3 chest = transform.position + Vector3.up * rules.chestHeight;
            Vector3 bp = fb.transform.position;
            bool inEnvelope = Vector3.Distance(bp, chest) <= rules.catchReach;

            if (!inEnvelope)
            {
                // About to cross catch height? Test reachability of the arrival point A.
                Vector3 bv = fb.Velocity;
                float tCross = TimeToCatchHeight(bp, bv, rules.chestHeight, rules.gravityScale);
                float step = Mathf.Max(Time.fixedDeltaTime * 2f, dt * 2f);
                if (tCross < 0f || tCross > step) return; // not the decision frame yet

                Vector3 a = bp + new Vector3(bv.x, 0f, bv.z) * tCross;
                a.y = rules.chestHeight;
                float reach = rules.catchReach;
                Vector3 toA = a - chest; toA.y = 0f;
                bool inStride = loco.Speed > 1f && toA.sqrMagnitude > 1e-4f &&
                                Vector3.Angle(loco.Velocity, toA) <= 45f;
                if (inStride) reach += rules.catchReachStride; // R3 in-stride allowance
                if (Vector3.Distance(chest, a) > reach)
                {
                    uncatchableThisThrow = true; // R3/R6: sails — INCOMPLETE on ground contact, no Drop
                    return;
                }
            }

            RollCatch(fb, rules, mm);
        }

        /// <summary>P(catch) per PRD §8.2 — unchanged dice, now rolled only when genuinely reachable.</summary>
        void RollCatch(Football fb, MatchRules rules, MatchManager mm)
        {
            catchAttempted = true;

            float p = rules.baseCatch;
            bool contested = cbTransform != null &&
                Vector3.Distance(Flat(cbTransform.position), Flat(transform.position)) < rules.contestDistance;
            if (contested) p -= rules.contestPenalty;
            bool highBall = fb.transform.position.y > rules.highBallHeight;
            if (highBall) p -= rules.highBallPenalty;

            Vector3 spot = transform.position;
            bool inBounds = field.IsInBounds(spot);              // v1: capsule center (§8.2)
            bool inEndZone = field.IsInOffensiveEndZone(spot);

            if (UnityEngine.Random.value <= p)
            {
                mm.resolver.ReportCatch(spot, inBounds, inEndZone, () =>
                {
                    // R5: secure on the HandsOnBall anim event, or at closest approach inside the
                    // reach envelope, or on the fallback timer — whichever comes first.
                    pendingCatchBall = fb;
                    handsEventFired = false;
                    lastSecureGap = float.MaxValue;
                    handsFallbackTimer = rules.handsOnBallFallback;
                    if (catchSocket != null) fb.BeginCatchApproach(catchSocket, catchGripOffset, catchApproachTime); // fly the ball into the hands, not the body
                });
            }
            else
            {
                mm.resolver.ReportDrop(spot, () =>
                {
                    fb.DeflectOffHands(fb.Velocity); // R6: deflection derives from incoming velocity
                    animDriver?.Trigger("Drop");
                });
            }
        }

        void HandleHandsOnBall()
        {
            handsEventFired = true; // processed in TickPendingCatch against the R5 gap check
        }

        /// <summary>Catch PRD R5: secure at hand contact — never against the ball's velocity direction.</summary>
        void TickPendingCatch(float dt)
        {
            if (pendingCatchBall == null) return;
            var rules = Rules;
            float gap = Vector3.Distance(pendingCatchBall.transform.position, socket.Socket.position);
            bool withinGap = gap <= rules.maxSecureGap;
            bool closestApproachPassed = gap > lastSecureGap + 1e-4f && lastSecureGap <= rules.maxSecureGap;
            handsFallbackTimer -= dt;

            if ((handsEventFired && withinGap) || closestApproachPassed || handsFallbackTimer <= 0f)
            {
                CompleteCatch();
                return;
            }
            lastSecureGap = gap;
        }

        void CompleteCatch()
        {
            var fb = pendingCatchBall;
            pendingCatchBall = null;
            if (catchSocket != null) fb.CatchInto(catchSocket, catchGripOffset, Vector3.zero, Rules.catchAttachLerp); // single attach @ contact, WR-only socket
            else fb.CatchBy(socket.Socket, false, Rules.catchAttachLerp); // fallback if no catchSocket assigned
            HasBall = true;
            State = WRState.Done; // freeze at the catch the instant it's secured — no turn-back, no run
            catchFacing = false; // catch secured — release the face-the-ball override so he doesn't turn toward the now-in-hand ball
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}