using UnityEngine;

namespace Lalim.Challenges.MoonRitual
{
    /// <summary>
    /// The eight moon phases of Bakunawa's ritual, in the order the moon passes through them
    /// (New Moon → Full Moon → back to dark), and the textures the circles draw them with: each
    /// phase's moon (the lit part bright, the dark part a faint disc), a soft ring for the rim and
    /// a vertical fade for the column of light. Each phase also has a colour. Made once at runtime, so the circles need no art.
    /// The moon is drawn as seen from the northern hemisphere: waxing lit on the right.
    /// </summary>
    public static class MoonPhases
    {
        public const int Count = 8;

        public static readonly string[] Names =
        {
            "New Moon", "Waxing Crescent", "First Quarter", "Waxing Gibbous",
            "Full Moon", "Waning Gibbous", "Last Quarter", "Waning Crescent",
        };

        /// <summary>
        /// Each phase's colour, shared by the places that sing it, the schools that belong there and
        /// the ritual's circles. Spread round the colour wheel so neighbours never look alike.
        /// </summary>
        public static readonly Color[] Colors =
        {
            new Color(0.68f, 0.35f, 1f),     // New Moon: violet
            new Color(0.2f, 0.95f, 0.82f),   // Waxing Crescent: teal
            new Color(0.5f, 0.95f, 0.35f),   // First Quarter: green
            new Color(1f, 0.8f, 0.25f),      // Waxing Gibbous: gold
            new Color(0.95f, 0.96f, 1f),     // Full Moon: pearl white
            new Color(1f, 0.52f, 0.32f),     // Waning Gibbous: coral orange
            new Color(1f, 0.38f, 0.5f),      // Last Quarter: rose pink
            new Color(0.25f, 0.42f, 1f),     // Waning Crescent: blue
        };

        public static Color ColorOf(int phase) => phase >= 0 && phase < Count ? Colors[phase] : new Color(0.25f, 0.95f, 1f);

        private static Texture2D[] _glyphs;
        private static Texture2D _ring, _column, _disc;

        /// <summary>The moon of phase <paramref name="phase"/> (0 New Moon … 7 Waning Crescent).</summary>
        public static Texture2D Glyph(int phase)
        {
            if (_glyphs == null) _glyphs = new Texture2D[Count];
            phase = ((phase % Count) + Count) % Count;
            if (_glyphs[phase] == null) _glyphs[phase] = MakeGlyph(phase);
            return _glyphs[phase];
        }

        /// <summary>A thin glowing ring (the circle's rim).</summary>
        public static Texture2D Ring => _ring != null ? _ring : (_ring = Make(256, "MoonRing", (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            float band = 1f - Mathf.Abs(d - 0.86f) / 0.06f;
            float halo = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.2f) * 0.35f;
            return Mathf.Clamp01(Mathf.Max(band, halo)) * (d < 1f ? 1f : 0f);
        }));

        /// <summary>A soft filled disc (the glow pooled on the stone under the moon).</summary>
        public static Texture2D Disc => _disc != null ? _disc : (_disc = Make(128, "MoonDisc", (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
        }));

        /// <summary>Light rising from the circle: bright at the bottom, gone at the top, soft at the sides.</summary>
        public static Texture2D Column => _column != null ? _column : (_column = Make(64, "MoonColumn", (u, v) =>
        {
            float up = (v + 1f) * 0.5f;                  // 0 bottom … 1 top
            float side = 1f - Mathf.Abs(u);
            return Mathf.Pow(1f - up, 1.6f) * Mathf.SmoothStep(0f, 1f, side);
        }, TextureWrapMode.Clamp));

        private static Texture2D MakeGlyph(int phase)
        {
            const int size = 256;
            const float r = 0.78f;                      // the moon's radius (of the half-size)
            float angle = phase * Mathf.PI * 2f / Count;
            float c = Mathf.Cos(angle);
            bool waxing = phase <= Count / 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MoonGlyph_" + phase };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float inside = Mathf.Clamp01((r - d) * size * 0.5f);          // anti-aliased edge
                float a = 0f, lum = 0f;
                if (inside > 0f)
                {
                    // The terminator: an ellipse whose half-width is r·cos(angle) at this height.
                    float edge = Mathf.Sqrt(Mathf.Max(0f, r * r - v * v));
                    float xt = edge * c;
                    float lit = waxing ? Mathf.Clamp01((u - xt) * size * 0.5f) : Mathf.Clamp01((-xt - u) * size * 0.5f);
                    // Lit: bright pearl. Dark: a faint glow (earthshine), so the New Moon still reads.
                    lum = Mathf.Lerp(0.5f, 1f, lit);
                    a = inside * Mathf.Lerp(0.38f, 1f, lit);
                }
                // A bright rim round the whole moon (thick enough to read from a distance, so the
                // New Moon is a glowing ring rather than a dark disc).
                float outline = Mathf.Clamp01(1f - Mathf.Abs(d - r) * size * 0.5f / 5f);
                a = Mathf.Max(a, outline);
                lum = Mathf.Max(lum, outline);
                // A small dot above the moon marks its top, so waxing and waning (lit on the right
                // or the left) can't be mistaken whichever way the moon is seen.
                float dot = Mathf.Clamp01((0.065f - Mathf.Sqrt(u * u + (v - 0.905f) * (v - 0.905f))) * size * 0.5f);
                a = Mathf.Max(a, dot);
                lum = Mathf.Max(lum, dot);
                px[y * size + x] = new Color(lum, lum, lum, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static Texture2D Make(int size, string name, System.Func<float, float, float> alpha, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = wrap, name = name };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(u, v)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
