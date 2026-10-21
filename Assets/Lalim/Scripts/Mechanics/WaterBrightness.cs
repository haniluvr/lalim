using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// How bright the water is right now, from the fog colour (cleansed zones turn it light teal).
    /// Additive glows (spirits, Moontide streaks) only add light, so against bright water they
    /// vanish; things that must stay readable switch to, or add, a deeper see-through layer as
    /// <see cref="Contrast"/> rises.
    /// </summary>
    public static class WaterBrightness
    {
        private static readonly Dictionary<Material, Material> _shades = new Dictionary<Material, Material>();
        private static Texture2D _dot;

        /// <summary>0 in dark (polluted, deep) water … 1 in bright clean water.</summary>
        public static float Contrast
        {
            get
            {
                Color f = RenderSettings.fogColor;
                float lum = 0.2126f * f.r + 0.7152f * f.g + 0.0722f * f.b;
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.45f, lum));
            }
        }

        /// <summary>
        /// A see-through (alpha-blended) copy of an additive particle material: same texture, but
        /// it can darken, so a deep colour shows on bright water. One copy per source, shared.
        /// </summary>
        public static Material ShadeOf(Material additive)
        {
            if (additive == null) return null;
            if (_shades.TryGetValue(additive, out Material m) && m != null) return m;
            m = new Material(additive) { name = additive.name + " (bright water)" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            // Glow textures are faint halos (bloom fills them in); a see-through colour needs a solid soft dot.
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", SoftDot());
            m.SetColor("_BaseColor", Color.white);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Drawn just before the additive glows, so a bright core still sits on top of it.
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 1;
            _shades[additive] = m;
            return m;
        }

        /// <summary>A white dot, opaque in the middle and fading smoothly to nothing at the edge.</summary>
        private static Texture2D SoftDot()
        {
            if (_dot != null) return _dot;
            const int Size = 64;
            _dot = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "SoftDot (runtime)", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x + 0.5f) / Size * 2f - 1f, dy = (y + 0.5f) / Size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);   // smooth falloff
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            _dot.SetPixels32(px);
            _dot.Apply(true, true);
            return _dot;
        }
    }
}
