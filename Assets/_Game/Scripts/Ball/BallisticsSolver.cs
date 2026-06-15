using System;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Analytic ballistic + lead-throw math (PRD §7.3). Pure static, unit-testable (§14).
    /// No drag: solutions are exact.
    /// </summary>
    public static class BallisticsSolver
    {
        /// <summary>Launch velocity so a projectile released at R under gravity gEff arrives exactly at P after T seconds.</summary>
        public static Vector3 SolveVelocity(Vector3 release, Vector3 target, float T, float gEff)
        {
            Vector3 d = target - release;
            return new Vector3(d.x / T, d.y / T + 0.5f * gEff * T, d.z / T);
        }

        /// <summary>Projectile position t seconds after launch.</summary>
        public static Vector3 PointAtTime(Vector3 release, Vector3 v0, float gEff, float t)
        {
            return release + v0 * t + 0.5f * gEff * t * t * Vector3.down;
        }

        /// <summary>
        /// Iterative lead solve (PRD §7.3): T = clamp(d / throwSpeed, tMin, tMax), P = predict(T), repeat (5 fixed iterations).
        /// predictTarget must return the receiver's chest-height world position tFuture seconds from now.
        /// </summary>
        public static float SolveLeadTime(Vector3 release, Func<float, Vector3> predictTarget, float throwSpeed, float tMin, float tMax, int iterations = 5)
        {
            Vector3 now = predictTarget(0f);
            float T = Mathf.Clamp(HorizontalDistance(release, now) / throwSpeed, tMin, tMax);
            for (int i = 0; i < iterations; i++)
            {
                Vector3 p = predictTarget(T);
                T = Mathf.Clamp(HorizontalDistance(release, p) / throwSpeed, tMin, tMax);
            }
            return T;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}