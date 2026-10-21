using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.Inventory;
using Lalim.Mechanics.Quest;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Play From ▸ Level N · Zone M — for testing: opens the level and plays it as if
    /// every earlier zone were finished (its steps done, cleansed, its gifts in the inventory, earlier
    /// levels complete), starting at that zone's checkpoint (Zone 1: the level's own spawn). A zone
    /// can name a different test start with a child checkpoint called "PlayFrom_Start" (its trigger
    /// off, so normal play never reaches it): e.g. Padre Burgos starts at the end of Trench B, just
    /// before Bakunawa rises, rather than at its checkpoint at the bottom of the abyss.
    ///
    /// Your real save (slot 0) is copied aside first and put back as soon as Play stops, so testing
    /// never touches your progress. If Unity closes mid-test, Tools ▸ Lalim ▸ Play From ▸ Restore My
    /// Save puts it back by hand.
    /// </summary>
    public static class PlayFromZone
    {
        private const int Slot = 0;
        private const string PendingKey = "Lalim.PlayFromZone.RestorePending";
        private const string StartName = "PlayFrom_Start";
        private const string ItemsFolder = "Assets/Lalim/Data/Items";
        private const string Level1Scene = "Assets/Lalim/Scenes/Levels/01_Luzon/02_Luzon.unity";
        private const string Level2Scene = "Assets/Lalim/Scenes/Levels/02_Visayas/03_Visayas.unity";

        [MenuItem("Tools/Lalim/Play From/Level 1 · Zone 1", false, 1)] private static void L1Z1() => Play(Level1Scene, 0);
        [MenuItem("Tools/Lalim/Play From/Level 1 · Zone 2", false, 2)] private static void L1Z2() => Play(Level1Scene, 1);
        [MenuItem("Tools/Lalim/Play From/Level 1 · Zone 3", false, 3)] private static void L1Z3() => Play(Level1Scene, 2);
        [MenuItem("Tools/Lalim/Play From/Level 2 · Zone 1", false, 20)] private static void L2Z1() => Play(Level2Scene, 0);
        [MenuItem("Tools/Lalim/Play From/Level 2 · Zone 2", false, 21)] private static void L2Z2() => Play(Level2Scene, 1);
        [MenuItem("Tools/Lalim/Play From/Level 2 · Zone 3", false, 22)] private static void L2Z3() => Play(Level2Scene, 2);

        [MenuItem("Tools/Lalim/Play From/Restore My Save", false, 40)]
        private static void RestoreMenu() => Debug.Log(Restore());

        [MenuItem("Tools/Lalim/Play From/Restore My Save", true)]
        private static bool RestoreMenuValid() => File.Exists(BackupPath(SavePath())) && !Application.isPlaying;

        private static string SavePath() => Path.Combine(Application.persistentDataPath, GameConstants.Save.GetSaveFileName(Slot));
        private static string BackupFileOf(string savePath) => Path.ChangeExtension(savePath, null) + "_backup.json";   // SaveManager's own fallback copy
        private static string BackupPath(string savePath) => Path.ChangeExtension(savePath, null) + "_before_play_from_zone.json";
        private static string SpareBackupPath(string savePath) => Path.ChangeExtension(savePath, null) + "_backup_before_play_from_zone.json";

        // ───────────────────────── Play ─────────────────────────

        public static void Play(string scenePath, int zoneIndex)
        {
            if (Application.isPlaying) { Debug.LogWarning("[PlayFromZone] Stop Play mode first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var director = Object.FindFirstObjectByType<LevelFlowDirector>();
            if (director == null || director.Zones.Count <= zoneIndex) { Debug.LogError($"[PlayFromZone] {scenePath} has no LevelFlowDirector zone {zoneIndex + 1}."); return; }

            string path = SavePath();
            string keep = BackupPath(path);
            // Keep the oldest copy: if an earlier test never restored, that copy is still your real save.
            if (File.Exists(path) && !File.Exists(keep))
            {
                File.Copy(path, keep);
                if (File.Exists(BackupFileOf(path))) File.Copy(BackupFileOf(path), SpareBackupPath(path), true);
            }

            SaveData save = File.Exists(path) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(path)) : new SaveData();
            string report = Prepare(save, director, zoneIndex, Path.GetFileNameWithoutExtension(scenePath));
            string json = JsonUtility.ToJson(save, true);
            File.WriteAllText(path, json);
            File.WriteAllText(BackupFileOf(path), json);

            SessionState.SetBool(PendingKey, true);
            Debug.Log($"[PlayFromZone] {report} Your save is kept aside and comes back when Play stops.");
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Earlier zones finished, this zone and later ones fresh; earlier levels complete.</summary>
        private static string Prepare(SaveData save, LevelFlowDirector director, int zoneIndex, string sceneName)
        {
            Region region = director.Region;
            var items = ItemsById();
            save.completedChallengeIds ??= new List<string>();
            save.completedZoneIds ??= new List<string>();
            save.collectedRelicIds ??= new List<string>();
            save.collectedToolIds ??= new List<string>();
            save.collectedQuestItemIds ??= new List<string>();

            // This level from scratch: drop its progress, then mark the earlier zones done.
            string prefix = region.ToString().ToLowerInvariant() + "_";
            save.completedChallengeIds.RemoveAll(id => id.StartsWith(prefix));
            save.completedZoneIds.RemoveAll(id => id.StartsWith(prefix));
            for (int z = 0; z < director.Zones.Count; z++)
            {
                ZoneQuest zone = director.Zones[z];
                if (zone == null) continue;
                bool done = z < zoneIndex;
                foreach (QuestStep step in zone.Steps)
                {
                    if (step == null) continue;
                    string item = new SerializedObject(step).FindProperty("_itemId")?.stringValue;
                    if (done)
                    {
                        if (!string.IsNullOrEmpty(step.StepId)) save.completedChallengeIds.Add(step.StepId);
                        if (!string.IsNullOrEmpty(item)) Give(save, item, items);
                    }
                    else if (!string.IsNullOrEmpty(item) && step is RelicRewardStep) Take(save, item);   // this zone's gift is earned again
                }
                if (done) save.completedZoneIds.Add(zone.ZoneId);
            }

            // Earlier levels complete, with the relics Level 2 needs.
            save.luzonStatus = (int)(region == Region.Luzon ? RegionStatus.Unlocked : RegionStatus.Completed);
            if (region != Region.Luzon)
            {
                save.visayasStatus = (int)(region == Region.Visayas ? RegionStatus.Unlocked : RegionStatus.Completed);
                foreach (string id in GameConstants.Zones.GetZonesForRegion(Region.Luzon))
                    if (!save.completedZoneIds.Contains(id)) save.completedZoneIds.Add(id);
                foreach (string id in new[] { GameConstants.Relics.RADIANT_CONCH, GameConstants.Relics.COMPASS, GameConstants.Relics.WOVEN_NET_FRAGMENT })
                    Give(save, id, items);
            }
            if (region == Region.Luzon) { save.collectedTrashIds?.Clear(); save.selectedTrashIds?.Clear(); }

            // Start at the zone's checkpoint (Zone 1: the level's spawn), fresh vitals, nothing equipped.
            Checkpoint cp = zoneIndex == 0 ? null : ZoneCheckpoint(director.Zones[zoneIndex]);
            save.lastSceneName = sceneName;
            save.lastCheckpointId = cp != null ? cp.CheckpointId : null;
            save.lastRegion = (int)region;
            save.equippedRelicIds?.Clear();
            save.levelToxicDps = -1f;
            save.levelToxicScene = null;
            if (save.vitals != null) save.vitals.valid = false;

            ZoneQuest start = director.Zones[zoneIndex];
            return $"Playing {sceneName} from Zone {zoneIndex + 1} ({start.DisplayName})" +
                   (cp != null ? $" at checkpoint {cp.CheckpointId}." : " at the level's spawn.");
        }

        /// <summary>The zone's test start (PlayFrom_Start), else its own checkpoint, else the one nearest its statue.</summary>
        private static Checkpoint ZoneCheckpoint(ZoneQuest zone)
        {
            foreach (Checkpoint c in zone.GetComponentsInChildren<Checkpoint>(true))
                if (c.gameObject.name == StartName) return c;
            Checkpoint own = null;
            foreach (Checkpoint c in zone.GetComponentsInChildren<Checkpoint>(true))
                if (c.gameObject.name != StartName) { own = c; break; }
            if (own != null) return own;
            if (zone.Statue == null) return null;
            Checkpoint best = null;
            float bestSqr = float.MaxValue;
            foreach (Checkpoint c in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                float d = (c.transform.position - zone.Statue.transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        private static Dictionary<string, ItemData> ItemsById()
        {
            var map = new Dictionary<string, ItemData>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { ItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && !string.IsNullOrEmpty(item.itemId)) map[item.itemId] = item;
            }
            return map;
        }

        private static List<string> ListFor(SaveData save, string itemId, Dictionary<string, ItemData> items)
        {
            if (!items.TryGetValue(itemId, out ItemData item)) return save.collectedRelicIds;
            switch (item.itemType)
            {
                case ItemType.Relic: return save.collectedRelicIds;
                case ItemType.QuestItem: return save.collectedQuestItemIds;
                default: return save.collectedToolIds;
            }
        }

        private static void Give(SaveData save, string itemId, Dictionary<string, ItemData> items)
        {
            List<string> list = ListFor(save, itemId, items);
            if (!list.Contains(itemId)) list.Add(itemId);
        }

        private static void Take(SaveData save, string itemId)
        {
            save.collectedRelicIds.Remove(itemId);
            save.collectedToolIds.Remove(itemId);
            save.collectedQuestItemIds.Remove(itemId);
        }

        // ───────────────────────── Restore ─────────────────────────

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(PendingKey, false)) return;
            Debug.Log(Restore());
        }

        public static string Restore()
        {
            SessionState.SetBool(PendingKey, false);
            string path = SavePath();
            string keep = BackupPath(path);
            if (!File.Exists(keep)) return "[PlayFromZone] No kept save to restore.";
            File.Copy(keep, path, true);
            string spare = SpareBackupPath(path);
            File.Copy(File.Exists(spare) ? spare : keep, BackupFileOf(path), true);
            File.Delete(keep);
            if (File.Exists(spare)) File.Delete(spare);
            return "[PlayFromZone] Your save is back.";
        }
    }
}
