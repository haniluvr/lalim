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

        // Score of every scored minigame so far (saved), for "this and all prior minigames".
        private readonly System.Collections.Generic.List<float> _minigameScores = new System.Collections.Generic.List<float>();
        public System.Collections.Generic.IReadOnlyList<float> MinigameScores => _minigameScores;

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
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Subscribe<SaveDataReplacedEvent>(OnSaveReplaced);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<SaveDataReplacedEvent>(OnSaveReplaced);
        }

        // A save was loaded or a new game started: reload, so nothing carries over from the last one.
        private void OnSaveReplaced(SaveDataReplacedEvent evt)
        {
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null) RestoreFromSave(SaveManager.Instance.CurrentSave);
        }

        private void Start()
        {
            // Restore from save if available
            if (SaveManager.Instance != null && SaveManager.Instance.HasCurrentSave)
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

            // Score this minigame, then blend it with every earlier minigame's score.
            float raw = ScorePerformance(_config, evt, out string breakdown);
            float history = _minigameScores.Count > 0 ? Average(_minigameScores) : raw;
            float blended = BlendWithHistory(raw, _minigameScores, _config.historyWeight);
            _minigameScores.Add(raw);

            Debug.Log($"[DDA] {evt.ChallengeId}: raw={raw:F2} ({breakdown}) history={history:F2} blended={blended:F2}");

            ApplyScore(blended, 1f, smoothed: true, source: $"{evt.ChallengeId} raw={raw:F2} hist={history:F2}");
        }

        /// <summary>
        /// 0..1 performance for one minigame: hesitation, errors, completion time, pickup rate
        /// (time to first correct action) and hint reliance, weighted by the config.
        /// Metrics without a baseline (expected time 0, no first action recorded) are left out.
        /// </summary>
        public static float ScorePerformance(DDAConfig config, ChallengeCompletedEvent evt, out string breakdown)
        {
            float sum = 0f, weights = 0f;
            var parts = new System.Text.StringBuilder();

            void Add(string name, float score, float weight)
            {
                if (weight <= 0f) return;
                sum += score * weight;
                weights += weight;
                parts.Append($"{name}={score:F2} ");
            }

            Add("hesitation", CalculateHesitationScore(evt.HesitationTime, evt.ExpectedTime), config.hesitationWeight);
            Add("errors", CalculateErrorScore(evt.ErrorCount), config.errorWeight);
            if (evt.ExpectedTime > 0f) Add("time", CalculateTimeScore(evt.CompletionTime, evt.ExpectedTime), config.completionTimeWeight);
            if (evt.ExpectedFirstAction > 0f && evt.FirstActionLatency >= 0f)
                Add("pickup", CalculateTimeScore(evt.FirstActionLatency, evt.ExpectedFirstAction), config.firstActionWeight);
            if (evt.HintBudget > 0) Add("hints", CalculateHintScore(evt.HintsUsed, evt.HintBudget), config.hintWeight);

            breakdown = parts.ToString().TrimEnd();
            return weights > 0f ? sum / weights : 0.5f;
        }

        /// <summary>lerp(latest, mean of all earlier scores, historyWeight). With no history the latest stands.</summary>
        public static float BlendWithHistory(float latest, System.Collections.Generic.IReadOnlyList<float> earlier, float historyWeight)
        {
            if (earlier == null || earlier.Count == 0) return latest;
            return Mathf.Lerp(latest, Average(earlier), Mathf.Clamp01(historyWeight));
        }

        private static float Average(System.Collections.Generic.IReadOnlyList<float> values)
        {
            float total = 0f;
            foreach (float v in values) total += v;
            return values.Count > 0 ? total / values.Count : 0f;
        }

        /// <summary>No hints = 1.0; using the whole budget = 0.2.</summary>
        public static float CalculateHintScore(int used, int budget)
        {
            if (budget <= 0) return 1f;
            return Mathf.Lerp(1f, 0.2f, Mathf.Clamp01((float)used / budget));
        }

        /// <summary>
        /// Feeds any performance signal (minigame failure, death, exploration section report) through
        /// the same thresholds and smoothing as minigame results. At most one adjustment per call.
        /// Only call at evaluation points — checkpoints, minigame end, respawn — never mid-action.
        /// </summary>
        /// <param name="score">0 = struggling … 1 = excelling.</param>
        /// <param name="stepScale">Multiplier on the configured step size (2 = double step).</param>
        public void ProcessSignal(float score, float stepScale, string source)
        {
            if (_config == null) return;
            ApplyScore(score, stepScale, smoothed: true, source: source);
        }

        /// <summary>Guaranteed, unsmoothed step down. Used once a player keeps dying in the same section.</summary>
        public void ForceStepDown(float stepScale, string source)
        {
            if (_config == null) return;
            ApplyScore(0f, stepScale, smoothed: false, source: source);
        }

        /// <summary>Last signal that moved (or tried to move) difficulty — shown in the F9 debug readout.</summary>
        public string LastSignal { get; private set; } = "";

        private void ApplyScore(float performanceScore, float stepScale, bool smoothed, string source)
        {
            float adjustment = 0f;

            if (performanceScore > _config.tooEasyThreshold)
            {
                adjustment = _config.difficultyStepSize * stepScale;
            }
            else if (performanceScore < _config.strugglingThreshold)
            {
                adjustment = -_config.difficultyStepSize * stepScale;
            }

            LastSignal = $"{source} score={performanceScore:F2} step={adjustment:+0.000;-0.000;0}";
            Debug.Log($"[DDA] {LastSignal}");

            if (Mathf.Abs(adjustment) > 0.001f)
            {
                float targetDifficulty = DifficultyLevel + adjustment;
                DifficultyLevel = smoothed
                    ? Mathf.Lerp(targetDifficulty, DifficultyLevel, _config.smoothingFactor)
                    : targetDifficulty;
                DifficultyLevel = Mathf.Clamp(DifficultyLevel, _config.minDifficulty, _config.maxDifficulty);

                Debug.Log($"[DDA] Difficulty adjusted to: {DifficultyLevel:F3}");
            }

            UpdateDerivedValues();
            PublishDifficultyChanged();
            UpdateSaveData();
        }

        // ─────────────────────────────────────────────
        // Metric Scoring
        // ─────────────────────────────────────────────

        /// <summary>
        /// Score hesitation: less hesitation = higher score (player was decisive).
        /// </summary>
        public static float CalculateHesitationScore(float hesitationTime, float expectedTime)
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
        public static float CalculateErrorScore(int errorCount)
        {
            // Diminishing returns: 0 errors = 1.0, 1 = 0.7, 2 = 0.5, 5+ = ~0.1
            return Mathf.Clamp01(1f / (1f + errorCount * 0.5f));
        }

        /// <summary>
        /// Score completion time: faster than expected = higher score.
        /// </summary>
        public static float CalculateTimeScore(float completionTime, float expectedTime)
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
            UpdateSaveData();
        }

        public DDAConfig Config => _config;

        // ─────────────────────────────────────────────
        // Save / Load
        // ─────────────────────────────────────────────

        private void UpdateSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentSave == null) return;

            var save = SaveManager.Instance.CurrentSave;
            save.difficultyLevel = DifficultyLevel;
            save.taskQuantityMultiplier = TaskQuantityMultiplier;
            save.timerStrictness = TimerStrictness;
            save.hintBudget = HintBudget;
            save.puzzleComplexityTier = PuzzleComplexityTier;
            save.cumulativeHesitationTime = _cumulativeHesitation;
            save.cumulativeErrors = _cumulativeErrors;
            save.challengesCompleted = _challengesCompleted;
            save.minigameScores = new System.Collections.Generic.List<float>(_minigameScores);
        }

        private void RestoreFromSave(SaveData save)
        {
            DifficultyLevel = save.difficultyLevel;
            _cumulativeHesitation = save.cumulativeHesitationTime;
            _cumulativeErrors = save.cumulativeErrors;
            _challengesCompleted = save.challengesCompleted;
            _minigameScores.Clear();
            if (save.minigameScores != null) _minigameScores.AddRange(save.minigameScores);

            UpdateDerivedValues();

            // Fix: previously never published, so listeners (HUD, abilities) kept default
            // values until the next challenge completed. Ensure everyone re-syncs on load.
            PublishDifficultyChanged();

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
            else if (_config != null)
            {
                // Previously ignored. A failed/abandoned minigame is a clear "struggling" signal.
                ProcessSignal(_config.failedChallengeScore, 1f, $"challenge-failed:{evt.ChallengeId}");
            }
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (_config != null) ProcessSignal(_config.failedChallengeScore, 1f, $"challenge-failed:{evt.ChallengeId} ({evt.Reason})");
        }
    }
}
