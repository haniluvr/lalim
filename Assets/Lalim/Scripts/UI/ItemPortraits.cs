using System.Collections.Generic;
using UnityEngine;
using Lalim.Inventory;

namespace Lalim.UI
{
    /// <summary>
    /// Item pictures for the inventory and the relic HUD buttons: each item's world model is
    /// rendered once, three-quarter view on a clear background, and cached for the session.
    /// Items have no icon art yet; an item with an icon sprite should use that instead.
    /// </summary>
    public static class ItemPortraits
    {
        private const int Size = 256;
        private static readonly Dictionary<string, RenderTexture> _cache = new Dictionary<string, RenderTexture>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _cache.Clear();
            _silhouettes.Clear();
        }

        /// <summary>The item's portrait, or null when it has no model.</summary>
        public static Texture Get(ItemData item)
        {
            if (item == null) return null;
            if (item.icon != null) return item.icon.texture;
            if (_cache.TryGetValue(item.itemId, out RenderTexture cached) && cached != null && cached.IsCreated()) return cached;
            RenderTexture rt = Render(item.worldModelPrefab);
            _cache[item.itemId] = rt;
            return rt;
        }

        private static readonly Dictionary<string, Texture2D> _silhouettes = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The item as a white shape with a little shading (tint it with the UI colour), like the
        /// line-art icons on the skill bubbles. Made from the portrait once and cached.
        /// </summary>
        public static Texture GetSilhouette(ItemData item)
        {
            if (item == null) return null;
            if (_silhouettes.TryGetValue(item.itemId, out Texture2D cached) && cached != null) return cached;
            var rt = Get(item) as RenderTexture;
            if (rt == null) return null;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false) { name = "ItemSilhouette", wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = prev;

            Color[] px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                float a = Mathf.Clamp01(px[i].a * 1.4f);
                float shade = Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(px[i].grayscale * 1.6f));
                px[i] = new Color(shade, shade, shade, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            _silhouettes[item.itemId] = tex;
            return tex;
        }

        private static RenderTexture Render(GameObject model)
        {
            if (model == null) return null;
            var stage = new GameObject("[ItemPortrait]");
            stage.transform.position = new Vector3(0f, -6000f, 0f);
            GameObject m = Object.Instantiate(model, stage.transform);
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(-15f, 145f, 10f);
            foreach (Behaviour b in m.GetComponentsInChildren<Behaviour>()) b.enabled = false;   // no scripts, lights or particles
            foreach (ParticleSystem p in m.GetComponentsInChildren<ParticleSystem>()) p.gameObject.SetActive(false);

            if (!TryBounds(m, out Bounds bounds)) { Object.DestroyImmediate(stage); return null; }
            float half = Mathf.Max(0.01f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * 1.15f);
            float radius = bounds.extents.magnitude;

            // A soft key light so the model reads on its own, whatever the scene lighting is.
            var key = new GameObject("Key").AddComponent<Light>();
            key.transform.SetParent(stage.transform, false);
            key.type = LightType.Directional;
            key.intensity = 1.2f;
            key.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            var rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = "ItemPortrait" };
            var cam = new GameObject("PortraitCamera").AddComponent<Camera>();
            cam.transform.SetParent(stage.transform, false);
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 26f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            float distance = half / Mathf.Tan(13f * Mathf.Deg2Rad);
            cam.transform.position = bounds.center + new Vector3(0f, 0.35f, -1f).normalized * distance;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.2f);
            cam.farClipPlane = distance + radius * 1.5f;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            Object.DestroyImmediate(stage);
            return rt;
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
