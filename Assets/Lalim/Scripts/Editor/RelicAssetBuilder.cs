using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Lalim.Core;
using Lalim.Inventory;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Level 1's relics — one per deity, given after the zone's last minigame and needed in Level 2:
    ///
    ///   Amansinaya → Radiant Conch        (Models/relics/ConchShell.fbx)
    ///   Haik       → Compass              (Models/relics/Compass.fbx)
    ///   Neguno     → Woven Net Fragment   (Models/relics/WovenNetFragment.fbx)
    ///
    /// The source FBXs are ~2M triangles each, far too heavy for a phone. This tool writes a
    /// simplified copy of each mesh (Unity's MeshLodUtility), a URP material with the metallic and
    /// roughness maps packed the way URP wants them, the PF_Relic_* prefab and the ItemData.
    /// The source FBXs are never modified.
    ///
    /// Tools ▸ Lalim ▸ Relics ▸ Build Relic Assets (keeps finished meshes; ▸ Rebuild re-simplifies).
    /// </summary>
    public static class RelicAssetBuilder
    {
        public const string ModelFolder = "Assets/Lalim/Models/relics";
        public const string OptimizedFolder = ModelFolder + "/Optimized";
        public const string MaterialFolder = "Assets/Lalim/Materials/Relics";
        public const string PrefabFolder = "Assets/Lalim/Prefabs/Relics";
        public const string ItemsFolder = "Assets/Lalim/Data/Items";
        public const string ItemDatabasePath = "Assets/Lalim/Data/ItemDatabase.asset";

        private const int TargetTriangles = 16000;
        private const int TextureSize = 1024;

        public struct Relic
        {
            public string Key;          // file stem for generated assets
            public string Fbx;          // source model
            public string Textures;     // Tripo texture prefix
            public string ItemAsset;    // ItemData file name
            public string ItemId;
            public string DisplayName;
            public string Description;
        }

        public static readonly Relic[] All =
        {
            new Relic
            {
                Key = "RadiantConch", Fbx = "ConchShell", Textures = "decorative_seashell_3d_model",
                ItemAsset = "00_RadiantConch", ItemId = GameConstants.Relics.RADIANT_CONCH, DisplayName = "Radiant Conch",
                Description = "The fisherfolk blew conches like this one to call each other home across the water. A gift from Amansinaya.",
            },
            new Relic
            {
                Key = "Compass", Fbx = "Compass", Textures = "magic_portal_ring_3d_model",
                ItemAsset = "01_Compass", ItemId = GameConstants.Relics.COMPASS, DisplayName = "Compass",
                Description = "A navigator's compass that holds its course through any storm. A gift from Haik.",
            },
            new Relic
            {
                Key = "WovenNetFragment", Fbx = "WovenNetFragment", Textures = "coral_bracelet_3d_model",
                ItemAsset = "02_WovenNetFragment", ItemId = GameConstants.Relics.WOVEN_NET_FRAGMENT, DisplayName = "Woven Net Fragment",
                Description = "A piece of an honest net, woven by hand to take only what is needed. A gift from Neguno.",
            },
        };

        // Relic items replaced by the three above.
        private static readonly string[] RetiredItems = { "01_WovenNet", "02_BioluminescentPearl", "03_TideLens" };

        public static string PrefabPath(Relic r) => $"{PrefabFolder}/PF_Relic_{r.Key}.prefab";

        [MenuItem("Tools/Lalim/Relics/Build Relic Assets")]
        public static void BuildMenu() => BuildAsync(false, Report);

        [MenuItem("Tools/Lalim/Relics/Rebuild Relic Meshes")]
        public static void RebuildMenu() => BuildAsync(true, Report);

        private static void Report(string report)
        {
            Debug.Log(report);
            File.WriteAllText("Temp/relic_build.txt", report);
        }

        /// <summary>
        /// Writes everything. Each relic mesh is simplified in stages: welding and UV matching run
        /// on a worker thread, only Unity's LOD generator runs on the main thread (a few seconds,
        /// with a progress bar). <paramref name="done"/> gets the report.
        /// </summary>
        public static void BuildAsync(bool resimplify, System.Action<string> done)
        {
            var report = new System.Text.StringBuilder();
            EnsureFolder(OptimizedFolder);
            var queue = new Queue<Relic>();
            foreach (Relic r in All) if (resimplify || NeedsSimplify(r)) queue.Enqueue(r);

            MeshJob job = null;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                try
                {
                    if (job == null)
                    {
                        if (queue.Count == 0)
                        {
                            EditorApplication.update -= tick;
                            EditorUtility.ClearProgressBar();
                            report.Append(Build(false));
                            done?.Invoke(report.ToString());
                            return;
                        }
                        job = new MeshJob(queue.Dequeue());
                    }
                    if (job.Task != null && !job.Task.IsCompleted) return;
                    if (Step(job, report)) job = null;
                }
                catch (System.Exception e)
                {
                    EditorApplication.update -= tick;
                    EditorUtility.ClearProgressBar();
                    done?.Invoke(report + "ERROR " + e);
                }
            };
            EditorApplication.update += tick;
        }

        /// <summary>Writes materials, prefabs and items. A missing mesh is simplified here too (blocking).</summary>
        public static string Build(bool resimplify)
        {
            var report = new System.Text.StringBuilder();
            EnsureFolder(OptimizedFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            var items = new List<ItemData>();
            foreach (Relic r in All)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r));
                if (mesh == null || resimplify)
                {
                    var job = new MeshJob(r);
                    while (true)
                    {
                        if (job.Task != null) job.Task.Wait();
                        if (Step(job, report)) break;
                    }
                    mesh = job.Result;
                }
                Material mat = EnsureMaterial(r, report);
                GameObject prefab = EnsurePrefab(r, mesh, mat, report);
                items.Add(EnsureItem(r, prefab, report));
            }
            RetireOldItems(items, report);
            EditorUtility.ClearProgressBar();

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // ───────────────────────── Mesh ─────────────────────────
        //
        // The Tripo meshes are split into many pieces along their UV seams. Simplifying them as-is
        // shrinks each piece's edges on its own and tears holes in the surface. So:
        //   1. weld (worker)    — merge vertices that share a position into one closed shell
        //   2. LODs (main)      — Unity's MeshLodUtility simplifies the shell; take the level in budget
        //   3. resolve (worker) — give each corner back the UV/normal of the original vertex whose
        //                         UV island matches the rest of its triangle
        //   4. save (main)      — bake the FBX node transform, centre the pivot, write the asset

        private static string MeshPath(Relic r) => $"{OptimizedFolder}/{r.Key}_Mesh.asset";

        private static bool NeedsSimplify(Relic r)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r));
            return mesh == null || mesh.GetIndexCount(0) / 3 > TargetTriangles * 1.5f;
        }

        private struct PosKey : System.IEquatable<PosKey>
        {
            private readonly int _x, _y, _z;
            public PosKey(Vector3 p)
            {
                _x = Mathf.RoundToInt(p.x * 100000f);
                _y = Mathf.RoundToInt(p.y * 100000f);
                _z = Mathf.RoundToInt(p.z * 100000f);
            }
            public bool Equals(PosKey o) => _x == o._x && _y == o._y && _z == o._z;
            public override bool Equals(object o) => o is PosKey && Equals((PosKey)o);
            public override int GetHashCode() { unchecked { return (_x * 73856093) ^ (_y * 19349663) ^ (_z * 83492791); } }
        }

        private class MeshJob
        {
            public readonly Relic Relic;
            public readonly System.Diagnostics.Stopwatch Timer = System.Diagnostics.Stopwatch.StartNew();
            public int Stage;
            public System.Threading.Tasks.Task Task;
            public Mesh Result;

            public Matrix4x4 Bake;
            public Vector3[] Pos, Nrm;
            public Vector2[] Uv;
            public int[] Tri;
            public int SourceTriangles, WeldedVertices;

            public Dictionary<PosKey, int> Weld;
            public int[] Head, Next;          // per weld: linked list of the source vertices at that position
            public Vector3[] WeldPos;
            public int[] WeldTri;

            public Vector3[] LodPos;
            public int[] LodTri;

            public List<int> OutSource;       // output vertex → source vertex
            public int[] OutTri;

            public MeshJob(Relic r) { Relic = r; }
        }

        /// <summary>Runs the job's next stage. Returns true when the job is finished.</summary>
        private static bool Step(MeshJob job, System.Text.StringBuilder report)
        {
            if (job.Task != null && job.Task.Exception != null) throw job.Task.Exception.InnerException;
            Relic r = job.Relic;
            switch (job.Stage)
            {
                case 0:
                {
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{r.Fbx}.fbx");
                    MeshFilter mf = fbx != null ? fbx.GetComponentInChildren<MeshFilter>(true) : null;
                    if (mf == null || mf.sharedMesh == null)
                    {
                        report.AppendLine($"{r.Key}: source mesh missing.");
                        job.Result = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r));
                        return true;
                    }
                    EditorUtility.DisplayProgressBar("Relics", $"{r.DisplayName}: welding seams…", 0.2f);
                    Mesh src = mf.sharedMesh;
                    job.Bake = fbx.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    job.Pos = src.vertices;
                    job.Nrm = src.normals;
                    job.Uv = src.uv;
                    job.Tri = src.triangles;
                    job.SourceTriangles = job.Tri.Length / 3;
                    job.Task = System.Threading.Tasks.Task.Run(() => WeldVertices(job));
                    job.Stage = 1;
                    return false;
                }
                case 1:
                {
                    EditorUtility.DisplayProgressBar("Relics", $"{r.DisplayName}: simplifying…", 0.5f);
                    GenerateLods(job);
                    job.Task = System.Threading.Tasks.Task.Run(() => ResolveCorners(job));
                    job.Stage = 2;
                    return false;
                }
                default:
                {
                    EditorUtility.DisplayProgressBar("Relics", $"{r.DisplayName}: saving…", 0.9f);
                    job.Result = SaveMesh(job);
                    report.AppendLine($"{r.Key}: {job.SourceTriangles:N0} → {job.Result.GetIndexCount(0) / 3:N0} triangles " +
                                      $"({job.Result.vertexCount:N0} verts; {job.Pos.Length:N0} source verts welded to {job.WeldedVertices:N0}) " +
                                      $"in {job.Timer.Elapsed.TotalSeconds:F0}s.");
                    EditorUtility.ClearProgressBar();
                    return true;
                }
            }
        }

        // Worker thread.
        private static void WeldVertices(MeshJob job)
        {
            int n = job.Pos.Length;
            job.Weld = new Dictionary<PosKey, int>(n / 2);
            job.Next = new int[n];
            var head = new List<int>(n / 2);
            var weldPos = new List<Vector3>(n / 2);
            var weldOf = new int[n];
            for (int i = 0; i < n; i++)
            {
                var key = new PosKey(job.Pos[i]);
                int w;
                if (!job.Weld.TryGetValue(key, out w))
                {
                    w = weldPos.Count;
                    job.Weld.Add(key, w);
                    weldPos.Add(job.Pos[i]);
                    head.Add(-1);
                }
                weldOf[i] = w;
                job.Next[i] = head[w];
                head[w] = i;
            }
            job.Head = head.ToArray();
            job.WeldPos = weldPos.ToArray();
            job.WeldedVertices = weldPos.Count;

            var tri = new List<int>(job.Tri.Length);
            for (int i = 0; i < job.Tri.Length; i += 3)
            {
                int a = weldOf[job.Tri[i]], b = weldOf[job.Tri[i + 1]], c = weldOf[job.Tri[i + 2]];
                if (a == b || b == c || a == c) continue;
                tri.Add(a); tri.Add(b); tri.Add(c);
            }
            job.WeldTri = tri.ToArray();
        }

        // Main thread: Unity's LOD generator on the welded shell.
        private static void GenerateLods(MeshJob job)
        {
            var welded = new Mesh { indexFormat = IndexFormat.UInt32 };
            welded.SetVertices(job.WeldPos);
            welded.SetTriangles(job.WeldTri, 0, false);
            MeshLodUtility.GenerateMeshLods(welded, 16);

            int pick = welded.lodCount - 1;
            for (int i = 0; i < welded.lodCount; i++)
            {
                if ((int)welded.GetLod(0, i).indexCount / 3 <= TargetTriangles * 1.3f) { pick = i; break; }
            }
            MeshLodRange range = welded.GetLod(0, pick);
            int start = (int)range.indexStart, count = (int)range.indexCount;

            // The raw index buffer holds every LOD's indices back to back.
            job.LodTri = new int[count];
            using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(welded))
            {
                var raw = data[0].GetIndexData<uint>();
                for (int i = 0; i < count; i++) job.LodTri[i] = (int)raw[start + i];
            }
            job.LodPos = welded.vertices;   // read back in case the generator reordered vertices
            Object.DestroyImmediate(welded);
        }

        // Worker thread: per triangle, pick for each corner the source vertex (same position) whose
        // UV sits closest to the other two corners', i.e. all three from the same UV island.
        private static void ResolveCorners(MeshJob job)
        {
            const int MaxCandidates = 8;
            var cand = new int[3][];
            var count = new int[3];
            for (int k = 0; k < 3; k++) cand[k] = new int[MaxCandidates];

            var outIndex = new Dictionary<int, int>();
            job.OutSource = new List<int>();
            var tris = new List<int>(job.LodTri.Length);
            bool hasUv = job.Uv.Length == job.Pos.Length;

            for (int t = 0; t < job.LodTri.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int w;
                    count[k] = 0;
                    if (!job.Weld.TryGetValue(new PosKey(job.LodPos[job.LodTri[t + k]]), out w)) continue;
                    for (int v = job.Head[w]; v >= 0 && count[k] < MaxCandidates; v = job.Next[v]) cand[k][count[k]++] = v;
                }
                if (count[0] == 0 || count[1] == 0 || count[2] == 0) continue;

                int best0 = cand[0][0], best1 = cand[1][0], best2 = cand[2][0];
                if (hasUv && (count[0] > 1 || count[1] > 1 || count[2] > 1))
                {
                    float best = float.MaxValue;
                    for (int i = 0; i < count[0]; i++)
                    for (int j = 0; j < count[1]; j++)
                    for (int l = 0; l < count[2]; l++)
                    {
                        Vector2 a = job.Uv[cand[0][i]], b = job.Uv[cand[1][j]], c = job.Uv[cand[2][l]];
                        float d = (a - b).sqrMagnitude + (b - c).sqrMagnitude + (c - a).sqrMagnitude;
                        if (d < best) { best = d; best0 = cand[0][i]; best1 = cand[1][j]; best2 = cand[2][l]; }
                    }
                }
                tris.Add(OutVertex(best0, outIndex, job.OutSource));
                tris.Add(OutVertex(best1, outIndex, job.OutSource));
                tris.Add(OutVertex(best2, outIndex, job.OutSource));
            }
            job.OutTri = tris.ToArray();
        }

        private static int OutVertex(int source, Dictionary<int, int> map, List<int> outSource)
        {
            int idx;
            if (map.TryGetValue(source, out idx)) return idx;
            idx = outSource.Count;
            map[source] = idx;
            outSource.Add(source);
            return idx;
        }

        // Main thread.
        private static Mesh SaveMesh(MeshJob job)
        {
            Relic r = job.Relic;
            int n = job.OutSource.Count;
            var pos = new Vector3[n];
            var nrm = new Vector3[n];
            var uv = new Vector2[n];
            bool hasNrm = job.Nrm.Length == job.Pos.Length, hasUv = job.Uv.Length == job.Pos.Length;
            for (int i = 0; i < n; i++)
            {
                int s = job.OutSource[i];
                pos[i] = job.Bake.MultiplyPoint3x4(job.Pos[s]);
                if (hasNrm) nrm[i] = job.Bake.MultiplyVector(job.Nrm[s]).normalized;
                if (hasUv) uv[i] = job.Uv[s];
            }
            var b = new Bounds(n > 0 ? pos[0] : Vector3.zero, Vector3.zero);
            foreach (Vector3 p in pos) b.Encapsulate(p);
            for (int i = 0; i < n; i++) pos[i] -= b.center;

            var simple = new Mesh { name = $"{r.Key}_Mesh" };
            simple.indexFormat = n > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            simple.vertices = pos;
            if (hasNrm) simple.normals = nrm;
            if (hasUv) simple.uv = uv;
            simple.SetTriangles(job.OutTri, 0);
            if (!hasNrm) simple.RecalculateNormals();
            simple.RecalculateBounds();
            simple.RecalculateTangents();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r));
            if (existing != null)
            {
                EditorUtility.CopySerialized(simple, existing);
                Object.DestroyImmediate(simple);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(simple, MeshPath(r));
            return simple;
        }

        // ───────────────────────── Material ─────────────────────────

        private static Material EnsureMaterial(Relic r, System.Text.StringBuilder report)
        {
            string basePath = $"{ModelFolder}/{r.Textures}_basecolor.JPEG";
            var baseImporter = AssetImporter.GetAtPath(basePath) as TextureImporter;
            if (baseImporter != null && baseImporter.maxTextureSize != TextureSize)
            {
                baseImporter.maxTextureSize = TextureSize;
                baseImporter.SaveAndReimport();
            }
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            Texture2D maskMap = EnsureMetallicSmoothness(r);

            string path = $"{MaterialFolder}/M_Relic_{r.Key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;   // hand tweaks are kept

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"M_Relic_{r.Key}" };
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
            if (maskMap != null)
            {
                mat.SetTexture("_MetallicGlossMap", maskMap);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 1f);
            }
            AssetDatabase.CreateAsset(mat, path);
            report.AppendLine($"Created material M_Relic_{r.Key}.");
            return mat;
        }

        /// <summary>URP Lit reads metallic from R and smoothness from A: packs metallic.JPEG and 1 − roughness.JPEG.</summary>
        private static Texture2D EnsureMetallicSmoothness(Relic r)
        {
            string outPath = $"{OptimizedFolder}/{r.Key}_MetallicSmoothness.png";
            if (File.Exists(outPath)) return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);

            Texture2D metal = LoadRaw($"{ModelFolder}/{r.Textures}_metallic.JPEG");
            Texture2D rough = LoadRaw($"{ModelFolder}/{r.Textures}_roughness.JPEG");
            if (metal == null || rough == null) return null;

            var packed = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
            var px = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                float v = (y + 0.5f) / TextureSize;
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = (x + 0.5f) / TextureSize;
                    byte mv = (byte)Mathf.RoundToInt(metal.GetPixelBilinear(u, v).r * 255f);
                    byte sv = (byte)Mathf.RoundToInt((1f - rough.GetPixelBilinear(u, v).r) * 255f);
                    px[y * TextureSize + x] = new Color32(mv, mv, mv, sv);
                }
            }
            packed.SetPixels32(px);
            File.WriteAllBytes(outPath, packed.EncodeToPNG());
            Object.DestroyImmediate(packed);
            Object.DestroyImmediate(metal);
            Object.DestroyImmediate(rough);

            AssetDatabase.ImportAsset(outPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.maxTextureSize = TextureSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        // Reads the file's own pixels (the imported texture may be compressed or unreadable).
        private static Texture2D LoadRaw(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
            return tex.LoadImage(File.ReadAllBytes(assetPath)) ? tex : null;
        }

        // ───────────────────────── Prefab ─────────────────────────

        private static GameObject EnsurePrefab(Relic r, Mesh mesh, Material mat, System.Text.StringBuilder report)
        {
            string path = PrefabPath(r);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;   // hand edits are kept

            var root = new GameObject($"PF_Relic_{r.Key}");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = model.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.AppendLine($"Created prefab PF_Relic_{r.Key}.");
            return saved;
        }

        // ───────────────────────── Items ─────────────────────────

        private static ItemData EnsureItem(Relic r, GameObject prefab, System.Text.StringBuilder report)
        {
            string path = $"{ItemsFolder}/{r.ItemAsset}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            bool created = item == null;
            if (created) item = ScriptableObject.CreateInstance<ItemData>();

            item.itemId = r.ItemId;
            item.displayName = r.DisplayName;
            if (string.IsNullOrEmpty(item.description)) item.description = r.Description;
            item.itemType = ItemType.Relic;
            item.regionOrigin = Region.Luzon;
            item.isKeyItem = true;
            if (item.worldModelPrefab == null) item.worldModelPrefab = prefab;

            if (created)
            {
                AssetDatabase.CreateAsset(item, path);
                report.AppendLine($"Created item {r.ItemAsset}.");
            }
            else EditorUtility.SetDirty(item);
            return item;
        }

        private static void RetireOldItems(List<ItemData> relics, System.Text.StringBuilder report)
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            var retired = new List<ItemData>();
            foreach (string name in RetiredItems)
            {
                var old = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/{name}.asset");
                if (old != null) retired.Add(old);
            }

            if (db != null)
            {
                var so = new SerializedObject(db);
                SerializedProperty list = so.FindProperty("_items");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (o == null || retired.Contains(o as ItemData))
                    {
                        list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        list.DeleteArrayElementAtIndex(i);
                    }
                }
                // Relics first, in deity order.
                for (int r = 0; r < relics.Count; r++)
                {
                    int at = -1;
                    for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == relics[r]) at = i;
                    if (at < 0) { list.InsertArrayElementAtIndex(r); list.GetArrayElementAtIndex(r).objectReferenceValue = relics[r]; }
                    else if (at != r) list.MoveArrayElement(at, r);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (ItemData old in retired)
            {
                string p = AssetDatabase.GetAssetPath(old);
                AssetDatabase.DeleteAsset(p);
                report.AppendLine($"Removed {Path.GetFileNameWithoutExtension(p)}.");
            }
        }

        // ───────────────────────── Level 2: the Pearl of Luminescence ─────────────────────────
        //
        // Bakunawa's gift (Level 3 uses it to light dark places). The source is a light glTF
        // (Models/relics/Pearl, ~4.9k triangles, imported by glTFast), so it needs no simplifying:
        // its mesh is copied centred and sized like the other relics, its metallic/roughness map is
        // packed for URP at 1024, and the material glows softly. PF_Relic_PearlOfLuminescence is
        // rebuilt in place, so the gift step and the item keep pointing at it.

        private const string PearlFolder = ModelFolder + "/Pearl";
        private const string PearlPrefabPath = PrefabFolder + "/PF_Relic_PearlOfLuminescence.prefab";
        private const string PearlMaterialPath = MaterialFolder + "/M_Relic_Pearl.mat";
        private const float PearlDiameter = 0.75f;

        [MenuItem("Tools/Lalim/Relics/Build Pearl of Luminescence")]
        public static void BuildPearlMenu() => Report(BuildPearl());

        public static string BuildPearl()
        {
            var report = new System.Text.StringBuilder();
            EnsureFolder(OptimizedFolder);

            // Mesh: centred, uniformly scaled to the relic size (the glTF is 100 units across).
            Mesh source = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"{PearlFolder}/PearlOfLuminescence.gltf"))
                if (o is Mesh m) { source = m; break; }
            if (source == null) return "Pearl: PearlOfLuminescence.gltf has no mesh (is glTFast installed?).";
            Vector3[] pos = source.vertices;
            Bounds b = source.bounds;
            float k = PearlDiameter / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            for (int i = 0; i < pos.Length; i++) pos[i] = (pos[i] - b.center) * k;
            var mesh = new Mesh { name = "PearlOfLuminescence_Mesh" };
            mesh.vertices = pos;
            mesh.normals = source.normals;
            mesh.uv = source.uv;
            mesh.SetTriangles(source.triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            string meshPath = $"{OptimizedFolder}/PearlOfLuminescence_Mesh.asset";
            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh != null) { EditorUtility.CopySerialized(mesh, existingMesh); Object.DestroyImmediate(mesh); mesh = existingMesh; EditorUtility.SetDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            report.AppendLine($"Pearl mesh: {mesh.GetIndexCount(0) / 3:N0} triangles, {PearlDiameter} m across.");

            // Textures: phone-sized; the normal map marked as one.
            SetImport($"{PearlFolder}/Pearl_BaseColor.jpg", false);
            SetImport($"{PearlFolder}/Pearl_Normal.jpg", true);
            Texture2D mask = PackGltfMetallicRoughness($"{PearlFolder}/Pearl_MetallicRoughness.jpg", $"{OptimizedFolder}/PearlOfLuminescence_MetallicSmoothness.png");

            // Material: pearl white, very smooth, a soft moonlit glow of its own.
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PearlMaterialPath);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Relic_Pearl" };
            else mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PearlFolder}/Pearl_BaseColor.jpg"));
            mat.SetColor("_BaseColor", new Color(1f, 0.957f, 1f));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PearlFolder}/Pearl_Normal.jpg"));
            mat.SetFloat("_BumpScale", 0.3f);
            mat.EnableKeyword("_NORMALMAP");
            if (mask != null)
            {
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 1f);
            }
            mat.SetColor("_EmissionColor", new Color(0.8f, 0.86f, 1f) * 0.35f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (created) AssetDatabase.CreateAsset(mat, PearlMaterialPath);
            else EditorUtility.SetDirty(mat);

            // Prefab: a root with a Model child, like the other relics.
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PearlPrefabPath) != null
                ? PrefabUtility.LoadPrefabContents(PearlPrefabPath)
                : new GameObject("PF_Relic_PearlOfLuminescence");
            Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(root.GetComponent<MeshFilter>());
            root.transform.localScale = Vector3.one;
            Transform model = root.transform.Find("Model");
            if (model == null) { model = new GameObject("Model").transform; model.SetParent(root.transform, false); }
            // (not ??: in the editor a missing component is a fake null that ?? doesn't catch)
            var mf = model.GetComponent<MeshFilter>();
            if (mf == null) mf = model.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = model.GetComponent<MeshRenderer>();
            if (mr == null) mr = model.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(root, PearlPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            report.AppendLine("Pearl prefab and material rebuilt.");

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        private static void SetImport(string path, bool normal)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
            bool changed = imp.maxTextureSize != TextureSize || (normal && imp.textureType != TextureImporterType.NormalMap);
            imp.maxTextureSize = TextureSize;
            if (normal) imp.textureType = TextureImporterType.NormalMap;
            if (changed) imp.SaveAndReimport();
        }

        /// <summary>glTF packs roughness in G and metallic in B; URP Lit wants metallic in R and smoothness in A.</summary>
        private static Texture2D PackGltfMetallicRoughness(string sourcePath, string outPath)
        {
            Texture2D src = LoadRaw(sourcePath);
            if (src == null) return null;
            var packed = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
            var px = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                float v = (y + 0.5f) / TextureSize;
                for (int x = 0; x < TextureSize; x++)
                {
                    Color c = src.GetPixelBilinear((x + 0.5f) / TextureSize, v);
                    byte mv = (byte)Mathf.RoundToInt(c.b * 255f);
                    byte sv = (byte)Mathf.RoundToInt((1f - c.g) * 255f);
                    px[y * TextureSize + x] = new Color32(mv, mv, mv, sv);
                }
            }
            packed.SetPixels32(px);
            File.WriteAllBytes(outPath, packed.EncodeToPNG());
            Object.DestroyImmediate(packed);
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(outPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.maxTextureSize = TextureSize;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
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
