using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Drives <see cref="QBFacing"/> from a QB animator state. Idle/scan uses trackingOnEnter =
    /// true (start the responsive scan track). Hike/Ready uses false (stop AND reset facing for a
    /// fresh play - zeroes yaw velocity). Optionally set beginScanAtNormalized (e.g. 0.6) on the
    /// Hike state so the QB starts turning as the hike settles, before Idle begins. Finds QBFacing
    /// on the Animator's parent (the QB ROOT).
    /// </summary>
    public class QBTrackingState : StateMachineBehaviour
    {
        [Tooltip("True on the scan/Idle state; false on Hike/Ready (also resets facing).")]
        public bool trackingOnEnter = true;

        [Tooltip("If >= 0, BeginScan once this state passes this normalized time (e.g. 0.6 on Hike).")]
        public float beginScanAtNormalized = -1f;

        bool scanStarted;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            scanStarted = false;
            var facing = animator.GetComponentInParent<QBFacing>();
            if (facing == null) return;
            if (trackingOnEnter)
                facing.BeginScan();
            else
            {
                facing.ResetFacing();
                facing.StopTracking();
            }
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (scanStarted || beginScanAtNormalized < 0f) return;
            if (stateInfo.normalizedTime < beginScanAtNormalized) return;
            var facing = animator.GetComponentInParent<QBFacing>();
            if (facing != null) facing.BeginScan();
            scanStarted = true;
        }
    }
}
