using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Singleton challenge lifecycle controller.
    /// Manages the active challenge state: timer, task progress, hint budget, and performance metrics.
    /// Individual challenge scripts (WasteSegregationChallenge, FishingChallenge, etc.) report
    /// task completions and errors to this manager, which handles the shared lifecycle.
    /// </summary>
    public class ChallengeManager : MonoBehaviour
    {
        public static ChallengeManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Active Challenge State
        // ─────────────────────────────────────────────

        public bool IsChallengeActive { get; private set; }
        public string ActiveChallengeId { get; private set; }
        public ChallengeType ActiveChallengeType { get; private set; }
        public ChallengePerspective ActivePerspective { get; private set; }

        /// <summary>True while a first-person challenge is running (navigation guides and toxic drain pause).</summary>
        public bool IsFirstPersonChallengeActive => IsChallengeActive && ActivePerspective == ChallengePerspective.FirstPerson;

        // Task Progress
        public int TotalTasks { get; private set; }
        public int CompletedTasks { get; private set; }
        public float TaskProgress => TotalTasks > 0 ? (float)CompletedTasks / TotalTasks : 0f;

        // Timer
        public float TimeLimit { get; private set; }
        public float TimeRemaining { get; private set; }
        public bool HasTimer => TimeLimit > 0f;

        // Hints
        public int HintBudget { get; private set; }
        public int HintsUsed { get; private set; }
        public int HintsRemaining => HintBudget - HintsUsed;

        // ─────────────────────────────────────────────
        // Performance Tracking (for DDA)
        // ─────────────────────────────────────────────

        private float _challengeStartTime;
        private float _expectedCompletionTime;
        private float _expectedFirstActionTime;
        private float _firstCorrectActionTime = -1f;

        private float _totalHesitationTime;
        private float _lastActionTime;
        private int _errorCount;

        [Header("Hesitation Detection")]
        [Tooltip("Seconds of inactivity before counting as hesitation")]
        [SerializeField] private float _hesitationThreshold = 3.0f;

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
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
        }

        private void Update()
        {
            if (!IsChallengeActive) return;

            // Update timer
            if (HasTimer)
            {
                TimeRemaining -= Time.deltaTime;

                if (TimeRemaining <= 0f)
                {
                    TimeRemaining = 0f;
                    FailChallenge("timeout");
                    return;
                }
            }

            // Track hesitation (time since last meaningful player action)
            float timeSinceLastAction = Time.time - _lastActionTime;
            if (timeSinceLastAction > _hesitationThreshold)
            {
                _totalHesitationTime += Time.deltaTime;
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Lifecycle
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            if (IsChallengeActive)
            {
                Debug.LogWarning("[ChallengeManager] A challenge is already active!");
                return;
            }

            IsChallengeActive = true;
            ActiveChallengeId = evt.ChallengeId;
            ActiveChallengeType = evt.Type;
            ActivePerspective = evt.Perspective;
            TotalTasks = evt.TaskQuantity;
            CompletedTasks = 0;
            TimeLimit = evt.TimeLimit;
            TimeRemaining = evt.TimeLimit;
            HintBudget = evt.HintBudget;
            HintsUsed = 0;

            // Reset performance tracking
            _expectedCompletionTime = 0f;
            _expectedFirstActionTime = 0f;
            _firstCorrectActionTime = -1f;
            _challengeStartTime = Time.time;
            _lastActionTime = Time.time;
            _totalHesitationTime = 0f;
            _errorCount = 0;

            // Hesitation threshold comes from the DDA config when there is one
            if (DDA.DDAManager.Instance != null && DDA.DDAManager.Instance.Config != null)
            {
                _hesitationThreshold = DDA.DDAManager.Instance.Config.hesitationThreshold;
            }

            Debug.Log($"[ChallengeManager] Challenge started: {evt.ChallengeId} " +
                      $"({evt.Type}), tasks={evt.TaskQuantity}, time={evt.TimeLimit:F0}s");

            // Set game state
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.InChallenge);
            }
        }

        // ─────────────────────────────────────────────
        // Task Reporting (called by challenge scripts)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Report that a task within the challenge has been completed.
        /// Call this from individual challenge scripts (e.g., when trash is sorted correctly).
        /// </summary>
        public void ReportTaskCompleted()
        {
            if (!IsChallengeActive) return;

            CompletedTasks++;
            _lastActionTime = Time.time;
            MarkFirstCorrectAction();

            Debug.Log($"[ChallengeManager] Task completed: {CompletedTasks}/{TotalTasks}");

            // Check if all tasks are done
            if (CompletedTasks >= TotalTasks)
            {
                CompleteChallenge(true);
            }
        }

        /// <summary>
        /// Report that the player made an error.
        /// Call this from individual challenge scripts (e.g., wrong bin, line snap, harm cut).
        /// </summary>
        public void ReportError()
        {
            if (!IsChallengeActive) return;

            _errorCount++;
            _lastActionTime = Time.time;

            Debug.Log($"[ChallengeManager] Error #{_errorCount} in challenge {ActiveChallengeId}");
        }

        /// <summary>
        /// Report that the player performed a meaningful action (resets hesitation timer).
        /// Call this even if the action didn't complete a task.
        /// </summary>
        public void ReportPlayerAction()
        {
            _lastActionTime = Time.time;
        }

        /// <summary>
        /// Report a correct step that isn't a whole task (e.g. the needle first enters the green band,
        /// a knot move that removes a crossing). The first one measures the "pickup rate".
        /// </summary>
        public void ReportCorrectAction()
        {
            if (!IsChallengeActive) return;
            _lastActionTime = Time.time;
            MarkFirstCorrectAction();
        }

        private void MarkFirstCorrectAction()
        {
            if (_firstCorrectActionTime < 0f) _firstCorrectActionTime = Time.time - _challengeStartTime;
        }

        /// <summary>Baseline for how quickly a player should make their first correct move (DDA "pickup rate").</summary>
        public void SetExpectedFirstActionTime(float seconds)
        {
            _expectedFirstActionTime = seconds;
        }

        /// <summary>
        /// Use a hint. Returns true if hints remain, false if budget exhausted.
        /// </summary>
        public bool UseHint()
        {
            if (!IsChallengeActive || HintsRemaining <= 0) return false;

            HintsUsed++;

            EventBus.Publish(new HintRequestedEvent
            {
                RemainingHints = HintsRemaining
            });

            Debug.Log($"[ChallengeManager] Hint used ({HintsRemaining} remaining)");
            return true;
        }

        // ─────────────────────────────────────────────
        // Challenge Resolution
        // ─────────────────────────────────────────────

        /// <summary>
        /// Complete the current challenge. Called automatically when all tasks
        /// are done, or manually by challenge scripts for custom completion logic.
        /// </summary>
        public void CompleteChallenge(bool success)
        {
            if (!IsChallengeActive) return;

            float completionTime = Time.time - _challengeStartTime;

            IsChallengeActive = false;

            Debug.Log($"[ChallengeManager] Challenge {(success ? "COMPLETED" : "ENDED")}: " +
                      $"{ActiveChallengeId} in {completionTime:F1}s, " +
                      $"errors={_errorCount}, hesitation={_totalHesitationTime:F1}s");

            EventBus.Publish(new ChallengeCompletedEvent
            {
                ChallengeId = ActiveChallengeId,
                Type = ActiveChallengeType,
                Success = success,
                HesitationTime = _totalHesitationTime,
                ErrorCount = _errorCount,
                CompletionTime = completionTime,
                ExpectedTime = _expectedCompletionTime,
                FirstActionLatency = _firstCorrectActionTime,
                ExpectedFirstAction = _expectedFirstActionTime,
                HintsUsed = HintsUsed,
                HintBudget = HintBudget
            });

            // Restore game state
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Exploring);
            }
        }

        /// <summary>
        /// Fail the current challenge with a reason.
        /// </summary>
        public void FailChallenge(string reason)
        {
            if (!IsChallengeActive) return;

            IsChallengeActive = false;

            Debug.Log($"[ChallengeManager] Challenge FAILED: {ActiveChallengeId} — {reason}");

            EventBus.Publish(new ChallengeFailedEvent
            {
                ChallengeId = ActiveChallengeId,
                Type = ActiveChallengeType,
                Reason = reason
            });

            // Restore game state
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Exploring);
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Set the expected completion time for the current challenge.
        /// Called by ZoneTrigger or challenge scripts to give DDA a baseline.
        /// </summary>
        public void SetExpectedCompletionTime(float expectedTime)
        {
            _expectedCompletionTime = expectedTime;
        }
    }
}
