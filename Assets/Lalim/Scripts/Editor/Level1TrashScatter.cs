using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Challenges;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 1 ▸ Scatter Zone 1 Trash — rebuilds zone 1's trash so that one visible
    /// piece is one pickup:
    ///
    ///  - The litter-pile models (Prop_Trash_Debris, Prop_Trash_Debris.007 — one mesh holding many
    ///    pieces) are switched off, as are spare bottles/bags.
    ///  - The pickup pool is 25 single pieces (the hard-difficulty count): plastic bottles, plastic
    ///    bags, glass bottles and cans, each typed so the sorting altar matches what it looks like.
    ///  - Every piece lies flat on open sand inside zone 1: not under or inside coral, rocks or
    ///    overhangs, spread out, away from the statue and altar.
    ///
    /// Re-running re-places the same pool (Undo-able). Nothing outside zone 1 is touched.
    /// </summary>
    public static class Level1TrashScatter
    {
        private const string Prefix = "Prop_Trash_Debris";
        private const string BottleMesh = "Prop_Trash_Debris.012";
        private const string BagMesh = "Prop_Trash_Debris.030";
        private const string MessMaker = "Assets/Mess Maker Free/Prefabs/Low Poly/";
        private const string MaterialFolder = "Assets/Lalim/Materials/Trash";

        private const int PlasticBottles = 7, Bags = 6;
        private static readonly string[] GlassPrefabs =
        {
            "Glass Bottles/Beer Bottle Green.prefab", "Glass Bottles/Wine Bottle Green.prefab", "Glass Bottles/Wine Bottle Red Broke.prefab",
            "Glass Bottles/Beer Bottle Green.prefab", "Glass Bottles/Wine Bottle Green.prefab", "Glass Bottles/Wine Bottle Red Broke.prefab",
        };
        private static readonly string[] CanPrefabs =
        {
            "Cans/Soda Can Red Crushed.prefab", "Cans/Beer Can Blue.prefab", "Cans/Soda Can Orange Crushed.prefab",
            "Cans/Beer Can Green Crushed.prefab", "Cans/Soda Can Green.prefab", "Cans/Soda Can Red Crushed.prefab",
        };
        private const float MessMakerScale = 2.2f;   // scene trash is drawn larger than life to read underwater

        // Placement
        private const float MinSpacing = 7f;
        private const float ClearRadius = 1.3f;
        private const float StatueClearance = 9f;
        private const float AltarClearance = 6f;
        private const float EdgeMargin = 6f;

        [MenuItem("Tools/Lalim/Level 1/Scatter Zone 1 Trash")]
        public static void ScatterMenu() => Debug.Log(Scatter(seed: 20261002));

        public static string Scatter(int seed)
        {
            var report = new System.Text.StringBuilder();
            GameObject envGo = GameObject.Find("LuzonScene");
            Transform env = envGo != null ? envGo.transform : null;
            ZoneQuest zone1 = FindZone("Zone1");
            UnderwaterBounds map = Object.FindAnyObjectByType<UnderwaterBounds>(FindObjectsInactive.Include);
            if (env == null || zone1 == null || zone1.ToxicVolume == null || map == null) return "Open 02_Luzon (needs LuzonScene, Zone1 and MapBounds).";

            Bounds zoneBounds = zone1.ToxicVolume.GetComponent<Collider>().bounds;
            var allTrash = new List<GameObject>();
            foreach (Transform c in env)
            {
                // The glass/can pieces this tool made are handled below, not as scene props.
                if (c.name.StartsWith(Prefix + "_Glass_") || c.name.StartsWith(Prefix + "_Can_")) continue;
                if (c.name.StartsWith(Prefix) && zoneBounds.Contains(c.position)) allTrash.Add(c.gameObject);
            }

            // ── 1. Choose the pool and switch everything else off ──
            var pool = new List<(GameObject go, OfferingType type, string id)>();
            int bottles = 0, bags = 0, off = 0;
            foreach (GameObject go in allTrash)
            {
                string mesh = MeshName(go);
                OfferingType type = OfferingType.PlasticBottle;
                bool keep = false;
                if (mesh == BottleMesh && bottles < PlasticBottles) { keep = true; type = OfferingType.PlasticBottle; bottles++; }
                else if (mesh == BagMesh && bags < Bags) { keep = true; type = OfferingType.GroceryBag; bags++; }

                if (keep)
                {
                    Undo.RecordObject(go, "Scatter trash");
                    go.SetActive(true);
                    string prefix = type == OfferingType.PlasticBottle ? "trash_plastic_bottle_" : "trash_plastic_bag_";
                    pool.Add((go, type, prefix + (type == OfferingType.PlasticBottle ? bottles : bags).ToString("00")));
                }
                else
                {
                    StripPickup(go);
                    if (go.activeSelf) { Undo.RecordObject(go, "Scatter trash"); go.SetActive(false); off++; }
                }
            }
            report.AppendLine($"Switched off {off} litter piles / spare pieces; kept {bottles} plastic bottles and {bags} bags.");

            // Glass and cans come from Mess Maker with URP materials.
            for (int i = 0; i < GlassPrefabs.Length; i++)
                pool.Add((EnsureMessMakerPiece(env, $"{Prefix}_Glass_{i + 1:00}", GlassPrefabs[i], true), OfferingType.GlassBottle, $"trash_glass_{i + 1:00}"));
            for (int i = 0; i < CanPrefabs.Length; i++)
                pool.Add((EnsureMessMakerPiece(env, $"{Prefix}_Can_{i + 1:00}", CanPrefabs[i], false), OfferingType.BeverageCan, $"trash_can_{i + 1:00}"));

            // ── 2. Find open sand ──
            var blockers = CollectVisualBlockers(env, allTrash, pool);
            Vector3 statue = zone1.Statue != null ? zone1.Statue.transform.position : zoneBounds.center;
            GameObject altarGo = GameObject.Find("Altar_AmanSinaya");
            Transform altar = altarGo != null ? altarGo.transform : null;
            float minX = Mathf.Max(zoneBounds.min.x, map.Min.x) + EdgeMargin, maxX = Mathf.Min(zoneBounds.max.x, map.Max.x) - EdgeMargin;
            float minZ = Mathf.Max(zoneBounds.min.z, map.Min.z) + EdgeMargin, maxZ = Mathf.Min(zoneBounds.max.z, map.Max.z) - EdgeMargin;

            // Pieces being placed must not block their own raycasts.
            foreach (var p in pool) p.go.SetActive(false);

            var rng = new System.Random(seed);
            var placed = new List<Vector3>();
            var spots = new List<RaycastHit>();
            float spacing = MinSpacing;
            for (int attempt = 0; attempt < 20000 && spots.Count < pool.Count; attempt++)
            {
                if (attempt == 8000) spacing = MinSpacing * 0.7f;   // relax if the sand is crowded
                float x = Mathf.Lerp(minX, maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ, maxZ, (float)rng.NextDouble());
                if (!OpenSand(new Vector3(x, map.Max.y - 1f, z), map, blockers, out RaycastHit hit)) continue;
                Vector3 pnt = hit.point;
                if (Flat(pnt - statue) < StatueClearance) continue;
                if (altar != null && Flat(pnt - altar.position) < AltarClearance) continue;
                bool crowded = false;
                foreach (Vector3 q in placed) if (Flat(pnt - q) < spacing) { crowded = true; break; }
                if (crowded) continue;
                placed.Add(pnt);
                spots.Add(hit);
            }

            // ── 3. Lay each piece flat on its spot ──
            for (int i = 0; i < pool.Count; i++)
            {
                var (go, type, id) = pool[i];
                go.SetActive(true);
                if (i >= spots.Count) { report.AppendLine($"No open sand left for {go.name}; left where it was."); continue; }
                LayFlat(go, spots[i], (float)rng.NextDouble() * 360f);
                EnsurePickup(go, type, id);
            }

            EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
            report.AppendLine($"Placed {Mathf.Min(spots.Count, pool.Count)} / {pool.Count} pickups on open sand in zone 1 (spacing {spacing:0.#} m).");
            return report.ToString();
        }

        // ───────────────────────── Placement checks ─────────────────────────

        // Straight down from the water's surface the first thing hit must be the seabed (nothing
        // above it), flat enough, with no collider or coral/rock mesh crowding the spot.
        private static bool OpenSand(Vector3 from, UnderwaterBounds map, List<Bounds> blockers, out RaycastHit hit)
        {
            hit = default;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(map.transform)) continue;   // invisible map walls
                hit = h;
                found = true;
                break;
            }
            if (!found || !IsSeabed(hit.collider)) return false;
            if (hit.normal.y < 0.85f) return false;
            if (hit.point.y < map.Min.y + 1f) return false;

            Vector3 probe = hit.point + Vector3.up * (ClearRadius + 0.15f);
            foreach (Collider c in Physics.OverlapSphere(probe, ClearRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsSeabed(c) || c.transform.IsChildOf(map.transform)) continue;
                return false;
            }
            var room = new Bounds(hit.point + Vector3.up * 0.8f, new Vector3(ClearRadius * 2f, 1.6f, ClearRadius * 2f));
            foreach (Bounds b in blockers) if (b.Intersects(room)) return false;
            return true;
        }

        private static bool IsSeabed(Collider c)
        {
            string n = c.name.ToLowerInvariant();
            return n.Contains("seabed") || n.Contains("terrain") || n.Contains("sand") || c is TerrainCollider;
        }

        // Coral, rocks and plants often have no collider, so their meshes count too.
        private static List<Bounds> CollectVisualBlockers(Transform env, List<GameObject> trash, List<(GameObject go, OfferingType type, string id)> pool)
        {
            var skip = new HashSet<GameObject>(trash);
            foreach (var p in pool) skip.Add(p.go);
            var list = new List<Bounds>();
            foreach (Renderer r in env.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || !r.enabled) continue;
                GameObject top = TopChild(env, r.transform);
                if (top == null || skip.Contains(top) || !top.activeInHierarchy) continue;
                string n = top.name.ToLowerInvariant();
                if (n.Contains("seabed") || n.Contains("terrain") || n.StartsWith("creature_") || n.Contains("light") || n.Contains("water")) continue;
                if (r.bounds.size.magnitude > 60f) continue;   // whole-level meshes would block everything
                list.Add(r.bounds);
            }
            return list;
        }

        private static GameObject TopChild(Transform env, Transform t)
        {
            while (t != null && t.parent != env) t = t.parent;
            return t != null ? t.gameObject : null;
        }

        // Smallest mesh axis points along the ground normal (bottles and cans lie on their side,
        // bags lie flat), random spin, then the lowest point rests on the sand.
        private static void LayFlat(GameObject go, RaycastHit hit, float spin)
        {
            Undo.RecordObject(go.transform, "Scatter trash");
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.GetComponentInChildren<MeshFilter>();
            Vector3 axis = Vector3.up;
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.localScale);
                axis = s.x <= s.y && s.x <= s.z ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
                if (mf.transform != go.transform) axis = mf.transform.localRotation * axis;
            }
            Quaternion lie = Quaternion.FromToRotation(axis, hit.normal);
            go.transform.rotation = Quaternion.AngleAxis(spin, hit.normal) * lie;
            go.transform.position = hit.point;

            Bounds b = WorldBounds(go);
            float sink = Mathf.Min(b.size.x, b.size.y, b.size.z) * 0.12f;   // settle a little into the sand
            go.transform.position += Vector3.up * (hit.point.y - b.min.y - sink);
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ───────────────────────── Pieces ─────────────────────────

        private static GameObject EnsureMessMakerPiece(Transform env, string name, string prefabPath, bool glass)
        {
            Transform existing = env.Find(name);
            if (existing != null) return existing.gameObject;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MessMaker + prefabPath);
            GameObject go = Object.Instantiate(prefab);
            go.name = name;
            foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(rb);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++) mats[m] = UrpMaterial(mats[m], glass);
                r.sharedMaterials = mats;
            }
            go.transform.SetParent(env, true);
            go.transform.localScale = Vector3.one * (MessMakerScale / env.lossyScale.x);
            Undo.RegisterCreatedObjectUndo(go, "Scatter trash");
            return go;
        }

        // Mess Maker ships 2D-lit materials (they draw flat white in 3D); make URP Lit twins.
        private static Material UrpMaterial(Material source, bool glass)
        {
            if (source == null) return null;
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Lalim/Materials", "Trash");
            string path = $"{MaterialFolder}/{source.name} URP.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Texture tex = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            string n = source.name.ToLowerInvariant();
            Color color = Color.white;
            if (n.Contains("glass green")) color = new Color(0.16f, 0.48f, 0.22f);
            else if (n.Contains("glass red")) color = new Color(0.45f, 0.16f, 0.08f);
            else if (n.Contains("glass")) color = new Color(0.35f, 0.55f, 0.6f);
            else if (n.Contains("metal")) color = new Color(0.72f, 0.74f, 0.76f);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", glass ? 0.88f : 0.55f);
            m.SetFloat("_Metallic", n.Contains("can") || n.Contains("metal") ? 0.7f : 0f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void EnsurePickup(GameObject go, OfferingType type, string id)
        {
            int interactable = LayerMask.NameToLayer("Interactable");
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) { Undo.RecordObject(t.gameObject, "Scatter trash"); if (interactable >= 0) t.gameObject.layer = interactable; }

            SphereCollider col = go.GetComponent<SphereCollider>();
            if (col == null) col = Undo.AddComponent<SphereCollider>(go);
            Bounds b = WorldBounds(go);
            Undo.RecordObject(col, "Scatter trash");
            col.isTrigger = false;
            col.center = go.transform.InverseTransformPoint(b.center);
            col.radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) / Mathf.Max(0.0001f, go.transform.lossyScale.x);

            OfferingItem item = go.GetComponent<OfferingItem>();
            if (item == null) item = Undo.AddComponent<OfferingItem>(go);
            var so = new SerializedObject(item);
            so.FindProperty("_saveId").stringValue = id;
            so.FindProperty("_type").enumValueIndex = (int)type;
            so.ApplyModifiedProperties();
        }

        private static void StripPickup(GameObject go)
        {
            foreach (var c in go.GetComponents<OfferingItem>()) Undo.DestroyObjectImmediate(c);
            foreach (var c in go.GetComponents<Lalim.Abilities.SpiritGlow>()) Undo.DestroyObjectImmediate(c);
        }

        private static string MeshName(GameObject go)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.GetComponentInChildren<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "";
        }

        private static ZoneQuest FindZone(string prefix)
        {
            foreach (ZoneQuest z in Object.FindObjectsByType<ZoneQuest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (z.name.StartsWith(prefix)) return z;
            return null;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
