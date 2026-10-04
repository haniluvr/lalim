using System.Collections.Generic;

namespace Lalim.Challenges.GhostNet
{
    /// <summary>
    /// Level-wide "shuffle bag" for trapped animals: every species appears once before any repeats,
    /// an area never gets the same species twice, and a refill never starts with the species dealt
    /// last. State is a plain string so it can live in SaveData.ghostNetBagState.
    /// </summary>
    public static class SpeciesBag
    {
        /// <summary>Deals <paramref name="count"/> species ids and returns the updated bag state.</summary>
        public static List<string> Deal(int count, IList<string> allIds, string state, System.Random rng, out string newState)
        {
            Parse(state, allIds, out List<string> remaining, out string last);
            var dealt = new List<string>();

            while (dealt.Count < count && allIds.Count > 0)
            {
                if (remaining.Count == 0) remaining = Refill(allIds, rng);

                // Take the first species that isn't already in this area (and doesn't repeat the last one dealt).
                int pick = -1;
                for (int i = 0; i < remaining.Count; i++)
                {
                    string id = remaining[i];
                    if (dealt.Contains(id) || id == last) continue;
                    pick = i;
                    break;
                }
                if (pick < 0)
                {
                    // Everything left clashes: refill, and fall back to any non-duplicate.
                    remaining.AddRange(Refill(allIds, rng));
                    for (int i = 0; i < remaining.Count && pick < 0; i++) if (!dealt.Contains(remaining[i])) pick = i;
                    if (pick < 0) break; // fewer species than requested
                }

                last = remaining[pick];
                dealt.Add(last);
                remaining.RemoveAt(pick);
            }

            newState = string.Join(",", remaining) + "|" + last;
            return dealt;
        }

        private static List<string> Refill(IList<string> allIds, System.Random rng)
        {
            var list = new List<string>(allIds);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private static void Parse(string state, IList<string> allIds, out List<string> remaining, out string last)
        {
            remaining = new List<string>();
            last = null;
            if (string.IsNullOrEmpty(state)) return;
            string[] parts = state.Split('|');
            foreach (string id in parts[0].Split(','))
            {
                if (!string.IsNullOrEmpty(id) && allIds.Contains(id)) remaining.Add(id);
            }
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) last = parts[1];
        }
    }
}
