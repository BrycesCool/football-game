using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Single source of truth for field geometry (PRD §4.2/§4.3). All units meters.
    /// +Z downfield (offense attacks +Z), X = 0 mid-field, Y = 0 field surface.
    /// All in/out-of-bounds checks are computed mathematically from this component.
    /// </summary>
    public class FieldBounds : MonoBehaviour
    {
        public const float YardsToMeters = 0.9144f;

        [Tooltip("Sidelines at ±halfWidthX")]
        public float halfWidthX = 24.38f;
        [Tooltip("Goal lines at ±goalLineZ")]
        public float goalLineZ = 45.72f;
        [Tooltip("Back of end zones at ±endZoneBackZ")]
        public float endZoneBackZ = 54.86f;

        public bool IsInBounds(Vector3 p, float margin = 0f)
        {
            return Mathf.Abs(p.x) <= halfWidthX - margin && Mathf.Abs(p.z) <= endZoneBackZ - margin;
        }

        public Vector3 ClampToField(Vector3 p, float margin = 0f)
        {
            p.x = Mathf.Clamp(p.x, -halfWidthX + margin, halfWidthX - margin);
            p.z = Mathf.Clamp(p.z, -endZoneBackZ + margin, endZoneBackZ - margin);
            return p;
        }

        /// <summary>Offense always attacks +Z (PRD §4.1).</summary>
        public bool IsInOffensiveEndZone(Vector3 p)
        {
            return p.z >= goalLineZ && p.z <= endZoneBackZ && Mathf.Abs(p.x) <= halfWidthX;
        }

        /// <summary>Yard-line label from a Z position: 1..50 own side, 50..1 opponent side.</summary>
        public string YardLineLabel(float z)
        {
            float yards = (z + goalLineZ) / YardsToMeters; // 0 = own goal line, 100 = opponent goal line
            yards = Mathf.Clamp(yards, 0f, 100f);
            int y = Mathf.RoundToInt(yards);
            return y <= 50 ? ("OWN " + y) : ("OPP " + (100 - y));
        }
    }
}