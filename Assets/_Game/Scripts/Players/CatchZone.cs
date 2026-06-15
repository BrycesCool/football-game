using System;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Chest-height sphere trigger that detects the ball (PRD §8). Owner subscribes to OnBallEnter.
    /// Audit M5: explicit one-shot latch — fires at most once per Arm() (owners re-arm in ResetForPlay),
    /// and only for a live ball, so the Stay safety net can't spam subscribers.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class CatchZone : MonoBehaviour
    {
        public event Action<Football> OnBallEnter;

        SphereCollider sphere;
        bool fired;

        void Awake()
        {
            sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
        }

        /// <summary>Re-enable the one-shot latch for a new play (audit M5).</summary>
        public void Arm()
        {
            fired = false;
        }

        public void SetRadius(float r)
        {
            if (sphere == null) sphere = GetComponent<SphereCollider>();
            sphere.radius = r;
        }

        void TryFire(Collider other)
        {
            if (fired) return;
            var ball = other.GetComponentInParent<Football>();
            if (ball == null || !ball.IsLive) return;
            fired = true;
            OnBallEnter?.Invoke(ball);
        }

        void OnTriggerEnter(Collider other) => TryFire(other);

        // Safety net: if the ball spawned/teleported inside the zone, Enter may not fire.
        void OnTriggerStay(Collider other) => TryFire(other);
    }
}