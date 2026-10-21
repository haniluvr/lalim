using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Lalim.Challenges.Catching;
using Lalim.Challenges.Gathering;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Level 2's smaller level pieces from the design doc, built from the zones' own rock and
    /// coral models so they match the art (each Build call removes what it built before):
    ///  - Ajuy: a raised throwing ledge beside each Root Pool (3–4 m above the floor, a clear view
    ///    into the pool), and the last amber lantern at the Trench B lip (the exit cue).
    ///  - Padre Burgos: soft-coral ledges down the abyss wall every 18 m, in three zig-zag
    ///    "ladders", and two wall caves the diver can hide in from Bakunawa (a HidingZone inside
    ///    each). Everything keeps clear of the Moontide currents and the sanctuaries.
    /// </summary>
    public static class Level2LevelPieces
    {
        private static readonly Vector3 AbyssCentre = new Vector3(1565f, 0f, -24f);

        // ───────────────────────── Ajuy ─────────────────────────

        public static string BuildThrowingLedges()
        {
            Physics.SyncTransforms();
            var report = new StringBuilder();
            GameObject flat = FindSource("Z2_Ground_SM_Rock_Flat_1"), boulder = FindSource("Z2_Ground_SM_Rock_Large_1");
            Transform zone = GameObject.Find("Village_Lanterns")?.transform.parent;
            if (flat == null || boulder == null || zone == null) return "Missing Ajuy rock models or the Village_Lanterns object.";
            Transform root = Fresh(zone, "ThrowingLedges");
            Vector3 village = new Vector3(900f, -5f, 0f);

            foreach (RootPool pool in Object.FindObjectsByType<RootPool>(FindObjectsSortMode.None))
            {
                Vector3 c = pool.transform.position;
                float radius = new SerializedObject(pool).FindProperty("_radius").floatValue;
                float bestScore = float.MinValue;
                Vector3 bestFloor = default, bestDir = default;
                for (int k = 0; k < 24; k++)
                {
                    float a = k * 15f * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 xz = c + dir * (radius + 4f);
                    if (!Floor(new Vector3(xz.x, c.y + 6f, xz.z), 30f, out RaycastHit h) || !h.collider.name.StartsWith("Terrain")) continue;
                    float slope = Slope(h.point, 2.5f, c.y + 6f);
                    if (slope > 1.5f) continue;
                    Vector3 top = h.point + Vector3.up * 3.6f;
                    if (Blocked(top, 2.6f) || Blocked(top + Vector3.up * 2f, 2.5f)) continue;
                    if (Physics.Linecast(top + Vector3.up * 1.5f, c, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    // Flat ground, and the village side (the huts frame the view) where it can.
                    float score = -slope * 2f + Vector3.Dot(dir, (village - c).normalized);
                    if (score > bestScore) { bestScore = score; bestFloor = h.point; bestDir = dir; }
                }
                if (bestScore == float.MinValue) { report.AppendLine($"{pool.name}: no clear spot"); continue; }

                var ledge = new GameObject("ThrowLedge_" + pool.name).transform;
                ledge.SetParent(root, false);
                ledge.position = bestFloor;
                float yaw = Mathf.Atan2(-bestDir.x, -bestDir.z) * Mathf.Rad2Deg;
                GameObject b = Place(boulder, ledge, "Base", Quaternion.Euler(0f, yaw + 20f, 0f), new Vector3(3.6f, 3.6f, 3.4f), bottom: bestFloor + Vector3.down * 0.6f);
                float baseTop = b.GetComponent<Renderer>().bounds.max.y;
                GameObject f = Place(flat, ledge, "Ledge", Quaternion.Euler(0f, yaw, 0f), new Vector3(4.6f, 0.9f, 4.2f), bottom: new Vector3(bestFloor.x, baseTop - 0.35f, bestFloor.z));
                report.AppendLine($"{pool.name}: ledge top {f.GetComponent<Renderer>().bounds.max.y - bestFloor.y:F1} m above the floor");
            }
            return report.ToString();
        }

        /// <summary>The design's exit cue: the last amber lantern, at the patch reef's edge by the Trench B lip.</summary>
        public static string BuildLipLantern()
        {
            Physics.SyncTransforms();
            Transform lanterns = GameObject.Find("Village_Lanterns")?.transform;
            GameObject post = FindSource("Z2_Stump_SM_Rock_Tall_2");
            Transform lampSource = lanterns != null ? lanterns.Find("Lantern_Hut_0") : null;
            if (lanterns == null || post == null || lampSource == null) return "Missing the village lanterns or the stump model.";
            Transform old = lanterns.Find("Lantern_TrenchLip");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            float bestScore = float.MinValue;
            Vector3 best = default;
            for (float x = 1125f; x <= 1146f; x += 1.5f)
            for (float z = -30f; z <= 30f; z += 1.5f)
            {
                if (Mathf.Abs(z - 2f) < 9f) continue;   // clear of the Moontide lane into the trench
                if (!Floor(new Vector3(x, 5f, z), 60f, out RaycastHit h) || !h.collider.name.StartsWith("Terrain")) continue;
                if (Blocked(h.point + Vector3.up * 3.5f, 1.8f)) continue;
                float score = x * 0.3f - Mathf.Abs(Mathf.Abs(z - 2f) - 12f);   // at the lip, beside the lane
                if (score > bestScore) { bestScore = score; best = h.point; }
            }
            if (bestScore == float.MinValue) return "No spot for the lip lantern.";

            GameObject p = Place(post, lanterns, "Lantern_TrenchLip", Quaternion.Euler(0f, 30f, 0f), new Vector3(0.9f, 6.5f, 0.9f), bottom: best + Vector3.down * 0.5f);
            float top = p.GetComponent<Renderer>().bounds.max.y;
            GameObject lamp = Object.Instantiate(lampSource.gameObject);
            lamp.name = "Lamp";
            lamp.transform.SetParent(p.transform, true);
            lamp.transform.SetPositionAndRotation(new Vector3(best.x, top + 0.4f, best.z), Quaternion.identity);
            SetWorldSize(lamp, new Vector3(0.55f, 0.55f, 0.55f));
            AddLight(p.transform, lamp.transform.position, new Color(1f, 0.68f, 0.3f), 18f, 2.4f);
            return $"Lip lantern at {best:F1}, lamp {lamp.transform.position.y:F1} m";
        }

        // ───────────────────────── Padre Burgos ─────────────────────────

        public static string BuildWallLedges(int seed)
        {
            Physics.SyncTransforms();
            var report = new StringBuilder();
            GameObject slab = FindSource("Z3_RimRock_SM_Rock_Flat_1");
            var corals = new List<GameObject>();
            foreach (string name in new[] { "TB_Ledge_SM_Plant_Funnel", "TB_Ledge_SM_Plant_Cluster_B", "TB_Ledge_SM_Plant_Sponge_2" })
            {
                GameObject c = FindSource(name);
                if (c != null) corals.Add(c);
            }
            Transform zone = ZoneRoot();
            if (slab == null || zone == null) return "Missing the Padre Burgos rock models or zone object.";
            Transform root = Fresh(zone, "PB_WallLedges");
            var avoid = Avoid();
            var rng = new System.Random(seed);
            int built = 0, skipped = 0;

            float[] ladders = { 145f, 225f, 300f };
            for (int l = 0; l < ladders.Length; l++)
            for (int level = 0; level < 7; level++)
            {
                float y = -66f - level * 18f;
                float angle = ladders[l] + (level % 2 == 0 ? -4f : 4f);   // zig-zag up the face
                if (!WallAt(angle, y, out Vector3 wall, out Vector3 n)) { skipped++; continue; }
                if (Near(wall, avoid, 18f)) { skipped++; continue; }
                Vector3 t = Vector3.Cross(Vector3.up, n);
                const float depth = 5.5f, embed = 1.8f;
                Vector3 centre = wall + n * (depth * 0.5f - embed);
                Quaternion rot = Quaternion.LookRotation(n) * Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 6.0 - 3.0));
                Vector3 topCentre = centre + n * 0.8f + Vector3.up * 1.6f;
                if (Blocked(topCentre, 1.2f)) { skipped++; continue; }

                var ledge = new GameObject($"Ledge_{l}_{level}").transform;
                ledge.SetParent(root, false);
                ledge.position = wall;
                GameObject s = Place(slab, ledge, "Slab", rot, new Vector3(7f, 1.4f, depth), centre: centre);
                if (corals.Count > 0)
                {
                    float top = s.GetComponent<Renderer>().bounds.max.y;
                    GameObject coral = corals[rng.Next(corals.Count)];
                    Vector3 at = centre + n * 1.2f + t * (float)(rng.NextDouble() * 3.0 - 1.5);
                    GameObject g = Place(coral, ledge, "Coral", Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), Vector3.zero, bottom: new Vector3(at.x, top - 0.15f, at.z), maxSize: 1.9f, collider: false);
                    g.isStatic = true;
                }
                built++;
            }
            report.Append($"{built} ledges built, {skipped} skipped (too close to a current, cave or sanctuary, or no clear wall)");
            return report.ToString();
        }

        /// <summary>Two rock hoods built out from the wall: a floor, a roof and two side rocks, open to the pit, with a HidingZone inside.</summary>
        public static string BuildWallCaves()
        {
            Physics.SyncTransforms();
            var report = new StringBuilder();
            GameObject slab = FindSource("Z3_RimRock_SM_Rock_Flat_1"), pillar = FindSource("BasinWall_SM_Rock_Massive_5");
            GameObject weed = FindSource("TB_Ledge_SM_Plant_Cluster_B");
            Transform zone = ZoneRoot();
            if (slab == null || pillar == null || zone == null) return "Missing the Padre Burgos rock models or zone object.";
            Transform root = Fresh(zone, "PB_WallCaves");
            var avoid = Avoid();

            // West wall, mid-depth; east wall, deep: one on each side of the pit.
            var wanted = new[] { (angle: 200f, y: -120f), (angle: 20f, y: -150f) };
            for (int i = 0; i < wanted.Length; i++)
            {
                bool done = false;
                for (int tries = 0; tries < 24 && !done; tries++)
                {
                    float angle = wanted[i].angle + (tries % 2 == 0 ? 1 : -1) * (tries / 2) * 4f;
                    float y = wanted[i].y;
                    if (!WallAt(angle, y, out Vector3 wall, out Vector3 n)) continue;
                    if (Near(wall, avoid, 22f)) continue;
                    Vector3 pocket = wall + n * 3f;
                    Quaternion rot = Quaternion.LookRotation(n);
                    // The pocket must be open water before the hood goes round it.
                    if (Physics.CheckBox(pocket + n * 0.5f, new Vector3(3.5f, 2.6f, 2f), rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;

                    Vector3 t = rot * Vector3.right;
                    var cave = new GameObject($"WallCave_{i + 1}").transform;
                    cave.SetParent(root, false);
                    cave.SetPositionAndRotation(pocket, rot);
                    Place(slab, cave, "Floor", rot, new Vector3(15f, 1.8f, 9f), centre: pocket - n * 0.5f + Vector3.down * 4.6f);
                    Place(slab, cave, "Roof", rot * Quaternion.Euler(0f, 0f, 180f), new Vector3(15f, 2.4f, 9f), centre: pocket - n * 0.5f + Vector3.up * 4.8f);
                    Place(pillar, cave, "SideL", rot * Quaternion.Euler(0f, 12f, 0f), new Vector3(4.5f, 10.5f, 7f), centre: pocket - t * 6.3f);
                    Place(pillar, cave, "SideR", rot * Quaternion.Euler(0f, -12f, 0f), new Vector3(4.5f, 10.5f, 7f), centre: pocket + t * 6.3f);
                    if (weed != null)
                    {
                        // Soft weed at the mouth's corners: it reads as a hiding place, like the Caluya thickets.
                        foreach (float side in new[] { -1f, 1f })
                            Place(weed, cave, "Weed", Quaternion.Euler(0f, side * 40f, 0f), Vector3.zero,
                                  bottom: pocket + n * 3.2f + t * side * 3.8f + Vector3.down * 3.7f, maxSize: 3.2f, collider: false);
                    }

                    var zoneGo = new GameObject("HideInside");
                    zoneGo.transform.SetParent(cave, false);
                    zoneGo.transform.SetPositionAndRotation(pocket, rot);
                    var box = zoneGo.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(9f, 7.5f, 5.5f);
                    var hide = zoneGo.AddComponent<HidingZone>();
                    var so = new SerializedObject(hide);
                    so.FindProperty("_kind").enumValueIndex = (int)HidingZone.Kind.Crevice;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    // A faint glow inside, so the mouth can be spotted across the dark pit.
                    AddLight(cave, pocket - n * 1f, new Color(0.35f, 0.85f, 0.9f), 10f, 1.3f);

                    avoid.Add(pocket);
                    report.AppendLine($"WallCave_{i + 1}: angle {angle:F0}°, {y:F0} m, mouth at {pocket:F1}");
                    done = true;
                }
                if (!done) report.AppendLine($"Cave {i + 1}: no clear wall near {wanted[i].angle}°");
            }
            return report.ToString();
        }

        // ───────────────────────── Look and feel ─────────────────────────

        private const string DressingRoot = "Visayas_Dressing";
        private const string GodRayPrefab = "Assets/Davis3D/LowpolyOceanPack/Prefabs/Prefab_GodRay.prefab";
        private const string WhaleModel = "Assets/Lalim/Models/sea creatures + elements (fbx)/Whale.fbx";
        private const string CreatureDir = "Assets/Lalim/Prefabs/Creatures/";
        private const string GlowMaterialPath = "Assets/Lalim/Materials/Environment/MAT_Anemone_Glow.mat";

        /// <summary>The plankton that follows the camera: each zone's water (see PlanktonByZone).</summary>
        public static string SetUpPlankton()
        {
            ParticleSystem plankton = null;
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ps.name.StartsWith("PF_PlanktonParticles")) plankton = ps;
            if (plankton == null) return "No PF_PlanktonParticles in the scene.";
            var zones = plankton.GetComponent<PlanktonByZone>() ?? Undo.AddComponent<PlanktonByZone>(plankton.gameObject);
            zones.EditorSetBands(new List<PlanktonByZone.Band>
            {
                new PlanktonByZone.Band { name = "Caluya: sparse, bright",   fromX = -1000f, rate = 18f, colour = new Color(0.75f, 1f, 0.95f, 0.5f),  size = new Vector2(0.02f, 0.05f) },
                new PlanktonByZone.Band { name = "Trench A",                 fromX = 480f,   rate = 26f, colour = new Color(0.72f, 0.95f, 0.9f, 0.5f), size = new Vector2(0.02f, 0.06f) },
                new PlanktonByZone.Band { name = "Ajuy: dense silt",         fromX = 690f,   rate = 70f, colour = new Color(0.8f, 0.72f, 0.5f, 0.55f), size = new Vector2(0.03f, 0.08f), sink = 0.0005f },
                new PlanktonByZone.Band { name = "Trench B",                 fromX = 1150f,  rate = 32f, colour = new Color(0.7f, 0.85f, 0.92f, 0.5f), size = new Vector2(0.02f, 0.06f) },
                new PlanktonByZone.Band { name = "Padre Burgos: marine snow", fromX = 1370f, rate = 45f, colour = new Color(0.93f, 0.96f, 1f, 0.6f),  size = new Vector2(0.03f, 0.09f), sink = 0.004f },
                new PlanktonByZone.Band { name = "Exit tunnel: dark",        fromX = 1800f,  rate = 12f, colour = new Color(0.6f, 0.7f, 0.8f, 0.35f),  size = new Vector2(0.02f, 0.05f) },
            });
            EditorUtility.SetDirty(zones);
            return "Plankton: 6 zone bands";
        }

        /// <summary>
        /// Light shafts from the surface (the pack's additive shaft; its tint alpha is its brightness, ~0.04 by default). All counts are 0: Level 2 is meant to read as deeper water, with no shafts
        /// from the surface. Kept so a zone can get them back by raising its count.
        /// </summary>
        public static string BuildGodRays(int seed)
        {
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GodRayPrefab);
            if (prefab == null) return "No god ray prefab.";
            UnderwaterBounds bounds = Object.FindFirstObjectByType<UnderwaterBounds>();
            float surface = bounds != null ? bounds.WaterSurfaceY : 0f;
            Transform root = Fresh(Dressing(), "GodRays");
            var rng = new System.Random(seed);
            var block = new MaterialPropertyBlock();
            var placed = new List<Vector3>();
            var counts = new StringBuilder();
            var zones = new[]
            {
                (name: "Caluya",       min: new Vector2(25f, -210f),   max: new Vector2(465f, 210f), count: 0,  scale: 1f,   tint: new Color(0.6f, 0.95f, 0.9f, 0.06f)),   // none: Caluya reads as deeper water
                (name: "Ajuy",         min: new Vector2(860f, -60f),   max: new Vector2(1120f, 60f), count: 0,  scale: 0.85f, tint: new Color(0.9f, 0.85f, 0.55f, 0.045f)),
                (name: "PadreBurgos",  min: new Vector2(1390f, -180f), max: new Vector2(1740f, 150f), count: 0, scale: 1.7f, tint: new Color(0.55f, 0.72f, 1f, 0.04f)),
            };
            foreach (var z in zones)
            {
                int made = 0;
                for (int tries = 0; tries < z.count * 30 && made < z.count; tries++)
                {
                    float x = Mathf.Lerp(z.min.x, z.max.x, (float)rng.NextDouble());
                    float zz = Mathf.Lerp(z.min.y, z.max.y, (float)rng.NextDouble());
                    // Open water from the surface down: nothing overhead (Ajuy's canopy, huts).
                    if (Physics.Raycast(new Vector3(x, surface - 0.5f, zz), Vector3.up, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;   // the safety ceiling is a few metres up
                    if (!Floor(new Vector3(x, surface - 0.6f, zz), 260f, out RaycastHit floor) || floor.point.y > surface - 6f) continue;
                    if (z.name == "Ajuy" && Physics.CheckSphere(new Vector3(x, -3f, zz), 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Near(new Vector3(x, 0f, zz), placed, z.name == "Caluya" ? 24f : 30f)) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                    go.name = $"GodRay_{z.name}_{made}";
                    go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), (float)(rng.NextDouble() * 10.0 - 5.0));
                    go.transform.localScale = Vector3.one * z.scale * (0.85f + (float)rng.NextDouble() * 0.3f);
                    go.transform.position = new Vector3(x, surface, zz);
                    Renderer r = go.GetComponentInChildren<Renderer>();
                    go.transform.position += new Vector3(0f, surface - 0.4f - r.bounds.max.y, 0f);   // hangs from the surface
                    foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    foreach (Renderer rr in go.GetComponentsInChildren<Renderer>())
                    {
                        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        rr.GetPropertyBlock(block);
                        block.SetColor("_BaseColor", z.tint);
                        rr.SetPropertyBlock(block);
                    }
                    placed.Add(new Vector3(x, 0f, zz));
                    made++;
                }
                counts.Append($"{z.name} {made} ");
            }
            return "God rays: " + counts;
        }

        /// <summary>
        /// The trenches' trail markers (the design doc's breadcrumbs): tall kelp every 25 m along
        /// Trench A, and glowing anemones every 20 m along Trench B shifting from Ajuy's amber to
        /// Padre Burgos's cyan. They follow the eastbound Moontide lanes, alternating sides.
        /// </summary>
        public static string BuildBreadcrumbs()
        {
            Physics.SyncTransforms();
            Transform root = Fresh(Dressing(), "TrenchBreadcrumbs");
            var report = new StringBuilder();
            var kelp = new[] { FindSource("Z1_Thicket_SM_Plant_Kelp_1"), FindSource("Z1_Thicket_SM_Plant_Kelp_2") };
            GameObject anemone = FindSource("Z1_Recover_SM_Plant_Anemone") ?? FindSource("Z1_Reef_SM_Plant_Anemone");
            Material glow = GlowMaterial();
            var block = new MaterialPropertyBlock();

            List<Vector3> laneA = Lane("Tide_TrenchA_East"), laneB = Lane("Tide_TrenchB_East");
            int a = 0;
            if (kelp[0] != null && laneA.Count > 1)
                foreach ((Vector3 p, Vector3 side, float t) in Along(laneA, 25f))
                {
                    Vector3 at = p + side * ((a % 2 == 0) ? 7f : -7f);
                    if (!Floor(new Vector3(at.x, p.y + 6f, at.z), 40f, out RaycastHit h)) continue;
                    var tuft = new GameObject($"Kelp_{a}").transform;
                    tuft.SetParent(root, false);
                    tuft.position = h.point;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 o = new Vector3(Mathf.Cos(k * 2.1f + a), 0f, Mathf.Sin(k * 2.1f + a)) * 0.9f;
                        Place(kelp[(a + k) % 2] ?? kelp[0], tuft, "Blade", Quaternion.Euler(0f, a * 47f + k * 120f, 0f), Vector3.zero,
                              bottom: h.point + o + Vector3.down * 0.2f, maxSize: 6f + k * 1.2f, collider: false);
                    }
                    a++;
                }
            report.Append($"Trench A kelp {a}; ");

            int b = 0;
            if (anemone != null && glow != null && laneB.Count > 1)
                foreach ((Vector3 p, Vector3 side, float t) in Along(laneB, 20f))
                {
                    Vector3 at = p + side * ((b % 2 == 0) ? 6f : -6f);
                    if (!Floor(new Vector3(at.x, p.y + 6f, at.z), 60f, out RaycastHit h)) continue;
                    GameObject g = Place(anemone, root, $"GlowAnemone_{b}", Quaternion.Euler(0f, b * 53f, 0f), Vector3.zero,
                                         bottom: h.point + Vector3.down * 0.1f, maxSize: 1.5f, collider: false);
                    var r = g.GetComponent<MeshRenderer>();
                    r.sharedMaterial = glow;
                    Color c = Color.Lerp(new Color(1f, 0.62f, 0.22f), new Color(0.25f, 0.9f, 1f), t);
                    r.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", c);
                    r.SetPropertyBlock(block);
                    if (b % 2 == 0) AddLight(g.transform, h.point + Vector3.up * 1.2f, c, 7f, 1.4f);
                    b++;
                }
            report.Append($"Trench B glowing anemones {b}");
            return report.ToString();
        }

        /// <summary>
        /// The reveal at the end of Trench B: a whale crossing in front of the opening onto the abyss,
        /// on a slow loop whose long side runs north–south, 40 to 65 m out from the trench mouth
        /// (the design doc's "60 m away"; further and Padre Burgos's fog hides it).
        /// </summary>
        public static string BuildWhaleSilhouette()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WhaleModel);
            if (model == null) return "No whale model.";
            Transform parent = Dressing();
            Transform old = parent.Find("WhaleSilhouette");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject("WhaleSilhouette");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(1440f, -36f, 0f);   // the loop's centre
            Undo.RegisterCreatedObjectUndo(root, "Whale silhouette");
            var cruise = root.AddComponent<CruisingAnimal>();
            var so = new SerializedObject(cruise);
            so.FindProperty("_radius").floatValue = 75f;
            so.FindProperty("_squash").floatValue = 0.35f;
            so.FindProperty("_speed").floatValue = 3f;
            so.FindProperty("_rise").floatValue = 3f;
            so.FindProperty("_fixedHeading").boolValue = true;
            so.FindProperty("_loopYaw").floatValue = 90f;   // the long side runs along z, across the trench line
            so.ApplyModifiedPropertiesWithoutUndo();

            var whale = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            whale.name = "Whale";
            foreach (Renderer r in whale.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 16 m long, its length along the root's +Z (CruisingAnimal faces the root where it swims).
            whale.transform.localRotation = Quaternion.identity;
            whale.transform.localScale = Vector3.one;
            Bounds b = RendererBounds(whale);
            bool alongX = b.size.x > b.size.z;
            // The whale model's head is at its -Z: turned round, it swims head first.
            whale.transform.localRotation = alongX ? Quaternion.Euler(0f, 270f, 0f) : Quaternion.Euler(0f, 180f, 0f);
            float length = Mathf.Max(b.size.x, b.size.z);
            whale.transform.localScale = Vector3.one * (16f / Mathf.Max(0.001f, length));
            whale.transform.localPosition = Vector3.zero;
            whale.transform.localPosition = root.transform.InverseTransformVector(root.transform.position - RendererBounds(whale).center);

            AnimationClip clip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(WhaleModel))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) { clip = c; break; }
            if (clip != null)
            {
                var rig = whale.AddComponent<CreatureRig>();
                var rso = new SerializedObject(rig);
                rso.FindProperty("_clip").objectReferenceValue = clip;
                rso.FindProperty("_speed").floatValue = 0.6f;
                rso.ApplyModifiedPropertiesWithoutUndo();
            }
            return $"Whale silhouette: 16 m whale (model long along {(alongX ? "x" : "z")}), loop 150 × 52 m centred {root.transform.position}, clip {(clip != null ? clip.name : "none")}";
        }

        /// <summary>
        /// Schools and wanderers in Caluya and Ajuy that come back as the water clears: each is a
        /// PollutionDirector "life" entry of its zone (hidden while the zone is polluted, some back
        /// at half-clean, the rest once the level is mostly clean), as in Level 1.
        /// </summary>
        public static string BuildAmbientLife(int seed)
        {
            Physics.SyncTransforms();
            var director = Object.FindFirstObjectByType<PollutionDirector>();
            if (director == null) return "No PollutionDirector.";
            Transform root = Fresh(Dressing(), "AmbientLife");
            var rng = new System.Random(seed);
            var keepAway = new List<Vector3>();
            foreach (StatueDialogueRouter s in Object.FindObjectsByType<StatueDialogueRouter>(FindObjectsSortMode.None)) keepAway.Add(s.transform.position);
            foreach (RootPool p in Object.FindObjectsByType<RootPool>(FindObjectsSortMode.None)) keepAway.Add(p.transform.position);
            foreach (Challenges.Relocation.Sanctuary s in Object.FindObjectsByType<Challenges.Relocation.Sanctuary>(FindObjectsInactive.Include, FindObjectsSortMode.None)) keepAway.Add(s.transform.position);
            var lanes = new List<Vector3>();
            foreach (TideCurrent c in Object.FindObjectsByType<TideCurrent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (Transform node in c.transform) lanes.Add(node.position);
            var placed = new List<Vector3>();

            var plan = new[]
            {
                (zone: Lalim.Core.GameConstants.Zones.VISAYAS_CALUYA, min: new Vector2(30f, -200f), max: new Vector2(460f, 200f),
                 kinds: new[] { ("PF_School_BlueTang", 0.5f), ("PF_School_BlueTang", 0.8f), ("PF_School_ReefFish", 0.5f), ("PF_School_ReefFish", 0.8f), ("PF_Wanderer_Turtle", 0.8f), ("PF_Wanderer_StingRay", 0.8f) }),
                (zone: Lalim.Core.GameConstants.Zones.VISAYAS_AJUY, min: new Vector2(720f, -200f), max: new Vector2(1130f, 200f),
                 kinds: new[] { ("PF_School_ClownFish", 0.5f), ("PF_School_ClownFish", 0.8f), ("PF_School_ReefFish", 0.5f), ("PF_School_Tuna", 0.8f), ("PF_Wanderer_Turtle", 0.8f) }),
            };
            var so = new SerializedObject(director);
            SerializedProperty zonesProp = so.FindProperty("_zones");
            var report = new StringBuilder();
            foreach (var z in plan)
            {
                SerializedProperty life = null;
                for (int i = 0; i < zonesProp.arraySize; i++)
                    if (zonesProp.GetArrayElementAtIndex(i).FindPropertyRelative("zoneId").stringValue == z.zone)
                        life = zonesProp.GetArrayElementAtIndex(i).FindPropertyRelative("life");
                if (life == null) { report.Append($"{z.zone}: no PollutionDirector zone; "); continue; }
                // Drop entries left by an earlier build (their objects were just deleted).
                for (int i = life.arraySize - 1; i >= 0; i--)
                    if (life.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue == null) life.DeleteArrayElementAtIndex(i);

                int made = 0;
                foreach ((string kind, float appears) in z.kinds)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureDir + kind + ".prefab");
                    if (prefab == null) continue;
                    for (int tries = 0; tries < 300; tries++)
                    {
                        var xz = new Vector3(Mathf.Lerp(z.min.x, z.max.x, (float)rng.NextDouble()), 0f, Mathf.Lerp(z.min.y, z.max.y, (float)rng.NextDouble()));
                        if (!Floor(new Vector3(xz.x, -0.6f, xz.z), 60f, out RaycastHit h) || !h.collider.name.StartsWith("Terrain") || h.point.y > -7f) continue;
                        Vector3 at = h.point + Vector3.up * 2.5f;
                        if (Near(at, keepAway, 30f) || Near(at, lanes, 10f) || Near(at, placed, 45f) || Blocked(at + Vector3.up * 1.5f, 2.5f)) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                        go.name = $"{kind.Replace("PF_", "")}_{z.zone.Substring(z.zone.Length - 1)}{made}";
                        go.transform.position = at;
                        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                        life.arraySize++;
                        SerializedProperty e = life.GetArrayElementAtIndex(life.arraySize - 1);
                        e.FindPropertyRelative("target").objectReferenceValue = go;
                        e.FindPropertyRelative("appearsAtLook").floatValue = appears;
                        placed.Add(at);
                        made++;
                        break;
                    }
                }
                report.Append($"{z.zone}: {made} of {z.kinds.Length}; ");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return "Ambient life: " + report;
        }

        private static Transform Dressing()
        {
            GameObject root = GameObject.Find(DressingRoot);
            if (root == null)
            {
                root = new GameObject(DressingRoot);
                Undo.RegisterCreatedObjectUndo(root, "Dressing");
            }
            return root.transform;
        }

        private static List<Vector3> Lane(string name)
        {
            var points = new List<Vector3>();
            GameObject lane = GameObject.Find(name);
            if (lane != null) foreach (Transform node in lane.transform) points.Add(node.position);
            return points;
        }

        /// <summary>Points every <paramref name="spacing"/> m along a path, with the sideways direction and how far along (0…1).</summary>
        private static IEnumerable<(Vector3 point, Vector3 side, float t)> Along(List<Vector3> path, float spacing)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float next = spacing * 0.5f, walked = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                float len = Vector3.Distance(a, b);
                while (next <= walked + len)
                {
                    Vector3 p = Vector3.Lerp(a, b, (next - walked) / Mathf.Max(0.001f, len));
                    Vector3 dir = b - a; dir.y = 0f;
                    yield return (p, Vector3.Cross(Vector3.up, dir.normalized), next / total);
                    next += spacing;
                }
                walked += len;
            }
        }

        private static Material GlowMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (mat != null) return mat;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) return null;
            mat = new Material(unlit) { name = "MAT_Anemone_Glow" };
            mat.SetColor("_BaseColor", new Color(1f, 0.7f, 0.3f));
            AssetDatabase.CreateAsset(mat, GlowMaterialPath);
            return mat;
        }

        private static Bounds RendererBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ───────────────────────── Restoration swaps ─────────────────────────

        /// <summary>
        /// What each cleanse heals, swapped by a ZoneRestorationController on the zone's ZoneCompletedEvent
        /// (and restored straight away when a save already has the zone done):
        ///  - Caluya: the bleached coral flats (Z1_DeadCoral_*) regrow as living coral of the same
        ///    kinds, with seagrass coming back between them.
        ///  - Ajuy: the cleared-roots scar (PH_Damage_ClearedRoots) is replanted with young mangroves,
        ///    and the dead reef heads (Z2_DeadReef_*) come back as living bommies.
        /// </summary>
        public static string BuildRestoration(int seed)
        {
            Physics.SyncTransforms();
            var report = new StringBuilder();
            var rng = new System.Random(seed);
            Lalim.Mechanics.Quest.ZoneQuest caluya = null, ajuy = null;
            foreach (Lalim.Mechanics.Quest.ZoneQuest z in Object.FindObjectsByType<Lalim.Mechanics.Quest.ZoneQuest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (z.ZoneId == Lalim.Core.GameConstants.Zones.VISAYAS_CALUYA) caluya = z;
                else if (z.ZoneId == Lalim.Core.GameConstants.Zones.VISAYAS_AJUY) ajuy = z;
            }
            if (caluya == null || ajuy == null) return "Missing the Caluya or Ajuy ZoneQuest.";

            // ── Caluya: dead coral → living coral, seagrass between ──
            var dead = new List<GameObject>();
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mf.name.StartsWith("Z1_DeadCoral_")) dead.Add(mf.gameObject);
            GameObject grass = FindSource("Z1_Grass_SM_Plant_Seaweed_A");
            var (cRoot, cHealed) = RestorationRoot(caluya.transform, caluya.ZoneId);
            int corals = 0, grassCount = 0;
            for (int i = 0; i < dead.Count; i++)
            {
                GameObject d = dead[i];
                GameObject living = FindSource("Z1_Reef_" + Kind(d.name, "Z1_DeadCoral_")) ?? FindSource("Z1_Recover_" + Kind(d.name, "Z1_DeadCoral_"));
                if (living == null) continue;
                Bounds b = d.GetComponent<Renderer>().bounds;
                Place(living, cHealed, "Coral_" + i, d.transform.rotation, Vector3.zero, bottom: new Vector3(b.center.x, b.min.y, b.center.z),
                      maxSize: Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)), collider: false);
                corals++;
                if (grass != null && i % 2 == 0)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2.0), r = 1.6f + (float)rng.NextDouble() * 1.6f;
                    Vector3 at = b.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (Mathf.Max(b.extents.x, b.extents.z) + r);
                    if (Floor(new Vector3(at.x, b.max.y + 4f, at.z), 20f, out RaycastHit h) && h.collider.name.StartsWith("Terrain"))
                    {
                        Place(grass, cHealed, "Seagrass_" + i, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), Vector3.zero,
                              bottom: h.point + Vector3.down * 0.1f, maxSize: 1.4f + (float)rng.NextDouble() * 0.8f, collider: false);
                        grassCount++;
                    }
                }
            }
            SetUp(cRoot, caluya.ZoneId, cHealed.gameObject, dead);
            report.AppendLine($"Caluya: {dead.Count} dead corals swap for {corals} living ones and {grassCount} seagrass clumps");

            // ── Ajuy: the cleared roots replanted, the dead reef heads alive again ──
            var broken = new List<GameObject>();
            GameObject scar = null;
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf.name.StartsWith("Z2_DeadReef_")) broken.Add(mf.gameObject);
                else if (mf.name == "PH_Damage_ClearedRoots") scar = mf.gameObject;
            }
            var (aRoot, aHealed) = RestorationRoot(ajuy.transform, ajuy.ZoneId);
            int heads = 0;
            foreach (GameObject d in broken)
            {
                GameObject living = FindSource("Z2_Bommie_" + Kind(d.name, "Z2_DeadReef_")) ?? FindSource("Z2_Patch_" + Kind(d.name, "Z2_DeadReef_"));
                if (living == null) continue;
                Bounds b = d.GetComponent<Renderer>().bounds;
                Place(living, aHealed, "ReefHead_" + heads, d.transform.rotation, Vector3.zero, bottom: new Vector3(b.center.x, b.min.y, b.center.z),
                      maxSize: Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)), collider: false);
                heads++;
            }
            int saplings = 0;
            GameObject mangrove = FindSource("Z2_GMangrove_Z2_GiantMangrove_1_H05");
            if (scar != null && mangrove != null)
            {
                broken.Add(scar);
                Bounds area = scar.GetComponent<Renderer>().bounds;
                var keepClear = new List<Vector3>();
                foreach (TideCurrent c in Object.FindObjectsByType<TideCurrent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    foreach (Transform node in c.transform) keepClear.Add(node.position);
                foreach (Animator villager in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                    if (area.Contains(new Vector3(villager.transform.position.x, area.center.y, villager.transform.position.z))) keepClear.Add(villager.transform.position);
                for (float x = area.min.x + 5f; x <= area.max.x - 4f; x += 9f)
                for (float z = area.min.z + 5f; z <= area.max.z - 4f; z += 8f)
                {
                    var p = new Vector3(x + (float)(rng.NextDouble() * 4.0 - 2.0), area.max.y + 6f, z + (float)(rng.NextDouble() * 4.0 - 2.0));
                    if (!Floor(p, 30f, out RaycastHit h) || !(h.collider.name.StartsWith("Terrain") || h.collider.gameObject == scar)) continue;
                    if (Near(h.point, keepClear, 7f)) continue;
                    Place(mangrove, aHealed, "Sapling_" + saplings, Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f), Vector3.zero,
                          bottom: new Vector3(p.x, h.point.y - 0.3f, p.z), maxSize: 4.5f + (float)rng.NextDouble() * 1.5f, collider: false);
                    saplings++;
                }
            }
            SetUp(aRoot, ajuy.ZoneId, aHealed.gameObject, broken);
            report.AppendLine($"Ajuy: {broken.Count} broken pieces swap for {heads} living reef heads and {saplings} young mangroves");
            return report.ToString();
        }

        /// <summary>"Z1_DeadCoral_SM_Plant_Coral_2B_04474" → "SM_Plant_Coral_2B_" (the model kind, to find its living twin).</summary>
        private static string Kind(string name, string prefix)
        {
            string rest = name.Substring(prefix.Length);
            int cut = rest.LastIndexOf('_');
            return cut > 0 ? rest.Substring(0, cut + 1) : rest;
        }

        private static (Transform root, Transform healed) RestorationRoot(Transform zone, string zoneId)
        {
            Transform root = Fresh(zone, "Restoration");
            var healed = new GameObject("Healed").transform;
            healed.SetParent(root, false);
            healed.gameObject.SetActive(false);   // shown by the controller once the zone is cleansed
            return (root, healed);
        }

        private static void SetUp(Transform root, string zoneId, GameObject healed, List<GameObject> broken)
        {
            var ctrl = root.gameObject.AddComponent<ZoneRestorationController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("_zoneId").stringValue = zoneId;
            so.FindProperty("_restoredStateContainer").objectReferenceValue = healed;
            SerializedProperty list = so.FindProperty("_additionalBrokenObjects");
            list.arraySize = broken.Count;
            for (int i = 0; i < broken.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = broken[i];
            so.FindProperty("_transitionDelay").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────────────────── Helpers ─────────────────────────

        private static Transform ZoneRoot()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Zone3_Padre_Burgos") return t;
            return null;
        }

        /// <summary>Points to stay away from: every Moontide current node, the sanctuaries and earlier caves.</summary>
        private static List<Vector3> Avoid()
        {
            var points = new List<Vector3>();
            foreach (TideCurrent c in Object.FindObjectsByType<TideCurrent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (Transform node in c.transform) points.Add(node.position);
            foreach (Challenges.Relocation.Sanctuary s in Object.FindObjectsByType<Challenges.Relocation.Sanctuary>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                points.Add(s.transform.position);
            Transform caves = ZoneRoot()?.Find("PB_WallCaves");
            if (caves != null) foreach (Transform c in caves) points.Add(c.position);
            return points;
        }

        private static bool Near(Vector3 p, List<Vector3> points, float range)
        {
            foreach (Vector3 q in points) if ((q - p).sqrMagnitude < range * range) return true;
            return false;
        }

        /// <summary>The abyss wall at this compass angle (0 = east) and depth, and its face's outward normal (into the pit).</summary>
        private static bool WallAt(float angle, float y, out Vector3 point, out Vector3 normal)
        {
            var dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            point = default;
            normal = -dir;
            if (!Physics.Raycast(new Vector3(AbyssCentre.x, y, AbyssCentre.z), dir, out RaycastHit h, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (h.distance < 80f) return false;   // a boulder on the floor, not the wall
            point = h.point;
            Vector3 flat = new Vector3(h.normal.x, 0f, h.normal.z);
            normal = flat.sqrMagnitude > 0.2f && Vector3.Dot(flat.normalized, -dir) > 0.3f ? flat.normalized : -dir;
            return true;
        }

        private static bool Floor(Vector3 from, float range, out RaycastHit hit) =>
            Physics.Raycast(from, Vector3.down, out hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        private static float Slope(Vector3 at, float r, float fromY)
        {
            float lo = at.y, hi = at.y;
            foreach (Vector3 o in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (Floor(new Vector3(at.x + o.x * r, fromY, at.z + o.z * r), 30f, out RaycastHit h)) { lo = Mathf.Min(lo, h.point.y); hi = Mathf.Max(hi, h.point.y); }
            return hi - lo;
        }

        private static bool Blocked(Vector3 p, float r) =>
            Physics.CheckSphere(p, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        /// <summary>The first scene object of this art model (its mesh and material are copied).</summary>
        private static GameObject FindSource(string prefix)
        {
            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (mf.name.StartsWith(prefix) && mf.sharedMesh != null) return mf.gameObject;
            return null;
        }

        private static Transform Fresh(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build " + name);
            return root;
        }

        /// <summary>
        /// A copy of <paramref name="source"/>'s mesh, sized to <paramref name="size"/> (or uniformly to
        /// <paramref name="maxSize"/> on its longest side), with its bounds centred on <paramref name="centre"/>
        /// or sitting on <paramref name="bottom"/>.
        /// </summary>
        private static GameObject Place(GameObject source, Transform parent, string name, Quaternion rotation, Vector3 size,
                                        Vector3? centre = null, Vector3? bottom = null, float maxSize = 0f, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
            Vector3 mb = go.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            go.transform.rotation = rotation;
            go.transform.localScale = maxSize > 0f
                ? Vector3.one * (maxSize / Mathf.Max(mb.x, Mathf.Max(mb.y, mb.z)))
                : new Vector3(size.x / mb.x, size.y / mb.y, size.z / mb.z);
            Vector3 target = centre ?? bottom ?? parent.position;
            go.transform.position = target;
            Bounds b = r.bounds;
            Vector3 anchor = bottom.HasValue ? new Vector3(b.center.x, b.min.y, b.center.z) : b.center;
            go.transform.position += target - anchor;
            if (collider) go.AddComponent<MeshCollider>();
            go.isStatic = true;
            return go;
        }

        private static void SetWorldSize(GameObject go, Vector3 size)
        {
            go.transform.localScale = Vector3.one;
            Vector3 ls = go.transform.lossyScale, mb = go.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            go.transform.localScale = new Vector3(size.x / (ls.x * mb.x), size.y / (ls.y * mb.y), size.z / (ls.z * mb.z));
        }

        private static void AddLight(Transform parent, Vector3 at, Color color, float range, float intensity)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }
    }
}
