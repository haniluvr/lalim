using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lalim.Challenges.GhostNet;

namespace Lalim.Tests
{
    public class NetPuzzleTests
    {
        private static readonly NetPuzzle.Fit[] Mixed =
            { NetPuzzle.Fit.Large, NetPuzzle.Fit.Small, NetPuzzle.Fit.Long, NetPuzzle.Fit.Small, NetPuzzle.Fit.Large };

        private static NetPuzzle.Fit[] Animals(int seed, int count)
        {
            var rng = new System.Random(seed);
            var list = new NetPuzzle.Fit[count];
            for (int i = 0; i < count; i++) list[i] = (NetPuzzle.Fit)rng.Next(3);
            return list;
        }

        private static void Solve(NetPuzzle p)
        {
            for (int k = 0; k < p.Positions.Count; k++) p.Positions[k] = p.Solved[k];
        }

        [Test]
        public void CrossingSegmentsAreDetected()
        {
            Assert.IsTrue(NetPuzzle.SegmentsIntersect(new Vector2(0, 0), new Vector2(2, 2), new Vector2(0, 2), new Vector2(2, 0)));
            Assert.IsFalse(NetPuzzle.SegmentsIntersect(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1)));
            Assert.IsFalse(NetPuzzle.SegmentsIntersect(new Vector2(0, 0), new Vector2(1, 1), new Vector2(2, 2), new Vector2(3, 0)));
            Assert.AreEqual(1f, NetPuzzle.SegmentDistance(new Vector2(0, 0), new Vector2(2, 0), new Vector2(1, 1), new Vector2(1, 3)), 1e-5f);
        }

        [Test]
        public void OneHolePerAnimalSizedToIt()
        {
            NetPuzzle p = NetPuzzle.Generate(Mixed, 1f, 0.8f, 1);
            Assert.AreEqual(Mixed.Length, p.Holes.Count);
            for (int i = 0; i < Mixed.Length; i++) Assert.AreEqual(Mixed[i], p.Holes[i].Fit);
            Assert.Greater(p.Holes[0].Radius, p.Holes[1].Radius, "a large animal needs a bigger opening than a small one");
        }

        [Test]
        public void SolvedNetIsUntangledAndFreesEveryAnimal([Values(3, 5, 7)] int animals)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                NetPuzzle p = NetPuzzle.Generate(Animals(seed, animals), 0.9f, 1f, seed);
                Solve(p);
                Assert.AreEqual(0, p.TotalCrossings(), $"seed {seed}: the solution must be untangled");
                for (int h = 0; h < p.Holes.Count; h++) Assert.IsTrue(p.IsFree(h), $"seed {seed}: animal {h} must fit through its hole");
            }
        }

        [Test]
        public void EveryAnimalStartsTrapped([Values(0.6f, 0.8f, 1f)] float scramble)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                NetPuzzle p = NetPuzzle.Generate(Animals(seed, 5), 0.9f, scramble, seed);
                Assert.Greater(p.TotalCrossings(), 0, $"seed {seed}");
                for (int h = 0; h < p.Holes.Count; h++) Assert.IsFalse(p.IsFree(h), $"seed {seed}: animal {h} starts free");
            }
        }

        [Test]
        public void BorderKnotsStayAndInnerKnotsStayInTheNet()
        {
            NetPuzzle p = NetPuzzle.Generate(Mixed, 1f, 0.8f, 7);
            int border = p.Movable.IndexOf(false);
            Vector2 before = p.Positions[border];
            p.MoveKnot(border, Vector2.zero);
            Assert.AreEqual(before, p.Positions[border]);

            int inner = p.Movable.IndexOf(true);
            p.MoveKnot(inner, new Vector2(100f, -100f));
            Assert.IsTrue(p.Bounds.Contains(p.Positions[inner]));
        }

        [Test]
        public void HintSendsABlockingKnotHome()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                NetPuzzle p = NetPuzzle.Generate(Mixed, 1f, 1f, seed);
                int k = p.HintKnot();
                Assert.GreaterOrEqual(k, 0, $"seed {seed}");
                Assert.IsTrue(p.Movable[k]);
                Assert.Greater((p.Positions[k] - p.Solved[k]).magnitude, 0.01f, "the hinted knot is out of place");
            }
        }

        [Test]
        public void HintsAloneSolveThePuzzle()
        {
            NetPuzzle p = NetPuzzle.Generate(Mixed, 1f, 1f, 4);
            for (int i = 0; i < 200; i++)
            {
                int k = p.HintKnot();
                if (k < 0) break;
                p.MoveKnot(k, p.Solved[k]);
            }
            for (int h = 0; h < p.Holes.Count; h++) Assert.IsTrue(p.IsFree(h));
        }
    }

    public class SpeciesBagTests
    {
        private static readonly string[] All =
            { "clownfish", "bluetang", "reeffish", "crab", "lobster", "seastar", "octopus", "squid", "moray", "pawikan", "stingray" };

        [Test]
        public void NoRepeatsWithinAnArea()
        {
            var dealt = SpeciesBag.Deal(7, All, null, new System.Random(1), out _);
            Assert.AreEqual(7, dealt.Count);
            Assert.AreEqual(7, new HashSet<string>(dealt).Count);
        }

        [Test]
        public void AllSpeciesAppearBeforeAnyRepeat()
        {
            string state = null;
            var seen = new List<string>();
            seen.AddRange(SpeciesBag.Deal(5, All, state, new System.Random(2), out state));
            seen.AddRange(SpeciesBag.Deal(6, All, state, new System.Random(3), out state));
            Assert.AreEqual(11, new HashSet<string>(seen).Count, "11 animals across two areas = all 11 species once");
        }

        [Test]
        public void HardestLevelRepeatsEachSpeciesAtMostTwice()
        {
            string state = null;
            var counts = new Dictionary<string, int>();
            for (int area = 0; area < 3; area++)
            {
                var dealt = SpeciesBag.Deal(7, All, state, new System.Random(10 + area), out state);
                Assert.AreEqual(7, new HashSet<string>(dealt).Count, "no repeats inside an area");
                foreach (string id in dealt) counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
            }
            foreach (var kv in counts) Assert.LessOrEqual(kv.Value, 2, kv.Key);
        }
    }
}
