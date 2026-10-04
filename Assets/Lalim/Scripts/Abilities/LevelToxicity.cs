using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Abilities
{
    /// <summary>
    /// Level-wide toxic drain. The level director sets the HP/s once when a level loads
    /// (from the player's performance so far) and it never changes mid-level.
    /// Zone volumes with "Use Level Rate" read it instead of the live difficulty-scaled rate.
    ///
    /// Also owns the drain pause: dialogue suppresses damage while open (minigames keep draining).
    /// </summary>
    public static class LevelToxicity
    {
        /// <summary>HP per second for level zone volumes. Negative = not set (volumes fall back to their config).</summary>
        public static float CurrentDps { get; private set; } = -1f;

        private static readonly HashSet<object> s_Suppressors = new HashSet<object>();

        public static bool IsSuppressed => s_Suppressors.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CurrentDps = -1f;
            s_Suppressors.Clear();
        }

        public static void SetLevelDps(float dps) => CurrentDps = Mathf.Max(0f, dps);

        public static void ClearLevelDps() => CurrentDps = -1f;

        public static void Suppress(object source) { if (source != null) s_Suppressors.Add(source); }

        public static void Release(object source) { if (source != null) s_Suppressors.Remove(source); }

        public static void ReleaseAll() => s_Suppressors.Clear();

        /// <summary>
        /// Drain for a level: the base rate scaled by the difficulty snapshot taken when the level loads.
        /// Level 1 pins the multiplier to 1 because the player has no history yet.
        /// </summary>
        public static float ComputeLevelDps(float baseDps, float difficultySnapshot, float minMultiplier, float maxMultiplier, bool pinToBase)
        {
            if (pinToBase) return baseDps;
            float t = Mathf.Clamp01(difficultySnapshot);
            float mult = t <= 0.5f ? Mathf.Lerp(minMultiplier, 1f, t / 0.5f) : Mathf.Lerp(1f, maxMultiplier, (t - 0.5f) / 0.5f);
            return baseDps * mult;
        }
    }
}
