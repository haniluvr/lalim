using UnityEngine;
using Lalim.Core;

namespace Lalim.DDA
{
    /// <summary>
    /// Tracks how the player is doing between checkpoints and turns it into DDA signals,
    /// so difficulty responds to exploration (toxic crossings, hazards), not just minigames.
    ///
    /// Per section it tracks deaths, near-deaths (HP dipping under 25%), time-weighted average HP,
    /// and time since the objective last progressed. It only reports at evaluation points:
    ///  - on respawn:              death → score 0.0 at a double step; from the 3rd death in the same
    ///                             section onward, a forced unsmoothed step down every respawn.
    ///  - on reaching a new section: clean section (no deaths, avg HP > 60%) → 0.85;
    ///                             3+ near-deaths → 0.3; otherwise no signal.
    /// At most one adjustment per evaluation.
    /// </summary>
    public class ExplorationMetricsTracker : MonoBehaviour
    {
        public static ExplorationMetricsTracker Instance { get; private set; }

        [SerializeField] private DDAConfig _config;

        public string CurrentSectionId { get; private set; } = "";
        public int SectionDeaths { get; private set; }
        public int SectionNearDeaths { get; private set; }
        public float SectionSeconds { get; private set; }
        public float SecondsSinceObjectiveProgress { get; private set; }
        public float AverageHpFraction => SectionSeconds > 0f ? _hpIntegral / SectionSeconds : _hpFraction;

        private float _hpFraction = 1f;
        private float _hpIntegral;
        private bool _nearDeathArmed = true;

        private DDAConfig Config => _config != null ? _config : (DDAManager.Instance != null ? DDAManager.Instance.Config : null);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Subscribe<CheckpointReachedEvent>(OnCheckpointReached);
            EventBus.Subscribe<ItemCollectedEvent>(OnObjectiveProgress);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnObjectiveProgress);
            EventBus.Subscribe<ZoneCompletedEvent>(OnObjectiveProgress);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Unsubscribe<CheckpointReachedEvent>(OnCheckpointReached);
            EventBus.Unsubscribe<ItemCollectedEvent>(OnObjectiveProgress);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnObjectiveProgress);
            EventBus.Unsubscribe<ZoneCompletedEvent>(OnObjectiveProgress);
        }

        private void Update()
        {
            // Only exploration time counts toward section stats (minigames report separately).
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Exploring) return;

            SectionSeconds += Time.deltaTime;
            SecondsSinceObjectiveProgress += Time.deltaTime;
            _hpIntegral += _hpFraction * Time.deltaTime;
        }

        // ─────────────────────────────────────────────
        // Event handlers
        // ─────────────────────────────────────────────

        private void OnHealthChanged(PlayerHealthChangedEvent evt)
        {
            _hpFraction = evt.Max > 0f ? Mathf.Clamp01(evt.Current / evt.Max) : 0f;

            DDAConfig cfg = Config;
            float nearDeath = cfg != null ? cfg.nearDeathHpFraction : 0.25f;
            float rearm = cfg != null ? cfg.nearDeathRearmHpFraction : 0.4f;

            // Edge-triggered: one dip = one near-death, re-armed once the player recovers.
            if (_nearDeathArmed && _hpFraction > 0f && _hpFraction < nearDeath)
            {
                SectionNearDeaths++;
                _nearDeathArmed = false;
            }
            else if (!_nearDeathArmed && _hpFraction >= rearm)
            {
                _nearDeathArmed = true;
            }
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            SectionDeaths++;
            string section = !string.IsNullOrEmpty(evt.SectionId) ? evt.SectionId : CurrentSectionId;
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null)
            {
                SaveData save = SaveManager.Instance.CurrentSave;
                save.SetSectionDeaths(section, save.GetSectionDeaths(section) + 1);
            }
        }

        private void OnPlayerRespawned(PlayerRespawnedEvent evt)
        {
            // Resuming a save also places the player at a checkpoint; that isn't a death.
            if (evt.IsResume) return;

            DDAConfig cfg = Config;
            DDAManager dda = DDAManager.Instance;
            _hpFraction = 1f;
            _nearDeathArmed = true;
            if (cfg == null || dda == null) return;

            int totalDeathsHere = SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null
                ? SaveManager.Instance.CurrentSave.GetSectionDeaths(CurrentSectionId)
                : SectionDeaths;

            if (totalDeathsHere >= cfg.deathStreakForceThreshold)
            {
                dda.ForceStepDown(cfg.deathStreakStepScale, $"death-streak:{CurrentSectionId} x{totalDeathsHere}");
            }
            else
            {
                dda.ProcessSignal(cfg.deathScore, cfg.deathStepScale, $"death:{CurrentSectionId}");
            }
        }

        private void OnCheckpointReached(CheckpointReachedEvent evt)
        {
            if (evt.SectionId == CurrentSectionId) return; // same section, e.g. a mid-section checkpoint

            EvaluateSection();

            // Getting past the section clears its death streak.
            if (!string.IsNullOrEmpty(CurrentSectionId) && SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null)
            {
                SaveManager.Instance.CurrentSave.SetSectionDeaths(CurrentSectionId, 0);
            }

            CurrentSectionId = evt.SectionId;
            ResetSection();
        }

        private void OnObjectiveProgress<T>(T evt) where T : struct => SecondsSinceObjectiveProgress = 0f;

        // ─────────────────────────────────────────────
        // Evaluation
        // ─────────────────────────────────────────────

        private void EvaluateSection()
        {
            DDAConfig cfg = Config;
            DDAManager dda = DDAManager.Instance;
            if (cfg == null || dda == null || string.IsNullOrEmpty(CurrentSectionId)) return;
            if (SectionSeconds < cfg.minSectionSecondsForEvaluation) return;

            // Deaths were already reported on respawn, so a section with deaths sends nothing more here.
            if (SectionDeaths > 0) return;

            if (SectionNearDeaths >= cfg.nearDeathCountThreshold)
            {
                dda.ProcessSignal(cfg.nearDeathSectionScore, 1f, $"section-near-deaths:{CurrentSectionId} x{SectionNearDeaths}");
            }
            else if (AverageHpFraction > cfg.cleanSectionAvgHpFraction)
            {
                dda.ProcessSignal(cfg.cleanSectionScore, 1f, $"section-clean:{CurrentSectionId} avgHp={AverageHpFraction:P0}");
            }
        }

        private void ResetSection()
        {
            SectionDeaths = 0;
            SectionNearDeaths = 0;
            SectionSeconds = 0f;
            _hpIntegral = 0f;
            _nearDeathArmed = _hpFraction >= (Config != null ? Config.nearDeathRearmHpFraction : 0.4f);
        }
    }
}
