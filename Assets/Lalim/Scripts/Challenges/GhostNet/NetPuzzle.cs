using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Challenges.GhostNet
{
    /// <summary>
    /// The untangle puzzle behind a ghost net, in 2D plane coordinates (metres, centred on the net).
    ///
    /// It is ONE square-mesh net: knots on a grid joined by ropes, each mesh square crossed by finer
    /// cord. The border knots are weighed down (they never move); the inner knots can be dragged.
    /// Every trapped animal lies under a torn hole sized to it — a small animal needs one mesh
    /// square, a long one two side by side, a large one a 2×2 square. The net is generated SOLVED,
    /// then inner knots are scrambled so ropes cross over the holes. An animal is free once no rope
    /// or cord passes through the room it needs: it fits through the opening.
    /// </summary>
    public class NetPuzzle
    {
        public enum Fit { Small, Long, Large }

        public struct Rope
        {
            public int A;
            public int B;
        }

        public class Hole
        {
            public Fit Fit;
            public Vector2 Center;
            public Vector2 Axis = Vector2.right;   // the long way (Long holes)
            public float HalfLength;               // half the animal's spine
            public float Radius;                   // room it needs on every side of the spine
            public readonly List<int> Corners = new List<int>();
        }

        public const int Rows = 3;
        public const int MeshLines = 2; // finer cords across each mesh square, each way

        public int Columns { get; private set; }
        public float CellSize { get; private set; }
        public Rect Bounds { get; private set; }

        public readonly List<Vector2> Positions = new List<Vector2>();
        public readonly List<Vector2> Solved = new List<Vector2>();
        public readonly List<bool> Exists = new List<bool>();
        public readonly List<bool> Movable = new List<bool>();
        public readonly List<Rope> Ropes = new List<Rope>();
        /// <summary>Mesh squares (not holes): corner knots counter-clockwise from bottom-left.</summary>
        public readonly List<int[]> Meshes = new List<int[]>();
        /// <summary>One hole per animal, in the order the animals were given.</summary>
        public readonly List<Hole> Holes = new List<Hole>();

        /// <param name="animals">The room each trapped animal needs.</param>
        /// <param name="cellSize">Mesh square size in metres.</param>
        /// <param name="scramble">0..1: how many inner knots start out of place.</param>
        public static NetPuzzle Generate(IList<Fit> animals, float cellSize, float scramble, int seed)
        {
            var rng = new System.Random(seed);
            int need = 0;
            foreach (Fit f in animals) need += f == Fit.Large ? 4 : f == Fit.Long ? 2 : 1;
            int columns = Mathf.Clamp(Mathf.CeilToInt(need * 1.7f / Rows), 4, 10);
            NetPuzzle puzzle = null;
            for (; columns <= 16; columns++)
            {
                puzzle = new NetPuzzle();
                if (puzzle.Layout(animals, columns, cellSize, rng)) break;
            }
            puzzle.Scramble(scramble, animals.Count, rng);
            return puzzle;
        }

        // ───────────────────────── Layout (solved) ─────────────────────────

        private int Knot(int i, int j) => j * (Columns + 1) + i;

        private bool Layout(IList<Fit> animals, int columns, float cell, System.Random rng)
        {
            Columns = columns;
            CellSize = cell;
            float w = columns * cell, h = Rows * cell;
            Bounds = new Rect(-w * 0.5f, -h * 0.5f, w, h);

            for (int j = 0; j <= Rows; j++)
            for (int i = 0; i <= columns; i++)
            {
                bool inner = i > 0 && i < columns && j > 0 && j < Rows;
                // A slightly uneven mesh reads as a hand-knotted net.
                Vector2 jitter = inner ? new Vector2(Rand(rng), Rand(rng)) * (cell * 0.1f) : Vector2.zero;
                Vector2 p = new Vector2(Bounds.xMin + i * cell, Bounds.yMin + j * cell) + jitter;
                Solved.Add(p);
                Positions.Add(p);
                Exists.Add(true);
                Movable.Add(inner);
            }

            // Holes, biggest first (they're the hardest to fit), kept in the animals' order.
            var owner = new int[columns, Rows];
            for (int i = 0; i < columns; i++) for (int j = 0; j < Rows; j++) owner[i, j] = -1;
            var order = new List<int>();
            for (int a = 0; a < animals.Count; a++) order.Add(a);
            order.Sort((x, y) => Cells(animals[y]).CompareTo(Cells(animals[x])) != 0 ? Cells(animals[y]).CompareTo(Cells(animals[x])) : x.CompareTo(y));
            var holes = new Hole[animals.Count];

            foreach (int a in order)
            {
                Fit fit = animals[a];
                var options = new List<RectInt>();
                for (int i = 0; i < columns; i++)
                for (int j = 0; j < Rows; j++)
                {
                    if (fit == Fit.Large) options.Add(new RectInt(i, j, 2, 2));
                    else if (fit == Fit.Long) { options.Add(new RectInt(i, j, 2, 1)); options.Add(new RectInt(i, j, 1, 2)); }
                    else options.Add(new RectInt(i, j, 1, 1));
                }
                Shuffle(options, rng);

                RectInt pick = default;
                bool found = false;
                // Prefer a spot that doesn't touch another hole, so net stays between the animals.
                for (int pass = 0; pass < 2 && !found; pass++)
                {
                    foreach (RectInt o in options)
                    {
                        if (!IsFreeBlock(owner, o) || (pass == 0 && TouchesHole(owner, o))) continue;
                        pick = o;
                        found = true;
                        break;
                    }
                }
                if (!found) return false;

                for (int i = pick.xMin; i < pick.xMax; i++) for (int j = pick.yMin; j < pick.yMax; j++) owner[i, j] = a;
                holes[a] = MakeHole(fit, pick);
            }
            Holes.AddRange(holes);

            // Ropes: every grid edge except those inside a hole (a large hole also loses its middle knot).
            for (int j = 0; j <= Rows; j++)
            for (int i = 0; i <= columns; i++)
            {
                if (i < columns && !InsideHole(owner, i, j - 1, i, j)) Ropes.Add(new Rope { A = Knot(i, j), B = Knot(i + 1, j) });
                if (j < Rows && !InsideHole(owner, i - 1, j, i, j)) Ropes.Add(new Rope { A = Knot(i, j), B = Knot(i, j + 1) });
            }
            foreach (Hole hole in Holes)
            {
                if (hole.Fit != Fit.Large) continue;
                // The middle knot of a 2×2 hole is the one not on its rim.
                Vector2 c = hole.Center;
                int mid = -1;
                float best = float.MaxValue;
                for (int k = 0; k < Solved.Count; k++)
                {
                    float d = (Solved[k] - c).sqrMagnitude;
                    if (d < best) { best = d; mid = k; }
                }
                Exists[mid] = false;
                Movable[mid] = false;
            }
            Ropes.RemoveAll(r => !Exists[r.A] || !Exists[r.B]);

            for (int i = 0; i < columns; i++)
            for (int j = 0; j < Rows; j++)
            {
                if (owner[i, j] >= 0) continue;
                Meshes.Add(new[] { Knot(i, j), Knot(i + 1, j), Knot(i + 1, j + 1), Knot(i, j + 1) });
            }
            return true;
        }

        private static int Cells(Fit f) => f == Fit.Large ? 4 : f == Fit.Long ? 2 : 1;

        private bool IsFreeBlock(int[,] owner, RectInt b)
        {
            if (b.xMin < 0 || b.yMin < 0 || b.xMax > Columns || b.yMax > Rows) return false;
            for (int i = b.xMin; i < b.xMax; i++) for (int j = b.yMin; j < b.yMax; j++) if (owner[i, j] >= 0) return false;
            return true;
        }

        private bool TouchesHole(int[,] owner, RectInt b)
        {
            for (int i = b.xMin - 1; i <= b.xMax; i++)
            for (int j = b.yMin - 1; j <= b.yMax; j++)
            {
                bool side = (i >= b.xMin && i < b.xMax) ^ (j >= b.yMin && j < b.yMax);
                if (side && i >= 0 && j >= 0 && i < Columns && j < Rows && owner[i, j] >= 0) return true;
            }
            return false;
        }

        // True when both cells beside an edge belong to the same hole.
        private bool InsideHole(int[,] owner, int i0, int j0, int i1, int j1)
        {
            if (i0 < 0 || j0 < 0 || i1 >= Columns || j1 >= Rows) return false;
            return owner[i0, j0] >= 0 && owner[i0, j0] == owner[i1, j1];
        }

        private Hole MakeHole(Fit fit, RectInt b)
        {
            var hole = new Hole { Fit = fit };
            Vector2 sum = Vector2.zero;
            int corners = 0;
            for (int i = b.xMin; i <= b.xMax; i++)
            for (int j = b.yMin; j <= b.yMax; j++)
            {
                bool rim = i == b.xMin || i == b.xMax || j == b.yMin || j == b.yMax;
                if (!rim) continue;
                hole.Corners.Add(Knot(i, j));
                if ((i == b.xMin || i == b.xMax) && (j == b.yMin || j == b.yMax)) { sum += Solved[Knot(i, j)]; corners++; }
            }
            hole.Center = sum / corners;
            if (fit == Fit.Long)
            {
                hole.Axis = b.width > b.height ? Vector2.right : Vector2.up;
                hole.HalfLength = CellSize * 0.5f;
            }
            hole.Radius = CellSize * (fit == Fit.Large ? 0.78f : 0.34f);
            return hole;
        }

        // ───────────────────────── Scramble ─────────────────────────

        private void Scramble(float scramble, int animals, System.Random rng)
        {
            var movable = new List<int>();
            for (int k = 0; k < Positions.Count; k++) if (Movable[k]) movable.Add(k);
            if (movable.Count == 0) return;
            int target = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(scramble) * (2f + animals * 0.9f)), Mathf.Min(3, movable.Count), movable.Count);

            var best = new List<Vector2>(Solved);
            int bestTrapped = -1, bestCrossings = -1;
            for (int attempt = 0; attempt < 80; attempt++)
            {
                for (int k = 0; k < Positions.Count; k++) Positions[k] = Solved[k];

                // Every hole gets at least one of its inner rim knots pulled across it.
                var pick = new List<int>();
                foreach (Hole hole in Holes)
                {
                    var rim = hole.Corners.FindAll(k => Movable[k]);
                    if (rim.Count == 0 || rim.Exists(pick.Contains)) continue;
                    pick.Add(rim[rng.Next(rim.Count)]);
                }
                var rest = movable.FindAll(k => !pick.Contains(k));
                Shuffle(rest, rng);
                for (int i = 0; pick.Count < target && i < rest.Count; i++) pick.Add(rest[i]);

                // Each picked knot is tugged a cell or so out of place, so the tangles stay local and readable.
                foreach (int k in pick)
                {
                    Vector2 p = Solved[k];
                    for (int tries = 0; tries < 16; tries++)
                    {
                        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float reach = Mathf.Lerp(0.8f, 1.5f, (float)rng.NextDouble()) * CellSize;
                        p = ClampToNet(Solved[k] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach);
                        if ((p - Solved[k]).magnitude >= CellSize * 0.6f && !Crowded(p, k)) break;
                    }
                    Positions[k] = p;
                }

                // A hole still open: pull one of its rim knots across to the far side, dragging its ropes over it.
                for (int h = 0; h < Holes.Count; h++)
                {
                    if (!IsFree(h)) continue;
                    var rim = Holes[h].Corners.FindAll(k => Movable[k]);
                    if (rim.Count == 0) continue;
                    int k = rim[rng.Next(rim.Count)];
                    Vector2 across = Holes[h].Center * 2f - Solved[k] + new Vector2(Rand(rng), Rand(rng)) * (CellSize * 0.3f);
                    Positions[k] = ClampToNet(across);
                }

                int trapped = 0;
                for (int h = 0; h < Holes.Count; h++) if (!IsFree(h)) trapped++;
                int crossings = TotalCrossings();
                if (trapped > bestTrapped || (trapped == bestTrapped && crossings > bestCrossings))
                {
                    bestTrapped = trapped;
                    bestCrossings = crossings;
                    best = new List<Vector2>(Positions);
                }
                if (trapped == Holes.Count && crossings >= Mathf.Max(2, target)) return;
            }
            for (int k = 0; k < Positions.Count; k++) Positions[k] = best[k];
        }

        private bool Crowded(Vector2 p, int self)
        {
            for (int k = 0; k < Positions.Count; k++)
            {
                if (k != self && Exists[k] && (Positions[k] - p).magnitude < CellSize * 0.35f) return true;
            }
            return false;
        }

        // ───────────────────────── Moves ─────────────────────────

        public Vector2 ClampToNet(Vector2 p)
        {
            Rect r = Shrink(Bounds, CellSize * 0.15f);
            return new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));
        }

        /// <summary>Moves an inner knot (clamped inside the net); border knots stay put.</summary>
        public void MoveKnot(int knot, Vector2 to)
        {
            if (knot < 0 || knot >= Positions.Count || !Movable[knot]) return;
            Positions[knot] = ClampToNet(to);
        }

        // ───────────────────────── Crossings ─────────────────────────

        public bool RopesCross(int i, int j)
        {
            Rope a = Ropes[i], b = Ropes[j];
            // Ropes that share a knot don't count as crossing.
            if (a.A == b.A || a.A == b.B || a.B == b.A || a.B == b.B) return false;
            return SegmentsIntersect(Positions[a.A], Positions[a.B], Positions[b.A], Positions[b.B]);
        }

        /// <summary>True if this rope crosses any other rope.</summary>
        public bool RopeIsCrossed(int rope)
        {
            for (int j = 0; j < Ropes.Count; j++) if (j != rope && RopesCross(rope, j)) return true;
            return false;
        }

        public int TotalCrossings()
        {
            int count = 0;
            for (int i = 0; i < Ropes.Count; i++)
                for (int j = i + 1; j < Ropes.Count; j++)
                    if (RopesCross(i, j)) count++;
            return count;
        }

        // ───────────────────────── Holes ─────────────────────────

        /// <summary>The finer cords across a mesh square (bilinear, so they follow its knots).</summary>
        public void MeshLine(int mesh, int line, out Vector2 a, out Vector2 b)
        {
            int[] c = Meshes[mesh];
            float t = (line % MeshLines + 1f) / (MeshLines + 1f);
            if (line < MeshLines) { a = Bilerp(c, t, 0f); b = Bilerp(c, t, 1f); }
            else { a = Bilerp(c, 0f, t); b = Bilerp(c, 1f, t); }
        }

        public const int LinesPerMesh = MeshLines * 2;

        private Vector2 Bilerp(int[] c, float u, float v) =>
            (1f - u) * (1f - v) * Positions[c[0]] + u * (1f - v) * Positions[c[1]] + u * v * Positions[c[2]] + (1f - u) * v * Positions[c[3]];

        /// <summary>Distance from a hole's spine to the nearest rope or cord.</summary>
        public float Clearance(int hole)
        {
            Hole h = Holes[hole];
            Vector2 a = h.Center - h.Axis * h.HalfLength, b = h.Center + h.Axis * h.HalfLength;
            float best = float.MaxValue;
            foreach (Rope r in Ropes) best = Mathf.Min(best, SegmentDistance(a, b, Positions[r.A], Positions[r.B]));
            for (int m = 0; m < Meshes.Count; m++)
            for (int l = 0; l < LinesPerMesh; l++)
            {
                MeshLine(m, l, out Vector2 p, out Vector2 q);
                best = Mathf.Min(best, SegmentDistance(a, b, p, q));
            }
            return best;
        }

        /// <summary>The animal under this hole fits through: nothing crosses the room it needs.</summary>
        public bool IsFree(int hole) => Clearance(hole) >= Holes[hole].Radius;

        /// <summary>
        /// Hint: an out-of-place knot whose rope or cord blocks the first still-trapped animal
        /// (sliding it home opens that animal's way out), or -1.
        /// </summary>
        public int HintKnot(IList<bool> freed = null)
        {
            for (int h = 0; h < Holes.Count; h++)
            {
                if ((freed != null && h < freed.Count && freed[h]) || IsFree(h)) continue;
                Hole hole = Holes[h];
                Vector2 a = hole.Center - hole.Axis * hole.HalfLength, b = hole.Center + hole.Axis * hole.HalfLength;
                var blockers = new List<int>();
                foreach (Rope r in Ropes)
                {
                    if (SegmentDistance(a, b, Positions[r.A], Positions[r.B]) < hole.Radius) { blockers.Add(r.A); blockers.Add(r.B); }
                }
                for (int m = 0; m < Meshes.Count; m++)
                for (int l = 0; l < LinesPerMesh; l++)
                {
                    MeshLine(m, l, out Vector2 p, out Vector2 q);
                    if (SegmentDistance(a, b, p, q) < hole.Radius) blockers.AddRange(Meshes[m]);
                }
                int knot = FarthestFromHome(blockers);
                if (knot >= 0) return knot;
            }
            var crossed = new List<int>();
            for (int r = 0; r < Ropes.Count; r++) if (RopeIsCrossed(r)) { crossed.Add(Ropes[r].A); crossed.Add(Ropes[r].B); }
            return FarthestFromHome(crossed);
        }

        private int FarthestFromHome(List<int> knots)
        {
            int best = -1;
            float bestD = CellSize * 0.02f;
            foreach (int k in knots)
            {
                if (!Movable[k]) continue;
                float d = (Positions[k] - Solved[k]).magnitude;
                if (d > bestD) { bestD = d; best = k; }
            }
            return best;
        }

        // ───────────────────────── Geometry ─────────────────────────

        /// <summary>Proper intersection test (touching endpoints / collinear overlaps count as crossing).</summary>
        public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1);
            float d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1);
            float d4 = Cross(p2 - p1, q2 - p1);
            const float eps = 1e-6f;
            if (((d1 > eps && d2 < -eps) || (d1 < -eps && d2 > eps)) && ((d3 > eps && d4 < -eps) || (d3 < -eps && d4 > eps))) return true;
            if (Mathf.Abs(d1) <= eps && OnSegment(q1, q2, p1)) return true;
            if (Mathf.Abs(d2) <= eps && OnSegment(q1, q2, p2)) return true;
            if (Mathf.Abs(d3) <= eps && OnSegment(p1, p2, q1)) return true;
            if (Mathf.Abs(d4) <= eps && OnSegment(p1, p2, q2)) return true;
            return false;
        }

        /// <summary>Shortest distance between two segments (0 when they cross).</summary>
        public static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (SegmentsIntersect(a, b, c, d)) return 0f;
            return Mathf.Min(Mathf.Min(PointSegment(a, c, d), PointSegment(b, c, d)), Mathf.Min(PointSegment(c, a, b), PointSegment(d, a, b)));
        }

        private static float PointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len < 1e-9f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
            p.x >= Mathf.Min(a.x, b.x) - 1e-6f && p.x <= Mathf.Max(a.x, b.x) + 1e-6f &&
            p.y >= Mathf.Min(a.y, b.y) - 1e-6f && p.y <= Mathf.Max(a.y, b.y) + 1e-6f;

        private static Rect Shrink(Rect r, float by) => new Rect(r.xMin + by, r.yMin + by, r.width - by * 2f, r.height - by * 2f);

        private static float Rand(System.Random rng) => (float)rng.NextDouble() - 0.5f;

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
