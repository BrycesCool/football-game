using UnityEngine;

namespace Gridiron
{
    /// <summary>World-space LOS (blue) and first-down (yellow) lines, full field width (PRD §11.3).</summary>
    public class FieldLines : MonoBehaviour
    {
        public LineRenderer losLine;
        public LineRenderer firstDownLine;
        public float lineY = 0.03f;

        MatchManager mm;

        void Start()
        {
            mm = MatchManager.Instance;
            if (mm != null) mm.OnStateChanged += _ => Refresh();
            Refresh();
        }

        void Refresh()
        {
            if (mm == null || mm.field == null || mm.Drive == null) return;
            bool show = mm.State == GameState.PreSnap || mm.State == GameState.RouteRunning ||
                        mm.State == GameState.BallInAir || mm.State == GameState.PlayResult;
            float w = mm.field.halfWidthX;
            SetLine(losLine, mm.Drive.LosZ, w, show);
            SetLine(firstDownLine, mm.Drive.FirstDownTargetZ, w, show);
        }

        void SetLine(LineRenderer lr, float z, float halfWidth, bool show)
        {
            if (lr == null) return;
            lr.enabled = show;
            if (!show) return;
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-halfWidth, lineY, z));
            lr.SetPosition(1, new Vector3(halfWidth, lineY, z));
        }
    }
}