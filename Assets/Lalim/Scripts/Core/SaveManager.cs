using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// JSON-based local save system. Persists player progress, inventory,
    /// DDA metrics, and region unlock state to Application.persistentDataPath.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Header("Debug")]
        [SerializeField] private bool _logSaveOperations = true;

        public SaveData CurrentSave { get; private set; }
        public int CurrentSlot { get; private set; } = 0;
        
        public const int MAX_SLOTS = 3;

        public bool HasCurrentSave => File.Exists(GetSavePath(CurrentSlot));

        private string GetSavePath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.Save.GetSaveFileName(slot));
        }

        private string GetBackupPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.Save.GetBackupFileName(slot));
        }
        
        public bool HasAnySave()
        {
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (HasSave(i)) return true;
            }
            return false;
        }

        public bool HasSave(int slot)
        {
            return File.Exists(GetSavePath(slot));
        }

        public void SetCurrentSlot(int slot)
        {
            if (slot >= 0 && slot < MAX_SLOTS)
            {
                CurrentSlot = slot;
            }
        }

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentSave = new SaveData();

            if (_logSaveOperations)
            {
                Debug.Log($"[SaveManager] Save directory: {Application.persistentDataPath}");
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ItemCollectedEvent>(OnItemCollected);
            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemCollectedEvent>(OnItemCollected);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        // ─────────────────────────────────────────────
        // Save
        // ─────────────────────────────────────────────

        /// <summary>
        /// Save current game state to disk.
        /// </summary>
        public void SaveGame()
        {
            try
            {
                // Gather data from all managers
                CollectSaveData();

                CurrentSave.lastSaveTimestamp = DateTime.UtcNow.ToString("o");

                string json = JsonUtility.ToJson(CurrentSave, true);

                // Write backup first (in case write is interrupted)
                string savePath = GetSavePath(CurrentSlot);
                string backupPath = GetBackupPath(CurrentSlot);

                if (File.Exists(savePath))
                {
                    File.Copy(savePath, backupPath, true);
                }

                File.WriteAllText(savePath, json);

                if (_logSaveOperations)
                {
                    Debug.Log($"[SaveManager] Game saved successfully to Slot {CurrentSlot + 1}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Load game state from disk.
        /// </summary>
        public bool LoadGame()
        {
            string savePath = GetSavePath(CurrentSlot);

            if (!File.Exists(savePath))
            {
                if (_logSaveOperations)
                {
                    Debug.Log($"[SaveManager] No save file found for Slot {CurrentSlot + 1}, starting fresh");
                }
                CurrentSave = new SaveData();
                return false;
            }

            try
            {
                string json = File.ReadAllText(savePath);
                CurrentSave = JsonUtility.FromJson<SaveData>(json);

                // Distribute save data to managers
                DistributeSaveData();

                if (_logSaveOperations)
                {
                    Debug.Log($"[SaveManager] Game loaded (saved: {CurrentSave.lastSaveTimestamp})");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed for Slot {CurrentSlot + 1}: {e.Message}. Trying backup...");
                return TryLoadBackup();
            }
        }

        /// <summary>
        /// Delete save file and start fresh.
        /// </summary>
        public void DeleteSave(int slot)
        {
            string savePath = GetSavePath(slot);
            string backupPath = GetBackupPath(slot);

            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(backupPath)) File.Delete(backupPath);

            if (slot == CurrentSlot)
            {
                CurrentSave = new SaveData();
            }
            Debug.Log($"[SaveManager] Save data deleted for Slot {slot + 1}");
        }

        // ─────────────────────────────────────────────
        // Data Collection & Distribution
        // ─────────────────────────────────────────────

        private void CollectSaveData()
        {
            // Region progress from GameManager
            if (GameManager.Instance != null)
            {
                var regionProgress = GameManager.Instance.GetRegionProgressForSave();
                CurrentSave.luzonStatus = (int)regionProgress.GetValueOrDefault(Region.Luzon, RegionStatus.Unlocked);
                CurrentSave.visayasStatus = (int)regionProgress.GetValueOrDefault(Region.Visayas, RegionStatus.Locked);
                CurrentSave.mindanaoStatus = (int)regionProgress.GetValueOrDefault(Region.Mindanao, RegionStatus.Locked);

                if (GameManager.Instance.CurrentRegion.HasValue)
                {
                    CurrentSave.lastRegion = (int)GameManager.Instance.CurrentRegion.Value;
                }
            }

            // DDA metrics are updated by DDAManager directly on CurrentSave
            // Inventory is updated by InventoryManager directly on CurrentSave
        }

        private void DistributeSaveData()
        {
            // Restore region progress to GameManager
            if (GameManager.Instance != null)
            {
                var regionProgress = new Dictionary<Region, RegionStatus>
                {
                    { Region.Luzon, (RegionStatus)CurrentSave.luzonStatus },
                    { Region.Visayas, (RegionStatus)CurrentSave.visayasStatus },
                    { Region.Mindanao, (RegionStatus)CurrentSave.mindanaoStatus }
                };
                GameManager.Instance.RestoreRegionProgress(regionProgress);
            }

            // DDA and Inventory managers will read from CurrentSave on their own init
        }

        private bool TryLoadBackup()
        {
            string backupPath = GetBackupPath(CurrentSlot);

            if (!File.Exists(backupPath))
            {
                Debug.LogError("[SaveManager] No backup found either. Starting fresh.");
                CurrentSave = new SaveData();
                return false;
            }

            try
            {
                string json = File.ReadAllText(backupPath);
                CurrentSave = JsonUtility.FromJson<SaveData>(json);
                DistributeSaveData();
                Debug.Log("[SaveManager] Backup loaded successfully");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Backup load also failed: {e.Message}. Starting fresh.");
                CurrentSave = new SaveData();
                return false;
            }
        }

        // ─────────────────────────────────────────────
        // Auto-Save Event Handlers
        // ─────────────────────────────────────────────

        private void OnItemCollected(ItemCollectedEvent evt)
        {
            // Auto-save on inventory change
            SaveGame();
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            // Auto-save on scene transition
            SaveGame();
        }
    }

    // ─────────────────────────────────────────────
    // Save Data Structure
    // ─────────────────────────────────────────────

    /// <summary>
    /// Serializable save data structure. Uses primitive types for JsonUtility compatibility.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // Metadata
        public string lastSaveTimestamp;
        public int lastRegion = -1; // -1 = none

        // Region Progress (stored as int for JsonUtility)
        public int luzonStatus = (int)RegionStatus.Unlocked;
        public int visayasStatus = (int)RegionStatus.Locked;
        public int mindanaoStatus = (int)RegionStatus.Locked;

        // Inventory (item IDs)
        public List<string> collectedRelicIds = new List<string>();
        public List<string> collectedToolIds = new List<string>();
        public List<string> collectedQuestItemIds = new List<string>();

        // Zone completion (RegionProgressTracker)
        public List<string> completedZoneIds = new List<string>();

        // DDA Metrics
        public float difficultyLevel = GameConstants.DDA.DEFAULT_DIFFICULTY;
        public float taskQuantityMultiplier = GameConstants.DDA.DEFAULT_TASK_MULTIPLIER;
        public float timerStrictness = GameConstants.DDA.DEFAULT_TIMER_STRICTNESS;
        public int hintBudget = GameConstants.DDA.DEFAULT_HINT_BUDGET;
        public int puzzleComplexityTier = 1;

        // Cumulative DDA tracking
        public float cumulativeHesitationTime;
        public int cumulativeErrors;
        public int challengesCompleted;

        // Per-region challenge completion tracking
        public List<string> completedChallengeIds = new List<string>();
    }
}
