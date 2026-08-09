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
        public bool HasSaveFile => File.Exists(GetSavePath());

        private string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.Save.SAVE_FILE_NAME);
        }

        private string GetBackupPath()
        {
            return Path.Combine(Application.persistentDataPath, GameConstants.Save.SAVE_BACKUP_NAME);
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
                Debug.Log($"[SaveManager] Save path: {GetSavePath()}");
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
                string savePath = GetSavePath();
                string backupPath = GetBackupPath();

                if (File.Exists(savePath))
                {
                    File.Copy(savePath, backupPath, true);
                }

                File.WriteAllText(savePath, json);

                if (_logSaveOperations)
                {
                    Debug.Log($"[SaveManager] Game saved successfully");
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
            string savePath = GetSavePath();

            if (!File.Exists(savePath))
            {
                if (_logSaveOperations)
                {
                    Debug.Log("[SaveManager] No save file found, starting fresh");
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
                Debug.LogError($"[SaveManager] Load failed: {e.Message}. Trying backup...");
                return TryLoadBackup();
            }
        }

        /// <summary>
        /// Delete save file and start fresh.
        /// </summary>
        public void DeleteSave()
        {
            string savePath = GetSavePath();
            string backupPath = GetBackupPath();

            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(backupPath)) File.Delete(backupPath);

            CurrentSave = new SaveData();
            Debug.Log("[SaveManager] Save data deleted");
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
            string backupPath = GetBackupPath();

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
