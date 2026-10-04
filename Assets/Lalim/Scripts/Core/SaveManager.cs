using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// JSON-based local save system. Persists player progress, inventory,
    /// DDA metrics, and region unlock state to Application.persistentDataPath.
    ///
    /// Session rules (used by the pause menu):
    ///  - A LOADED game is bound to the slot it came from: Save writes straight there.
    ///  - A NEW game isn't bound to a slot until the player saves once (they pick the slot);
    ///    after that it behaves like a loaded game.
    ///  - Auto-saves (item pickups, scene loads, challenge/region completion) only happen once
    ///    the session is bound, so a new game never silently claims a slot.
    ///  - HasUnsavedProgress is set by progress events and cleared by any save/load, so the
    ///    pause menu can warn before returning to the main menu.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public enum SessionOrigin { Unknown, NewGame, LoadedGame }

        /// <summary>How this play session started.</summary>
        public SessionOrigin Origin { get; private set; } = SessionOrigin.Unknown;

        /// <summary>True once the player has saved this session (e.g. a new game's first save).</summary>
        public bool HasSavedThisSession { get; private set; }

        /// <summary>The session has a slot to save to without asking (loaded game, or saved once already).</summary>
        public bool IsSlotBound => Origin == SessionOrigin.LoadedGame || HasSavedThisSession;

        /// <summary>Progress happened since the last save (or a new game has never been saved).</summary>
        public bool HasUnsavedProgress { get; private set; }

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

        /// <summary>Starts a new game. Nothing is written until the player saves (and picks a slot);
        /// 'preferredSlot' is only the default highlighted in the slot picker.</summary>
        public void BeginNewGame(int preferredSlot)
        {
            SetCurrentSlot(preferredSlot);
            CurrentSave = new SaveData();
            Origin = SessionOrigin.NewGame;
            HasSavedThisSession = false;
            HasUnsavedProgress = true;
            // Boot loaded the last save into the persistent systems; start them fresh too.
            DistributeSaveData();
        }

        /// <summary>Manual save into a specific slot (pause menu). Binds the session to that slot.</summary>
        public bool SaveToSlot(int slot)
        {
            if (slot < 0 || slot >= MAX_SLOTS) return false;
            SetCurrentSlot(slot);
            bool ok = WriteCurrentSlot();
            if (ok) HasSavedThisSession = true;
            return ok;
        }

        /// <summary>Auto-save hook: only writes when the session already has a slot.</summary>
        public void AutoSave()
        {
            if (IsSlotBound) WriteCurrentSlot();
        }

        /// <summary>Reads a slot's data for display (slot picker) without loading it.</summary>
        public SaveData PeekSlot(int slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch { return null; }
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
            EventBus.Subscribe<ZoneCompletedEvent>(OnProgress);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnProgress);
            EventBus.Subscribe<CheckpointReachedEvent>(OnProgress);
            EventBus.Subscribe<RegionCompletedEvent>(OnProgress);
            EventBus.Subscribe<RegionUnlockedEvent>(OnProgress);
            EventBus.Subscribe<CodexEntryUnlockedEvent>(OnProgress);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ItemCollectedEvent>(OnItemCollected);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
            EventBus.Unsubscribe<ZoneCompletedEvent>(OnProgress);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnProgress);
            EventBus.Unsubscribe<CheckpointReachedEvent>(OnProgress);
            EventBus.Unsubscribe<RegionCompletedEvent>(OnProgress);
            EventBus.Unsubscribe<RegionUnlockedEvent>(OnProgress);
            EventBus.Unsubscribe<CodexEntryUnlockedEvent>(OnProgress);
        }

        // Progress events now write immediately (when the session has a slot), so quitting
        // the app never loses a finished step or a reached checkpoint.
        private void OnProgress<T>(T evt) where T : struct
        {
            HasUnsavedProgress = true;
            AutoSave();
        }

        /// <summary>
        /// New games auto-save from the first level checkpoint: binds the session to the preferred
        /// slot (the one highlighted when the new game started). The pause menu can still save
        /// elsewhere. Sessions started without the menu (Unknown origin, e.g. editor testing) are
        /// never bound automatically, so they can't overwrite a real save.
        /// </summary>
        public void EnsureNewGameSlotBound()
        {
            if (IsSlotBound || Origin != SessionOrigin.NewGame) return;
            SaveToSlot(CurrentSlot);
        }

        // Mobile: swiping the app away kills it without OnApplicationQuit, so pause/focus-loss
        // is the save that actually lands.
        private void OnApplicationPause(bool paused) { if (paused) AutoSave(); }
        private void OnApplicationFocus(bool focused) { if (!focused) AutoSave(); }
        private void OnApplicationQuit() => AutoSave();

        // ─────────────────────────────────────────────
        // Save
        // ─────────────────────────────────────────────

        /// <summary>
        /// Save current game state to the current slot. Kept for existing callers; binds the session.
        /// Prefer AutoSave() for automatic saves and SaveToSlot() for the pause menu.
        /// </summary>
        public void SaveGame()
        {
            if (WriteCurrentSlot()) HasSavedThisSession = true;
        }

        private bool WriteCurrentSlot()
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
                HasUnsavedProgress = false;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
                return false;
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
                CurrentSave.MigrateSceneNames();

                // Distribute save data to managers
                DistributeSaveData();
                MarkLoaded();

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
            Lalim.Abilities.PlayerVitals.Capture(CurrentSave);

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

            // Inventory, zone progress and DDA reload from CurrentSave.
            EventBus.Publish(new SaveDataReplacedEvent());
        }

        private void MarkLoaded()
        {
            Origin = SessionOrigin.LoadedGame;
            HasSavedThisSession = false;
            HasUnsavedProgress = false;
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
                CurrentSave.MigrateSceneNames();
                DistributeSaveData();
                MarkLoaded();
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
            HasUnsavedProgress = true;
            // Auto-save on inventory change (only once the session has a slot)
            AutoSave();
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            // Auto-save on scene transition (only once the session has a slot)
            AutoSave();
        }
    }

    // ─────────────────────────────────────────────
    // Save Data Structure
    // ─────────────────────────────────────────────

    /// <summary>
    /// Serializable save data structure. Uses primitive types for JsonUtility compatibility.
    /// </summary>
    [Serializable]
    public class SavedVitals
    {
        public bool valid;           // false = nothing captured (respawn values apply)
        public string scene;         // only restored when resuming in this scene
        public float playerHp;
        public float stamina;
        public float dugongHp = -1f;
        public bool dugongExhausted;
        public List<SavedAbility> abilities = new List<SavedAbility>();
    }

    [Serializable]
    public class SavedAbility
    {
        public int id;               // AbilityId
        public float cooldown;       // seconds left
        public float charge = -1f;   // charge-based skills only
    }

    [Serializable]
    public class SaveData
    {
        // Metadata
        public string playerName = "Player";
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
        public List<float> minigameScores = new List<float>();   // every scored minigame (DDA history)

        // Per-region challenge completion tracking. Also holds level-flow quest step IDs
        // (e.g. "luzon_z2_water", "luzon_z2_water_a") so a reload resumes at the right step.
        public List<string> completedChallengeIds = new List<string>();

        // Resume point (checkpoints let a player who quits come back where they were)
        public string lastSceneName;
        public string lastCheckpointId;

        // Level 1 in-progress state
        public List<string> selectedTrashIds = new List<string>();   // trash chosen for this run (DDA count)
        public List<string> collectedTrashIds = new List<string>();  // trash already picked up
        public float levelToxicDps = -1f;                             // drain snapshot for the current level (-1 = not set)
        public string levelToxicScene;                                // scene the snapshot belongs to

        /// <summary>
        /// Level 1 was built as "02_LuzonNew" and renamed to "02_Luzon" (the old 02_Luzon became
        /// 01_Tutorial), so saves that remember the old name still resume in Level 1.
        /// </summary>
        public void MigrateSceneNames()
        {
            const string oldLevel1 = "02_LuzonNew";
            if (lastSceneName == oldLevel1) lastSceneName = GameConstants.Scenes.LUZON;
            if (levelToxicScene == oldLevel1) levelToxicScene = GameConstants.Scenes.LUZON;
        }
        public string ghostNetBagState;                               // species shuffle bag (ghost nets)

        // HP, stamina, dugong health and skill cooldowns when the game was saved, so resuming
        // picks up exactly where the player left off (PlayerVitals).
        public SavedVitals vitals = new SavedVitals();

        // Achievements and one-time tutorial tips
        public List<string> unlockedAchievementIds = new List<string>();
        public List<string> achievementProgress = new List<string>();   // achievement progress flags (e.g. l1_steady_hands:broken)
        public List<string> tutorialFlags = new List<string>();

        public bool HasChallengeId(string id) => completedChallengeIds != null && completedChallengeIds.Contains(id);

        public void AddChallengeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (completedChallengeIds == null) completedChallengeIds = new List<string>();
            if (!completedChallengeIds.Contains(id)) completedChallengeIds.Add(id);
        }

        // Deaths per exploration section, so the DDA death-streak rule survives quit/reload.
        // A list (not a Dictionary) because JsonUtility can't serialize dictionaries.
        public List<SectionDeathCount> sectionDeathCounts = new List<SectionDeathCount>();

        public int GetSectionDeaths(string sectionId)
        {
            foreach (var entry in sectionDeathCounts) if (entry.sectionId == sectionId) return entry.deaths;
            return 0;
        }

        public void SetSectionDeaths(string sectionId, int deaths)
        {
            foreach (var entry in sectionDeathCounts)
            {
                if (entry.sectionId == sectionId) { entry.deaths = deaths; return; }
            }
            sectionDeathCounts.Add(new SectionDeathCount { sectionId = sectionId, deaths = deaths });
        }
    }

    [System.Serializable]
    public class SectionDeathCount
    {
        public string sectionId;
        public int deaths;
    }
}
