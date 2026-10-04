using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Teleport ▸ Add Arch Portal Video — fills each arch opening of the Teleport
    /// model with the looping portal video.
    ///
    /// The openings are found by casting a grid of rays straight through the arch model: enclosed
    /// empty areas are the openings. Each gets an ArchPortalSurface (rectangle + semicircle) sized
    /// to it, a few centimetres larger so its edges hide inside the stone, placed halfway through
    /// the frame's depth. Portals are numbered left to right as seen from the front. All surfaces share one RenderTexture fed by one VideoPlayer (ArchPortalVideo).
    /// Re-running rebuilds the surfaces; the material is kept if it already exists.
    /// </summary>
    public static class TeleportArchPortals
    {
        private const string ScenePath = "Assets/Lalim/Scenes/Others/TeleportScene/TeleportScene.unity";
        private const string ArchName = "Teleport";
        private const string ContainerName = "ArchPortalVideo";
        private const string ClipPath = "Assets/Lalim/Videos/PortalSwirl_Loop.mp4";
        private const string Folder = "Assets/Lalim/Videos/ArchPortal";
        private const string TexturePath = Folder + "/RT_PortalSwirl.renderTexture";
        private const string MaterialPath = Folder + "/M_ArchPortal.mat";

        private const float Cell = 0.004f;        // grid step in the model's local units (~3.6 cm in the scene)
        private const int MinCells = 150;         // ignore small gaps between ruined stones
        private const int Dilate = 2;             // cells the surface reaches into the stone

        [MenuItem("Tools/Lalim/Teleport/Add Arch Portal Video")]
        public static void BuildMenu()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            GameObject arch = GameObject.Find(ArchName);
            if (arch != null && arch.transform.Find(ContainerName) != null &&
                !EditorUtility.DisplayDialog("Rebuild arch portals?",
                    "This replaces the existing portal surfaces, including any hand placement.", "Rebuild", "Cancel")) return;
            Debug.Log(Build());
        }

        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            GameObject arch = GameObject.Find(ArchName);
            var col = arch != null ? arch.GetComponent<MeshCollider>() : null;
            var mf = arch != null ? arch.GetComponent<MeshFilter>() : null;
            if (col == null || mf == null || mf.sharedMesh == null) return $"'{ArchName}' with a MeshCollider not found in the open scene.";
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);
            if (clip == null) return $"Missing {ClipPath}.";

            List<Opening> openings = FindOpenings(arch.transform, col, mf.sharedMesh.bounds, report);
            if (openings.Count == 0) return report.Append("No arch openings found.").ToString();

            EnsureFolder(Folder);
            RenderTexture rt = EnsureTexture(clip);
            Material mat = EnsureMaterial(rt);

            // Container under the arch at identity, so surfaces are authored in the arch's local space.
            Transform old = arch.transform.Find(ContainerName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Add arch portal video");
            container.transform.SetParent(arch.transform, false);

            float videoAspect = clip.height > 0 ? (float)clip.width / clip.height : 0.5625f;
            var surfaces = new List<Renderer>();
            // Local +X runs right-to-left seen from the front, so number them by descending X (left to right).
            openings.Sort((a, b) => b.CenterX.CompareTo(a.CenterX));
            for (int i = 0; i < openings.Count; i++)
            {
                Opening o = openings[i];
                var go = new GameObject($"Portal_{i}");
                go.transform.SetParent(container.transform, false);
                // Arch shape (rectangle + semicircle) over the opening, edges reaching into the stone.
                float margin = Dilate * Cell;
                go.transform.localPosition = new Vector3(o.CenterX, o.Origin.y + o.MinY * Cell - margin, o.Depth);
                go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                go.AddComponent<ArchPortalSurface>().EditorSetup(o.Width + margin * 2f, o.Height + margin * 2f, videoAspect);
                surfaces.Add(mr);

                Vector3 size = Vector3.Scale(new Vector3(o.Width, o.Height, 0f), arch.transform.lossyScale);
                report.AppendLine($"Portal_{i}: {size.x:F2} × {size.y:F2} m opening, {o.Cells.Count} cells, surface at depth {o.Depth:F3}.");
            }

            container.AddComponent<VideoPlayer>();
            var player = container.AddComponent<ArchPortalVideo>();
            player.EditorSetup(clip, rt, surfaces.ToArray());

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(arch.scene);
            report.AppendLine($"Added {openings.Count} portal surface(s) playing {clip.name} ({clip.length:F2}s loop).");
            report.Append(SetUpGates());
            return report.ToString();
        }

        // ───────────────────────── Finding the openings ─────────────────────────

        private class Opening
        {
            public readonly List<Vector2Int> Cells = new List<Vector2Int>();
            public int MinX = int.MaxValue, MinY = int.MaxValue, MaxX = int.MinValue, MaxY = int.MinValue;
            public float Depth;                  // local z of the surface
            public Vector3 Origin;               // local position of cell (0, 0)
            public float Width => (MaxX - MinX + 1) * Cell;
            public float Height => (MaxY - MinY + 1) * Cell;
            public float CenterX => Origin.x + (MinX + MaxX + 1) * 0.5f * Cell;
        }

        // The model's long side is local X (the row of arches), height local Y, depth local Z.
        private static List<Opening> FindOpenings(Transform arch, MeshCollider col, Bounds b, System.Text.StringBuilder report)
        {
            Physics.SyncTransforms();
            int nx = Mathf.CeilToInt(b.size.x / Cell), ny = Mathf.CeilToInt(b.size.y / Cell);
            var origin = new Vector3(b.min.x, b.min.y, 0f);
            float zFront = b.max.z + 0.05f, zBack = b.min.z - 0.05f;
            Vector3 fwd = arch.TransformDirection(Vector3.back);   // from +z face toward -z face
            float length = arch.TransformVector(new Vector3(0f, 0f, zFront - zBack)).magnitude;

            var open = new bool[nx, ny];
            var frontZ = new float[nx, ny];
            var backZ = new float[nx, ny];
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            {
                float lx = origin.x + (x + 0.5f) * Cell, ly = origin.y + (y + 0.5f) * Cell;
                RaycastHit hit;
                bool front = col.Raycast(new Ray(arch.TransformPoint(new Vector3(lx, ly, zFront)), fwd), out hit, length);
                frontZ[x, y] = front ? arch.InverseTransformPoint(hit.point).z : float.NaN;
                bool back = col.Raycast(new Ray(arch.TransformPoint(new Vector3(lx, ly, zBack)), -fwd), out hit, length);
                backZ[x, y] = back ? arch.InverseTransformPoint(hit.point).z : float.NaN;
                open[x, y] = !front && !back;
            }

            // Open cells connected to the grid border are outside the arches.
            var region = new int[nx, ny];   // 0 unvisited, -1 outside, >0 opening id
            var stack = new Stack<Vector2Int>();
            for (int x = 0; x < nx; x++) { Seed(x, 0); Seed(x, ny - 1); }
            for (int y = 0; y < ny; y++) { Seed(0, y); Seed(nx - 1, y); }
            Flood(-1, null);

            var openings = new List<Opening>();
            int dropped = 0;
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            {
                if (!open[x, y] || region[x, y] != 0) continue;
                var o = new Opening { Origin = origin };
                region[x, y] = openings.Count + 1;
                stack.Push(new Vector2Int(x, y));
                Flood(openings.Count + 1, o);
                if (o.Cells.Count < MinCells) { dropped++; continue; }
                o.Depth = FrameDepth(o, open, region, frontZ, backZ, nx, ny);
                openings.Add(o);
            }
            report.AppendLine($"Scanned {nx}×{ny} rays; {openings.Count} opening(s), {dropped} small gap(s) ignored.");
            return openings;

            void Seed(int sx, int sy)
            {
                if (!open[sx, sy] || region[sx, sy] != 0) return;
                region[sx, sy] = -1;
                stack.Push(new Vector2Int(sx, sy));
            }

            void Flood(int id, Opening o)
            {
                while (stack.Count > 0)
                {
                    Vector2Int c = stack.Pop();
                    if (o != null)
                    {
                        o.Cells.Add(c);
                        o.MinX = Mathf.Min(o.MinX, c.x); o.MaxX = Mathf.Max(o.MaxX, c.x);
                        o.MinY = Mathf.Min(o.MinY, c.y); o.MaxY = Mathf.Max(o.MaxY, c.y);
                    }
                    foreach (Vector2Int d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                    {
                        Vector2Int n = c + d;
                        if (n.x < 0 || n.y < 0 || n.x >= nx || n.y >= ny || !open[n.x, n.y] || region[n.x, n.y] != 0) continue;
                        region[n.x, n.y] = id;
                        stack.Push(n);
                    }
                }
            }
        }

        // Median of the middle of the stone just around the opening.
        private static float FrameDepth(Opening o, bool[,] open, int[,] region, float[,] frontZ, float[,] backZ, int nx, int ny)
        {
            var mids = new List<float>();
            foreach (Vector2Int c in o.Cells)
            {
                for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (x < 0 || y < 0 || x >= nx || y >= ny || open[x, y]) continue;
                    if (float.IsNaN(frontZ[x, y]) || float.IsNaN(backZ[x, y])) continue;
                    mids.Add((frontZ[x, y] + backZ[x, y]) * 0.5f);
                }
            }
            if (mids.Count == 0) return 0f;
            mids.Sort();
            return mids[mids.Count / 2];
        }

        // ───────────────────────── Region gates ─────────────────────────

        private const string FogTexturePath = Folder + "/T_FogPuff.png";
        private const string FogMaterialPath = Folder + "/M_PortalFog.mat";

        private static readonly Region[] GateRegions = { Region.Luzon, Region.Visayas, Region.Mindanao };
        private static readonly string[] GateMessages =
        {
            "",
            "Complete Luzon to open the way to the Visayas.",
            "Complete the Visayas to open the way to Mindanao.",
        };
        private static readonly string[] RegionScenes =
        {
            "Assets/Lalim/Scenes/Levels/01_Luzon/02_Luzon.unity",
            "Assets/Lalim/Scenes/Levels/02_Visayas/03_Visayas.unity",
            "Assets/Lalim/Scenes/Levels/03_Mindanao/04_Mindanao.unity",
        };

        /// <summary>
        /// Tools ▸ Lalim ▸ Teleport ▸ Set Up Arch Gates — Portal_0/1/2 lead to Luzon/Visayas/Mindanao.
        /// Locked ones are grey, fogged and blocked; open ones load their region when swum through.
        /// Leaves the portals' placement and size alone.
        /// </summary>
        [MenuItem("Tools/Lalim/Teleport/Set Up Arch Gates")]
        public static void SetUpGatesMenu() => Debug.Log(SetUpGates());

        public static string SetUpGates()
        {
            var report = new System.Text.StringBuilder();
            GameObject arch = GameObject.Find(ArchName);
            Transform container = arch != null ? arch.transform.Find(ContainerName) : null;
            if (container == null) return "Add the arch portal video first (no Teleport/ArchPortalVideo).";

            EnsureFolder(Folder);
            UsePortalShader(report);
            Material fogMat = EnsureFogMaterial();

            var gates = new Transform[GateRegions.Length];
            for (int i = 0; i < GateRegions.Length; i++)
            {
                Transform portal = container.Find($"Portal_{i}");
                if (portal == null) { report.AppendLine($"Portal_{i} not found."); continue; }
                gates[i] = portal;
                Undo.RegisterFullObjectHierarchyUndo(portal.gameObject, "Set up arch gate");

                // Solid blocker + "complete … first" message (only active while locked).
                Transform blockerT = portal.Find("Blocker");
                if (blockerT == null)
                {
                    blockerT = new GameObject("Blocker").transform;
                    blockerT.SetParent(portal, false);
                }
                var blocker = blockerT.GetComponent<BoxCollider>();
                if (blocker == null) blocker = blockerT.gameObject.AddComponent<BoxCollider>();
                var note = blockerT.GetComponent<BarrierNotification>();
                if (note == null) note = blockerT.gameObject.AddComponent<BarrierNotification>();
                var so = new SerializedObject(note);
                so.FindProperty("_warningMessage").stringValue = GateMessages[i];
                so.FindProperty("_checkRegion").boolValue = true;
                so.FindProperty("_targetRegion").enumValueIndex = (int)GateRegions[i];
                so.FindProperty("_showDistance").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Fog shrouding the arch (only shown while locked).
                Transform fogT = portal.Find("LockedFog");
                if (fogT == null)
                {
                    fogT = new GameObject("LockedFog").transform;
                    fogT.SetParent(portal, false);
                }
                ConfigureFog(fogT.gameObject, fogMat);

                TeleportArchGate gate = portal.GetComponent<TeleportArchGate>();
                if (gate == null) gate = portal.gameObject.AddComponent<TeleportArchGate>();
                gate.EditorSetup(GateRegions[i], fogT.gameObject, blocker);
                report.AppendLine($"Portal_{i} → {GameConstants.Scenes.GetSceneForRegion(GateRegions[i])} ({GateRegions[i]}).");
            }

            // The "… teleport unlocked" banner + burst plays at the arch that just opened.
            for (int i = 1; i < GateRegions.Length; i++)
            {
                if (gates[i] == null) continue;
                TeleportUnlockReveal reveal = null;
                foreach (TeleportUnlockReveal r in arch.GetComponents<TeleportUnlockReveal>())
                {
                    if (new SerializedObject(r).FindProperty("_region").enumValueIndex == (int)GateRegions[i]) reveal = r;
                }
                if (reveal == null) reveal = Undo.AddComponent<TeleportUnlockReveal>(arch);
                var rso = new SerializedObject(reveal);
                rso.FindProperty("_region").enumValueIndex = (int)GateRegions[i];
                rso.FindProperty("_teleporter").objectReferenceValue = gates[i];
                rso.ApplyModifiedProperties();
            }

            EnsureBuildScenes(report);
            EditorSceneManager.MarkSceneDirty(arch.scene);
            return report.ToString();
        }

        private static void UsePortalShader(System.Text.StringBuilder report)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader shader = Shader.Find("Lalim/PortalVideo");
            if (mat == null || shader == null || mat.shader == shader) return;
            Texture video = mat.GetTexture("_BaseMap");
            Color tint = mat.GetColor("_BaseColor");
            mat.shader = shader;
            mat.SetTexture("_BaseMap", video);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(mat);
            report.AppendLine("M_ArchPortal now uses Lalim/PortalVideo (saturation/brightness for locked portals).");
        }

        private static void ConfigureFog(GameObject go, Material mat)
        {
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.72f, 0.8f, 0.6f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // sizes in the portal's own units

            var emission = ps.emission;
            emission.rateOverTime = 8f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 3f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        private static Material EnsureFogMaterial()
        {
            Texture2D puff = EnsureFogTexture();
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FogMaterialPath);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_PortalFog" };
            mat.SetTexture("_BaseMap", puff);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, FogMaterialPath);
            return mat;
        }

        // A soft, slightly lumpy puff: radial falloff broken up by a little fractal noise.
        private static Texture2D EnsureFogTexture()
        {
            if (System.IO.File.Exists(FogTexturePath)) return AssetDatabase.LoadAssetAtPath<Texture2D>(FogTexturePath);
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float falloff = Mathf.Clamp01(1f - d);
                falloff = falloff * falloff * (3f - 2f * falloff);
                float n = 0f, amp = 0.5f, f = 3f;
                for (int o = 0; o < 4; o++) { n += amp * Mathf.PerlinNoise(x / (float)size * f + 17.3f, y / (float)size * f + 4.1f); amp *= 0.5f; f *= 2f; }
                float a = falloff * Mathf.Lerp(0.55f, 1.15f, n);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            System.IO.File.WriteAllBytes(FogTexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(FogTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(FogTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(FogTexturePath);
        }

        private static void EnsureBuildScenes(System.Text.StringBuilder report)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = false;
            foreach (string path in RegionScenes)
            {
                if (!System.IO.File.Exists(path) || scenes.Exists(s => s.path == path)) continue;
                scenes.Add(new EditorBuildSettingsScene(path, true));
                report.AppendLine($"Added {System.IO.Path.GetFileNameWithoutExtension(path)} to Build Settings.");
                changed = true;
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ───────────────────────── Assets ─────────────────────────

        private static RenderTexture EnsureTexture(VideoClip clip)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(TexturePath);
            if (rt != null) return rt;
            rt = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "RT_PortalSwirl",
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            AssetDatabase.CreateAsset(rt, TexturePath);
            return rt;
        }

        private static Material EnsureMaterial(RenderTexture rt)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;   // hand tweaks (tint, glow) are kept
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_ArchPortal" };
            mat.SetTexture("_BaseMap", rt);
            mat.SetColor("_BaseColor", new Color(1.15f, 1.15f, 1.25f, 1f));   // a touch over 1 so bloom lifts it
            mat.SetFloat("_Cull", (float)CullMode.Off);                      // visible from both sides of the arch
            mat.doubleSidedGI = true;
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
