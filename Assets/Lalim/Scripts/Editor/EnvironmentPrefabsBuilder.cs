using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// The level-wide underwater ambience, as prefabs any level can drop in:
    ///   PF_PlanktonParticles       drifting plankton + rising bubbles, kept around the camera
    ///   PF_UnderwaterEffectsVolume the underwater post-processing (bloom, cool tint, vignette)
    ///                              and the underwater sky dome
    ///   Creatures/PF_School_*      schools of fish (MarineSchool)
    ///   Creatures/PF_Wanderer_*    lone turtles / stingrays (WanderingMarineAnimal)
    /// Tools ▸ Lalim ▸ Environment ▸ Build Environment Prefabs makes or upgrades them (only filling
    /// in what's missing); Tools ▸ Lalim ▸ Level 1 ▸ Add Environment To Luzon places them.
    /// </summary>
    public static class EnvironmentPrefabsBuilder
    {
        private const string EnvFolder = "Assets/Lalim/Prefabs/Environment";
        private const string CreatureFolder = "Assets/Lalim/Prefabs/Creatures";
        private const string MatFolder = "Assets/Lalim/Materials/Environment";
        private const string TexFolder = "Assets/Lalim/Textures/Environment";
        private const string PlanktonPath = EnvFolder + "/PF_PlanktonParticles.prefab";
        private const string VolumePath = EnvFolder + "/PF_UnderwaterEffectsVolume.prefab";
        private const string ProfilePath = EnvFolder + "/VP_Underwater.asset";
        private const string SkyMatPath = "Assets/Lalim/Materials/M_UnderwaterSky.mat";
        private const string HaloTexPath = "Assets/Lalim/Textures/Sorting/T_SortHalo.png";
        private const string ModelsFolder = "Assets/Lalim/Models/sea creatures + elements (fbx)";

        private struct SchoolKind { public string name, model; public int count; public float length, radius, speed; }
        private struct WandererKind { public string name, model; public float length, speed, minHeight, maxHeight; }

        private static readonly SchoolKind[] Schools =
        {
            new SchoolKind { name = "ClownFish", model = "ClownFish", count = 10, length = 0.3f, radius = 9f, speed = 1.4f },
            new SchoolKind { name = "BlueTang", model = "DoryFish", count = 9, length = 0.35f, radius = 10f, speed = 1.6f },
            new SchoolKind { name = "ReefFish", model = "BrownFish", count = 10, length = 0.45f, radius = 11f, speed = 1.6f },
            new SchoolKind { name = "Tuna", model = "TunaFish", count = 6, length = 0.9f, radius = 16f, speed = 2.6f },
        };

        private static readonly WandererKind[] Wanderers =
        {
            new WandererKind { name = "Turtle", model = "Turtle", length = 1.2f, speed = 1.1f, minHeight = 1.5f, maxHeight = 6f },
            new WandererKind { name = "StingRay", model = "StingRay", length = 1.4f, speed = 1.3f, minHeight = 0.8f, maxHeight = 3f },
        };

        [MenuItem("Tools/Lalim/Environment/Build Environment Prefabs")]
        private static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            Directory.CreateDirectory(MatFolder);
            Directory.CreateDirectory(TexFolder);
            Directory.CreateDirectory(CreatureFolder);
            UpgradePlankton(report);
            UpgradeEffectsVolume(report);
            EnsureDepthFogPass(report);
            foreach (SchoolKind k in Schools) EnsureSchool(k, report);
            foreach (WandererKind k in Wanderers) EnsureWanderer(k, report);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // ───────────────────────── Bubbles & plankton ─────────────────────────

        private static void UpgradePlankton(System.Text.StringBuilder report)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlanktonPath);
            try
            {
                Material plankton = ParticleMaterial($"{MatFolder}/M_Plankton.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(HaloTexPath), additive: true);
                Material bubbles = ParticleMaterial($"{MatFolder}/M_Bubbles.mat", BubbleTexture(), additive: false);

                var ps = root.GetComponent<ParticleSystem>();
                var r = root.GetComponent<ParticleSystemRenderer>();
                if (r != null && r.sharedMaterial == null) { r.sharedMaterial = plankton; report.AppendLine("Plankton: gave it a soft glowing speck material."); }
                if (ps != null && ps.main.startColor.color.a <= 0.1f)
                {
                    var main = ps.main;
                    main.startColor = new Color(0.75f, 1f, 0.95f, 0.45f);
                }
                if (root.GetComponent<FollowMainCamera>() == null) root.AddComponent<FollowMainCamera>();

                if (root.transform.Find("Bubbles") == null)
                {
                    BuildBubbles(root.transform, bubbles);
                    report.AppendLine("Plankton: added rising bubbles (follows the camera).");
                }
                PrefabUtility.SaveAsPrefabAsset(root, PlanktonPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BuildBubbles(Transform parent, Material material)
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -3f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
            main.startColor = new Color(0.9f, 1f, 1f, 0.85f);
            main.maxParticles = 250;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 22f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 12f, 16f);

            // Rising, wobbling a little on the way up.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.3f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.15f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.maxParticleSize = 0.6f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>A bubble: a bright thin rim, a faint body and a highlight up and to the left.</summary>
        private static Texture2D BubbleTexture()
        {
            string path = $"{TexFolder}/T_Bubble.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                float d = p.magnitude;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.07f);
                float body = d < 0.86f ? 0.12f + 0.25f * Mathf.Pow(d / 0.86f, 4f) : 0f;
                float spot = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(-0.35f, 0.38f)) / 0.2f);
                float a = Mathf.Clamp01(Mathf.Max(rim * 0.9f, body) + spot * spot);
                if (d > 0.95f) a = 0f;
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material ParticleMaterial(string path, Texture2D texture, bool additive)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ───────────────────────── Post-processing & sky ─────────────────────────

        private static void UpgradeEffectsVolume(System.Text.StringBuilder report)
        {
            VolumeProfile profile = UnderwaterProfile(report);
            GameObject root = PrefabUtility.LoadPrefabContents(VolumePath);
            try
            {
                var volume = root.GetComponent<Volume>();
                if (volume != null && volume.sharedProfile == null)
                {
                    volume.sharedProfile = profile;
                    report.AppendLine("Effects volume: assigned the underwater profile.");
                }
                if (root.GetComponent<UnderwaterDepthFog>() == null)
                {
                    root.AddComponent<UnderwaterDepthFog>();
                    report.AppendLine("Effects volume: added the depth-texture underwater fog.");
                }
                if (root.GetComponentInChildren<UnderwaterSky>(true) == null)
                {
                    var sky = new GameObject("UnderwaterSky").AddComponent<UnderwaterSky>();
                    sky.transform.SetParent(root.transform, false);
                    sky.EditorSetup(AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath));
                    report.AppendLine("Effects volume: added the underwater sky.");
                }
                PrefabUtility.SaveAsPrefabAsset(root, VolumePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private const string DepthFogMatPath = MatFolder + "/M_UnderwaterDepthFog.mat";
        private const string DepthFogFeatureName = "UnderwaterDepthFog";

        /// <summary>
        /// The depth fog runs as a Full Screen Pass (before post-processing, needing the depth
        /// texture) on every URP renderer the project uses. It does nothing unless the scene has an
        /// UnderwaterDepthFog (the effects prefab).
        /// </summary>
        private static void EnsureDepthFogPass(System.Text.StringBuilder report)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(DepthFogMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Lalim/Underwater Depth Fog"));
                AssetDatabase.CreateAsset(mat, DepthFogMatPath);
            }

            var renderers = new HashSet<ScriptableRendererData>();
            var assets = new List<RenderPipelineAsset> { GraphicsSettings.defaultRenderPipeline };
            for (int i = 0; i < QualitySettings.names.Length; i++) assets.Add(QualitySettings.GetRenderPipelineAssetAt(i));
            var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (RenderPipelineAsset a in assets)
            {
                if (!(a is UniversalRenderPipelineAsset urp)) continue;
                foreach (ScriptableRendererData data in (ScriptableRendererData[])listField.GetValue(urp)) if (data != null) renderers.Add(data);
            }

            var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (ScriptableRendererData data in renderers)
            {
                if (data.rendererFeatures.Exists(f => f != null && f.name == DepthFogFeatureName)) continue;
                var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = DepthFogFeatureName;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
                feature.requirements = ScriptableRenderPassInput.Depth;
                feature.passMaterial = mat;
                feature.fetchColorBuffer = true;
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long id);
                data.rendererFeatures.Add(feature);
                ((List<long>)mapField.GetValue(data)).Add(id);
                data.SetDirty();
                EditorUtility.SetDirty(data);
                report.AppendLine($"Added the depth-fog pass to {data.name}.");
            }
        }

        /// <summary>Soft bloom (emissive things glow), a cool clear tint and a deep-blue vignette.</summary>
        private static VolumeProfile UnderwaterProfile(System.Text.StringBuilder report)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(0.8f, 0.97f, 1f));

            var color = Add<ColorAdjustments>(profile);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            color.colorFilter.Override(new Color(0.95f, 1f, 1f));

            var white = Add<WhiteBalance>(profile);
            white.temperature.Override(-8f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0f, 0.05f, 0.12f));

            EditorUtility.SetDirty(profile);
            report.AppendLine("Created VP_Underwater (bloom, colour, white balance, vignette).");
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            T c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // ───────────────────────── Marine life ─────────────────────────

        private static void EnsureSchool(SchoolKind k, System.Text.StringBuilder report)
        {
            string path = $"{CreatureFolder}/PF_School_{k.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{k.model}.fbx");
            if (model == null) { report.AppendLine($"Missing model {k.model}."); return; }
            var root = new GameObject($"PF_School_{k.name}");
            // The sea-creature models face -Z: turn them to swim head first.
            root.AddComponent<MarineSchool>().EditorSetup(new[] { model }, k.count, k.length, 180f, k.radius, k.speed);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.AppendLine($"Created {Path.GetFileName(path)} ({k.count} × {k.model}).");
        }

        private static void EnsureWanderer(WandererKind k, System.Text.StringBuilder report)
        {
            string path = $"{CreatureFolder}/PF_Wanderer_{k.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{k.model}.fbx");
            if (model == null) { report.AppendLine($"Missing model {k.model}."); return; }
            var root = new GameObject($"PF_Wanderer_{k.name}");
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the models face -Z
            foreach (Collider c in body.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (Renderer r in body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            Bounds b = RendererBounds(body);
            body.transform.localScale *= k.length / Mathf.Max(0.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            body.transform.position += root.transform.position - RendererBounds(body).center;
            root.AddComponent<WanderingMarineAnimal>().EditorSetup(k.speed, 14f, k.minHeight, k.maxHeight);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.AppendLine($"Created {Path.GetFileName(path)}.");
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ───────────────────────── Placing them in Luzon ─────────────────────────

        [MenuItem("Tools/Lalim/Level 1/Add Environment To Luzon")]
        private static void AddToLuzonMenu() => Debug.Log(AddToLuzon());

        /// <summary>
        /// Adds the bubbles, the effects volume (replacing the old empty one, and taking over the
        /// underwater sky) and marine life around each zone's statue. Safe to run again.
        /// </summary>
        public static string AddToLuzon()
        {
            var report = new System.Text.StringBuilder();
            report.Append(Build());
            var scene = EditorSceneManager.GetActiveScene();

            if (FindInstance(PlanktonPath) == null)
            {
                Undo.RegisterCreatedObjectUndo(PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlanktonPath), scene), "Environment");
                report.AppendLine("Added PF_PlanktonParticles (bubbles + plankton).");
            }

            if (FindInstance(VolumePath) == null)
            {
                foreach (Volume v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (v.sharedProfile == null && PrefabUtility.GetCorrespondingObjectFromSource(v.gameObject) == null)
                    {
                        report.AppendLine($"Removed the empty volume '{v.name}' (no profile).");
                        Undo.DestroyObjectImmediate(v.gameObject);
                    }
                }
                Undo.RegisterCreatedObjectUndo(PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VolumePath), scene), "Environment");
                report.AppendLine("Added PF_UnderwaterEffectsVolume (post-processing + underwater sky).");
            }
            // The sky now lives in the effects prefab.
            foreach (UnderwaterSky sky in Object.FindObjectsByType<UnderwaterSky>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(sky) != null) continue;
                Undo.DestroyObjectImmediate(sky);
                report.AppendLine("Moved the underwater sky into the effects prefab.");
            }

            report.Append(PlaceMarineLife());
            report.Append(AnimateSceneCreatures());
            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        private static GameObject FindInstance(string prefabPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == asset) return t.gameObject;
                }
            }
            return null;
        }

        // Per zone (by statue): two schools and one wanderer.
        private static readonly string[][] ZoneLife =
        {
            new[] { "School_ClownFish", "School_BlueTang", "Wanderer_Turtle" },   // Amansinaya's shallows
            new[] { "School_ReefFish", "School_Tuna", "Wanderer_StingRay" },     // Haik's waters
            new[] { "School_BlueTang", "School_ReefFish", "Wanderer_Turtle" },   // Neguno's hall
        };

        private static string PlaceMarineLife()
        {
            var report = new System.Text.StringBuilder();
            GameObject holder = GameObject.Find("[MarineLife]");
            if (holder != null) return "Marine life already placed (delete [MarineLife] to place again).\n";
            holder = new GameObject("[MarineLife]");
            Undo.RegisterCreatedObjectUndo(holder, "Marine life");

            var statues = new List<StatueDialogueRouter>(Object.FindObjectsByType<StatueDialogueRouter>(FindObjectsSortMode.None));
            statues.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));   // shallow → deep = zone 1 → 3
            for (int z = 0; z < statues.Count && z < ZoneLife.Length; z++)
            {
                Vector3 centre = statues[z].transform.position;
                for (int i = 0; i < ZoneLife[z].Length; i++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CreatureFolder}/PF_{ZoneLife[z][i]}.prefab");
                    if (prefab == null) continue;
                    if (!OpenWaterNear(centre, i * 120f + z * 40f, out Vector3 spot)) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                    go.transform.position = spot;
                    report.AppendLine($"Placed {prefab.name} near {statues[z].name} at {spot:F0}.");
                }
            }
            return report.ToString();
        }

        private struct Swim { public string kind; public float speed, radius, low, high, bob; public bool yawOnly; }

        // The scene model's own creatures (LuzonScene/Creature_*), by kind.
        private static readonly Swim[] CreatureSwims =
        {
            new Swim { kind = "Dolphin", speed = 2.4f, radius = 18f, low = 2f, high = 8f, bob = 0.2f },
            new Swim { kind = "Whale", speed = 1f, radius = 30f, low = 6f, high = 16f, bob = 0.3f },
            new Swim { kind = "Hammerhead", speed = 1.8f, radius = 18f, low = 2f, high = 8f, bob = 0.1f },
            new Swim { kind = "Shark", speed = 1.8f, radius = 18f, low = 2f, high = 8f, bob = 0.1f },
            new Swim { kind = "Turtle", speed = 1f, radius = 12f, low = 1f, high = 5f, bob = 0.15f },
            new Swim { kind = "StingRay", speed = 1f, radius = 12f, low = 0.5f, high = 2f, bob = 0.08f },
            new Swim { kind = "Squid", speed = 1.1f, radius = 10f, low = 2f, high = 6f, bob = 0.2f },
            new Swim { kind = "Eel", speed = 0.7f, radius = 6f, low = 0.4f, high = 1.5f, bob = 0.05f },
            new Swim { kind = "Fish", speed = 1.4f, radius = 10f, low = 1f, high = 5f, bob = 0.1f },
            new Swim { kind = "Octopus", speed = 0.4f, radius = 3f, low = 0.2f, high = 0.8f, bob = 0.05f, yawOnly = true },
        };

        /// <summary>
        /// Makes the scene's own sea creatures swim (the ones trapped in the ghost nets stay put).
        /// Each keeps its placed size and pose; its head is the far end of its longest side (checked
        /// for every kind in Luzon).
        /// </summary>
        public static string AnimateSceneCreatures()
        {
            var report = new System.Text.StringBuilder();
            GameObject scene = GameObject.Find("LuzonScene");
            if (scene == null) return "";
            int count = 0;
            foreach (Transform t in scene.transform)
            {
                if (!t.name.StartsWith("Creature_") || t.name.Contains("Trapped")) continue;
                if (t.GetComponent<WanderingMarineAnimal>() != null) continue;
                Swim? swim = null;
                foreach (Swim sw in CreatureSwims) if (t.name.Contains("_" + sw.kind + "_")) { swim = sw; break; }
                MeshFilter mf = t.GetComponentInChildren<MeshFilter>();
                if (swim == null || mf == null || mf.sharedMesh == null) continue;

                Bounds b = mf.sharedMesh.bounds;
                float sx = mf.transform.TransformVector(Vector3.right * b.size.x).magnitude;
                float sy = mf.transform.TransformVector(Vector3.up * b.size.y).magnitude;
                float sz = mf.transform.TransformVector(Vector3.forward * b.size.z).magnitude;
                Vector3 axis = sx >= sy && sx >= sz ? Vector3.right : sy >= sz ? Vector3.up : Vector3.forward;
                Vector3 head = t.InverseTransformDirection(mf.transform.TransformDirection(axis)).normalized;

                var mover = Undo.AddComponent<WanderingMarineAnimal>(t.gameObject);
                Swim s = swim.Value;
                mover.EditorSetup(s.speed, s.radius, s.low, s.high);
                mover.EditorSetupPosed(head, s.yawOnly, s.bob);
                count++;
            }
            if (count > 0) report.AppendLine($"The scene's own sea creatures swim now ({count}; the ones caught in nets stay put).");
            return report.ToString();
        }

        // Somewhere 10–18 m from the statue, 3 m over the seabed, with room to swim.
        private static bool OpenWaterNear(Vector3 centre, float startAngle, out Vector3 spot)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                float angle = startAngle + tries * 37f;
                float dist = 10f + (tries % 3) * 4f;
                Vector3 p = centre + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist;
                if (!Physics.Raycast(new Vector3(p.x, centre.y + 15f, p.z), Vector3.down, out RaycastHit ground, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                p.y = ground.point.y + 3f;
                if (Physics.CheckSphere(p, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                spot = p;
                return true;
            }
            spot = default;
            return false;
        }
    }
}
