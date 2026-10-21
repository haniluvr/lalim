using System;
using UnityEngine;

namespace Lalim.UI
{
    /// <summary>
    /// Generated sprites for the frosted-glass minigame panels (Sea Sensors, coral site prep):
    /// rounded cards and outlines (9-sliced), a top sheen, arc rings, circles, an alert ring, a check, a triangle,
    /// a light bulb, a chevron and a closed eye.
    /// Made once at runtime, so the panels need no art.
    /// </summary>
    public static class GlassSprites
    {
        private static Sprite _rounded, _roundedOutline, _roundedSheen, _ring, _ringWide, _circle, _circleOutline, _alertRing, _check, _triangle, _bulb, _chevron, _closedEye;

        public static Sprite Rounded => _rounded ??= Make(128, 48, (x, y) => RoundedBox(x, y, 128, 48));
        public static Sprite RoundedOutline => _roundedOutline ??= Make(128, 48, (x, y) =>
        {
            float d = RoundedSdf(x, y, 128, 48);
            return Mathf.Clamp01(1.5f - Mathf.Abs(d + 2f));
        });
        // Light along the top edge fading down: the frosted-glass sheen.
        public static Sprite RoundedSheen => _roundedSheen ??= Make(128, 48, (x, y) =>
            RoundedBox(x, y, 128, 48) * Mathf.Pow(Mathf.Clamp01((y - 40f) / 88f), 2f) * 0.22f);
        public static Sprite Ring => _ring ??= Make(512, 0, (x, y) => RingAlpha(x, y, 512, 0.725f));
        public static Sprite RingWide => _ringWide ??= Make(512, 0, (x, y) => RingAlpha(x, y, 512, 0.69f));
        public static Sprite Circle => _circle ??= Make(128, 0, (x, y) => Mathf.Clamp01(62f - Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f))));
        public static Sprite CircleOutline => _circleOutline ??= Make(128, 0, (x, y) =>
            Mathf.Clamp01(2.5f - Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) - 58f)));
        // A bold ring with a soft glow either side: reads at a glance even small on a phone (the predator alert).
        public static Sprite AlertRing => _alertRing ??= Make(256, 0, (x, y) =>
        {
            float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f)) - 116f);
            float band = Mathf.Clamp01(4.5f - d);
            float glow = Mathf.Clamp01(1f - d / 11f) * 0.35f;
            return Mathf.Max(band, glow);
        });
        public static Sprite Check => _check ??= Make(128, 0, (x, y) =>
        {
            var p = new Vector2(x, y);
            float d = Mathf.Min(Segment(p, new Vector2(26f, 64f), new Vector2(52f, 38f)), Segment(p, new Vector2(52f, 38f), new Vector2(102f, 92f)));
            return Mathf.Clamp01(9f - d);
        });
        // Pointing up, slightly rounded.
        public static Sprite Triangle => _triangle ??= Make(128, 0, (x, y) =>
        {
            var p = new Vector2(x, y);
            Vector2 a = new Vector2(64f, 118f), b = new Vector2(12f, 14f), c = new Vector2(116f, 14f);
            float e1 = Cross(a, b, p), e2 = Cross(b, c, p), e3 = Cross(c, a, p);
            bool inside = (e1 >= 0f && e2 >= 0f && e3 >= 0f) || (e1 <= 0f && e2 <= 0f && e3 <= 0f);
            float d = Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a)));
            return inside ? 1f : Mathf.Clamp01(1f - d);
        });

        // A light bulb with a screw base and three little rays: the hint button.
        public static Sprite Bulb => _bulb ??= Make(128, 0, (x, y) =>
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float glass = Mathf.Clamp01(31f - Vector2.Distance(p, new Vector2(64f, 70f)));
            // The neck narrows from the glass down to the base.
            float k = Mathf.InverseLerp(52f, 34f, p.y);
            float neckHalf = Mathf.Lerp(19f, 14f, k);
            float neck = p.y >= 34f && p.y <= 56f ? Mathf.Clamp01(neckHalf - Mathf.Abs(p.x - 64f)) : 0f;
            // Screw base: two rounded bands with a gap.
            float band1 = Mathf.Clamp01(5f - Segment(p, new Vector2(52f, 27f), new Vector2(76f, 27f)));
            float band2 = Mathf.Clamp01(4.5f - Segment(p, new Vector2(56f, 15f), new Vector2(72f, 15f)));
            float rays = Mathf.Max(Mathf.Clamp01(3.5f - Segment(p, new Vector2(64f, 112f), new Vector2(64f, 124f))),
                         Mathf.Max(Mathf.Clamp01(3.5f - Segment(p, new Vector2(26f, 100f), new Vector2(18f, 108f))),
                                   Mathf.Clamp01(3.5f - Segment(p, new Vector2(102f, 100f), new Vector2(110f, 108f)))));
            return Mathf.Max(Mathf.Max(glass, neck), Mathf.Max(Mathf.Max(band1, band2), rays));
        });

        // An upward chevron, for "swipe up".
        public static Sprite Chevron => _chevron ??= Make(128, 0, (x, y) =>
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(Segment(p, new Vector2(18f, 44f), new Vector2(64f, 88f)), Segment(p, new Vector2(64f, 88f), new Vector2(110f, 44f)));
            return Mathf.Clamp01(10f - d);
        });

        // A closed eye (a lid curve with lashes hanging down): "hidden here".
        public static Sprite ClosedEye => _closedEye ??= Make(128, 0, (x, y) =>
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            // The lid: a downward-bulging arc from (18, 72) to (110, 72), lowest at (64, 50).
            float lid = float.MaxValue;
            Vector2 prev = new Vector2(18f, 72f);
            for (int i = 1; i <= 16; i++)
            {
                float t = i / 16f;
                var q = new Vector2(Mathf.Lerp(18f, 110f, t), 72f - 22f * Mathf.Sin(t * Mathf.PI));
                lid = Mathf.Min(lid, Segment(p, prev, q));
                prev = q;
            }
            float lashes = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                float t = 0.18f + i * 0.16f;
                var root = new Vector2(Mathf.Lerp(18f, 110f, t), 72f - 22f * Mathf.Sin(t * Mathf.PI));
                var tip = root + new Vector2((t - 0.5f) * 26f, -16f);
                lashes = Mathf.Min(lashes, Segment(p, root, tip));
            }
            return Mathf.Max(Mathf.Clamp01(6.5f - lid), Mathf.Clamp01(5f - lashes));
        });

        private static float RoundedSdf(float x, float y, int size, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(x + 0.5f - size * 0.5f), Mathf.Abs(y + 0.5f - size * 0.5f)) - new Vector2(size * 0.5f - r, size * 0.5f - r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static float RoundedBox(float x, float y, int size, float r) => Mathf.Clamp01(0.5f - RoundedSdf(x, y, size, r));

        private static float RingAlpha(float x, float y, int size, float inner)
        {
            float half = size * 0.5f;
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half));
            return Mathf.Clamp01(half - 1f - d) * Mathf.Clamp01(d - half * inner);
        }

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        private static Sprite Make(int size, int border, Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                 new Vector4(border, border, border, border));
        }
    }
}
