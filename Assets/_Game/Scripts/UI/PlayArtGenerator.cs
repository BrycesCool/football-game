using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Procedural route diagrams for the play-select cards (PRD §13: play-art sprites generated
    /// procedurally). Mini-field background + route polyline + break dots + arrowhead.
    /// </summary>
    public static class PlayArtGenerator
    {
        public static Texture2D Generate(RouteDefinition route, int w = 110, int h = 140)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var bg = new Color(0.07f, 0.28f, 0.12f);
            var lineCol = new Color(1f, 1f, 1f, 0.25f);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            tex.SetPixels(px);

            // faint yard lines
            for (int y = h / 6; y < h; y += h / 6)
                DrawLine(tex, 4, y, w - 5, y, lineCol, 1);

            if (route != null && route.waypoints != null && route.waypoints.Length > 0)
            {
                // scale: route extents (meters) → texture space; WR start near bottom center
                float maxZ = 5f, maxX = 5f;
                foreach (var wp in route.waypoints)
                {
                    maxZ = Mathf.Max(maxZ, Mathf.Abs(wp.offset.y));
                    maxX = Mathf.Max(maxX, Mathf.Abs(wp.offset.x));
                }
                float sz = (h - 30f) / maxZ;
                float sx = (w * 0.5f - 12f) / Mathf.Max(maxX, 4f);
                float s = Mathf.Min(sz, sx);
                int x0 = w / 2, y0 = 14;

                int px0 = x0, py0 = y0;
                int pxPrev = px0, pyPrev = py0;
                for (int i = 0; i < route.waypoints.Length; i++)
                {
                    int x1 = x0 + Mathf.RoundToInt(route.waypoints[i].offset.x * s);
                    int y1 = y0 + Mathf.RoundToInt(route.waypoints[i].offset.y * s);
                    DrawLine(tex, pxPrev, pyPrev, x1, y1, Color.white, 2);
                    if (route.waypoints[i].isBreak) DrawDot(tex, x1, y1, 3, new Color(1f, 0.85f, 0.2f));
                    pxPrev = x1; pyPrev = y1;
                }
                DrawDot(tex, x0, y0, 3, new Color(0.4f, 0.7f, 1f)); // WR start
                DrawDot(tex, pxPrev, pyPrev, 4, new Color(1f, 0.3f, 0.25f)); // route end
            }

            tex.Apply();
            return tex;
        }

        static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            int guard = 0;
            while (guard++ < 4096)
            {
                DrawDot(tex, x0, y0, thickness - 1, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        static void DrawDot(Texture2D tex, int x, int y, int r, Color c)
        {
            for (int ix = -r; ix <= r; ix++)
                for (int iy = -r; iy <= r; iy++)
                {
                    int tx = x + ix, ty = y + iy;
                    if (tx >= 0 && ty >= 0 && tx < tex.width && ty < tex.height && ix * ix + iy * iy <= r * r)
                        tex.SetPixel(tx, ty, c);
                }
        }
    }
}