using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 1 ▸ Find Zone 2 Sites — finds the pool of spots zone 2's minigames can
    /// be placed at each playthrough (ZoneSiteShuffle): flat open sand down on the canyon floor
    /// (never up on the rim at the map's edge or its slopes) with nothing above it, and no rock,
    /// coral, plant or prop touching the work area or the view point — tested against each
    /// prop's actual mesh, not its bounding box — away from Haik's statue, spread out. The water
    /// readings and the coral replanting share the spots (area A/B/C ↔ A/B/C).
    /// </summary>
    public static class Level1ZoneSites
    {
        private const float Step = 2f;            // sampling grid
        private const float ClearRadius = 2.6f;   // open water around the site (work area + view point)
        private const float ClearHeight = 3f;
        private const float FloorTolerance = 3f;  // how far above the canyon floor still counts as the floor
        private const float SlopeProbe = 5f;      // everything this far around must be floor too
        private const float StatueClearance = 10f;
        private const float PoolSpacing = 5f;     // between candidates in the pool
        private const float EdgeMargin = 4f;

        [MenuItem("Tools/Lalim/Level 1/Find Zone 2 Sites")]
        public static void Menu() => Debug.Log(Install());

        public static string Install()
        {
            ZoneQuest zone = null;
            foreach (ZoneQuest z in Object.FindObjectsByType<ZoneQuest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (z.name.StartsWith("Zone2")) zone = z;
            GameObject envGo = GameObject.Find("LuzonScene");
            UnderwaterBounds map = Object.FindAnyObjectByType<UnderwaterBounds>(FindObjectsInactive.Include);
            if (zone == null || envGo == null || map == null || zone.ToxicVolume == null) return "Open 02_Luzon (needs Zone2, LuzonScene and the map bounds).";

            var water = Stations(zone, "Water_Area_");
            var coral = Stations(zone, "Coral_Area_");
            var checkpoints = new List<Checkpoint>();
            foreach (string key in new[] { "a", "b", "c" })
            {
                GameObject cp = GameObject.Find($"cp_z2_area_{key}");
                checkpoints.Add(cp != null ? cp.GetComponent<Checkpoint>() : null);
            }

            // What a site must not overlap: every visible prop, by its real shape — props without a
            // collider get a temporary one for the search (removed again below).
            var stationRoots = new HashSet<Transform>();
            foreach (AreaStation s in water) if (s != null) stationRoots.Add(s.transform);
            foreach (AreaStation s in coral) if (s != null) stationRoots.Add(s.transform);
            var temporary = new List<MeshCollider>();
            foreach (MeshFilter mf in envGo.GetComponentsInChildren<MeshFilter>())
            {
                Renderer r = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled || mf.GetComponent<Collider>() != null) continue;
                string n = mf.name.ToLowerInvariant();
                if (n.Contains("seabed") || n.StartsWith("layout_") || n.StartsWith("baseline_") || n.StartsWith("vfx_")) continue;
                var c = mf.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = mf.sharedMesh;
                temporary.Add(c);
            }
            Physics.SyncTransforms();

            Bounds zb = zone.ToxicVolume.GetComponent<Collider>().bounds;
            float minX = Mathf.Max(zb.min.x, map.Min.x) + EdgeMargin, maxX = Mathf.Min(zb.max.x, map.Max.x) - EdgeMargin;
            float minZ = Mathf.Max(zb.min.z, map.Min.z) + EdgeMargin, maxZ = Mathf.Min(zb.max.z, map.Max.z) - EdgeMargin;
            Vector3 statue = zone.Statue != null ? zone.Statue.transform.position : zb.center;
            float floorTop = CanyonFloor(minX, maxX, minZ, maxZ, map) + FloorTolerance;

            var sites = new List<ZoneSiteShuffle.Site>();
            for (float x = minX; x <= maxX; x += Step)
            {
                for (float z = minZ; z <= maxZ; z += Step)
                {
                    if (!OpenSand(new Vector3(x, map.Max.y - 1f, z), map, stationRoots, out RaycastHit hit)) continue;
                    Vector3 p = hit.point;
                    if (!OnFloor(p, floorTop, map)) continue;
                    if (!zb.Contains(new Vector3(p.x, Mathf.Clamp(p.y + 1f, zb.min.y, zb.max.y), p.z))) continue;
                    if (Flat(p - statue) < StatueClearance) continue;
                    bool crowded = false;
                    foreach (var s in sites) if (Flat(s.ground - p) < PoolSpacing) { crowded = true; break; }
                    if (crowded) continue;
                    // Face toward Haik's statue, so the view point (behind the site) looks over the work toward him.
                    Vector3 toStatue = statue - p; toStatue.y = 0f;
                    float yaw = toStatue.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toStatue).eulerAngles.y : 0f;
                    sites.Add(new ZoneSiteShuffle.Site { ground = p, yaw = yaw });
                }
            }

            foreach (MeshCollider c in temporary) if (c != null) Object.DestroyImmediate(c);
            Physics.SyncTransforms();

            var shuffle = zone.GetComponent<ZoneSiteShuffle>();
            if (shuffle == null) shuffle = Undo.AddComponent<ZoneSiteShuffle>(zone.gameObject);
            Undo.RecordObject(shuffle, "Zone 2 sites");
            shuffle.EditorSetup(sites, water, coral, checkpoints);
            EditorUtility.SetDirty(shuffle);
            EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
            return $"Zone 2 (canyon floor below {floorTop:0.0} m): {sites.Count} candidate spots for the minigames (water {water.Count} areas, coral {coral.Count} areas, {checkpoints.FindAll(c => c != null).Count} checkpoints).";
        }

        private static List<AreaStation> Stations(ZoneQuest zone, string prefix)
        {
            var list = new List<AreaStation>();
            foreach (string key in new[] { "A", "B", "C" })
            {
                AreaStation found = null;
                foreach (AreaStation s in zone.GetComponentsInChildren<AreaStation>(true)) if (s.name == prefix + key) found = s;
                list.Add(found);
            }
            return list;
        }

        // Straight down from the surface the first solid thing must be the seabed, flat, with open water around.
        private static bool OpenSand(Vector3 from, UnderwaterBounds map, HashSet<Transform> ignore, out RaycastHit hit)
        {
            hit = default;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.transform.IsChildOf(map.transform) || Ignored(h.collider.transform, ignore)) continue;
                hit = h;
                found = true;
                break;
            }
            if (!found || !hit.collider.name.ToLowerInvariant().Contains("seabed") || hit.normal.y < 0.9f) return false;

            Vector3 centre = hit.point + Vector3.up * (ClearHeight * 0.5f + 0.25f);
            foreach (Collider c in Physics.OverlapBox(centre, new Vector3(ClearRadius, ClearHeight * 0.5f, ClearRadius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.name.ToLowerInvariant().Contains("seabed") || c.transform.IsChildOf(map.transform) || Ignored(c.transform, ignore)) continue;
                return false;
            }
            return true;
        }

        // The canyon floor: the typical low seabed height in the zone (a quarter of the samples are lower).
        private static float CanyonFloor(float minX, float maxX, float minZ, float maxZ, UnderwaterBounds map)
        {
            var heights = new List<float>();
            for (float x = minX; x <= maxX; x += 4f)
                for (float z = minZ; z <= maxZ; z += 4f)
                    if (SeabedAt(x, z, map, out float y)) heights.Add(y);
            if (heights.Count == 0) return float.MaxValue;
            heights.Sort();
            return heights[heights.Count / 4];
        }

        private static bool OnFloor(Vector3 p, float floorTop, UnderwaterBounds map)
        {
            if (p.y > floorTop) return false;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * SlopeProbe;
                if (!SeabedAt(p.x + d.x, p.z + d.z, map, out float y) || y > floorTop + 1f) return false;
            }
            return true;
        }

        private static bool SeabedAt(float x, float z, UnderwaterBounds map, out float y)
        {
            y = 0f;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, map.Max.y + 20f, z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!h.collider.name.ToLowerInvariant().Contains("seabed")) continue;
                y = h.point.y;
                return true;
            }
            return false;
        }

        private static bool Ignored(Transform t, HashSet<Transform> roots)
        {
            foreach (Transform r in roots) if (t.IsChildOf(r)) return true;
            return false;
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
