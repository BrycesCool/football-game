using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Resolves the hand attachment point for the ball: humanoid RightHand bone when a rigged
    /// character is present, otherwise an override child (capsule stand-ins), otherwise self.
    /// </summary>
    public class BallSocket : MonoBehaviour
    {
        public Transform overrideSocket;
        Transform cached;

        public Transform Socket
        {
            get
            {
                if (cached != null) return cached;
                if (overrideSocket != null) { cached = overrideSocket; return cached; }
                var anim = GetComponentInChildren<Animator>();
                if (anim != null && anim.isHuman)
                    cached = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (cached == null) cached = transform;
                return cached;
            }
        }
    }
}