using UnityEngine;
using Lalim.Core;

namespace Lalim.DDA
{
    /// <summary>
    /// Tracks player performance metrics during an active challenge.
    /// Activated when a challenge begins, deactivated when it ends.
    /// Packages raw metrics into the ChallengeCompletedEvent for DDAManager.
    /// 
    /// Tracked metrics:
    /// - HesitationTime: cumulative idle time (no input for threshold duration)
    /// - ErrorFrequency: count of incorrect actions
    /// - CompletionTimeDeviation: actual vs expected time
    /// </summary>
    public class PlayerMetricsTracker : MonoBehaviour
    {
        public static PlayerMetricsTracker Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private float _hesitationThreshold = 3.0f; // Seconds idle before counting

        // ─────────────────────────────────────────────
        // Current Challenge Metrics
        // ─────────────────────────────────────────────

        public bool IsTracking { get; private set; }

        private string _currentChallengeId;
        private ChallengeType _currentChallengeType;

        // Hesitation tracking
        private float _totalHesitationTime;
        private float _lastInputTime;
        private bool _isHesitating;
        private float _hesitationStart;

        // Error tracking
        private int _errorCount;

        // Time tracking
        private float _challengeStartTime;
        private float _expectedTime;

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
            if (!IsTracking) return;

            UpdateHesitationTracking();
        }

        // ─────────────────────────────────────────────
        // Tracking Control
        // ─────────────────────────────────────────────

        /// <summary>
        /// Begin tracking metrics for a new challenge.
        /// </summary>
        public void StartTracking(string challengeId, ChallengeType type, float expectedTime)
        {
            _currentChallengeId = challengeId;
            _currentChallengeType = type;
            _expectedTime = expectedTime;

            // Reset all metrics
            _totalHesitationTime = 0f;
            _errorCount = 0;
            _challengeStartTime = Time.time;
            _lastInputTime = Time.time;
            _isHesitating = false;

            IsTracking = true;

            Debug.Log($"[MetricsTracker] Started tracking challenge: {challengeId}");
        }

        /// <summary>
        /// Stop tracking and return collected metrics via ChallengeCompletedEvent.
        /// </summary>
        public ChallengeCompletedEvent StopTracking(bool success)
        {
            IsTracking = false;

            // If currently hesitating, add final hesitation period
            if (_isHesitating)
            {
                _totalHesitationTime += Time.time - _hesitationStart;
            }

            float completionTime = Time.time - _challengeStartTime;

            ChallengeCompletedEvent result = new ChallengeCompletedEvent
            {
                ChallengeId = _currentChallengeId,
                Type = _currentChallengeType,
                Success = success,
                HesitationTime = _totalHesitationTime,
                ErrorCount = _errorCount,
                CompletionTime = completionTime,
                ExpectedTime = _expectedTime
            };

            Debug.Log($"[MetricsTracker] Stopped tracking. " +
                      $"Hesitation={_totalHesitationTime:F1}s, " +
                      $"Errors={_errorCount}, " +
                      $"Time={completionTime:F1}s/{_expectedTime:F1}s");

            return result;
        }

        // ─────────────────────────────────────────────
        // Metric Recording
        // ─────────────────────────────────────────────

        /// <summary>
        /// Call this whenever the player performs any input action during a challenge.
        /// Resets the hesitation timer.
        /// </summary>
        public void RecordInput()
        {
            if (!IsTracking) return;

            // If we were hesitating, close the hesitation period
            if (_isHesitating)
            {
                _totalHesitationTime += Time.time - _hesitationStart;
                _isHesitating = false;
            }

            _lastInputTime = Time.time;
        }

        /// <summary>
        /// Record an error (wrong item used, missed target, etc.)
        /// </summary>
        public void RecordError()
        {
            if (!IsTracking) return;

            _errorCount++;

            EventBus.Publish(new PlayerMetricRecordedEvent
            {
                MetricName = "Error",
                Value = _errorCount
            });

            Debug.Log($"[MetricsTracker] Error recorded (total: {_errorCount})");
        }

        // ─────────────────────────────────────────────
        // Hesitation Detection
        // ─────────────────────────────────────────────

        private void UpdateHesitationTracking()
        {
            float timeSinceLastInput = Time.time - _lastInputTime;

            if (!_isHesitating && timeSinceLastInput >= _hesitationThreshold)
            {
                // Player has been idle for too long — start hesitation period
                _isHesitating = true;
                _hesitationStart = _lastInputTime + _hesitationThreshold;
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            StartTracking(evt.ChallengeId, evt.Type, evt.TimeLimit);
        }
    }
}
