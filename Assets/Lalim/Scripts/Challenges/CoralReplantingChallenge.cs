using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Coral Replanting Challenge controller.
    ///
    /// Setup: Place this on a parent GameObject in the zone. Under it, place:
    ///   - N x CoralFragment children (collectible fragments scattered in the area)
    ///   - M x CoralSlot children (substrate anchor points on the reef)
    ///   - Optionally, SpawnPoints for randomized fragment/slot placement
    ///
    /// Flow:
    ///   1. ChallengeStartedEvent activates this (if matching ChallengeType)
    ///   2. Activates fragments and slots based on DDA-adjusted task count
    ///   3. Player collects fragments → walks to slots → hold-to-plant
    ///   4. CoralSlot reports back here via OnCoralPlanted()
    ///   5. When all slots are filled, challenge auto-completes via ChallengeManager
    ///
    /// Task count = number of slots to fill (fragments available ≥ slots).
    /// The ChallengeManager handles timer, hint budget, and completion events.
    /// </summary>
    public class CoralReplantingChallenge : MonoBehaviour
    {
        [Header("Coral Fragments")]
        [Tooltip("Pre-placed coral fragments (children of this object).")]
        [SerializeField] private List<CoralFragment> _preplacedFragments = new List<CoralFragment>();

        [Header("Planting Slots")]
        [Tooltip("Pre-placed coral slots (children of this object).")]
        [SerializeField] private List<CoralSlot> _preplacedSlots = new List<CoralSlot>();

        [Header("Spawn Configuration (alternative to pre-placed)")]
        [SerializeField] private List<Transform> _fragmentSpawnPoints = new List<Transform>();
        [SerializeField] private CoralFragment _fragmentPrefab;
        [SerializeField] private List<Transform> _slotSpawnPoints = new List<Transform>();
        [SerializeField] private CoralSlot _slotPrefab;

        [Header("Fragment Balance")]
        [Tooltip("Extra fragments beyond the number of slots (so player has spares). " +
                 "E.g., 2 means player gets 2 more fragments than slots.")]
        [SerializeField] private int _extraFragments = 2;

        [Header("Feedback")]
        [SerializeField] private AudioClip _challengeStartSound;
        [SerializeField] private AudioClip _challengeCompleteSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isActive;
        private int _totalSlots;
        private int _slotsPlanted;
        private int _fragmentsCollected;
        private int _fragmentsUsed;

        private List<CoralFragment> _activeFragments = new List<CoralFragment>();
        private List<CoralSlot> _activeSlots = new List<CoralSlot>();
        private List<CoralFragment> _spawnedFragments = new List<CoralFragment>();
        private List<CoralSlot> _spawnedSlots = new List<CoralSlot>();

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
            // Hide all pre-placed objects until challenge starts
            foreach (var fragment in _preplacedFragments)
            {
                if (fragment != null) fragment.gameObject.SetActive(false);
            }
            foreach (var slot in _preplacedSlots)
            {
                if (slot != null) slot.gameObject.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Activation
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // Only respond to coral replanting challenges
            if (evt.Type != ChallengeType.CoralReplanting) return;

            _isActive = true;
            _slotsPlanted = 0;
            _fragmentsCollected = 0;
            _fragmentsUsed = 0;
            _activeFragments.Clear();
            _activeSlots.Clear();

            int taskCount = evt.TaskQuantity;

            // Activate or spawn slots and fragments
            if (_preplacedSlots.Count > 0)
            {
                ActivatePreplacedSlots(taskCount);
                ActivatePreplacedFragments(taskCount + _extraFragments);
            }
            else if (_slotSpawnPoints.Count > 0 && _slotPrefab != null)
            {
                SpawnSlots(taskCount);
                SpawnFragments(taskCount + _extraFragments);
            }
            else
            {
                Debug.LogError("[CoralReplanting] No slots configured! " +
                    "Set either pre-placed slots or spawn points + prefab.");
                return;
            }

            _totalSlots = _activeSlots.Count;

            // Play start sound
            if (_challengeStartSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeStartSound, transform.position);
            }

            Debug.Log($"[CoralReplanting] Challenge started: {_totalSlots} slots, " +
                      $"{_activeFragments.Count} fragments available");
        }

        // ─────────────────────────────────────────────
        // Pre-placed Activation
        // ─────────────────────────────────────────────

        private void ActivatePreplacedSlots(int count)
        {
            List<CoralSlot> shuffled = new List<CoralSlot>(_preplacedSlots);
            ShuffleList(shuffled);

            int toActivate = Mathf.Min(count, shuffled.Count);

            for (int i = 0; i < toActivate; i++)
            {
                if (shuffled[i] == null) continue;
                shuffled[i].gameObject.SetActive(true);
                shuffled[i].ResetSlot();
                _activeSlots.Add(shuffled[i]);
            }
        }

        private void ActivatePreplacedFragments(int count)
        {
            List<CoralFragment> shuffled = new List<CoralFragment>(_preplacedFragments);
            ShuffleList(shuffled);

            int toActivate = Mathf.Min(count, shuffled.Count);

            for (int i = 0; i < toActivate; i++)
            {
                if (shuffled[i] == null) continue;
                shuffled[i].gameObject.SetActive(true);
                shuffled[i].ResetFragment();
                _activeFragments.Add(shuffled[i]);
            }
        }

        // ─────────────────────────────────────────────
        // Spawning
        // ─────────────────────────────────────────────

        private void SpawnSlots(int count)
        {
            List<Transform> shuffled = new List<Transform>(_slotSpawnPoints);
            ShuffleList(shuffled);

            int toSpawn = Mathf.Min(count, shuffled.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                CoralSlot spawned = Instantiate(
                    _slotPrefab,
                    shuffled[i].position,
                    shuffled[i].rotation,
                    transform
                );
                _spawnedSlots.Add(spawned);
                _activeSlots.Add(spawned);
            }
        }

        private void SpawnFragments(int count)
        {
            List<Transform> shuffled = new List<Transform>(_fragmentSpawnPoints);
            ShuffleList(shuffled);

            int toSpawn = Mathf.Min(count, shuffled.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                // Slight random offset for natural scatter
                Vector3 offset = new Vector3(
                    Random.Range(-0.3f, 0.3f),
                    0f,
                    Random.Range(-0.3f, 0.3f)
                );

                CoralFragment spawned = Instantiate(
                    _fragmentPrefab,
                    shuffled[i].position + offset,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    transform
                );
                _spawnedFragments.Add(spawned);
                _activeFragments.Add(spawned);
            }
        }

        // ─────────────────────────────────────────────
        // Reporting (called by child scripts)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Called by CoralFragment when the player picks up a fragment.
        /// </summary>
        public void OnFragmentCollected(CoralFragment fragment)
        {
            if (!_isActive) return;

            _fragmentsCollected++;

            Debug.Log($"[CoralReplanting] Fragment collected ({_fragmentsCollected} total, " +
                      $"{FragmentsAvailable} available for planting)");
        }

        /// <summary>
        /// Called by CoralSlot when a coral fragment is successfully planted.
        /// </summary>
        public void OnCoralPlanted(CoralSlot slot)
        {
            if (!_isActive) return;

            _slotsPlanted++;
            _fragmentsUsed++;

            Debug.Log($"[CoralReplanting] Coral planted: {_slotsPlanted}/{_totalSlots}");

            // Check for challenge completion
            if (_slotsPlanted >= _totalSlots)
            {
                Debug.Log("[CoralReplanting] All slots filled! Challenge complete.");

                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = "CoralReplanting",
                    Type = ChallengeType.CoralReplanting,
                    Success = true,
                    ErrorCount = 0
                });
            }
        }

        // ─────────────────────────────────────────────
        // Fragment Inventory
        // ─────────────────────────────────────────────

        /// <summary>
        /// Number of fragments the player has collected but not yet planted.
        /// </summary>
        public int FragmentsAvailable => _fragmentsCollected - _fragmentsUsed;

        /// <summary>
        /// Returns true if the player has at least one fragment available to plant.
        /// Called by CoralSlot to gate the planting interaction.
        /// </summary>
        public bool HasFragmentsAvailable()
        {
            return FragmentsAvailable > 0;
        }

        // ─────────────────────────────────────────────
        // Challenge End
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.Type != ChallengeType.CoralReplanting) return;

            _isActive = false;

            if (evt.Success && _challengeCompleteSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeCompleteSound, transform.position);
            }

            Cleanup();

            Debug.Log($"[CoralReplanting] Challenge {(evt.Success ? "COMPLETED" : "ENDED")}: " +
                      $"planted={_slotsPlanted}/{_totalSlots}, " +
                      $"fragments collected={_fragmentsCollected}, used={_fragmentsUsed}");
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.Type != ChallengeType.CoralReplanting) return;

            _isActive = false;
            Cleanup();

            Debug.Log($"[CoralReplanting] Challenge FAILED: {evt.Reason}");
        }

        private void Cleanup()
        {
            // Deactivate pre-placed objects
            foreach (var fragment in _preplacedFragments)
            {
                if (fragment != null) fragment.gameObject.SetActive(false);
            }
            foreach (var slot in _preplacedSlots)
            {
                if (slot != null) slot.gameObject.SetActive(false);
            }

            // Destroy spawned objects
            foreach (var fragment in _spawnedFragments)
            {
                if (fragment != null) Destroy(fragment.gameObject);
            }
            foreach (var slot in _spawnedSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }

            _spawnedFragments.Clear();
            _spawnedSlots.Clear();
            _activeFragments.Clear();
            _activeSlots.Clear();
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the current planting progress (0 to 1).
        /// </summary>
        public float Progress => _totalSlots > 0 ? (float)_slotsPlanted / _totalSlots : 0f;

        /// <summary>
        /// Returns the number of empty slots remaining.
        /// </summary>
        public int RemainingSlots => _totalSlots - _slotsPlanted;

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Fisher-Yates shuffle for any list.
        /// </summary>
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
