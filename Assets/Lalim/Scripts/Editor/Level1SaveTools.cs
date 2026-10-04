using System.IO;
using UnityEditor;
using UnityEngine;
using Lalim.Core;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 1 ▸ Reset Level 1 Progress — edits the save slot the editor loads
    /// (GameManager loads slot 0 on every Play) so Level 1 can be played from the start again.
    /// Only Level 1 progress is removed; the player name, settings and other regions' data stay.
    /// The previous file is kept next to it as *_before_reset.json.
    /// </summary>
    public static class Level1SaveTools
    {
        private static readonly string[] Level1Tools = { "07_watersamplekit", "09_coralseed", "05_coralreplantingkit" };

        [MenuItem("Tools/Lalim/Level 1/Reset Level 1 Progress (save slot 0)")]
        public static void ResetFromMenu()
        {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
            Debug.Log(ResetLevel1(0));
        }

        public static string ResetLevel1(int slot)
        {
            string path = Path.Combine(Application.persistentDataPath, GameConstants.Save.GetSaveFileName(slot));
            if (!File.Exists(path)) return $"No save at {path} — nothing to reset.";

            string json = File.ReadAllText(path);
            File.WriteAllText(Path.ChangeExtension(path, null) + "_before_reset.json", json);

            SaveData save = JsonUtility.FromJson<SaveData>(json);
            save.completedChallengeIds?.RemoveAll(id => id.StartsWith("luzon_"));
            save.completedZoneIds?.RemoveAll(id => id.StartsWith("luzon_"));
            save.collectedTrashIds?.Clear();
            save.selectedTrashIds?.Clear();
            save.tutorialFlags?.Clear();
            save.unlockedAchievementIds?.RemoveAll(id => id.StartsWith("l1_"));
            save.achievementProgress?.RemoveAll(id => id.StartsWith("l1_"));
            foreach (string tool in Level1Tools) save.collectedToolIds?.Remove(tool);
            save.lastSceneName = null;
            save.lastCheckpointId = null;
            save.levelToxicDps = -1f;
            save.levelToxicScene = null;
            save.ghostNetBagState = null;
            save.luzonStatus = (int)RegionStatus.Unlocked;
            if (save.visayasStatus != (int)RegionStatus.Completed) save.visayasStatus = (int)RegionStatus.Locked;

            // Difficulty history from test runs shouldn't carry over.
            save.difficultyLevel = GameConstants.DDA.DEFAULT_DIFFICULTY;
            save.minigameScores?.Clear();
            save.sectionDeathCounts?.Clear();

            File.WriteAllText(path, JsonUtility.ToJson(save, true));
            return $"Level 1 progress reset in {path} (backup: *_before_reset.json).";
        }
    }
}
