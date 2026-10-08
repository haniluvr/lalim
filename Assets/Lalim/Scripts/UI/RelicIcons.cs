using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Flat icons for the relic HUD button, in the style of the skill bubbles: a solid white shape
    /// with its details cut out as lines (tint it cream with the UI colour). Drawn once in code from
    /// simple shapes, anti-aliased, so they need no art: the Radiant Conch (a spiral shell with its
    /// opening), the Woven Net Fragment (knotted diamond mesh with frayed ends) and the Compass
    /// (ring, ticks and needle). Unknown relics get null.
    /// </summary>
    public static class RelicIcons
    {
        private const int Size = 256;
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cache.Clear();

        public static Sprite For(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_cache.TryGetValue(itemId, out Sprite s) && s != null) return s;
            System.Func<Vector2, float> shape;
            switch (itemId)
            {
                case GameConstants.Relics.RADIANT_CONCH: shape = Conch; break;
                case GameConstants.Relics.WOVEN_NET_FRAGMENT: shape = Net; break;
                case GameConstants.Relics.COMPASS: shape = Compass; break;
                default: return null;
            }
            s = Draw(itemId, shape);
            _cache[itemId] = s;
            return s;
        }

        /// <summary>Rasterises a coverage function (−1..1 square, y up) into a white sprite.</summary>
        private static Sprite Draw(string name, System.Func<Vector2, float> coverage)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "RelicIcon_" + name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[Size * Size];
            const int ss = 3;   // 3×3 supersampling for clean edges
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float sum = 0f;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var p = new Vector2((x + (sx + 0.5f) / ss) / Size * 2f - 1f, (y + (sy + 0.5f) / ss) / Size * 2f - 1f);
                    sum += Mathf.Clamp01(coverage(p));
                }
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(sum / (ss * ss) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        }

        // ───────────────────────── Shape helpers (1 = inside, 0 = outside) ─────────────────────────

        private static Vector2 Rotate(Vector2 p, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
        }

        /// <summary>Signed distance (approx.) to an ellipse: negative inside.</summary>
        private static float Ellipse(Vector2 p, Vector2 centre, Vector2 radii, float degrees)
        {
            Vector2 q = Rotate(p - centre, -degrees);
            float k = new Vector2(q.x / radii.x, q.y / radii.y).magnitude;
            return (k - 1f) * Mathf.Min(radii.x, radii.y);
        }

        private static float Circle(Vector2 p, Vector2 centre, float r) => (p - centre).magnitude - r;

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        private static bool Inside(float d) => d < 0f;
        private static bool Stroke(float d, float halfWidth) => Mathf.Abs(d) < halfWidth;

        // ───────────────────────── Radiant Conch ─────────────────────────

        private static float Conch(Vector2 p)
        {
            // An upright conch, leaning a little: pointed spire on top, a flared lip on the right,
            // the siphon canal at the bottom.
            p = Rotate(p, 18f);
            const float line = 0.03f;

            bool body = Ellipse(p, new Vector2(-0.04f, -0.08f), new Vector2(0.38f, 0.46f), 0f) < 0f
                        || InTriangle(p, new Vector2(-0.30f, -0.30f), new Vector2(0.22f, -0.32f), new Vector2(0.02f, -0.88f));
            bool spire = InTriangle(p, new Vector2(-0.36f, 0.26f), new Vector2(0.30f, 0.26f), new Vector2(-0.02f, 0.90f));
            float lipD = Ellipse(p, new Vector2(0.26f, -0.10f), new Vector2(0.30f, 0.44f), -12f);
            bool lip = lipD < 0f;
            // Knobs on the shoulder.
            bool knob = Circle(p, new Vector2(-0.36f, 0.22f), 0.075f) < 0f || Circle(p, new Vector2(-0.14f, 0.34f), 0.07f) < 0f
                        || Circle(p, new Vector2(0.10f, 0.34f), 0.07f) < 0f;
            if (!(body || spire || lip || knob)) return 0f;

            // Whorl lines across the spire.
            if (spire && !body)
            {
                if (Segment(p, new Vector2(-0.32f, 0.44f), new Vector2(0.24f, 0.40f)) < line) return 0f;
                if (Segment(p, new Vector2(-0.24f, 0.60f), new Vector2(0.15f, 0.57f)) < line * 0.9f) return 0f;
                if (Segment(p, new Vector2(-0.15f, 0.74f), new Vector2(0.07f, 0.72f)) < line * 0.8f) return 0f;
            }
            // The seam where the lip folds over the body, and the dark opening inside it.
            if (body && Stroke(lipD, line)) return 0f;
            if (Ellipse(p, new Vector2(0.22f, -0.14f), new Vector2(0.13f, 0.30f), -12f) < 0f) return 0f;
            // Two growth ridges across the body.
            if (!lip && Stroke(Ellipse(p, new Vector2(-0.04f, 0.30f), new Vector2(0.42f, 0.30f), 0f), line * 0.8f) && p.y < 0.22f && p.y > -0.2f) return 0f;
            if (!lip && Stroke(Ellipse(p, new Vector2(-0.04f, 0.10f), new Vector2(0.40f, 0.34f), 0f), line * 0.8f) && p.y < -0.05f && p.y > -0.42f) return 0f;
            return 1f;
        }

        private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        // ───────────────────────── Woven Net Fragment ─────────────────────────

        private static float Net(Vector2 p)
        {
            // A torn, roughly round patch of diamond mesh.
            float ang = Mathf.Atan2(p.y, p.x);
            float edge = 0.7f + 0.06f * Mathf.Sin(ang * 4f + 0.6f) + 0.03f * Mathf.Sin(ang * 9f);
            float r = p.magnitude;
            if (r > edge + 0.08f) return 0f;

            const float spacing = 0.3f, half = 0.026f;
            float u = (p.x + p.y) / 1.41421f, v = (p.x - p.y) / 1.41421f;
            float du = Mathf.Abs(Mathf.Repeat(u + spacing * 0.5f, spacing) - spacing * 0.5f);
            float dv = Mathf.Abs(Mathf.Repeat(v + spacing * 0.5f, spacing) - spacing * 0.5f);
            bool cord = (du < half || dv < half) && r < edge;
            bool knot = (new Vector2(du, dv)).magnitude < half * 2.1f && r < edge;
            // Frayed ends poking past the torn edge.
            bool fray = r >= edge && r < edge + 0.07f && (du < half * 0.8f || dv < half * 0.8f);
            return cord || knot || fray ? 1f : 0f;
        }

        // ───────────────────────── Compass ─────────────────────────

        private static float Compass(Vector2 p)
        {
            float r = p.magnitude;
            if (Stroke(r - 0.74f, 0.07f)) return 1f;                                  // ring
            // Ticks at north, east, south and west.
            if (Segment(p, new Vector2(0f, 0.52f), new Vector2(0f, 0.62f)) < 0.035f) return 1f;
            if (Segment(p, new Vector2(0f, -0.52f), new Vector2(0f, -0.62f)) < 0.035f) return 1f;
            if (Segment(p, new Vector2(0.52f, 0f), new Vector2(0.62f, 0f)) < 0.035f) return 1f;
            if (Segment(p, new Vector2(-0.52f, 0f), new Vector2(-0.62f, 0f)) < 0.035f) return 1f;
            // The needle, tilted: north half solid, south half outlined.
            Vector2 q = Rotate(p, -35f);
            float halfWidth = 0.13f * (1f - Mathf.Abs(q.y) / 0.5f);
            bool inNeedle = Mathf.Abs(q.y) < 0.5f && Mathf.Abs(q.x) < halfWidth;
            if (inNeedle && q.y > 0f) return 1f;
            if (inNeedle && q.y <= 0f)
            {
                float inner = 0.13f * (1f - Mathf.Abs(q.y) / 0.5f) - 0.04f;
                if (Mathf.Abs(q.x) > inner || q.y > -0.06f || q.y < -0.44f) return 1f;
            }
            if (r < 0.06f) return 1f;                                                  // pivot
            return 0f;
        }
    }
}
