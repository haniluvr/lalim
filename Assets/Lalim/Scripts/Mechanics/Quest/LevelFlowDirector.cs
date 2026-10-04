using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.DDA;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Runs a level's zones and their quest steps in order, and owns the level-wide rules:
    ///  - resume: finished steps (saved IDs) are restored instantly, the first unfinished one begins,
    ///    and the player is placed at the last checkpoint;
    ///  - toxic drain: the level rate is set once on load (difficulty snapshot) and paused during
    ///    dialogue and first-person minigames;
    ///  - music: the zone the player is in picks the track (polluted / cleansed).
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelFlowDirector : MonoBehaviour
    {
        public static LevelFlowDirector Instance { get; private set; }

        [Header("Level")]
        [SerializeField] private Region _region = Region.Luzon;
        [SerializeField] private string _levelDisplayName = "Level 1";
        [SerializeField] private List<ZoneQuest> _zones = new List<ZoneQuest>();

        [Header("Toxic drain (set once per level)")]
        [Tooltip("HP per second at Normal difficulty.")]
        [SerializeField, Min(0f)] private float _baseToxicDps = 0.5f;
        [Tooltip("Level 1: no performance history yet, so the drain is exactly the base rate.")]
        [SerializeField] private bool _pinDrainToBase = true;
        [SerializeField, Min(0f)] private float _drainMultiplierAtEasiest = 0.6f;
        [SerializeField, Min(0f)] private float _drainMultiplierAtHardest = 1.6f;

        [Header("Music")]
        [SerializeField, Min(0.1f)] private float _zoneCheckInterval = 0.5f;

        private readonly List<QuestStep> _allSteps = new List<QuestStep>();
        private int _currentIndex = -1;
        private float _nextZoneCheck;
        private ZoneQuest _musicZone;

        public QuestStep CurrentStep => _currentIndex >= 0 && _currentIndex < _allSteps.Count ? _allSteps[_currentIndex] : null;
        public ZoneQuest CurrentZone => CurrentStep != null ? CurrentStep.Zone : null;
        public IReadOnlyList<ZoneQuest> Zones => _zones;
        public Region Region => _region;
        public string LevelDisplayName => _levelDisplayName;

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            foreach (ZoneQuest zone in _zones)
            {
                if (zone == null) continue;
                foreach (QuestStep step in zone.Steps)
                {
                    if (step == null) continue;
                    step.Zone = zone;
                    _allSteps.Add(step);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            LevelToxicity.ReleaseAll();
            LevelToxicity.ClearLevelDps();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeEnded);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeEnded);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private IEnumerator Start()
        {
            // Let every singleton (save, DDA, inventory, progress) finish its own Start first.
            yield return null;

            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Exploring)
            {
                GameManager.Instance.SetState(GameState.Exploring);
            }

            SetupToxicity();

            bool resumed = PlaceAtSavedCheckpoint();

            // Replay: finished steps restore instantly, the first unfinished one begins.
            // A step added in an update before the save's progress (e.g. a relic gift in a zone
            // already cleansed) is caught up too, instead of sending the player back to it.
            int first = ResumeIndex(_allSteps.ConvertAll(IsDone));
            for (int i = 0; i < first; i++)
            {
                QuestStep step = _allSteps[i];
                if (!IsDone(step) && Save != null && !string.IsNullOrEmpty(step.StepId))
                {
                    Debug.Log($"[LevelFlow] Catching up step {step.StepId} (added after this save was made).");
                    Save.AddChallengeId(step.StepId);
                }
                step.RestoreCompleted();
            }

            RunStep(first);
        }

        // ───────────────────────── Steps ─────────────────────────

        /// <summary>The step to resume at: the one after the last finished step (earlier gaps are caught up).</summary>
        public static int ResumeIndex(IList<bool> done)
        {
            int first = 0;
            for (int i = 0; i < done.Count; i++) if (done[i]) first = i + 1;
            return first;
        }

        private static bool IsDone(QuestStep step) =>
            Save != null && !string.IsNullOrEmpty(step.StepId) && Save.HasChallengeId(step.StepId);

        private void RunStep(int index)
        {
            _currentIndex = index;
            if (index >= _allSteps.Count)
            {
                Debug.Log($"[LevelFlow] {_levelDisplayName}: all steps finished.");
                return;
            }

            QuestStep step = _allSteps[index];
            Debug.Log($"[LevelFlow] Begin step {index + 1}/{_allSteps.Count}: {step.StepId} ({step.GetType().Name})");
            step.Begin(() => OnStepCompleted(step));
        }

        private void OnStepCompleted(QuestStep step)
        {
            if (Save != null) Save.AddChallengeId(step.StepId);

            EventBus.Publish(new QuestStepCompletedEvent
            {
                StepId = step.StepId,
                ZoneId = step.Zone != null ? step.Zone.ZoneId : ""
            });
            SaveManager.Instance?.AutoSave();

            int next = _allSteps.IndexOf(step) + 1;
            RunStep(next);
        }

        /// <summary>Debug helper: completes the current step (F-key cheats, tests).</summary>
        public void DebugSkipCurrentStep()
        {
            QuestStep step = CurrentStep;
            if (step == null) return;
            Debug.LogWarning($"[LevelFlow] Skipping step {step.StepId}");
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive)
            {
                ChallengeManager.Instance.CompleteChallenge(true);
            }
            step.SendMessage("DebugForceComplete", SendMessageOptions.DontRequireReceiver);
        }

        // ───────────────────────── Resume ─────────────────────────

        private bool PlaceAtSavedCheckpoint()
        {
            SaveData save = Save;
            if (save == null || string.IsNullOrEmpty(save.lastCheckpointId)) return false;
            if (save.lastSceneName != gameObject.scene.name) return false;
            return CheckpointManager.Instance != null && CheckpointManager.Instance.PlaceAtCheckpoint(save.lastCheckpointId);
        }

        // ───────────────────────── Toxic drain ─────────────────────────

        private void SetupToxicity()
        {
            SaveData save = Save;
            string scene = gameObject.scene.name;
            float dps;

            // One snapshot per level, kept across reloads so the drain never changes mid-level.
            if (save != null && save.levelToxicDps >= 0f && save.levelToxicScene == scene)
            {
                dps = save.levelToxicDps;
            }
            else
            {
                float difficulty = DDAManager.Instance != null ? DDAManager.Instance.DifficultyLevel : GameConstants.DDA.DEFAULT_DIFFICULTY;
                dps = LevelToxicity.ComputeLevelDps(_baseToxicDps, difficulty, _drainMultiplierAtEasiest, _drainMultiplierAtHardest, _pinDrainToBase);
                if (save != null)
                {
                    save.levelToxicDps = dps;
                    save.levelToxicScene = scene;
                }
            }

            LevelToxicity.SetLevelDps(dps);
            Debug.Log($"[LevelFlow] Toxic drain for {_levelDisplayName}: {dps:F2} HP/s");
        }

        private void OnDialogueStarted(DialogueStartedEvent evt) => LevelToxicity.Suppress(this + ":dialogue");
        private void OnDialogueEnded(DialogueEndedEvent evt) => LevelToxicity.Release(this + ":dialogue");

        // Minigames don't pause the toxic drain: the water is still dirty until the zone is cleansed
        // (ToxicWaterTracker only stops it from killing the player mid-minigame).
        private void OnChallengeStarted(ChallengeStartedEvent evt) { }

        private void OnChallengeEnded(ChallengeCompletedEvent evt) => LevelToxicity.Release(this + ":minigame");
        private void OnChallengeFailed(ChallengeFailedEvent evt) => LevelToxicity.Release(this + ":minigame");

        // ───────────────────────── Music ─────────────────────────

        private void Update()
        {
            if (Time.time < _nextZoneCheck) return;
            _nextZoneCheck = Time.time + _zoneCheckInterval;

            ZoneQuest zone = ZoneAtPlayer();
            if (zone == null) return;
            if (zone != _musicZone || (ProximityMusic.Instance != null && zone.CurrentTrack != null))
            {
                _musicZone = zone;
                if (zone.CurrentTrack != null) ProximityMusic.Instance?.PlayTrack(zone.CurrentTrack);
            }
        }

        public ZoneQuest ZoneAtPlayer()
        {
            if (PlayerController.Instance == null) return null;
            Vector3 p = PlayerController.Instance.transform.position;
            foreach (ZoneQuest z in _zones) if (z != null && z.Contains(p)) return z;
            return null;
        }

#if UNITY_EDITOR
        public void EditorSetup(Region region, string displayName, List<ZoneQuest> zones)
        {
            _region = region;
            _levelDisplayName = displayName;
            _zones = zones;
        }
#endif
    }
}
