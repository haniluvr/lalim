using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Player character controller for third-person exploration.
    /// Reads input from InputManager, moves character with CharacterController.
    /// Disables movement during challenges (first-person mode is handled separately).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = GameConstants.Player.MOVE_SPEED;
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _gravity = GameConstants.Player.GRAVITY;

        [Header("Interaction")]
        [SerializeField] private float _interactionRange = GameConstants.Player.INTERACTION_RANGE;
        [SerializeField] private LayerMask _interactableLayer;

        // ─────────────────────────────────────────────
        // Components
        // ─────────────────────────────────────────────

        private CharacterController _characterController;
        private Transform _cameraTransform;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private Vector3 _velocity;
        private bool _movementEnabled = true;
        private bool _isInChallenge = false;

        public bool IsGrounded => _characterController != null && _characterController.isGrounded;
        public bool IsMoving => InputManager.Instance != null && InputManager.Instance.MoveInput.magnitude > 0.1f;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // Find main camera (will be managed by CameraController)
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void Update()
        {
            if (!_movementEnabled) return;

            HandleMovement();
            HandleGravity();

            // Allow interaction during walk-around challenges too
            HandleInteraction();
        }

        // ─────────────────────────────────────────────
        // Movement
        // ─────────────────────────────────────────────

        private void HandleMovement()
        {
            if (InputManager.Instance == null || _characterController == null) return;

            // Handle 1P camera-based rotation first, regardless of movement
            if (_isInChallenge && _cameraTransform != null)
            {
                // 1P Mode: Character always faces where the camera is looking
                Vector3 camForward = _cameraTransform.forward;
                camForward.y = 0f; // Keep character upright
                if (camForward.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(camForward.normalized);
                }
            }

            Vector2 moveInput = InputManager.Instance.MoveInput;
            
            // Skip actual translation if not moving
            if (moveInput.magnitude < 0.1f) return;

            // Calculate movement direction relative to camera
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_cameraTransform != null)
            {
                forward = _cameraTransform.forward;
                right = _cameraTransform.right;
            }

            // Flatten to horizontal plane
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            // Calculate movement direction
            Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x).normalized;

            // Move
            _characterController.Move(moveDirection * _moveSpeed * Time.deltaTime);

            // 3P Mode: Character faces movement direction
            if (!_isInChallenge && moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    _rotationSpeed * Time.deltaTime
                );
            }
        }

        private void HandleGravity()
        {
            if (_characterController == null) return;

            if (_characterController.isGrounded && _velocity.y < 0)
            {
                _velocity.y = -2f; // Small downward force to keep grounded
            }

            _velocity.y += _gravity * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);
        }

        // ─────────────────────────────────────────────
        // Interaction
        // ─────────────────────────────────────────────

        private void HandleInteraction()
        {
            if (InputManager.Instance == null || !InputManager.Instance.InteractPressed) return;

            // Use camera for raycasting if available, otherwise fallback to player body
            Ray ray;
            if (_cameraTransform != null)
            {
                ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
            }
            else
            {
                ray = new Ray(transform.position + Vector3.up, transform.forward);
            }

            Debug.DrawRay(ray.origin, ray.direction * _interactionRange, Color.red, 2.0f);
            Debug.Log($"[PlayerController] 'E' Pressed. Raycasting from {ray.origin} direction {ray.direction}");

            if (Physics.Raycast(ray, out RaycastHit hit, _interactionRange, _interactableLayer))
            {
                Debug.Log($"[PlayerController] Raycast hit: {hit.collider.name} on Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                
                // Avoid hitting our own player capsule
                if (hit.collider.transform.root == transform)
                {
                    Debug.Log("[PlayerController] Raycast hit the player's own body. Ignoring.");
                    return;
                }

                // Try to get IInteractable from hit object
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    Debug.Log($"[PlayerController] Found IInteractable on {hit.collider.name}, triggering Interact()!");
                    interactable.Interact();
                }
                else
                {
                    Debug.Log($"[PlayerController] Object {hit.collider.name} does not have an IInteractable component.");
                }
            }
            else
            {
                Debug.Log("[PlayerController] Raycast hit nothing.");
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Teleport player to a specific position.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }

        /// <summary>
        /// Enable or disable player movement.
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            _isInChallenge = true;

            // Some challenges still need player movement (walk to bins, etc.)
            _movementEnabled = IsMoveableChallenge(evt.Type);
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            _isInChallenge = false;
            _movementEnabled = true;
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            _isInChallenge = false;
            _movementEnabled = true;
        }

        /// <summary>
        /// Returns true for challenge types where the player needs
        /// to walk around (3P stays active). Returns false for
        /// stationary challenges (lock movement, switch to 1P).
        /// </summary>
        private bool IsMoveableChallenge(ChallengeType type)
        {
            switch (type)
            {
                case ChallengeType.WasteSegregation:
                case ChallengeType.CoralReplanting:
                case ChallengeType.WaterSampling:
                case ChallengeType.PollutionScan:
                case ChallengeType.HazardDefusal:
                case ChallengeType.GhostNetRescue:
                    return true;

                // Stationary 1P challenges
                case ChallengeType.SustainableFishing:
                case ChallengeType.FrequencyMatching:
                case ChallengeType.TrenchDescent:
                case ChallengeType.RitualOffering:
                default:
                    return false;
            }
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _movementEnabled = evt.NewState == GameState.Exploring;
        }
    }

    /// <summary>
    /// Interface for objects the player can interact with.
    /// </summary>
    public interface IInteractable
    {
        void Interact();
        string GetInteractionPrompt();
    }
}
