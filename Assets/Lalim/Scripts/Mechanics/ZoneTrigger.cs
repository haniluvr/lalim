using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Trigger collider placed at each challenge zone entrance.
    /// When the player enters and confirms, starts the associated challenge:
    /// - Locks player movement
    /// - Publishes ChallengeStartedEvent with DDA-adjusted parameters
    /// - Camera transitions to 1P via CameraController's event subscription
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("Unique zone identifier (use GameConstants.Zones values)")]
        [SerializeField] private string _zoneId;

        [Tooltip("Which region this zone belongs to")]
        [SerializeField] private Region _region;

        [Tooltip("The type of challenge in this zone")]
        [SerializeField] private ChallengeType _challengeType;

        [Header("Challenge Parameters (Base Values — DDA adjusts these)")]
        [Tooltip("Base number of tasks (e.g., trash items, coral slots, sample locations)")]
        [SerializeField] private int _baseTaskQuantity = 10;

        [Tooltip("Base time limit in seconds (0 = no timer)")]
        [SerializeField] private float _baseTimeLimit = 120f;

        [Tooltip("Expected completion time for DDA deviation calculation")]
        [SerializeField] private float _expectedCompletionTime = 90f;

        [Header("Player Positioning")]
        [Tooltip("Where to place the player when the challenge starts")]
        [SerializeField] private Transform _challengeStartPoint;

        [Header("Interaction")]
        [Tooltip("Prompt shown when player is in range")]
        [SerializeField] private string _interactionPrompt = "Press E to begin challenge";

        [Tooltip("Required tool item ID (empty = no tool required)")]
        [SerializeField] private string _requiredToolId = "";

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isCompleted;
        private bool _playerInRange;
        private bool _challengeActive;

        public string ZoneId => _zoneId;
        public Region Region => _region;
        public ChallengeType ChallengeType => _challengeType;
        public bool IsCompleted => _isCompleted;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Check if this zone was already completed (from save)
            if (RegionProgressTracker.Instance != null)
            {
                _isCompleted = RegionProgressTracker.Instance.IsZoneCompleted(_zoneId);
            }

            // Ensure collider is a trigger
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void Update()
        {
            if (!_playerInRange || _isCompleted || _challengeActive) return;

            // Wait for interact input
            if (InputManager.Instance != null && InputManager.Instance.InteractPressed)
            {
                TryStartChallenge();
            }
        }

        // ─────────────────────────────────────────────
        // Trigger Detection
        // ─────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_isCompleted || _challengeActive) return;

            if (other.CompareTag(GameConstants.Tags.PLAYER))
            {
                _playerInRange = true;
                Debug.Log($"[ZoneTrigger] Player entered zone: {_zoneId}");

                // TODO: Show interaction prompt via HUD
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(GameConstants.Tags.PLAYER))
            {
                _playerInRange = false;

                // TODO: Hide interaction prompt
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Start
        // ─────────────────────────────────────────────

        private void TryStartChallenge()
        {
            // Check required tool
            if (!string.IsNullOrEmpty(_requiredToolId))
            {
                if (Inventory.InventoryManager.Instance == null ||
                    !Inventory.InventoryManager.Instance.HasItem(_requiredToolId))
                {
                    Debug.LogWarning($"[ZoneTrigger] Missing required tool: {_requiredToolId}");
                    // TODO: Show "You need [tool] to enter this area" prompt
                    return;
                }
            }

            // Check region accessibility
            if (GameManager.Instance != null && !GameManager.Instance.IsRegionAccessible(_region))
            {
                Debug.LogWarning($"[ZoneTrigger] Region {_region} is locked");
                return;
            }

            StartChallenge();
        }

        private void StartChallenge()
        {
            _challengeActive = true;

            // Position player at challenge start point
            if (_challengeStartPoint != null)
            {
                PlayerController player = FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    player.SetPosition(_challengeStartPoint.position);
                }
            }

            // Calculate DDA-adjusted parameters
            int adjustedQuantity = _baseTaskQuantity;
            float adjustedTimeLimit = _baseTimeLimit;
            int hintBudget = GameConstants.DDA.DEFAULT_HINT_BUDGET;

            if (DDA.DDAManager.Instance != null)
            {
                adjustedQuantity = DDA.DDAManager.Instance.GetAdjustedTaskQuantity(_baseTaskQuantity);
                adjustedTimeLimit = DDA.DDAManager.Instance.GetAdjustedTimeLimit(_baseTimeLimit);
                hintBudget = DDA.DDAManager.Instance.HintBudget;
            }

            Debug.Log($"[ZoneTrigger] Starting challenge: {_zoneId} ({_challengeType}) " +
                      $"tasks={adjustedQuantity}, time={adjustedTimeLimit:F0}s, hints={hintBudget}");

            // Publish challenge start — CameraController auto-switches to 1P
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = _challengeType,
                ChallengeId = _zoneId,
                TaskQuantity = adjustedQuantity,
                TimeLimit = adjustedTimeLimit,
                HintBudget = hintBudget
            });
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.ChallengeId != _zoneId) return;

            _challengeActive = false;

            if (evt.Success)
            {
                _isCompleted = true;
                Debug.Log($"[ZoneTrigger] Zone completed: {_zoneId}");

                // Notify region progress tracker
                EventBus.Publish(new ZoneCompletedEvent
                {
                    ZoneId = _zoneId,
                    Region = _region,
                    ChallengeType = _challengeType
                });
            }
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.ChallengeId != _zoneId) return;

            _challengeActive = false;
            Debug.Log($"[ZoneTrigger] Challenge failed in zone: {_zoneId} — Reason: {evt.Reason}");

            // Player can re-enter to retry
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Mark this zone as completed (e.g., when restoring from save).
        /// </summary>
        public void SetCompleted(bool completed)
        {
            _isCompleted = completed;
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color zoneColor = _isCompleted ? Color.green : Color.yellow;
            zoneColor.a = 0.3f;
            Gizmos.color = zoneColor;

            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }

            // Label
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3f,
                $"{_zoneId}\n{_challengeType}\n{(_isCompleted ? "✓ DONE" : "○ PENDING")}"
            );

            // Draw line to start point
            if (_challengeStartPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _challengeStartPoint.position);
                Gizmos.DrawSphere(_challengeStartPoint.position, 0.3f);
            }
        }
#endif
    }
}
