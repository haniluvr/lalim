using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Level 1 achievements. Each unlocks once per save and shows the achievement toast.
    ///  - Clean Sweep:  trash sorting finished with no wrong-bowl mistakes.
    ///  - Steady Hands: all three water readings locked without the needle ever slipping out of the band.
    ///  - Net Breaker:  a ghost-net area cleared without using a hint.
    ///  - Untouched:    a zone cleansed while above 80% HP.
    /// </summary>
    [DisallowMultipleComponent]
    public class AchievementService : MonoBehaviour
    {
        public struct Achievement
        {
            public string Id;
            public string Name;
            public string Description;
        }

        public static readonly Achievement CleanSweep = new Achievement { Id = "l1_clean_sweep", Name = "Clean Sweep", Description = "Sorted every piece of trash without a mistake" };
        public static readonly Achievement SteadyHands = new Achievement { Id = "l1_steady_hands", Name = "Steady Hands", Description = "Locked every water reading without slipping" };
        public static readonly Achievement NetBreaker = new Achievement { Id = "l1_net_breaker", Name = "Net Breaker", Description = "Cleared a ghost net without a hint" };
        public static readonly Achievement Untouched = new Achievement { Id = "l1_untouched", Name = "Untouched", Description = "Cleansed a zone above 80% health" };

        [SerializeField] private string _sortChallengeId = "luzon_z1_sort";
        [SerializeField] private string _waterChallengePrefix = "luzon_z2_water_";
        [SerializeField] private string _netChallengePrefix = "luzon_z3_nets_";
        [SerializeField, Range(0f, 1f)] private float _untouchedHpFraction = 0.8f;

        private const string SteadyBrokenFlag = "l1_steady_hands:broken";
        private const int WaterAreas = 3;

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ZoneCompletedEvent>(OnZoneCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ZoneCompletedEvent>(OnZoneCompleted);
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!evt.Success || string.IsNullOrEmpty(evt.ChallengeId)) return;

            if (evt.ChallengeId == _sortChallengeId && evt.ErrorCount == 0) Unlock(CleanSweep);

            if (evt.ChallengeId.StartsWith(_waterChallengePrefix))
            {
                SaveData save = Save;
                if (evt.ErrorCount > 0) AddFlag(SteadyBrokenFlag);
                AddFlag("l1_water_done:" + evt.ChallengeId);
                if (save != null && !HasFlag(SteadyBrokenFlag) && CountFlags("l1_water_done:") >= WaterAreas) Unlock(SteadyHands);
            }

            if (evt.ChallengeId.StartsWith(_netChallengePrefix) && evt.HintsUsed == 0) Unlock(NetBreaker);
        }

        private void OnZoneCompleted(ZoneCompletedEvent evt)
        {
            PlayerHealth hp = PlayerHealth.Instance;
            if (hp != null && hp.Max > 0f && hp.Current / hp.Max > _untouchedHpFraction) Unlock(Untouched);
        }

        public static bool IsUnlocked(string id)
        {
            SaveData save = Save;
            return save != null && save.unlockedAchievementIds != null && save.unlockedAchievementIds.Contains(id);
        }

        public static void Unlock(Achievement a)
        {
            SaveData save = Save;
            if (save != null)
            {
                if (save.unlockedAchievementIds == null) save.unlockedAchievementIds = new List<string>();
                if (save.unlockedAchievementIds.Contains(a.Id)) return;
                save.unlockedAchievementIds.Add(a.Id);
            }
            Debug.Log($"[Achievement] {a.Name} unlocked");
            ActToastUI.Post("Achievement Unlocked", $"{a.Name}\n{a.Description}", ToastKind.Achievement);
            SaveManager.Instance?.AutoSave();
        }

        private static void AddFlag(string flag)
        {
            SaveData save = Save;
            if (save == null) return;
            if (save.achievementProgress == null) save.achievementProgress = new List<string>();
            if (!save.achievementProgress.Contains(flag)) save.achievementProgress.Add(flag);
        }

        private static bool HasFlag(string flag) => Save != null && Save.achievementProgress != null && Save.achievementProgress.Contains(flag);

        private static int CountFlags(string prefix)
        {
            int n = 0;
            if (Save?.achievementProgress != null) foreach (string f in Save.achievementProgress) if (f.StartsWith(prefix)) n++;
            return n;
        }
    }
}
