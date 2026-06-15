using System;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Pure route-following math (no MonoBehaviour) so PredictPosition is unit-testable (PRD §6.3, §14).
    /// Both Advance() and PredictPosition() walk the same Step() code path so prediction matches movement.
    /// Audit C2/H1: motion now carries a persistent speed with accel/brake limits and slows through
    /// break waypoints (cut cost). The tuning lives in MotionTuning; the legacy constructor uses
    /// MotionTuning.Instant (infinite accel) to preserve the original analytic behavior for tests.
    /// </summary>
    public class RoutePath
    {
        public const float ReachRadius = 0.3f;   // waypoint counts reached at distance < 0.3 m (§6.3)
        const float SimStep = 0.02f;             // prediction sim step (matches FixedUpdate cadence)

        /// <summary>Accel/brake/cut tuning (audit C2). Instant = legacy analytic motion.</summary>
        public struct MotionTuning
        {
            public float accel;          // m/s²
            public float brake;          // m/s²
            public float cutSpeedFactor; // speed factor through break waypoints
            public float cutDecelRadius; // m, decel zone before a break

            public static MotionTuning Instant => new MotionTuning
            { accel = float.MaxValue, brake = float.MaxValue, cutSpeedFactor = 1f, cutDecelRadius = 0f };

            public static MotionTuning From(MatchRules rules) => rules == null ? Instant : new MotionTuning
            { accel = rules.accel, brake = rules.brake, cutSpeedFactor = rules.cutSpeedFactor, cutDecelRadius = rules.cutDecelRadius };
        }

        public struct State
        {
            public Vector3 pos;
            public int seg;        // waypoint index currently pursued; == points.Length → finished/extrapolating
            public Vector3 dir;
            public float curSpeed; // persistent speed (audit C2)
        }

        readonly Vector3[] points;
        readonly float[] speeds;   // speed while pursuing points[i] (baseSpeed * speedMultiplier)
        readonly bool[] breaks;
        readonly float baseSpeed;
        readonly bool holdAtEnd;
        readonly MotionTuning tuning;
        State state;

        /// <summary>Fired from Advance() only (never from prediction). Arg = waypoint index reached.</summary>
        public event Action<int> OnWaypointReached;

        public Vector3 Position => state.pos;
        public Vector3 Direction => state.dir;
        public bool Finished => state.seg >= points.Length;
        public bool HoldsAtEnd => holdAtEnd;
        public bool Holding => holdAtEnd && Finished;
        public float CurrentSpeed => Holding ? 0f : state.curSpeed;
        public bool NextWaypointIsBreak => !Finished && breaks[state.seg];
        public float DistanceToNextWaypoint => Finished ? float.MaxValue : Flat(points[state.seg] - state.pos).magnitude;
        public bool IsBreakWaypoint(int index) => index >= 0 && index < breaks.Length && breaks[index];

        /// <summary>Legacy constructor — instant speed (analytic), used by the edit-mode tests.</summary>
        public RoutePath(Vector3 startWorld, RouteWaypoint[] waypoints, float baseSpeed, bool holdAtEnd, bool mirrorX)
            : this(startWorld, waypoints, baseSpeed, holdAtEnd, mirrorX, MotionTuning.Instant) { }

        public RoutePath(Vector3 startWorld, RouteWaypoint[] waypoints, float baseSpeed, bool holdAtEnd, bool mirrorX, MotionTuning tuning)
        {
            this.baseSpeed = baseSpeed;
            this.holdAtEnd = holdAtEnd;
            this.tuning = tuning;
            int n = waypoints != null ? waypoints.Length : 0;
            points = new Vector3[n];
            speeds = new float[n];
            breaks = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float x = waypoints[i].offset.x * (mirrorX ? -1f : 1f);
                points[i] = startWorld + new Vector3(x, 0f, waypoints[i].offset.y);
                float mult = waypoints[i].speedMultiplier <= 0f ? 1f : waypoints[i].speedMultiplier;
                speeds[i] = baseSpeed * mult;
                breaks[i] = waypoints[i].isBreak;
            }
            state.pos = startWorld;
            state.seg = 0;
            state.dir = n > 0 ? Flat(points[0] - startWorld).normalized : Vector3.forward;
            state.curSpeed = 0f;
        }

        /// <summary>Nudge the internal position (audit M2: symmetric soft push must reach the path, not just the transform).</summary>
        public void Nudge(Vector3 offset)
        {
            offset.y = 0f;
            state.pos += offset;
        }

        public void Advance(float dt) => Step(ref state, dt, true);

        /// <summary>
        /// World position this route occupies tFuture seconds from now, accounting for per-segment
        /// speed multipliers, extrapolating past the final waypoint along the final direction,
        /// and clamping at the hold point for Curl/Comeback (PRD §6.3). REQUIRED API.
        /// </summary>
        public Vector3 PredictPosition(float tFuture)
        {
            if (tFuture <= 0f) return state.pos;
            State sim = state;
            float remaining = tFuture;
            while (remaining > 0f)
            {
                float dt = Mathf.Min(SimStep, remaining);
                Step(ref sim, dt, false);
                remaining -= dt;
                if (sim.seg >= points.Length && holdAtEnd) break; // holding: position frozen
            }
            return sim.pos;
        }

        /// <summary>
        /// Advance curSpeed toward target under accel/brake limits and return the EXACT average
        /// speed over dt (trapezoid while changing + plateau after reaching target). Exact averaging
        /// makes position integration independent of step size, so PredictPosition (0.02 s sim steps)
        /// matches Advance (frame dt) — the Catch PRD AC5 parity contract.
        /// </summary>
        static float IntegrateSpeed(ref float curSpeed, float target, MotionTuning tuning, float dt)
        {
            float rate = target >= curSpeed ? tuning.accel : tuning.brake;
            float diff = Mathf.Abs(target - curSpeed);
            float tChange = (rate > 0f && !float.IsInfinity(rate)) ? diff / rate : 0f;
            float avg;
            if (tChange >= dt)
            {
                float next = Mathf.MoveTowards(curSpeed, target, rate * dt);
                avg = 0.5f * (curSpeed + next);
                curSpeed = next;
            }
            else
            {
                avg = dt > 1e-6f
                    ? (0.5f * (curSpeed + target) * tChange + target * (dt - tChange)) / dt
                    : target;
                curSpeed = target;
            }
            return avg;
        }

        void Step(ref State s, float dt, bool fireEvents)
        {
            int guard = 0;
            while (dt > 0f && guard++ < 64)
            {
                if (s.seg >= points.Length)
                {
                    if (!holdAtEnd)
                    {
                        float avgExtra = IntegrateSpeed(ref s.curSpeed, baseSpeed, tuning, dt);
                        s.pos += s.dir * avgExtra * dt;
                    }
                    return;
                }

                Vector3 to = Flat(points[s.seg] - s.pos);
                float dist = to.magnitude;

                // Target speed: segment speed, reduced through the cut zone before a break (audit C2/H1).
                float targetSpeed = speeds[s.seg];
                if (breaks[s.seg] && tuning.cutDecelRadius > 0.01f && dist < tuning.cutDecelRadius)
                    targetSpeed *= Mathf.Lerp(tuning.cutSpeedFactor, 1f, dist / tuning.cutDecelRadius);

                float speed = Mathf.Max(0.05f, IntegrateSpeed(ref s.curSpeed, targetSpeed, tuning, dt));
                float step = speed * dt;

                if (dist <= ReachRadius || step >= dist - ReachRadius)
                {
                    // Reach the waypoint boundary this tick, then advance to the next segment.
                    float travel = Mathf.Max(0f, dist - ReachRadius);
                    if (travel > 0f)
                    {
                        Vector3 dir = to / dist;
                        s.pos += dir * travel;
                        s.dir = dir;
                        dt -= travel / speed;
                    }
                    int reached = s.seg;
                    s.seg++;
                    if (s.seg < points.Length)
                    {
                        Vector3 next = Flat(points[s.seg] - s.pos);
                        if (next.sqrMagnitude > 1e-6f) s.dir = next.normalized;
                    }
                    else if (dist > 1e-4f)
                    {
                        s.dir = to / dist; // final segment direction for extrapolation
                    }
                    if (fireEvents) OnWaypointReached?.Invoke(reached);
                }
                else
                {
                    s.dir = to / dist;
                    s.pos += s.dir * step;
                    return;
                }
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}