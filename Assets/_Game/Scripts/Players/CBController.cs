using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Cornerback (PRD §8.4). Movement decisions come exclusively from an ICBBrain
    /// (AICBBrain in v1; PlayerCBBrain is a stretch goal — see §15).
    /// Ball interactions (swat/intercept rolls) are handled here on catch-zone contact.
    /// Audit pass: persistent-velocity locomotion with arrive braking (C2/H2), SmoothDamped
    /// brain target (H2), velocity-vs-facing backpedal decision + hip-flip turn cost (H3),
    /// local-space MoveX/MoveZ for the 2D directional blend tree (C4), damped animator
    /// floats (C1/C3), symmetric soft push (M2).
    /// </summary>
    public class CBController : MonoBehaviour
    {
        public CatchZone catchZone;
        public BallSocket socket;
        public CharacterAnimatorDriver animDriver;

        [Header("Scene refs (wired by MatchManager/scene builder)")]
        public WRController wr;
        public Football ball;
        public FieldBounds field;

        readonly KinematicLocomotion loco = new KinematicLocomotion();

        ICBBrain brain;
        bool rollAttempted;
        bool active;
        bool ballThrown;

        // H2: smoothed brain target
        Vector3 smoothedTarget;
        Vector3 smoothedTargetVel;
        bool targetInitialized;

        // H3: hip flip
        bool wasBackpedaling;
        float hipFlipTimer;

        public CBDifficulty Difficulty => MatchManager.Instance.Difficulty;

        static MatchRules fallbackRules;
        /// <summary>Null-safe: per-frame anim code must survive domain reloads / missing MatchManager.</summary>
        public MatchRules Rules
        {
            get
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.rules != null) return mm.rules;
                if (fallbackRules == null) fallbackRules = ScriptableObject.CreateInstance<MatchRules>();
                return fallbackRules;
            }
        }
        /// <summary>cbSpeed = wrBaseSpeed × speedRatio (difficulty knob, §8.4).</summary>
        public float Speed => wr.CurrentBaseSpeed * Difficulty.speedRatio;
        public Vector3 BallPosition => ball != null ? ball.transform.position : transform.position;
        public Vector3 Velocity => loco.Velocity;

        void Awake()
        {
            brain = new AICBBrain();
        }

        void Start()
        {
            if (catchZone != null) catchZone.OnBallEnter += HandleBallEnter;
            if (wr != null) wr.OnRouteWaypoint += HandleWRWaypoint;
        }

        void OnDestroy() // audit L1
        {
            if (catchZone != null) catchZone.OnBallEnter -= HandleBallEnter;
            if (wr != null) wr.OnRouteWaypoint -= HandleWRWaypoint;
        }

        public void ResetForPlay(Vector3 position)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(Vector3.back); // face the WR/LOS
            rollAttempted = false;
            active = false;
            ballThrown = false;
            targetInitialized = false;
            wasBackpedaling = false;
            hipFlipTimer = 0f;
            loco.Reset();
            brain.ResetForPlay(this);
            if (catchZone != null)
            {
                catchZone.SetRadius(Rules.cbCatchRadius);
                catchZone.Arm(); // audit M5: explicit one-shot latch per play
            }
            if (animDriver != null)
            {
                animDriver.floatDampTime = Rules.animSpeedDamp;
                animDriver.ResetLocomotion(); // zero Speed + MoveX/MoveZ so he holds Defender Stance pre-snap (not a moving blend)
            }
        }

        public void OnSnapped()
        {
            active = true;
            brain.OnSnap(this);
        }

        public void OnThrowNotify(ThrowData data)
        {
            ballThrown = true;
            brain.OnThrow(this, data);
        }

        public void EndPlay()
        {
            active = false;
            animDriver?.SetSpeed(0f);
            animDriver?.SetBackpedal(false);
        }

        void HandleWRWaypoint(int index, bool isBreak)
        {
            if (isBreak) brain.NotifyWRBreak(); // reaction-delay penalty at breaks (§8.4)
        }

        void Update()
        {
            if (!active) return;
            float dt = Mathf.Min(Time.deltaTime, 0.04f); // clamp dt spikes
            var mmCB = MatchManager.Instance;
            if (mmCB != null && mmCB.resolver != null && mmCB.resolver.ResolvedThisPlay)
            {
                // Play is over (catch / incomplete / INT) — stop chasing, settle to the defender stance.
                animDriver?.SetSpeed(0f, dt);
                animDriver?.SetMoveLocal(0f, 0f, dt);
                animDriver?.SetBackpedal(false);
                return;
            }
            var rules = Rules;
            CBCommand cmd = brain.Tick(this, dt);

            // ---- H2: smooth the brain's target so phase changes / delayed-sample jumps don't snap motion ----
            if (!targetInitialized)
            {
                smoothedTarget = cmd.moveTarget;
                smoothedTargetVel = Vector3.zero;
                targetInitialized = true;
            }
            smoothedTarget = Vector3.SmoothDamp(smoothedTarget, cmd.moveTarget, ref smoothedTargetVel,
                rules.cbTargetSmoothTime, Mathf.Infinity, dt);

            // ---- C2: desired velocity with arrive braking + turn coupling, integrated through loco ----
            Vector3 to = smoothedTarget - transform.position; to.y = 0f;
            float dist = to.magnitude;
            Vector3 desired = Vector3.zero;
            if (dist > 0.05f)
            {
                Vector3 dir = to / dist;
                float speed = KinematicLocomotion.ArriveSpeed(dist, cmd.maxSpeed, rules.brake);
                speed *= KinematicLocomotion.TurnSpeedFactor(loco.Velocity, dir, rules.minTurnSpeedFactor); // H1
                if (hipFlipTimer > 0f) speed = Mathf.Min(speed, cmd.maxSpeed * rules.hipFlipSpeedFactor);    // H3
                desired = dir * speed;
            }

            transform.position += loco.Tick(desired, rules.accel, rules.brake, dt);

            SoftPushFromWR(dt);

            Vector3 p = transform.position;
            if (field != null) p = field.ClampToField(p, 0f);
            p.y = 0f;
            transform.position = p;

            // ---- Facing: track the WR until the ball is in the air, then face the movement direction ----
            Vector3 faceDir;
            if (!ballThrown) faceDir = Flat(wr.transform.position - transform.position);
            else if (loco.Speed > 0.5f) faceDir = Flat(loco.Velocity);
            else faceDir = Flat(wr.transform.position - transform.position);

            if (faceDir.sqrMagnitude > 1e-4f)
            {
                float rate = KinematicLocomotion.TurnRate(rules.turnRateDeg, loco.Speed, Mathf.Max(0.1f, Speed)); // H1
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(faceDir.normalized), rate * dt);
            }

            // ---- H3: locomotion style from velocity in facing-local space (not phase flags) ----
            Vector3 local = transform.InverseTransformDirection(loco.Velocity);
            bool backpedal = local.z < -0.3f && loco.Speed > 0.5f;
            if (wasBackpedaling && !backpedal && loco.Speed > 0.5f)
                hipFlipTimer = rules.hipFlipDuration; // pay the hip-flip turn cost (H3)
            wasBackpedaling = backpedal;
            if (hipFlipTimer > 0f) hipFlipTimer -= dt;

            // ---- C1/C3/C4: damped animator parameters ----
            if (animDriver != null)
            {
                animDriver.SetSpeed(loco.Speed, dt);
                animDriver.SetBackpedal(backpedal);
                animDriver.SetMoveLocal(local.x, local.z, dt);
                float mult = loco.Speed < 0.5f
                    ? 1f
                    : Mathf.Clamp(loco.Speed / Mathf.Max(0.5f, rules.runClipNaturalSpeed), 1f, rules.maxAnimSpeedMult);
                animDriver.SetSpeedMult(mult, dt);
            }
        }

        /// <summary>
        /// CB–WR capsules collide softly: push-out only, max 2 m/s, no physics forces (§8.4).
        /// Audit M2: applied symmetrically (half each) and routed into the WR's path so it sticks.
        /// </summary>
        void SoftPushFromWR(float dt)
        {
            if (wr == null) return;
            Vector3 delta = Flat(transform.position - wr.transform.position);
            float minDist = Rules.cbWRMinDistance;
            float d = delta.magnitude;
            if (d < minDist && d > 1e-4f)
            {
                float push = Mathf.Min(Rules.softPushMaxSpeed * dt, minDist - d) * 0.5f;
                Vector3 dir = delta / d;
                transform.position += dir * push;
                wr.ApplyPush(-dir * push);
            }
        }

        void HandleBallEnter(Football fb)
        {
            if (rollAttempted || fb == null || !fb.IsLive) return;
            var mm = MatchManager.Instance;
            if (mm == null || mm.State != GameState.BallInAir || mm.resolver.ResolvedThisPlay) return;
            rollAttempted = true;

            var diff = Difficulty;
            Vector3 spot = transform.position;

            if (Random.value <= diff.interceptChance)
            {
                mm.resolver.ReportIntercept(spot, () =>
                {
                    fb.CatchBy(socket.Socket, true, Rules.catchAttachLerp); // H7: lerp, not teleport
                    animDriver?.Trigger("Catch");
                });
            }
            else if (Random.value <= diff.swatChance)
            {
                mm.resolver.ReportSwat(spot, () =>
                {
                    fb.Swat();
                    animDriver?.Trigger("Swat");
                });
            }
            // else: whiff — ball continues (§8.4)
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}