using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Waste Segregation Challenge controller.
    /// 
    /// Setup: Place this on a parent GameObject in the zone. Under it, place:
    ///   - 3x SortingBin children (one per DebrisCategory)
    ///   - N x DebrisItem children (mix of categories)
    ///   - Optionally, SpawnPoints for randomized debris placement
    /// 
    /// Flow:
    ///   1. ChallengeStartedEvent activates this (if matching ChallengeType)
    ///   2. Spawns/activates debris items based on DDA-adjusted task count
    ///   3. Player picks up debris → carries to bin → SortingBin validates
    ///   4. SortingBin reports back here via OnDebrisSorted()
    ///   5. When all debris sorted, challenge auto-completes via ChallengeManager
    /// 
    /// The ChallengeManager handles timer, hint budget, and completion events.
    /// This script only manages the challenge-specific spawning and feedback logic.
    /// </summary>
    public class WasteSegregationChallenge : MonoBehaviour
    {
        [Header("Debris Setup")]
        [Tooltip("Pre-placed debris items (children of this object). " +
                 "If empty, uses spawn points + prefabs instead.")]
        [SerializeField] private List<DebrisItem> _preplacedDebris = new List<DebrisItem>();

        [Header("Spawn Configuration (alternative to pre-placed)")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
        [SerializeField] private List<DebrisItem> _debrisPrefabs = new List<DebrisItem>();

        [Header("Sorting Bins")]
        [SerializeField] private SortingBin _plasticBin;
        [SerializeField] private SortingBin _metalBin;
        [SerializeField] private SortingBin _generalBin;

        [Header("Feedback")]
        [SerializeField] private AudioClip _challengeStartSound;
        [SerializeField] private AudioClip _challengeCompleteSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isActive;
        private int _totalDebris;
        private int _sortedCorrectly;
        private int _sortedIncorrectly;
        private List<DebrisItem> _activeDebris = new List<DebrisItem>();
        private List<DebrisItem> _spawnedDebris = new List<DebrisItem>();

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
            // Hide all pre-placed debris until challenge starts
            foreach (var debris in _preplacedDebris)
            {
                if (debris != null) debris.gameObject.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Activation
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // Only respond to waste segregation challenges
            if (evt.Type != ChallengeType.WasteSegregation) return;

            _isActive = true;
            _sortedCorrectly = 0;
            _sortedIncorrectly = 0;
            _activeDebris.Clear();

            int taskCount = evt.TaskQuantity;

            // Activate debris (pre-placed or spawned)
            if (_preplacedDebris.Count > 0)
            {
                ActivatePreplacedDebris(taskCount);
            }
            else if (_spawnPoints.Count > 0 && _debrisPrefabs.Count > 0)
            {
                SpawnDebris(taskCount);
            }
            else
            {
                Debug.LogError("[WasteSegregation] No debris configured! " +
                    "Set either pre-placed debris or spawn points + prefabs.");
                return;
            }

            _totalDebris = _activeDebris.Count;

            // Play start sound
            if (_challengeStartSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeStartSound, transform.position);
            }

            Debug.Log($"[WasteSegregation] Challenge started with {_totalDebris} debris items");
        }

        /// <summary>
        /// Activate a subset of pre-placed debris based on task count.
        /// Uses Fisher-Yates shuffle to randomize which items appear.
        /// </summary>
        private void ActivatePreplacedDebris(int count)
        {
            // Build shuffled list
            List<DebrisItem> shuffled = new List<DebrisItem>(_preplacedDebris);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            // Activate up to count items, ensuring at least 1 of each category
            int toActivate = Mathf.Min(count, shuffled.Count);

            // First pass: guarantee one of each category if possible
            HashSet<DebrisCategory> categoriesCovered = new HashSet<DebrisCategory>();
            List<DebrisItem> prioritized = new List<DebrisItem>();

            foreach (var debris in shuffled)
            {
                if (debris == null) continue;
                if (!categoriesCovered.Contains(debris.Category) && prioritized.Count < toActivate)
                {
                    prioritized.Add(debris);
                    categoriesCovered.Add(debris.Category);
                }
            }

            // Second pass: fill remaining slots
            foreach (var debris in shuffled)
            {
                if (prioritized.Count >= toActivate) break;
                if (debris == null || prioritized.Contains(debris)) continue;
                prioritized.Add(debris);
            }

            // Activate selected items
            foreach (var debris in prioritized)
            {
                debris.gameObject.SetActive(true);
                _activeDebris.Add(debris);
            }
        }

        /// <summary>
        /// Spawn debris from prefabs at randomized spawn points.
        /// Ensures balanced category distribution.
        /// </summary>
        private void SpawnDebris(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Pick random spawn point
                Transform spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Count)];

                // Pick prefab (cycle through to balance categories)
                DebrisItem prefab = _debrisPrefabs[i % _debrisPrefabs.Count];

                // Spawn with slight random offset
                Vector3 offset = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    0f,
                    Random.Range(-0.5f, 0.5f)
                );

                DebrisItem spawned = Instantiate(
                    prefab,
                    spawnPoint.position + offset,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    transform
                );

                _spawnedDebris.Add(spawned);
                _activeDebris.Add(spawned);
            }
        }

        // ─────────────────────────────────────────────
        // Sorting Feedback (called by SortingBin)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Called by SortingBin when a debris item enters a bin.
        /// </summary>
        public void OnDebrisSorted(DebrisCategory category, bool correct)
        {
            if (!_isActive) return;

            if (correct)
            {
                _sortedCorrectly++;
                Debug.Log($"[WasteSegregation] Progress: {_sortedCorrectly}/{_totalDebris}");
            }
            else
            {
                _sortedIncorrectly++;
            }
        }

        // ─────────────────────────────────────────────
        // Challenge End
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.Type != ChallengeType.WasteSegregation) return;

            _isActive = false;

            if (evt.Success && _challengeCompleteSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeCompleteSound, transform.position);
            }

            CleanupDebris();

            Debug.Log($"[WasteSegregation] Challenge {(evt.Success ? "COMPLETED" : "ENDED")}: " +
                      $"correct={_sortedCorrectly}, errors={_sortedIncorrectly}");
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.Type != ChallengeType.WasteSegregation) return;

            _isActive = false;
            CleanupDebris();

            Debug.Log($"[WasteSegregation] Challenge FAILED: {evt.Reason}");
        }

        private void CleanupDebris()
        {
            // Deactivate pre-placed debris
            foreach (var debris in _preplacedDebris)
            {
                if (debris != null) debris.gameObject.SetActive(false);
            }

            // Destroy spawned debris
            foreach (var debris in _spawnedDebris)
            {
                if (debris != null) Destroy(debris.gameObject);
            }
            _spawnedDebris.Clear();
            _activeDebris.Clear();
        }
    }
}
