using UnityEngine;

namespace Gridiron
{
    public struct CBCommand
    {
        public Vector3 moveTarget;
        public float maxSpeed;
        public bool backpedal;
    }

    /// <summary>
    /// Decision interface for the cornerback (PRD §15 stretch requirement): CBController reads
    /// movement decisions exclusively through this, so a PlayerCBBrain can be swapped in later
    /// without refactoring. AICBBrain is the v1 implementation.
    /// </summary>
    public interface ICBBrain
    {
        void ResetForPlay(CBController cb);
        void OnSnap(CBController cb);
        void OnThrow(CBController cb, ThrowData data);
        void NotifyWRBreak();
        CBCommand Tick(CBController cb, float dt);
    }
}