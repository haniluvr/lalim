using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Central game manager — singleton that persists across all scenes.
    /// Manages game state machine and region unlock progression.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        [Header("Current State")]
        [SerializeField] private GameState _currentState = GameState.Menu; // Start as Menu so Boot triggers a change
        public GameState CurrentState => _currentState;

        // Region progression: linear unlock (Luzon → Visayas → Mindanao)
        private Dictionary<Region, RegionStatus> _regionStatus = new Dictionary<Region, RegionStatus>()
        {
            { Region.Luzon, RegionStatus.Unlocked },
            { Region.Visayas, RegionStatus.Locked },
            { Region.Mindanao, RegionStatus.Locked }
        };

        public Region? CurrentRegion { get; private set; }

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

            Debug.Log("[GameManager] Initialized");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Subscribe<RegionCompletedEvent>(OnRegionCompleted);
            EventBus.Subscribe<PauseRequestedEvent>(OnPauseRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<RegionCompletedEvent>(OnRegionCompleted);
            EventBus.Unsubscribe<PauseRequestedEvent>(OnPauseRequested);
        }

        private void Start()
        {
            // Boot sequence: load save data
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.LoadGame();
            }

            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Only redirect to Main Menu if starting from the Boot scene
            if (activeScene == GameConstants.Scenes.BOOT)
            {
                // Do nothing here! 
                // BootSequenceUI will handle fading the logos and THEN it will load the Main Menu.
                SetState(GameState.Boot);
            }
            else if (activeScene == GameConstants.Scenes.MAIN_MENU)
            {
                SetState(GameState.Menu);
            }
            else
            {
                // In Editor / directly testing a gameplay region (e.g. 02_Luzon)
                CurrentRegion = GameConstants.Scenes.GetRegionForScene(activeScene) ?? Region.Luzon;
                SetState(GameState.Exploring);
            }
        }

        // ─────────────────────────────────────────────
        // State Machine
        // ─────────────────────────────────────────────

        public void SetState(GameState newState)
        {
            if (_currentState == newState) return;

            GameState previous = _currentState;
            _currentState = newState;

            Debug.Log($"[GameManager] State: {previous} → {newState}");

            EventBus.Publish(new GameStateChangedEvent
            {
                PreviousState = previous,
                NewState = newState
            });

            // Handle state-specific logic
            switch (newState)
            {
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;

                case GameState.Boot:
                case GameState.Exploring:
                case GameState.InChallenge:
                case GameState.Menu:
                    Time.timeScale = 1f;
                    break;
            }
        }

        // ─────────────────────────────────────────────
        // Region Progression
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the unlock status of a region.
        /// </summary>
        public RegionStatus GetRegionStatus(Region region)
        {
            return _regionStatus.ContainsKey(region) ? _regionStatus[region] : RegionStatus.Locked;
        }

        /// <summary>
        /// Check if a region is accessible (unlocked or completed).
        /// </summary>
        public bool IsRegionAccessible(Region region)
        {
            RegionStatus status = GetRegionStatus(region);
            return status == RegionStatus.Unlocked || status == RegionStatus.Completed;
        }

        /// <summary>
        /// Try to enter a region. Returns false if region is locked.
        /// </summary>
        public bool TryEnterRegion(Region region)
        {
            if (!IsRegionAccessible(region))
            {
                Debug.LogWarning($"[GameManager] Region {region} is locked!");
                return false;
            }

            CurrentRegion = region;
            string sceneName = GameConstants.Scenes.GetSceneForRegion(region);

            EventBus.Publish(new SceneLoadRequestedEvent
            {
                SceneName = sceneName,
                ShowLoadingScreen = true
            });

            SetState(GameState.Exploring);
            return true;
        }

        /// <summary>
        /// Unlock the next region in sequence after completing one.
        /// </summary>
        private void UnlockNextRegion(Region completedRegion)
        {
            _regionStatus[completedRegion] = RegionStatus.Completed;

            Region? next = GetNextRegion(completedRegion);
            if (next.HasValue && _regionStatus[next.Value] == RegionStatus.Locked)
            {
                _regionStatus[next.Value] = RegionStatus.Unlocked;
                Debug.Log($"[GameManager] Region unlocked: {next.Value}");

                EventBus.Publish(new RegionUnlockedEvent
                {
                    UnlockedRegion = next.Value
                });
            }
        }

        private Region? GetNextRegion(Region current)
        {
            switch (current)
            {
                case Region.Luzon: return Region.Visayas;
                case Region.Visayas: return Region.Mindanao;
                default: return null;
            }
        }

        /// <summary>
        /// Return to the main menu.
        /// </summary>
        public void ReturnToMenu()
        {
            CurrentRegion = null;
            SetState(GameState.Menu);

            EventBus.Publish(new SceneLoadRequestedEvent
            {
                SceneName = GameConstants.Scenes.MAIN_MENU,
                ShowLoadingScreen = true
            });
        }

        // ─────────────────────────────────────────────
        // Save / Load Support
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get region status dictionary for save serialization.
        /// </summary>
        public Dictionary<Region, RegionStatus> GetRegionProgressForSave()
        {
            return new Dictionary<Region, RegionStatus>(_regionStatus);
        }

        /// <summary>
        /// Restore region status from save data.
        /// </summary>
        public void RestoreRegionProgress(Dictionary<Region, RegionStatus> savedProgress)
        {
            if (savedProgress != null)
            {
                _regionStatus = new Dictionary<Region, RegionStatus>(savedProgress);
                Debug.Log("[GameManager] Region progress restored from save");
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            SetState(GameState.InChallenge);
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            SetState(GameState.Exploring);

            // Auto-save after challenge completion
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame();
            }
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            SetState(GameState.Exploring);
        }

        private void OnRegionCompleted(RegionCompletedEvent evt)
        {
            UnlockNextRegion(evt.CompletedRegion);

            // Auto-save after region completion
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame();
            }
        }

        private void OnPauseRequested(PauseRequestedEvent evt)
        {
            if (evt.IsPaused && _currentState != GameState.Paused)
            {
                SetState(GameState.Paused);
            }
            else if (!evt.IsPaused && _currentState == GameState.Paused)
            {
                // Return to whichever state we were in before pausing
                SetState(CurrentRegion.HasValue ? GameState.Exploring : GameState.Menu);
            }
        }
    }

    /// <summary>
    /// Region progression status.
    /// </summary>
    public enum RegionStatus
    {
        Locked,
        Unlocked,
        Completed
    }
}
