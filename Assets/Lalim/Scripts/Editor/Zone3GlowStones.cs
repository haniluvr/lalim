using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 2 ▸ Build Zone 3 Glow Stones — Padre Burgos is deep and dark, so like the
    /// teleport scene's glowing mint stones, clusters of fluorescent pebbles (and a soft light with
    /// some of them) line the sides of Trench B, climb the abyss walls in rings and dot the abyss
    /// floor (clear of the ritual's ring of circles around the lair). Rebuilds from scratch each run.
    /// </summary>
    public static class Zone3GlowStones
    {
        private const string Parent = "Visayas_Dressing";
        private const string RootName = "Zone3_GlowStones";
        private const string MaterialFolder = "Assets/Lalim/Materials/Visayas";
        private static readonly string[] StoneModels =
        {
            "Assets/Davis3D/LowpolyOceanPack/Prefabs/Rocks/SM_Rock_Flat_1 Variant.prefab",
            "Assets/Davis3D/LowpolyOceanPack/Prefabs/Rocks/SM_Rock_Flat_2 Variant.prefab",
            "Assets/Davis3D/LowpolyOceanPack/Prefabs/Rocks/SM_Rock_Flat_3 Variant.prefab",
        };

        private static readonly Color Mint = new Color(0.45f, 1f, 0.72f);
        private static readonly Color Cyan = new Color(0.4f, 0.92f, 1f);

        // Padre Burgos: Trench B, and the abyss around Bakunawa's lair.
        private const float TrenchFrom = 1262f, TrenchTo = 1384f, TrenchStep = 13f;
        private static readonly Vector3 AbyssCentre = new Vector3(1560f, 0f, -20f);
        private static readonly float[] WallHeights = { -190f, -170f, -150f, -130f, -110f, -90f, -70f, -50f };
        private const int WallDirections = 26;
        private static readonly float[] FloorRings = { 45f, 62f, 80f, 100f, 120f };   // the ritual's circles lie at ~30 m

        [MenuItem("Tools/Lalim/Level 2/Build Zone 3 Glow Stones")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            Transform parent = GameObject.Find(Parent)?.transform;
            if (parent == null) return $"No {Parent} in the open scene.";
            Transform old = parent.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);

            Material mint = GlowMaterial("M_GlowStone_Mint", Mint, 18f);
            Material cyan = GlowMaterial("M_GlowStone_Cyan", Cyan, 20f);
            var models = new List<GameObject>();
            foreach (string path in StoneModels) { var m = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (m != null) models.Add(m); }
            if (models.Count == 0) return "No stone models.";

            var rng = new System.Random(3);
            int clusters = 0, stones = 0, lights = 0;

            // 1. Along both sides of Trench B, where the floor meets the wall.
            Transform trench = Group(root, "TrenchB");
            int side = 1;
            for (float x = TrenchFrom; x <= TrenchTo; x += TrenchStep, side = -side)
            {
                Vector3 mid = new Vector3(x, -56f, 0f);
                if (!Physics.Raycast(mid, Vector3.forward * side, out RaycastHit wall, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                Vector3 foot = wall.point + wall.normal * 1.2f;
                if (!Physics.Raycast(foot + Vector3.up * 2f, Vector3.down, out RaycastHit floor, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                stones += Cluster(trench, floor.point, floor.normal, 6, 3.2f, models, (clusters % 3 == 2) ? cyan : mint, clusters % 2 == 0, rng, ref lights);
                clusters++;
            }

            // 2. Up the abyss walls, in rings.
            Transform walls = Group(root, "AbyssWalls");
            foreach (float y in WallHeights)
                for (int i = 0; i < WallDirections; i++)
                {
                    float a = (i + (y / 35f) % 1f) / WallDirections * Mathf.PI * 2f;   // each ring turned a little
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 from = new Vector3(AbyssCentre.x, y, AbyssCentre.z);
                    if (!Physics.Raycast(from, dir, out RaycastHit hit, 260f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.distance < 20f) continue;   // inside a pillar near the middle: skip
                    stones += Cluster(walls, hit.point, hit.normal, 5, 4f, models, (i % 4 == 1) ? cyan : mint, i % 6 == 0, rng, ref lights);
                    clusters++;
                }

            // 3. Around the abyss floor, outside the ritual's ring.
            Transform floorGroup = Group(root, "AbyssFloor");
            foreach (float r in FloorRings)
            {
                int n = Mathf.RoundToInt(r / 5f);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + 0.5f) / n * Mathf.PI * 2f;
                    Vector3 p = AbyssCentre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!Physics.Raycast(new Vector3(p.x, -120f, p.z), Vector3.down, out RaycastHit hit, 120f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    stones += Cluster(floorGroup, hit.point, hit.normal, 6, 4.5f, models, (i % 3 == 0) ? cyan : mint, i % 4 == 0, rng, ref lights);
                    clusters++;
                }
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            return $"Zone 3 glow stones: {clusters} clusters, {stones} stones, {lights} lights.";
        }

        private static Transform Group(Transform root, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        /// <summary>A small cluster of glowing pebbles lying on the surface at <paramref name="at"/>, and maybe a soft light over them.</summary>
        private static int Cluster(Transform parent, Vector3 at, Vector3 normal, int count, float spread, List<GameObject> models,
                                   Material mat, bool light, System.Random rng, ref int lights)
        {
            var cluster = new GameObject("GlowStones").transform;
            cluster.SetParent(parent, false);
            cluster.position = at;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, rad = Mathf.Sqrt((float)rng.NextDouble()) * spread;
                Vector3 guess = at + (tangent * Mathf.Cos(ang) + bitangent * Mathf.Sin(ang)) * rad;
                Vector3 p = guess, n = normal;
                if (Physics.Raycast(guess + normal * 1.5f, -normal, out RaycastHit hit, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) { p = hit.point; n = hit.normal; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(models[rng.Next(models.Count)], cluster);
                go.name = "GlowStone";
                foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = Vector3.one;
                Bounds b = Bounds(go);
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float size = Mathf.Lerp(1.6f, 3.4f, (float)rng.NextDouble());
                go.transform.localScale = Vector3.one * (size / Mathf.Max(0.001f, longest));
                go.transform.position = p + n * 0.02f;
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int k = 0; k < mats.Length; k++) mats[k] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                made++;
            }
            if (light)
            {
                var l = new GameObject("Glow").AddComponent<Light>();
                l.transform.SetParent(cluster, false);
                l.transform.position = at + normal * 1.6f;
                l.type = LightType.Point;
                l.color = mat.GetColor("_BaseColor");
                l.range = 18f;
                l.intensity = 3f;
                l.shadows = LightShadows.None;
                lights++;
            }
            return made;
        }

        private static Bounds Bounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        private static Material GlowMaterial(string name, Color colour, float glow)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", Color.Lerp(colour, Color.white, 0.35f));
            m.SetFloat("_Smoothness", 0.45f);
            m.SetFloat("_Metallic", 0f);
            m.SetColor("_EmissionColor", colour * glow);
            // URP re-derives _EMISSION from the GI flags: realtime emissive keeps it on.
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.EnableKeyword("_EMISSION");
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
