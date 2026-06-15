using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Shared velocity-based movement model (audit C2/H1). Pure class — no MonoBehaviour — so
    /// WRController and CBController integrate the same physics: persistent velocity with
    /// accel/brake Δv clamps, arrive braking, and turn-rate/speed coupling.
    /// </summary>
    public class KinematicLocomotion
    {
        public Vector3 Velocity { get; private set; }
        public float Speed => Velocity.magnitude;

        public void Reset() => Velocity = Vector3.zero;

        /// <summary>Seed velocity when handing off from another movement system (e.g., route → pursuit).</summary>
        public void ForceVelocity(Vector3 v)
        {
            v.y = 0f;
            Velocity = v;
        }

        /// <summary>
        /// Move velocity toward desiredVelocity, clamping Δv: accel when gaining speed,
        /// brake when shedding or reversing. Returns the position delta for this tick.
        /// </summary>
        public Vector3 Tick(Vector3 desiredVelocity, float accel, float brake, float dt)
        {
            desiredVelocity.y = 0f;
            bool braking = desiredVelocity.sqrMagnitude < Velocity.sqrMagnitude
                           || Vector3.Dot(desiredVelocity, Velocity) < 0f;
            float rate = braking ? brake : accel;
            Vector3 dv = desiredVelocity - Velocity;
            Velocity += Vector3.ClampMagnitude(dv, rate * dt);
            return Velocity * dt;
        }

        /// <summary>Max speed that can still brake to zero over `dist` (v = √(2·a·d)) — no overshoot at targets.</summary>
        public static float ArriveSpeed(float dist, float maxSpeed, float brake)
        {
            return Mathf.Min(maxSpeed, Mathf.Sqrt(2f * brake * Mathf.Max(0f, dist)));
        }

        /// <summary>H1: desired speed shrinks with the angle between current momentum and the new direction.</summary>
        public static float TurnSpeedFactor(Vector3 currentDir, Vector3 desiredDir, float minFactor)
        {
            currentDir.y = 0f; desiredDir.y = 0f;
            if (currentDir.sqrMagnitude < 0.25f || desiredDir.sqrMagnitude < 1e-4f) return 1f;
            float angle = Vector3.Angle(currentDir, desiredDir);
            return Mathf.Lerp(1f, minFactor, Mathf.Clamp01(angle / 90f));
        }

        /// <summary>H1: max turn rate scales down with speed (full rate when slow, 40% at max speed).</summary>
        public static float TurnRate(float baseTurnRateDeg, float speed, float maxSpeed)
        {
            float t = maxSpeed > 0.01f ? Mathf.Clamp01(speed / maxSpeed) : 0f;
            return baseTurnRateDeg * Mathf.Lerp(1f, 0.4f, t);
        }
    }
}