using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Lalim.Challenges;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 1 ▸ Build Sorting Shrines — turns the three Prop_Tribal_Statue_Shrine
    /// pillars into the trash-sorting station: a bowl on each (Glass, Plastic, Metal, left to right
    /// as the diver sees them), a glowing orb with the category's icon floating in each bowl, the
    /// diver's sorting spot in front of them and a spot for the dugong out of view.
    ///
    /// Surgical: existing SortBowl_* groups are kept (only moved onto their shrine; delete one to
    /// have it re-made); the old altar placeholders (plinth, placeholder bowls, tray) are removed.
    /// Icons are Textures/Sorting/T_SortIcon_{Glass,Plastic,Metal}.png.
    /// </summary>
    public static class SortingShrinesBuilder
    {
        private const string BowlModel = "Assets/Lalim/Models/items/bowl.glb";
        private const string ArtFolder = "Assets/Lalim/Textures/Sorting";
        private const string MatFolder = "Assets/Lalim/Materials/Sorting";

        private const float BowlWidth = 1.5f;
        private const float OrbHeight = 0.8f;       // orb centre above the bowl's rim base
        private const float OrbSize = 0.75f;
        private const float IconSize = 0.95f;       // relative to the orb
        private const float HaloSize = 2.6f;        // relative to the orb
        private const float StandDistance = 7.0f;   // diver's eye from the shrines' centre
        private const float EyeAboveBowls = 2.0f;
        private const float EyeHeight = 1.7f;       // CameraController's first-person offset

        private static readonly OfferingCategory[] Order = { OfferingCategory.Glass, OfferingCategory.Plastic, OfferingCategory.Metal };

        [MenuItem("Tools/Lalim/Level 1/Build Sorting Shrines")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            SegregationAltar altar = Object.FindFirstObjectByType<SegregationAltar>(FindObjectsInactive.Include);
            if (altar == null) return "No SegregationAltar in the open scene.";
            Transform root = altar.transform;

            var shrines = new List<Transform>();
            for (int i = 1; i <= 3; i++)
            {
                GameObject g = GameObject.Find($"Prop_Tribal_Statue_Shrine_0{i}");
                if (g == null) return $"Prop_Tribal_Statue_Shrine_0{i} not found (it must be active in the scene).";
                shrines.Add(g.transform);
            }

            // The middle of the arc is the shrine closest to the other two; the diver faces it from the open side.
            int middle = 0;
            float bestSum = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float sum = 0f;
                for (int j = 0; j < 3; j++) sum += Vector3.Distance(shrines[i].position, shrines[j].position);
                if (sum < bestSum) { bestSum = sum; middle = i; }
            }
            Vector3 outerMid = Vector3.zero;
            for (int i = 0; i < 3; i++) if (i != middle) outerMid += shrines[i].position * 0.5f;
            Vector3 forward = shrines[middle].position - outerMid;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            shrines.Sort((a, b) => Vector3.Dot(a.position, right).CompareTo(Vector3.Dot(b.position, right)));

            // Old big-altar placeholders go.
            var old = new List<GameObject>();
            foreach (Transform c in root) if (c.name.Contains("PLACEHOLDER") || c.name == "TrayOrigin") old.Add(c.gameObject);
            foreach (GameObject g in old) Undo.DestroyObjectImmediate(g);
            if (old.Count > 0) report.AppendLine($"Removed {old.Count} old altar placeholders.");

            EnsureFolder(ArtFolder);
            EnsureFolder(MatFolder);

            var bowls = new List<OfferingBowl>();
            Vector3 centre = Vector3.zero;
            float topY = float.MinValue;
            for (int i = 0; i < 3; i++)
            {
                OfferingCategory cat = Order[i];
                Bounds b = shrines[i].GetComponent<Renderer>().bounds;
                Vector3 top = new Vector3(b.center.x, b.max.y, b.center.z);
                centre += top / 3f;
                topY = Mathf.Max(topY, top.y);

                Transform group = root.Find($"SortBowl_{cat}");
                bool made = group == null;
                if (made) group = BuildBowl(root, cat, top, forward);
                else
                {
                    Undo.RecordObject(group, "Sorting shrines");
                    group.position = top;
                    group.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                }
                FitIcon(group, cat);
                EnsureGlow(group, cat);
                Transform oldLabel = group.Find("Label");
                if (oldLabel != null) Undo.DestroyObjectImmediate(oldLabel.gameObject);
                bowls.Add(new OfferingBowl
                {
                    category = cat,
                    shrine = shrines[i],
                    top = group,
                    dropPoint = group.Find("DropPoint"),
                    orb = group.Find("Orb"),
                });
                report.AppendLine($"{cat}: {shrines[i].name}, bowl {(made ? "made" : "kept")}");
            }

            // The diver's eye looks at the bowls from the open side of the arc.
            Transform stand = Ensure(root, "SortStandPoint");
            Undo.RecordObject(stand, "Sorting shrines");
            Vector3 eye = new Vector3(centre.x, topY + EyeAboveBowls, centre.z) - forward * StandDistance;
            stand.position = eye - Vector3.up * EyeHeight;
            stand.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Transform dugong = Ensure(root, "SortDugongSpot");
            Undo.RecordObject(dugong, "Sorting shrines");
            dugong.position = stand.position - forward * 4f + right * 2.5f;
            dugong.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Undo.RecordObject(altar, "Sorting shrines");
            altar.EditorSetup(bowls, dugong);
            EditorUtility.SetDirty(altar);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            report.AppendLine($"Sorting spot at {stand.position:F1}, facing the shrines.");
            return report.ToString();
        }

        // ─────────────────────────────────────────────
        // One bowl: model, drop point, orb (+ icon, glow)
        // ─────────────────────────────────────────────

        private static Transform BuildBowl(Transform root, OfferingCategory cat, Vector3 top, Vector3 forward)
        {
            var group = new GameObject($"SortBowl_{cat}").transform;
            Undo.RegisterCreatedObjectUndo(group.gameObject, "Sorting shrines");
            group.SetParent(root, false);
            group.position = top;
            group.rotation = Quaternion.identity;

            // Bowl model, centred on the shrine top and sitting on it.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BowlModel);
            var bowl = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bowl.name = "Bowl";
            bowl.transform.SetParent(group, false);
            bowl.transform.localScale = Vector3.one * BowlWidth;
            Bounds bb = RenderBounds(bowl.transform);
            bowl.transform.position += new Vector3(top.x - bb.center.x, top.y - bb.min.y, top.z - bb.center.z);
            float rim = RenderBounds(bowl.transform).max.y - top.y;

            var drop = new GameObject("DropPoint").transform;
            drop.SetParent(group, false);
            drop.localPosition = Vector3.up * (rim * 0.45f);

            Color tint = Tint(cat);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            orb.transform.SetParent(group, false);
            orb.transform.localPosition = Vector3.up * (rim + OrbHeight * 0.5f);
            orb.transform.localScale = Vector3.one * OrbSize;
            var orbRenderer = orb.GetComponent<MeshRenderer>();
            orbRenderer.sharedMaterial = TransparentUnlit($"{MatFolder}/M_SortOrb_{cat}.mat", new Color(tint.r, tint.g, tint.b, 0.38f), null);
            orbRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            icon.name = "Icon";
            Object.DestroyImmediate(icon.GetComponent<Collider>());
            icon.transform.SetParent(orb.transform, false);
            icon.transform.localScale = Vector3.one * 0.8f;
            var iconRenderer = icon.GetComponent<MeshRenderer>();
            iconRenderer.sharedMaterial = TransparentUnlit($"{MatFolder}/M_SortIcon_{cat}.mat", Color.white, IconTexture(cat));
            iconRenderer.shadowCastingMode = ShadowCastingMode.Off;
            icon.AddComponent<FaceCamera>();


            group.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            return group;
        }

        private static Color Tint(OfferingCategory cat)
        {
            switch (cat)
            {
                case OfferingCategory.Plastic: return new Color(1f, 0.62f, 0.2f);
                case OfferingCategory.Glass: return new Color(0.3f, 0.9f, 1f);
                default: return new Color(0.78f, 0.8f, 0.88f);
            }
        }

        // ─────────────────────────────────────────────
        // Materials and icons
        // ─────────────────────────────────────────────

        private static Material TransparentUnlit(string path, Color color, Texture2D tex)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D IconTexture(OfferingCategory cat)
        {
            string path = $"{ArtFolder}/T_SortIcon_{cat}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None))
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;   // keep the picture's proportions
                importer.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogWarning($"[SortingShrines] Missing icon {path}");
            return tex;
        }

        /// <summary>
        /// The orb's glow: a bright core, a soft additive halo behind it (SegregationAltar makes it
        /// breathe) and a coloured light on the bowl. The level has no bloom, so the halo does the glowing.
        /// </summary>
        private static void EnsureGlow(Transform group, OfferingCategory cat)
        {
            Transform orb = group.Find("Orb");
            if (orb == null) return;
            Color tint = Tint(cat);

            var orbRenderer = orb.GetComponent<Renderer>();
            if (orbRenderer != null)
                orbRenderer.sharedMaterial = TransparentUnlit($"{MatFolder}/M_SortOrb_{cat}.mat",
                    new Color(Mathf.Lerp(tint.r, 1f, 0.35f), Mathf.Lerp(tint.g, 1f, 0.35f), Mathf.Lerp(tint.b, 1f, 0.35f), 0.55f), null);

            Transform halo = orb.Find("Halo");
            if (halo == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Halo";
                Object.DestroyImmediate(go.GetComponent<Collider>());
                Undo.RegisterCreatedObjectUndo(go, "Sorting shrines");
                halo = go.transform;
                halo.SetParent(orb, false);
                halo.localScale = Vector3.one * HaloSize;
                go.AddComponent<FaceCamera>();
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Material haloMat = TransparentUnlit($"{MatFolder}/M_SortHalo_{cat}.mat", new Color(tint.r, tint.g, tint.b, 0.9f), HaloTexture());
            haloMat.SetFloat("_Blend", 2f);                                   // URP Additive: it only ever brightens
            haloMat.SetFloat("_DstBlend", (float)BlendMode.One);
            SetQueueOffset(haloMat, -10);                                      // behind the orb and icon
            EditorUtility.SetDirty(haloMat);
            halo.GetComponent<Renderer>().sharedMaterial = haloMat;

            Transform lightT = orb.Find("Glow");
            Light light = lightT != null ? lightT.GetComponent<Light>() : null;
            if (light == null)
            {
                light = new GameObject("Glow").AddComponent<Light>();
                Undo.RegisterCreatedObjectUndo(light.gameObject, "Sorting shrines");
                light.transform.SetParent(orb, false);
            }
            light.type = LightType.Point;
            light.color = tint;
            light.range = 4f;
            light.intensity = 4f;
            light.shadows = LightShadows.None;
        }

        // URP re-validates materials (queue from the surface type, blend from _Blend), so draw order
        // goes through its _QueueOffset property rather than renderQueue alone.
        private static void SetQueueOffset(Material m, int offset)
        {
            m.SetFloat("_QueueOffset", offset);
            m.renderQueue = (int)RenderQueue.Transparent + offset;
            EditorUtility.SetDirty(m);
        }

        /// <summary>Soft round falloff, white, for the halos.</summary>
        private static Texture2D HaloTexture()
        {
            string path = $"{ArtFolder}/T_SortHalo.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f).magnitude;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                px[y * N + x] = new Color(1f, 1f, 1f, a * a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Points the orb's icon at the category's picture, keeping the picture's proportions.</summary>
        private static void FitIcon(Transform group, OfferingCategory cat)
        {
            Transform icon = group.Find("Orb/Icon");
            if (icon == null) return;
            Texture2D tex = IconTexture(cat);
            var r = icon.GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null)
            {
                if (tex != null) r.sharedMaterial.SetTexture("_BaseMap", tex);
                SetQueueOffset(r.sharedMaterial, 10);   // drawn over the orb, true colours
                EditorUtility.SetDirty(r.sharedMaterial);
            }
            if (tex == null) return;
            float aspect = (float)tex.width / tex.height;
            Undo.RecordObject(icon, "Sorting shrines");
            icon.localScale = aspect >= 1f ? new Vector3(IconSize, IconSize / aspect, 1f) : new Vector3(IconSize * aspect, IconSize, 1f);
        }

        // ─────────────────────────────────────────────

        private static Transform Ensure(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Sorting shrines");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Bounds RenderBounds(Transform t)
        {
            Bounds b = new Bounds(t.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
