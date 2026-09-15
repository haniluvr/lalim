using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Player character controller for underwater swimming and exploration.
    /// Supports:
    /// - 3D diagonal swimming oriented to camera pitch (critical for mobile virtual joystick).
    /// - Direct vertical controls (Space to ascend, Control/C to descend).
    /// - Neutral buoyancy and fluid water drag / deceleration.
    /// - Smooth diver body pitch and yaw tilting.
    /// - Free-look camera orbiting (Mouse button 4).
    /// - Seamless boundary collisions and scene confinement via UnderwaterBounds.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Underwater Swimming")]
        [Tooltip("Horizontal swimming speed")]
        [SerializeField] private float _moveSpeed = GameConstants.Player.MOVE_SPEED;

        [Tooltip("Vertical swim speed when pressing Space (ascend) or Control/C (descend)")]
        [SerializeField] private float _swimVerticalSpeed = 3.5f;

        [Tooltip("Speed multiplier when holding sprint (LeftShift)")]
        [SerializeField] private float _sprintMultiplier = 1.5f;

        [Tooltip("Acceleration rate to reach target swim speed (fluid buildup)")]
        [SerializeField] private float _swimAcceleration = 8.0f;

        [Tooltip("Water resistance / drag deceleration rate when releasing inputs (glide to stop)")]
        [SerializeField] private float _waterDrag = 5.0f;

        [Tooltip("When true, diver hovers at current depth when no vertical keys are pressed")]
        [SerializeField] private bool _neutralBuoyancy = true;

        [Tooltip("Subtle vertical drift (positive = float up, negative = sink down). 0 = perfect hover")]
        [SerializeField] private float _buoyancyDrift = 0.0f;

        [Tooltip("When camera points up/down, forward movement swims diagonally in 3D (essential for mobile)")]
        [SerializeField] private bool _pitchSwimming = true;

        [Header("Diver Orientation")]
        [Tooltip("Turn speed towards horizontal movement direction")]
        [SerializeField] private float _rotationSpeed = 8.0f;

        [Tooltip("Degrees diver body pitches up/down during ascend/descend")]
        [SerializeField] private float _swimPitchTilt = 25.0f;

        [Tooltip("Smoothing speed for body pitch transitions")]
        [SerializeField] private float _pitchSmoothSpeed = 5.0f;

        [Header("Interaction")]
        [SerializeField] private float _interactionRange = GameConstants.Player.INTERACTION_RANGE;
        [SerializeField] private LayerMask _interactableLayer;

        // ─────────────────────────────────────────────
        // Components & References
        // ─────────────────────────────────────────────

        private CharacterController _characterController;
        private Transform _cameraTransform;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private Vector3 _currentVelocity;
        private float _currentPitchAngle;
        private Quaternion _targetYaw = Quaternion.identity;

        private bool _movementEnabled = true;
        private bool _isInChallenge = false;

        // Free look heading lock
        private bool _wasFreeLooking;
        private Vector3 _freeLookForward;
        private Vector3 _freeLookRight;

        public bool IsGrounded => _characterController != null && _characterController.isGrounded;
        public bool IsMoving => _currentVelocity.sqrMagnitude > 0.05f || 
                                (InputManager.Instance != null && (InputManager.Instance.MoveInput.sqrMagnitude > 0.01f || Mathf.Abs(InputManager.Instance.VerticalMoveInput) > 0.01f));

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _targetYaw = transform.rotation;
        }

        private void Start()
        {
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

            HandleUnderwaterMovement();
            HandleInteraction();
        }

        // ─────────────────────────────────────────────
        // Underwater Movement
        // ─────────────────────────────────────────────

        private void HandleUnderwaterMovement()
        {
            if (InputManager.Instance == null || _characterController == null) return;

            // --- 1P Challenge Mode: Character faces camera look direction ---
            if (_isInChallenge && _cameraTransform != null)
            {
                Vector3 camForwardFlat = _cameraTransform.forward;
                camForwardFlat.y = 0f;
                if (camForwardFlat.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(camForwardFlat.normalized);
                }
            }

            // --- Free Look Heading Management ---
            bool isFreeLooking = InputManager.Instance.IsFreeLooking;
            if (isFreeLooking)
            {
                if (!_wasFreeLooking)
                {
                    // Snapshot camera heading at the instant free-look begins
                    _freeLookForward = _cameraTransform != null ? _cameraTransform.forward : transform.forward;
                    _freeLookRight = _cameraTransform != null ? _cameraTransform.right : transform.right;
                    _wasFreeLooking = true;
                }
            }
            else
            {
                _wasFreeLooking = false;
            }

            // --- Determine Movement Reference Vectors ---
            Vector3 camForward;
            Vector3 camRight;

            if (_wasFreeLooking)
            {
                camForward = _freeLookForward;
                camRight = _freeLookRight;
            }
            else if (_cameraTransform != null)
            {
                camForward = _cameraTransform.forward;
                camRight = _cameraTransform.right;
            }
            else
            {
                camForward = transform.forward;
                camRight = transform.right;
            }

            // Strafe is always purely horizontal
            camRight.y = 0f;
            camRight.Normalize();

            // Diagonal forward swimming:
            // When _pitchSwimming is true, camForward keeps its 3D pitch (Y axis).
            // Looking up swims diagonally up; looking down swims diagonally down.
            if (!_pitchSwimming)
            {
                camForward.y = 0f;
                camForward.Normalize();
            }

            // --- Read Inputs ---
            Vector2 moveInput = InputManager.Instance.MoveInput;
            float verticalInput = InputManager.Instance.VerticalMoveInput; // +1 = Space (Up), -1 = Ctrl/C (Down)
            bool isSprinting = InputManager.Instance.SwimSprintHeld;

            float currentSwimSpeed = _moveSpeed * (isSprinting ? _sprintMultiplier : 1.0f);

            // --- Compute Target 3D Velocity ---
            Vector3 targetVelocity = Vector3.zero;

            // Forward/backward and strafe swimming
            if (moveInput.sqrMagnitude > 0.01f)
            {
                targetVelocity += (camForward * moveInput.y + camRight * moveInput.x) * currentSwimSpeed;
            }

            // Direct vertical thrust (Space / Control / C)
            if (Mathf.Abs(verticalInput) > 0.01f)
            {
                targetVelocity.y += verticalInput * _swimVerticalSpeed;
            }

            // Buoyancy drift (0 if neutral)
            if (!_neutralBuoyancy)
            {
                targetVelocity.y += _buoyancyDrift;
            }

            // --- Fluid Acceleration & Water Drag ---
            if (targetVelocity.sqrMagnitude > 0.01f)
            {
                _currentVelocity = Vector3.MoveTowards(_currentVelocity, targetVelocity, _swimAcceleration * Time.deltaTime);
            }
            else
            {
                // Smooth glide deceleration from water resistance
                _currentVelocity = Vector3.MoveTowards(_currentVelocity, Vector3.zero, _waterDrag * Time.deltaTime);
            }

            // --- Ocean Currents Pushback near Boundaries ---
            if (UnderwaterBounds.Instance != null)
            {
                Vector3 currentPush = UnderwaterBounds.Instance.GetPushbackForce(transform.position);
                _currentVelocity += currentPush * Time.deltaTime;
            }

            // --- Apply Motion to CharacterController ---
            _characterController.Move(_currentVelocity * Time.deltaTime);

            // Cancel accumulated downward velocity when touching seabed
            if (_characterController.isGrounded && _currentVelocity.y < 0f)
            {
                _currentVelocity.y = 0f;
            }

            // --- Hard Clamp within Underwater Bounds (Safety Safeguard) ---
            if (UnderwaterBounds.Instance != null)
            {
                Vector3 clampedPos = UnderwaterBounds.Instance.ClampPosition(transform.position, _characterController.radius);
                if ((clampedPos - transform.position).sqrMagnitude > 0.0001f)
                {
                    transform.position = clampedPos;
                }
            }

            // --- Diver Body Rotation & Pitch/Roll Tilting ---
            if (!_isInChallenge)
            {
                UpdateDiverOrientation();
            }
        }

        private void UpdateDiverOrientation()
        {
            // Horizontal yaw: align with movement direction
            Vector3 horizontalVel = new Vector3(_currentVelocity.x, 0f, _currentVelocity.z);

            if (horizontalVel.sqrMagnitude > 0.1f && !_wasFreeLooking)
            {
                _targetYaw = Quaternion.LookRotation(horizontalVel.normalized, Vector3.up);
            }

            // Vertical pitch: tilt diver body up when ascending, down when descending
            float normalizedVertical = Mathf.Clamp(_currentVelocity.y / _swimVerticalSpeed, -1f, 1f);
            float targetPitch = -normalizedVertical * _swimPitchTilt;

            _currentPitchAngle = Mathf.Lerp(_currentPitchAngle, targetPitch, _pitchSmoothSpeed * Time.deltaTime);
            Quaternion pitchRotation = Quaternion.Euler(_currentPitchAngle, 0f, 0f);

            // Combine yaw and pitch smoothly
            Quaternion desiredRotation = _targetYaw * pitchRotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, _rotationSpeed * Time.deltaTime);
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
            _currentVelocity = Vector3.zero;
        }

        /// <summary>
        /// Enable or disable player movement.
        /// </summary>
        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;
            if (!enabled)
            {
                _currentVelocity = Vector3.zero;
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            _isInChallenge = true;
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
