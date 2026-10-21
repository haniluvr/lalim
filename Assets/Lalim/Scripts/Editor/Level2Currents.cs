using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Lays Level 2's Moontide currents beyond Caluya's and Trench A's hand-placed ones. Each route
    /// is a few waypoints; between them a grid search threads the current through open water
    /// (clear of roots, planks and rock, off the seabed), then the path is smoothed and laid as the
    /// current's nodes. A waypoint's height is either a depth to hold (the search eases between
    /// them, so a route can climb the abyss wall) or NaN: stay as shallow as the water allows.
    /// New currents keep their distance from every current already in the scene (two tubes side by
    /// side, often in opposite directions, would cancel out), except near their own ends.
    ///
    ///  - Ajuy: the trench mouth, the village, the four teaching pools (two loops in lesson order),
    ///    the Padre Burgos gate, and the lane back west to Trench A.
    ///  - Caluya: a spur from the east–west lanes to the Seagrass Meadow.
    ///  - Trench B: a lane each way between the Ajuy gate and the Padre Burgos wall.
    ///  - Padre Burgos: a climb out of the abyss up the north wall, and a loop round the pit past
    ///    the four Zone 3 sanctuaries. A current carries at 10 m/s, faster than Bakunawa's chase.
    /// </summary>
    public static class Level2Currents
    {
        private const float Cell = 3f;            // grid spacing (m)
        private const float Ceiling = -4.5f;      // preferred highest node for shallow routes
        private const float ShallowCeiling = -1.0f; // allowed only where the seabed itself is that high (Trench A's sand bar)
        private const float Clearance = 1.5f;     // free radius needed around the path
        private const float AboveFloor = 1.5f;    // keep the tube off the seabed
        private const float NodeSpacing = 12f;
        private const float Apart = 7.5f;
        private const float HubRadius = 18f;
        private const float N = float.NaN;        // "as shallow as the water allows"

        public struct Route
        {
            public string Name, Parent;
            public Vector3[] Waypoints;
            public Route(string parent, string name, params Vector3[] waypoints) { Parent = parent; Name = name; Waypoints = waypoints; }
        }

        private static Vector3 S(float x, float z) => new Vector3(x, N, z);

        public static readonly Route[] Routes =
        {
            // ── Ajuy ──
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_TrenchToVillage", S(680, 6), S(760, 3), S(825, 1)),
            // North loop, in lesson order: village → Amihan (throw) → Lakan (sense) → village.
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_VillageToAmihan", S(882, 30), S(840, 90), S(815, 152)),
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_AmihanToLakan", S(833, 166), S(880, 163), S(926, 150)),
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_LakanToVillage", S(938, 124), S(925, 85), S(908, 32)),
            // South loop: village → Inang Sela (patience) → Dalisay (the test) → village.
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_VillageToInangSela", S(908, -32), S(935, -70), S(957, -97)),
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_InangSelaToDalisay", S(955, -122), S(932, -160)),
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_DalisayToVillage", S(908, -165), S(895, -100), S(893, -35)),
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_VillageToGate", S(925, 10), S(1010, 26), S(1145, 3)),
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_GateToVillage", S(1142, -12), S(1040, -30), S(925, -12)),
            // Back west to Trench A, south of the inbound lane (which bends north over the sand bar),
            // ending where Trench A's west tide starts.
            new Route("Zone2_Ajuy/Currents_Ajuy", "Tide_Ajuy_VillageToTrench", S(826, -14), S(772, -4), S(736, -6), S(704, -7), S(652, 2)),

            // ── Caluya ──
            new Route("Currents", "Tide_Caluya_ToSeagrass", S(132, 14), S(98, 52), S(68, 86)),

            // ── Trench B (east lane deep, west lane higher and to the north) ──
            new Route("Zone3_PadreBurgos/Currents_TrenchB", "Tide_TrenchB_East",
                      new Vector3(1150, -9, 6), new Vector3(1200, -30, 4), new Vector3(1265, -48, 4), new Vector3(1335, -50, 4), new Vector3(1372, -50, 8)),
            new Route("Zone3_PadreBurgos/Currents_TrenchB", "Tide_TrenchB_West",
                      new Vector3(1368, -32, 22), new Vector3(1300, -34, 18), new Vector3(1225, -24, 17), new Vector3(1160, -9, 8), new Vector3(1146, -6, -6)),

            // ── Padre Burgos ──
            // Out of the abyss: from the floor north of the lair, up the wall to Whale Shark Bay.
            new Route("Zone3_PadreBurgos/Currents_PadreBurgos", "Tide_PB_AbyssClimb",
                      new Vector3(1565, -186, 44), new Vector3(1565, -150, 80), new Vector3(1566, -100, 104), new Vector3(1580, -48, 124)),
            // Round the pit, just inside the wall: Turtle Grotto → Wall Ledge Nursery → Deep Reef
            // Refuge → Whale Shark Bay → back toward the Turtle Grotto and Trench B.
            new Route("Zone3_PadreBurgos/Currents_PadreBurgos", "Tide_PB_WallLoop",
                      new Vector3(1414, -55, -6), new Vector3(1440, -50, -118), new Vector3(1545, -50, -206), new Vector3(1640, -62, -178),
                      new Vector3(1700, -112, -50), new Vector3(1672, -84, 50), new Vector3(1636, -50, 126), new Vector3(1525, -55, 72),
                      new Vector3(1452, -55, 28), new Vector3(1422, -55, 10)),
        };

        /// <summary>Builds (or rebuilds) the named routes, or every route when <paramref name="only"/> is null.
        /// Currents not being rebuilt stay as they are and are steered around.</summary>
        public static string Build(float carrySpeed, float radius, Material streaks, ICollection<string> only = null)
        {
            Physics.SyncTransforms();
            var report = new StringBuilder();
            var rebuild = new HashSet<string>();
            foreach (Route r in Routes) if (only == null || only.Contains(r.Name)) rebuild.Add(r.Name);

            // Barriers (trench gates) block the planner but not the finished game: lift them while planning.
            var lifted = new List<GameObject>();
            foreach (string b in new[] { "Barrier_TrenchA", "Barrier_TrenchB", "Barrier_ExitTunnel" })
            {
                GameObject go = GameObject.Find(b);
                if (go != null && go.activeSelf) { go.SetActive(false); lifted.Add(go); }
            }
            Physics.SyncTransforms();
            _terrainTop.Clear();
            try
            {
                _laid.Clear();
                foreach (TideCurrent existing in Object.FindObjectsByType<TideCurrent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!rebuild.Contains(existing.name)) AddLaid(NodesOf(existing.transform));

                foreach (Route route in Routes)
                {
                    if (!rebuild.Contains(route.Name)) continue;
                    Transform parent = EnsureParent(route.Parent);
                    _ends.Clear();
                    _ends.Add(new Vector2(route.Waypoints[0].x, route.Waypoints[0].z));
                    _ends.Add(new Vector2(route.Waypoints[route.Waypoints.Length - 1].x, route.Waypoints[route.Waypoints.Length - 1].z));
                    List<Vector3> path = Plan(route.Waypoints, out string problem);
                    if (path == null) { report.AppendLine($"{route.Name}: FAILED ({problem})"); continue; }
                    Transform old = parent.Find(route.Name);
                    if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
                    var go = new GameObject(route.Name);
                    go.transform.SetParent(parent, false);
                    for (int i = 0; i < path.Count; i++)
                    {
                        var node = new GameObject("Node_" + i);
                        node.transform.SetParent(go.transform, false);
                        node.transform.position = path[i];
                    }
                    go.AddComponent<TideCurrent>().EditorSetup(carrySpeed, 0f, radius, streaks);
                    Undo.RegisterCreatedObjectUndo(go, "Level 2 currents");
                    AddLaid(path);
                    float length = 0f;
                    for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
                    report.AppendLine($"{route.Name}: {path.Count} nodes, {length:F0} m, y {MinY(path):F1}..{MaxY(path):F1}{(problem != null ? " (" + problem + ")" : "")}");
                }
            }
            finally
            {
                foreach (GameObject go in lifted) go.SetActive(true);
            }
            return report.ToString();
        }

        private static Transform EnsureParent(string path)
        {
            string[] parts = path.Split('/');
            Transform t = null;
            GameObject root = GameObject.Find(parts[0]);
            if (root == null)
            {
                root = new GameObject(parts[0]);
                Undo.RegisterCreatedObjectUndo(root, "Level 2 currents");
            }
            t = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = t.Find(parts[i]);
                if (child == null)
                {
                    child = new GameObject(parts[i]).transform;
                    child.SetParent(t, false);
                    Undo.RegisterCreatedObjectUndo(child.gameObject, "Level 2 currents");
                }
                t = child;
            }
            return t;
        }

        private static List<Vector3> NodesOf(Transform current)
        {
            var nodes = new List<Vector3>();
            foreach (Transform c in current) nodes.Add(c.position);
            return nodes;
        }

        private static void AddLaid(List<Vector3> path)
        {
            for (int i = 0; i < path.Count - 1; i++)
                for (float d = 0f; d < Vector3.Distance(path[i], path[i + 1]); d += 2f)
                    _laid.Add(Vector3.MoveTowards(path[i], path[i + 1], d));
        }

        private static float MinY(List<Vector3> p) { float m = float.MaxValue; foreach (Vector3 v in p) m = Mathf.Min(m, v.y); return m; }
        private static float MaxY(List<Vector3> p) { float m = float.MinValue; foreach (Vector3 v in p) m = Mathf.Max(m, v.y); return m; }

        // ───────────────────────── Planning ─────────────────────────

        private static readonly List<Vector3> _laid = new List<Vector3>();
        private static readonly List<Vector2> _ends = new List<Vector2>();

        private static bool NearLaid(Vector3 p)
        {
            foreach (Vector2 e in _ends)
                if (new Vector2(p.x - e.x, p.z - e.y).sqrMagnitude < HubRadius * HubRadius) return false;
            foreach (Vector3 q in _laid)
                if ((q - p).sqrMagnitude < Apart * Apart) return true;
            return false;
        }

        private static bool SegmentNearLaid(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            for (float d = 0f; d <= len; d += 2f)
                if (NearLaid(Vector3.MoveTowards(a, b, d))) return true;
            return false;
        }

        private static List<Vector3> Plan(Vector3[] waypoints, out string problem)
        {
            problem = null;
            var raw = new List<Vector3>();
            for (int i = 0; i < waypoints.Length - 1; i++)
            {
                List<Vector3> leg = Search(waypoints[i], waypoints[i + 1]);
                if (leg == null) { problem = $"no open water from {waypoints[i]} to {waypoints[i + 1]}"; return null; }
                if (raw.Count > 0) leg.RemoveAt(0);
                raw.AddRange(leg);
            }
            List<Vector3> nodes = Resample(StringPull(raw), NodeSpacing);
            // The Catmull-Rom curve bulges a little between nodes: check the curve itself.
            for (int i = 0; i < nodes.Count - 1; i++)
                if (Physics.SphereCast(nodes[i], Clearance * 0.6f, (nodes[i + 1] - nodes[i]).normalized, out RaycastHit hit,
                                       Vector3.Distance(nodes[i], nodes[i + 1]), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    problem = $"tight at node {i} ({hit.collider.name})";
            return nodes;
        }

        private struct Key : System.IEquatable<Key>
        {
            public int X, Z, L;
            public bool Equals(Key o) => X == o.X && Z == o.Z && L == o.L;
            public override int GetHashCode() => (X * 73856093) ^ (Z * 19349663) ^ (L * 83492791);
        }

        // Depth levels, shallow to deep: fine near the surface, coarser down the abyss wall.
        private static readonly float[] Levels = BuildLevels();

        private static float[] BuildLevels()
        {
            var l = new List<float> { -1.0f, -1.5f, -2.5f, -3.5f, -4.5f, -5.5f };
            for (float y = -7f; y >= -30f; y -= 1.5f) l.Add(y);
            for (float y = -33f; y >= -198f; y -= 3f) l.Add(y);
            return l.ToArray();
        }

        private static int NearestLevel(float y)
        {
            int best = 0;
            for (int i = 1; i < Levels.Length; i++) if (Mathf.Abs(Levels[i] - y) < Mathf.Abs(Levels[best] - y)) best = i;
            return best;
        }

        private static readonly Dictionary<Key, bool> _free = new Dictionary<Key, bool>();

        private static Vector3 World(Vector2 origin, Key k) => new Vector3(origin.x + k.X * Cell, Levels[k.L], origin.y + k.Z * Cell);

        private static bool Free(Vector2 origin, Key k)
        {
            if (_free.TryGetValue(k, out bool f)) return f;
            Vector3 p = World(origin, k);
            // Above the terrain's top surface (a point under the seabed would look like open water:
            // the terrain mesh can't be seen from below), clear of everything else, off the floor.
            f = p.y <= ShallowCeiling + 0.01f
                && p.y >= TerrainTop(p.x, p.z) + AboveFloor
                && !Physics.CheckSphere(p, Clearance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && Physics.Raycast(p, Vector3.down, out RaycastHit hit, 260f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.distance >= AboveFloor
                && !NearLaid(p);
            _free[k] = f;
            return f;
        }

        private static readonly Dictionary<Vector2Int, float> _terrainTop = new Dictionary<Vector2Int, float>();

        /// <summary>The highest terrain surface at (x, z), ignoring roots, rocks and props above it.</summary>
        private static float TerrainTop(float x, float z)
        {
            var key = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
            if (_terrainTop.TryGetValue(key, out float top)) return top;
            top = -260f;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, 5f, z), Vector3.down, 270f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (h.collider.name.StartsWith("Terrain") && h.point.y > top) top = h.point.y;
            _terrainTop[key] = top;
            return top;
        }

        /// <summary>
        /// A* over a 3 m grid with depth levels. A NaN-height leg prefers staying shallow (in the
        /// light) and level; a leg with heights eases from one depth to the next.
        /// </summary>
        private static List<Vector3> Search(Vector3 from, Vector3 to)
        {
            _free.Clear();
            Vector2 origin = new Vector2(from.x, from.z);
            bool shallow = float.IsNaN(from.y) || float.IsNaN(to.y);
            var goal = new Key
            {
                X = Mathf.RoundToInt((to.x - origin.x) / Cell),
                Z = Mathf.RoundToInt((to.z - origin.y) / Cell),
                L = shallow ? 0 : NearestLevel(to.y)
            };
            float totalFlat = Mathf.Max(1f, new Vector2(to.x - from.x, to.z - from.z).magnitude);
            int span = Mathf.Max(Mathf.Abs(goal.X), Mathf.Abs(goal.Z)) + 25;

            var open = new SortedSet<(float f, int id)>();
            var nodes = new List<Key>();
            var g = new Dictionary<Key, float>();
            var came = new Dictionary<Key, Key>();
            var ids = new Dictionary<Key, int>();

            // Start on the free level nearest the wanted one (shallow legs: -4.5 first, the very shallow last).
            bool started = false;
            int ceilingLevel = NearestLevel(Ceiling);
            int want = shallow ? ceilingLevel : NearestLevel(from.y);
            for (int r = 0; r <= 3 && !started; r++)
            for (int dx = -r; dx <= r && !started; dx++)
            for (int dz = -r; dz <= r && !started; dz++)
            for (int step = 0; step < Levels.Length * 2 && !started; step++)
            {
                int l = shallow ? (step + ceilingLevel) % Levels.Length : want + ((step % 2 == 0 ? 1 : -1) * ((step + 1) / 2));
                if (l < 0 || l >= Levels.Length || (shallow && step >= Levels.Length)) continue;
                var k = new Key { X = dx, Z = dz, L = l };
                if (!Free(origin, k)) continue;
                g[k] = 0f;
                ids[k] = nodes.Count; nodes.Add(k);
                open.Add((H(k, goal, shallow), ids[k]));
                started = true;
            }
            if (!started) return null;

            int guard = 0;
            while (open.Count > 0 && guard++ < 400000)
            {
                var top = open.Min;
                open.Remove(top);
                Key cur = nodes[top.id];
                bool arrived = Mathf.Abs(cur.X - goal.X) <= 1 && Mathf.Abs(cur.Z - goal.Z) <= 1
                               && (shallow || Mathf.Abs(Levels[cur.L] - Levels[goal.L]) <= 3.5f);
                if (arrived)
                {
                    var path = new List<Vector3>();
                    Key k = cur;
                    path.Add(World(origin, k));
                    while (came.TryGetValue(k, out Key prev)) { k = prev; path.Add(World(origin, k)); }
                    path.Reverse();
                    return path;
                }
                float gc = g[cur];
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                for (int dl = -1; dl <= 1; dl++)
                {
                    if (dx == 0 && dz == 0 && dl == 0) continue;
                    var n = new Key { X = cur.X + dx, Z = cur.Z + dz, L = cur.L + dl };
                    if (n.L < 0 || n.L >= Levels.Length || Mathf.Abs(n.X) > span || Mathf.Abs(n.Z) > span) continue;
                    if (!Free(origin, n)) continue;
                    float dy = Levels[n.L] - Levels[cur.L];
                    float stepCost = Mathf.Sqrt((dx * dx + dz * dz) * Cell * Cell + dy * dy);
                    if (shallow)
                        stepCost += Mathf.Abs(dy) * 0.8f + n.L * 0.15f + (Levels[n.L] > Ceiling + 0.01f ? 8f : 0f);   // above the ceiling only where it must
                    else
                    {
                        // Hold the depth eased between the two waypoints by how far along the leg we are.
                        Vector3 w = World(origin, n);
                        float t = 1f - Mathf.Clamp01(new Vector2(to.x - w.x, to.z - w.z).magnitude / totalFlat);
                        float wantY = Mathf.Lerp(from.y, to.y, t);
                        stepCost += Mathf.Abs(Levels[n.L] - wantY) * 0.5f;
                    }
                    float ng = gc + stepCost;
                    if (g.TryGetValue(n, out float old) && old <= ng) continue;
                    if (ids.TryGetValue(n, out int oldId)) open.Remove((old + H(n, goal, shallow), oldId));
                    else { ids[n] = nodes.Count; nodes.Add(n); }
                    g[n] = ng;
                    came[n] = cur;
                    open.Add((ng + H(n, goal, shallow), ids[n]));
                }
            }
            return null;
        }

        private static float H(Key k, Key goal, bool shallow)
        {
            float flat = Mathf.Sqrt((k.X - goal.X) * (k.X - goal.X) + (k.Z - goal.Z) * (k.Z - goal.Z)) * Cell;
            return shallow ? flat : Mathf.Sqrt(flat * flat + (Levels[k.L] - Levels[goal.L]) * (Levels[k.L] - Levels[goal.L]));
        }

        /// <summary>Drops points while the straight line past them stays clear.</summary>
        private static List<Vector3> StringPull(List<Vector3> p)
        {
            var result = new List<Vector3> { p[0] };
            int i = 0;
            while (i < p.Count - 1)
            {
                // Look ahead along the path, not just in space: a loop's end lies near its start.
                int j = Mathf.Min(p.Count - 1, i + 13);
                for (; j > i + 1; j--)
                {
                    Vector3 a = p[i], b = p[j];
                    if (Vector3.Distance(a, b) > 40f) continue;   // keep some shape on long legs
                    if (!Physics.SphereCast(a, Clearance * 0.8f, (b - a).normalized, out _, Vector3.Distance(a, b), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                        && FloorClear(a, b) && !SegmentNearLaid(a, b)) break;
                }
                result.Add(p[j]);
                i = j;
            }
            return result;
        }

        private static bool FloorClear(Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            for (float d = 0f; d <= len; d += 2f)
            {
                Vector3 q = Vector3.Lerp(a, b, d / Mathf.Max(0.01f, len));
                if (!Physics.Raycast(q, Vector3.down, out RaycastHit hit, 260f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.distance < AboveFloor * 0.8f) return false;
            }
            return true;
        }

        private static List<Vector3> Resample(List<Vector3> p, float spacing)
        {
            var result = new List<Vector3> { p[0] };
            for (int i = 0; i < p.Count - 1; i++)
            {
                float len = Vector3.Distance(p[i], p[i + 1]);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / spacing));
                for (int s = 1; s <= n; s++) result.Add(Vector3.Lerp(p[i], p[i + 1], s / (float)n));
            }
            return result;
        }
    }
}
