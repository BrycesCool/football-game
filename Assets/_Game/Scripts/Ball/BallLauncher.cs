using System.Collections;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// The lead-throw solver + launch sequencing (PRD §7.3). Lives on the QB.
    /// Launch happens at the ReleaseBall animation event, with a timer fallback so gameplay
    /// never blocks on animation fidelity (PRD §12.2 directive).
    /// </summary>
    public class BallLauncher : MonoBehaviour
    {
        public Football ball;
        public BallSocket qbSocket;
        public WRController wr;
        public CharacterAnimatorDriver animDriver;
        QBFacing facing;
        [Header("Throw arc tuning")]
        [Tooltip("Higher = flatter, faster throws (shorter flight time).")]
        [SerializeField] float ballSpeed = 18f;
        [Tooltip("Min flight time — caps how flat a short throw can be.")]
        [SerializeField] float flightTimeMin = 0.5f;
        [Tooltip("Max flight time — caps how loopy a long throw can be.")]
        [SerializeField] float flightTimeMax = 1.8f;

        bool launchQueued;
        bool launchedThisPlay;
        ThrowType queuedType;
        Coroutine fallbackRoutine;

        MatchRules Rules => MatchManager.Instance.rules;

        void Start()
        {
            facing = GetComponent<QBFacing>();
            if (animDriver != null) animDriver.ReleaseBallEvent += OnReleaseBallEvent;
        }

        void OnDestroy()
        {
            if (animDriver != null) animDriver.ReleaseBallEvent -= OnReleaseBallEvent;
        }

        public void ResetForPlay()
        {
            launchQueued = false;
            launchedThisPlay = false;
            if (fallbackRoutine != null) { StopCoroutine(fallbackRoutine); fallbackRoutine = null; }
        }

        /// <summary>Called by QBController on throw input. Actual launch deferred to the anim event (§7.3 step 5).</summary>
        public void QueueLaunch(ThrowType type)
        {
            if (launchQueued || launchedThisPlay) return;
            launchQueued = true;
            queuedType = type;
            fallbackRoutine = StartCoroutine(FallbackLaunch());
        }

        IEnumerator FallbackLaunch()
        {
            yield return new WaitForSeconds(Rules.throwReleaseFallback);
            OnReleaseBallEvent();
        }

        void OnReleaseBallEvent()
        {
            if (!launchQueued || launchedThisPlay) return;
            launchQueued = false;
            launchedThisPlay = true;
            if (fallbackRoutine != null) { StopCoroutine(fallbackRoutine); fallbackRoutine = null; }
            LaunchNow(queuedType);
        }

        void LaunchNow(ThrowType type)
        {
            // Predicted catch point (shared with QB facing) — keeps the lead so the ball goes
            // where the WR WILL be, not where he is right now.
            Vector3 target = ComputeAimPoint(type);

            // Launch from where the ball actually is (it has been riding the throwing hand),
            // not a hardcoded chest/root point. Capture BEFORE physics/detach takes over.
            Vector3 origin = ball.transform.position;

            Vector3 toTarget = target - origin;
            float horizDist = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;
            float flightTime = Mathf.Clamp(horizDist / Mathf.Max(0.01f, ballSpeed), flightTimeMin, flightTimeMax);

            // Time-based kinematic solve: the velocity that lands on `target` in `flightTime`
            // under standard gravity. Short flight = flat dart, long flight = higher arc.
            Vector3 velocity = toTarget / flightTime;
            velocity.y += 0.5f * Mathf.Abs(Physics.gravity.y) * flightTime;

            // Freeze QB facing at the final aim point so body & ball agree (yaw-only), then commit.
            if (facing != null) { facing.SnapToPoint(target); facing.StopTracking(); }

            var data = new ThrowData
            {
                type = type,
                releasePos = origin,
                velocity = velocity,
                targetPoint = target,
                flightTime = flightTime,
                launchTime = Time.time
            };
            // gravityScale 1 => flight uses standard gravity, matching the solve so the ball lands exactly.
            ball.Launch(data, 1f);
            MatchManager.Instance.NotifyThrow(data);
        }

        /// <summary>
        /// Single source of truth for the lead/catch point (no accuracy noise) - the QB's true aim.
        /// Queried by QBFacing each frame so the body leads the WR exactly where the ball goes.
        /// </summary>
        public Vector3 ComputeAimPoint(ThrowType type)
        {
            if (wr == null) return transform.position + transform.forward * 8f;
            return SolveLead(type, out _, out _, out _, out _);
        }

        Vector3 SolveLead(ThrowType type, out ThrowTuning tuning, out Vector3 release, out float T, out System.Func<float, Vector3> predict)
        {
            var rules = Rules;
            tuning = type == ThrowType.Bullet ? rules.bullet : rules.lob;
            release = qbSocket != null ? qbSocket.Socket.position : transform.position + Vector3.up * 1.8f;
            Vector3 chest = Vector3.up * rules.chestHeight;
            predict = t => wr.PredictFuturePosition(t) + chest;
            T = BallisticsSolver.SolveLeadTime(release, predict, tuning.throwSpeed, tuning.tMin, tuning.tMax);
            return predict(T);
        }

        /// <summary>
        /// Catch PRD R1: sample the route prediction and return the on-path point (chest height)
        /// nearest to `worldPoint`. Used to keep shortened deep balls on the receiver's line.
        /// </summary>
        static Vector3 NearestPointOnRoute(System.Func<float, Vector3> predict, Vector3 worldPoint, float tMax)
        {
            Vector3 best = worldPoint;
            float bestSqr = float.MaxValue;
            for (float t = 0f; t <= tMax + 0.001f; t += 0.05f)
            {
                Vector3 p = predict(t);
                float dx = p.x - worldPoint.x, dz = p.z - worldPoint.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }
    }
}