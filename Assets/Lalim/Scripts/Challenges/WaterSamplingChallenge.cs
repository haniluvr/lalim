using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Water Sampling Challenge controller.
    ///
    /// Setup: Place this on a parent GameObject in the zone. Under it, place:
    ///   - N x SamplePoint children (one per sampling location)
    ///   - Optionally, SpawnPoints for randomized sample point placement
    ///
    /// Flow:
    ///   1. ChallengeStartedEvent activates this (if matching ChallengeType)
    ///   2. Activates sample points based on DDA-adjusted task count
    ///   3. Player equips Water Sample Kit → approaches point → hold-to-sample
    ///   4. SamplePoint reports back here via OnSampleCollected()
    ///   5. When all points sampled, challenge auto-completes via ChallengeManager
    ///
    /// The ChallengeManager handles timer, hint budget, and completion events.
    /// This script manages the challenge-specific activation and result tracking.
    /// </summary>
    public class WaterSamplingChallenge : MonoBehaviour
    {
        [Header("Sample Points")]
        [Tooltip("Pre-placed sample point children. If empty, uses spawn points + prefab.")]
        [SerializeField] private List<SamplePoint> _preplacedPoints = new List<SamplePoint>();

        [Header("Spawn Configuration (alternative to pre-placed)")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
        [SerializeField] private SamplePoint _samplePointPrefab;

        [Header("Parameter Distribution")]
        [Tooltip("If true, spawned points cycle through pH → Salinity → Temperature. " +
                 "If false, all spawned points use a random parameter.")]
        [SerializeField] private bool _balanceParameters = true;

        [Header("Feedback")]
        [SerializeField] private AudioClip _challengeStartSound;
        [SerializeField] private AudioClip _challengeCompleteSound;
        [SerializeField] private AudioClip _allSamplesCollectedSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isActive;
        private int _totalSamples;
        private int _samplesCollected;
        private List<SamplePoint> _activePoints = new List<SamplePoint>();
        private List<SamplePoint> _spawnedPoints = new List<SamplePoint>();

        // Results tracking — which parameters were sampled
        private int _phCount;
        private int _salinityCount;
        private int _temperatureCount;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void Start()
        {
            // Hide all pre-placed points until challenge starts
            foreach (var point in _preplacedPoints)
            {
                if (point != null) point.gameObject.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Activation
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // Only respond to water sampling challenges
            if (evt.Type != ChallengeType.WaterSampling) return;

            _isActive = true;
            _samplesCollected = 0;
            _phCount = 0;
            _salinityCount = 0;
            _temperatureCount = 0;
            _activePoints.Clear();

            int taskCount = evt.TaskQuantity;

            // Activate sample points (pre-placed or spawned)
            if (_preplacedPoints.Count > 0)
            {
                ActivatePreplacedPoints(taskCount);
            }
            else if (_spawnPoints.Count > 0 && _samplePointPrefab != null)
            {
                SpawnSamplePoints(taskCount);
            }
            else
            {
                Debug.LogError("[WaterSampling] No sample points configured! " +
                    "Set either pre-placed points or spawn points + prefab.");
                return;
            }

            _totalSamples = _activePoints.Count;

            // Play start sound
            if (_challengeStartSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeStartSound, transform.position);
            }

            Debug.Log($"[WaterSampling] Challenge started with {_totalSamples} sample points");
        }

        /// <summary>
        /// Activate a subset of pre-placed sample points based on task count.
        /// Uses Fisher-Yates shuffle to randomize which points appear.
        /// Ensures balanced parameter coverage when possible.
        /// </summary>
        private void ActivatePreplacedPoints(int count)
        {
            // Build shuffled list
            List<SamplePoint> shuffled = new List<SamplePoint>(_preplacedPoints);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            int toActivate = Mathf.Min(count, shuffled.Count);

            // First pass: guarantee one of each parameter if possible
            HashSet<SampleParameter> parametersCovered = new HashSet<SampleParameter>();
            List<SamplePoint> prioritized = new List<SamplePoint>();

            foreach (var point in shuffled)
            {
                if (point == null) continue;
                if (!parametersCovered.Contains(point.Parameter) && prioritized.Count < toActivate)
                {
                    prioritized.Add(point);
                    parametersCovered.Add(point.Parameter);
                }
            }

            // Second pass: fill remaining slots
            foreach (var point in shuffled)
            {
                if (prioritized.Count >= toActivate) break;
                if (point == null || prioritized.Contains(point)) continue;
                prioritized.Add(point);
            }

            // Activate selected points
            foreach (var point in prioritized)
            {
                point.gameObject.SetActive(true);
                point.ResetPoint();
                _activePoints.Add(point);
            }
        }

        /// <summary>
        /// Spawn sample point instances from a prefab at randomized positions.
        /// </summary>
        private void SpawnSamplePoints(int count)
        {
            // Shuffle spawn points
            List<Transform> shuffledSpawns = new List<Transform>(_spawnPoints);
            for (int i = shuffledSpawns.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffledSpawns[i];
                shuffledSpawns[i] = shuffledSpawns[j];
                shuffledSpawns[j] = temp;
            }

            int toSpawn = Mathf.Min(count, shuffledSpawns.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                Transform spawnPoint = shuffledSpawns[i];

                SamplePoint spawned = Instantiate(
                    _samplePointPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    transform
                );

                _spawnedPoints.Add(spawned);
                _activePoints.Add(spawned);
            }
        }

        // ─────────────────────────────────────────────
        // Sample Reporting (called by SamplePoint)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Called by SamplePoint when a sample is successfully collected.
        /// </summary>
        public void OnSampleCollected(SamplePoint point)
        {
            if (!_isActive) return;

            _samplesCollected++;

            // Track parameter distribution
            switch (point.Parameter)
            {
                case SampleParameter.pH:          _phCount++; break;
                case SampleParameter.Salinity:    _salinityCount++; break;
                case SampleParameter.Temperature: _temperatureCount++; break;
            }

            Debug.Log($"[WaterSampling] Sample collected: {point.Parameter} " +
                      $"({_samplesCollected}/{_totalSamples})");

            // Check for challenge completion
            if (_samplesCollected >= _totalSamples)
            {
                if (_allSamplesCollectedSound != null)
                {
                    AudioSource.PlayClipAtPoint(_allSamplesCollectedSound, transform.position);
                }

                Debug.Log($"[WaterSampling] All samples collected! " +
                          $"pH={_phCount}, Salinity={_salinityCount}, Temp={_temperatureCount}");

                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = "WaterSampling",
                    Type = ChallengeType.WaterSampling,
                    Success = true,
                    ErrorCount = 0
                });
            }
        }

        // ─────────────────────────────────────────────
        // Challenge End
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.Type != ChallengeType.WaterSampling) return;

            _isActive = false;

            if (evt.Success && _challengeCompleteSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeCompleteSound, transform.position);
            }

            CleanupPoints();

            Debug.Log($"[WaterSampling] Challenge {(evt.Success ? "COMPLETED" : "ENDED")}: " +
                      $"sampled={_samplesCollected}/{_totalSamples}");
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.Type != ChallengeType.WaterSampling) return;

            _isActive = false;
            CleanupPoints();

            Debug.Log($"[WaterSampling] Challenge FAILED: {evt.Reason}");
        }

        private void CleanupPoints()
        {
            // Deactivate pre-placed points
            foreach (var point in _preplacedPoints)
            {
                if (point != null) point.gameObject.SetActive(false);
            }

            // Destroy spawned points
            foreach (var point in _spawnedPoints)
            {
                if (point != null) Destroy(point.gameObject);
            }
            _spawnedPoints.Clear();
            _activePoints.Clear();
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the current sampling progress (0 to 1).
        /// </summary>
        public float Progress => _totalSamples > 0 ? (float)_samplesCollected / _totalSamples : 0f;

        /// <summary>
        /// Returns the number of un-sampled points remaining.
        /// </summary>
        public int RemainingPoints => _totalSamples - _samplesCollected;
    }
}
