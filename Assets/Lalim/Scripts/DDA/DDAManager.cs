using UnityEngine;
using Lalim.Core;

namespace Lalim.DDA
{
    /// <summary>
    /// Core DDA engine. Tracks difficulty level (0.0–1.0), processes performance
    /// metrics after each challenge, and publishes adjusted difficulty parameters.
    /// 
    /// Processing is a local heuristic (no ML):
    /// 1. Receive metrics from ChallengeCompletedEvent
    /// 2. Calculate performance score (weighted combination of hesitation, errors, completion time)
    /// 3. Compare against thresholds (too easy / struggling)
    /// 4. Adjust difficulty with exponential moving average for smoothing
    /// 5. Publish DifficultyChangedEvent with new parameters
    /// </summary>
    public class DDAManager : MonoBehaviour
    {
        public static DDAManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private DDAConfig _config;

        // ─────────────────────────────────────────────
        // Current State
        // ─────────────────────────────────────────────

        public float DifficultyLevel { get; private set; }
        public float TaskQuantityMultiplier { get; private set; }
        public float TimerStrictness { get; private set; }
        public int HintBudget { get; private set; }
        public int PuzzleComplexityTier { get; private set; }

        // Cumulative metrics (for save/load)
        private float _cumulativeHesitation;
        private int _cumulativeErrors;
        private int _challengesCompleted;

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

            InitializeDefaults();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        }

        private void Start()
        {
            // Restore from save if available
            if (SaveManager.Instance != null && SaveManager.Instance.HasSaveFile)
            {
                RestoreFromSave(SaveManager.Instance.CurrentSave);
            }
        }

        // ─────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────

        private void InitializeDefaults()
        {
            if (_config != null)
            {
                DifficultyLevel = _config.startingDifficulty;
            }
            else
            {
                DifficultyLevel = GameConstants.DDA.DEFAULT_DIFFICULTY;
            }

            UpdateDerivedValues();
        }

        // ─────────────────────────────────────────────
        // Core DDA Processing
        // ─────────────────────────────────────────────

        /// <summary>
        /// Process performance metrics and adjust difficulty.
        /// </summary>
        private void ProcessChallengeMetrics(ChallengeCompletedEvent evt)
        {
            if (_config == null)
            {
                Debug.LogWarning("[DDAManager] No DDAConfig assigned, skipping adjustment");
                return;
            }

            // Update cumulative tracking
            _cumulativeHesitation += evt.HesitationTime;
            _cumulativeErrors += evt.ErrorCount;
            _challengesCompleted++;

            // Step 1: Normalize each metric to a 0-1 "performance" score
            // Higher score = player did well (less hesitation, fewer errors, faster)
            float hesitationScore = CalculateHesitationScore(evt.HesitationTime, evt.ExpectedTime);
            float errorScore = CalculateErrorScore(evt.ErrorCount);
            float timeScore = CalculateTimeScore(evt.CompletionTime, evt.ExpectedTime);

            // Step 2: Weighted combination
            float totalWeight = _config.hesitationWeight + _config.errorWeight + _config.completionTimeWeight;
            float performanceScore = (
                hesitationScore * _config.hesitationWeight +
                errorScore * _config.errorWeight +
                timeScore * _config.completionTimeWeight
            ) / totalWeight;

            Debug.Log($"[DDA] Performance: {performanceScore:F2} " +
                      $"(hesitation={hesitationScore:F2}, errors={errorScore:F2}, time={timeScore:F2})");

            // Step 3: Determine adjustment direction
            float adjustment = 0f;

            if (performanceScore > _config.tooEasyThreshold)
            {
                // Player is doing well — increase difficulty
                adjustment = _config.difficultyStepSize;
                Debug.Log("[DDA] Player excelling → increasing difficulty");
            }
            else if (performanceScore < _config.strugglingThreshold)
            {
                // Player is struggling — decrease difficulty
                adjustment = -_config.difficultyStepSize;
                Debug.Log("[DDA] Player struggling → decreasing difficulty");
            }
            else
            {
                Debug.Log("[DDA] Player in sweet spot → no adjustment");
            }

            // Step 4: Apply with exponential moving average smoothing
            if (Mathf.Abs(adjustment) > 0.001f)
            {
                float targetDifficulty = DifficultyLevel + adjustment;
                DifficultyLevel = Mathf.Lerp(targetDifficulty, DifficultyLevel, _config.smoothingFactor);
                DifficultyLevel = Mathf.Clamp(DifficultyLevel, _config.minDifficulty, _config.maxDifficulty);

                Debug.Log($"[DDA] Difficulty adjusted to: {DifficultyLevel:F3}");
            }

            // Step 5: Update derived values and publish
            UpdateDerivedValues();
            PublishDifficultyChanged();

            // Update save data
            UpdateSaveData();
        }

        // ─────────────────────────────────────────────
        // Metric Scoring
        // ─────────────────────────────────────────────

        /// <summary>
        /// Score hesitation: less hesitation = higher score (player was decisive).
        /// </summary>
        private float CalculateHesitationScore(float hesitationTime, float expectedTime)
        {
            if (expectedTime <= 0) return 0.5f;

            // Hesitation as fraction of expected time
            float hesitationRatio = hesitationTime / expectedTime;

            // Invert: 0 hesitation = score 1.0, hesitation >= expected time = score 0.0
            return Mathf.Clamp01(1f - hesitationRatio);
        }

        /// <summary>
        /// Score errors: fewer errors = higher score.
        /// </summary>
        private float CalculateErrorScore(int errorCount)
        {
            // Diminishing returns: 0 errors = 1.0, 1 = 0.7, 2 = 0.5, 5+ = ~0.1
            return Mathf.Clamp01(1f / (1f + errorCount * 0.5f));
        }

        /// <summary>
        /// Score completion time: faster than expected = higher score.
        /// </summary>
        private float CalculateTimeScore(float completionTime, float expectedTime)
        {
            if (expectedTime <= 0) return 0.5f;

            float ratio = completionTime / expectedTime;

            // ratio < 1 = faster than expected (good), ratio > 1 = slower (bad)
            // Map: 0.5x speed = 1.0, 1.0x = 0.5, 2.0x = 0.0
            return Mathf.Clamp01(1f - (ratio - 0.5f));
        }

        // ─────────────────────────────────────────────
        // Derived Values
        // ─────────────────────────────────────────────

        private void UpdateDerivedValues()
        {
            if (_config != null)
            {
                TaskQuantityMultiplier = _config.GetTaskMultiplier(DifficultyLevel);
                TimerStrictness = DifficultyLevel; // Direct mapping
                HintBudget = _config.GetHintBudget(DifficultyLevel);
                PuzzleComplexityTier = _config.GetComplexityTier(DifficultyLevel);
            }
            else
            {
                TaskQuantityMultiplier = GameConstants.DDA.DEFAULT_TASK_MULTIPLIER;
                TimerStrictness = GameConstants.DDA.DEFAULT_TIMER_STRICTNESS;
                HintBudget = GameConstants.DDA.DEFAULT_HINT_BUDGET;
                PuzzleComplexityTier = 1;
            }
        }

        private void PublishDifficultyChanged()
        {
            EventBus.Publish(new DifficultyChangedEvent
            {
                DifficultyLevel = DifficultyLevel,
                TaskQuantityMultiplier = TaskQuantityMultiplier,
                TimerStrictness = TimerStrictness,
                HintBudget = HintBudget,
                PuzzleComplexityTier = PuzzleComplexityTier
            });
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the adjusted task quantity for a challenge.
        /// Example: base 10 trash × 1.3 multiplier = 13 trash.
        /// </summary>
        public int GetAdjustedTaskQuantity(int baseQuantity)
        {
            return Mathf.RoundToInt(baseQuantity * TaskQuantityMultiplier);
        }

        /// <summary>
        /// Get the adjusted time limit for a challenge.
        /// </summary>
        public float GetAdjustedTimeLimit(float baseTimeLimit)
        {
            if (_config != null)
            {
                return baseTimeLimit * _config.GetTimerMultiplier(DifficultyLevel);
            }
            return baseTimeLimit;
        }

        /// <summary>
        /// Manually set difficulty (e.g., from settings menu).
        /// </summary>
        public void SetDifficulty(float level)
        {
            DifficultyLevel = Mathf.Clamp01(level);
            UpdateDerivedValues();
            PublishDifficultyChanged();
        }

        // ─────────────────────────────────────────────
        // Save / Load
        // ─────────────────────────────────────────────

        private void UpdateSaveData()
        {
            if (SaveManager.Instance == null) return;

            var save = SaveManager.Instance.CurrentSave;
            save.difficultyLevel = DifficultyLevel;
            save.taskQuantityMultiplier = TaskQuantityMultiplier;
            save.timerStrictness = TimerStrictness;
            save.hintBudget = HintBudget;
            save.puzzleComplexityTier = PuzzleComplexityTier;
            save.cumulativeHesitationTime = _cumulativeHesitation;
            save.cumulativeErrors = _cumulativeErrors;
            save.challengesCompleted = _challengesCompleted;
        }

        private void RestoreFromSave(SaveData save)
        {
            DifficultyLevel = save.difficultyLevel;
            _cumulativeHesitation = save.cumulativeHesitationTime;
            _cumulativeErrors = save.cumulativeErrors;
            _challengesCompleted = save.challengesCompleted;

            UpdateDerivedValues();
            Debug.Log($"[DDAManager] Restored from save: difficulty={DifficultyLevel:F3}");
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.Success)
            {
                ProcessChallengeMetrics(evt);
            }
        }
    }
}
