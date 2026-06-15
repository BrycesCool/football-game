using System;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// MonoBehaviour wrapper around RoutePath: applies route motion to the transform with
    /// speed-coupled turn rates and field clamping (PRD §6.3, §8.3).
    /// Audit C5/H1: the instant rotation snap at breaks is replaced by a fast-but-finite
    /// plant turn window, and the route itself carries accel/brake + cut decel (RoutePath.MotionTuning).
    /// </summary>
    public class RouteRunner : MonoBehaviour
    {
        public float turnRateDeg = 540f;
        public float plantTurnRateDeg = 880f;
        public float plantTurnWindow = 0.35f;
        public float fieldMargin = 0.5f;

        RoutePath path;
        FieldBounds field;
        float plantTimer;
        float routeBaseSpeed = 7.5f;

        public RoutePath Path => path;
        public bool Active { get; private set; }

        /// <summary>(waypointIndex, isBreak) — fired when a waypoint is reached during movement.</summary>
        public event Action<int, bool> OnWaypointReached;

        public void Begin(RouteDefinition route, bool mirror, FieldBounds fieldBounds)
        {
            field = fieldBounds;
            var rules = MatchManager.Instance != null ? MatchManager.Instance.rules : null;
            if (rules != null)
            {
                turnRateDeg = rules.turnRateDeg;
                plantTurnRateDeg = rules.plantTurnRateDeg;
                plantTurnWindow = rules.plantTurnWindow;
            }
            routeBaseSpeed = route.baseSpeed;
            path = new RoutePath(transform.position, route.waypoints, route.baseSpeed, route.holdAtFinalPoint,
                mirror, RoutePath.MotionTuning.From(rules));
            path.OnWaypointReached += HandleWaypoint;
            plantTimer = 0f;
            Active = true;
        }

        public void StopRoute()
        {
            Active = false;
        }

        /// <summary>Audit M2: soft push must reach the path's internal position, not just the transform.</summary>
        public void Nudge(Vector3 offset)
        {
            if (path != null) path.Nudge(offset);
        }

        void HandleWaypoint(int index)
        {
            bool isBreak = path.IsBreakWaypoint(index);
            if (isBreak) plantTimer = plantTurnWindow; // fast finite turn instead of instant snap (audit C5)
            OnWaypointReached?.Invoke(index, isBreak);
        }

        /// <summary>REQUIRED public method (PRD §6.3): world position the WR will occupy tFuture seconds from now.</summary>
        public Vector3 PredictPosition(float tFuture)
        {
            if (path == null) return transform.position;
            Vector3 p = path.PredictPosition(tFuture);
            return field != null ? field.ClampToField(p, fieldMargin) : p;
        }

        /// <summary>Advance the route one tick. Single enforcement point for in-bounds clamping (§8.3).</summary>
        public void Tick(float dt)
        {
            if (!Active || path == null) return;
            path.Advance(dt);
            Vector3 p = path.Position;
            if (field != null) p = field.ClampToField(p, fieldMargin);
            p.y = 0f;
            transform.position = p;

            if (plantTimer > 0f) plantTimer -= dt;

            Vector3 dir = path.Direction; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                // Plant window: very fast turn (audit C5). Otherwise: turn rate scales down with speed (audit H1).
                float rate = plantTimer > 0f
                    ? plantTurnRateDeg
                    : KinematicLocomotion.TurnRate(turnRateDeg, path.CurrentSpeed, Mathf.Max(0.1f, routeBaseSpeed));
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rate * dt);
            }
        }
    }
}