using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Yaw-only "face the aim point" for the QB. Lives on the QB ROOT (never the Model) so it
    /// never fights the Animator's in-place humanoid clips. Damped via SmoothDampAngle (capped
    /// by maxTurnSpeed) for fluid ease-in/ease-out turns - no raycasts, no LookAt.
    ///
    /// The aim point comes from AimProvider (the SAME lead/catch point the ball is thrown to);
    /// facingLeadBonus adds a little extra WR-velocity lead to the FACING ONLY so the body sits
    /// slightly ahead of a crossing WR. The ball's aim point is never changed.
    /// Scan = responsive read; Throw = commit HARDER (faster) through the windup; frozen at ball
    /// release; reset each play.
    /// </summary>
    public class QBFacing : MonoBehaviour
    {
        public enum FacingMode { Scan, Throw }

        [Tooltip("Fallback target if no AimProvider is set - assign the WR ROOT transform.")]
        public Transform receiver;

        [Tooltip("SmoothDamp time during the scan (smaller = snappier).")]
        public float smoothTime = 0.12f;

        [Tooltip("SmoothDamp time during the throw windup - SMALLER than scan: commit to the aim.")]
        public float throwSmoothTime = 0.08f;

        [Tooltip("Cap on yaw rate (deg/sec) to avoid whip-around on extreme angle changes.")]
        public float maxTurnSpeed = 720f;

        [Tooltip("Seconds of EXTRA lead beyond the ball's aim point, applied to FACING ONLY, so the\nbody sits slightly ahead of a crossing WR (cancels the SmoothDamp trail). 0 = identical to current.")]
        public float facingLeadBonus = 0.15f;

        /// <summary>Supplies the world-space aim point (lead/catch point). Set at runtime by QBController.</summary>
        public System.Func<Vector3> AimProvider { get; set; }

        /// <summary>Master switch - when false the QB holds its current facing.</summary>
        public bool trackingEnabled { get; set; }

        /// <summary>Current smoothing profile (Scan vs Throw).</summary>
        public FacingMode Mode { get; private set; } = FacingMode.Scan;

        float currentYaw;
        float yawVelocity;
        WRController wrCtrl;

        void OnEnable() => ResetFacing();

        Vector3 AimPoint()
        {
            Vector3 baseAim;
            if (AimProvider != null)
            {
                Vector3 p = AimProvider();
                baseAim = float.IsNaN(p.x) ? FallbackAim() : p;
            }
            else baseAim = FallbackAim();

            // FACING-ONLY extra lead: nudge the target ahead of the WR using the WR's own velocity
            // (the same source the ball's aim solver uses). The ball's aim point is untouched, so
            // release alignment is preserved. facingLeadBonus = 0 => identical to the ball's aim.
            if (facingLeadBonus != 0f)
            {
                if (wrCtrl == null && receiver != null) wrCtrl = receiver.GetComponent<WRController>();
                if (wrCtrl != null) baseAim += wrCtrl.Velocity * facingLeadBonus;
            }
            return baseAim;
        }

        Vector3 FallbackAim() => receiver != null ? receiver.position : transform.position + transform.forward;

        void Update()
        {
            if (!trackingEnabled) return;

            Vector3 dir = AimPoint() - transform.position;
            dir.y = 0f;                                 // yaw only - no pitch / lean
            if (dir.sqrMagnitude < 0.0001f) return;     // aim basically on top of us

            float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float st = Mode == FacingMode.Throw ? throwSmoothTime : smoothTime;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, st, maxTurnSpeed);
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        /// <summary>Responsive scan track (Idle/scan begins, or late in the hike).</summary>
        public void BeginScan()
        {
            Mode = FacingMode.Scan;
            trackingEnabled = true;
        }

        /// <summary>Commit harder through the windup but keep tracking (Throw trigger fired).</summary>
        public void BeginThrow()
        {
            Mode = FacingMode.Throw;
            trackingEnabled = true;
        }

        /// <summary>Snap yaw exactly to face a world point (used at ball release), zero velocity.</summary>
        public void SnapToPoint(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                currentYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);
            }
            yawVelocity = 0f;
        }

        /// <summary>Freeze facing (ball release).</summary>
        public void StopTracking() => trackingEnabled = false;

        /// <summary>Snap internal yaw to the current transform and kill velocity (play reset).</summary>
        public void ResetFacing()
        {
            currentYaw = transform.eulerAngles.y;
            yawVelocity = 0f;
            Mode = FacingMode.Scan;
        }
    }
}
