using UnityEngine;

namespace Gridiron
{
    [System.Serializable]
    public struct RouteWaypoint
    {
        /// <summary>(x, z) METERS from WR pre-snap position. +z downfield, +x toward sideline on WR's side.
        /// NOTE: stored in METERS (PRD §6.2 recommendation; yards converted at authoring time).</summary>
        public Vector2 offset;
        /// <summary>Speed multiplier while pursuing this waypoint. 1 = normal, &lt;1 slows into a cut. 0/unset treated as 1.</summary>
        public float speedMultiplier;
        /// <summary>Sharp cut: WR plays plant animation, CB break-reaction penalty applies here.</summary>
        public bool isBreak;
    }

    [CreateAssetMenu(menuName = "Gridiron/Route", fileName = "Route")]
    public class RouteDefinition : ScriptableObject
    {
        public string routeName;
        public RouteWaypoint[] waypoints;
        public float baseSpeed = 7.5f;
        /// <summary>Curl/Comeback: WR stops at the final point and faces the QB instead of extrapolating (PRD §6.2).</summary>
        public bool holdAtFinalPoint;
    }
}