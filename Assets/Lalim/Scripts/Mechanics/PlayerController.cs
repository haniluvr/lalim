using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.UI;

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
        public static PlayerController Instance { get; private set; }

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
        private IInteractable _currentLookTarget;
        private string _currentPromptText = "";
        public string CurrentPromptText => _currentPromptText;

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
            Instance = this;
            _characterController = GetComponent<CharacterController>();
            _targetYaw = transform.rotation;

            // Auto-fallback if LayerMask is unassigned (0 / Nothing) in Inspector
            if (_interactableLayer.value == 0)
            {
                int interactableLayer = LayerMask.NameToLayer("Interactable");
                int defaultLayer = LayerMask.NameToLayer("Default");
                int mask = 0;
                if (interactableLayer != -1) mask |= (1 << interactableLayer);
                if (defaultLayer != -1) mask |= (1 << defaultLayer);
                _interactableLayer = mask != 0 ? mask : ~0;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            if (Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
            else
            {
                CameraController cam = FindObjectOfType<CameraController>();
                if (cam != null)
                {
                    _cameraTransform = cam.transform;
                }
                else
                {
                    Camera anyCam = FindObjectOfType<Camera>();
                    if (anyCam != null) _cameraTransform = anyCam.transform;
                }
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
            UpdateInteractionPrompt();
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
        // Interaction & Prompts
        // ─────────────────────────────────────────────

        /// <summary>
        /// Updates the current interactable in sight and syncs prompt to HUD / GUI.
        /// </summary>
        private void UpdateInteractionPrompt()
        {
            _currentLookTarget = FindInteractableInSight();

            if (_currentLookTarget != null)
            {
                _currentPromptText = _currentLookTarget.GetInteractionPrompt();
                if (HUDController.Instance != null)
                {
                    HUDController.Instance.ShowInteractionPrompt(_currentPromptText);
                }
            }
            else
            {
                _currentPromptText = "";
                if (HUDController.Instance != null)
                {
                    HUDController.Instance.SetInteractionPromptVisible(false);
                }
            }
        }

        private void HandleInteraction()
        {
            // Allow input from InputManager OR direct KeyDown fallback
            bool interactPressed = (InputManager.Instance != null && InputManager.Instance.InteractPressed)
                                   || Input.GetKeyDown(KeyCode.E);

            if (!interactPressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                interactPressed = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
            }

            if (!interactPressed) return;

            IInteractable target = _currentLookTarget ?? FindInteractableInSight();
            if (target != null)
            {
                Debug.Log($"[PlayerController] Interacting with: {target.GetType().Name}");
                target.Interact();
                return;
            }

            Debug.Log("[PlayerController] 'E' Pressed - no interactable object in front of player.");
        }

        /// <summary>
        /// Finds the closest interactable in sight or in front of the player.
        /// </summary>
        private IInteractable FindInteractableInSight()
        {
            // Make sure camera reference is valid
            if (_cameraTransform == null)
            {
                if (Camera.main != null) _cameraTransform = Camera.main.transform;
                else
                {
                    CameraController cam = FindObjectOfType<CameraController>();
                    if (cam != null) _cameraTransform = cam.transform;
                }
            }

            // Determine ray origin & direction
            Vector3 rayOrigin;
            Vector3 rayDirection;

            if (_cameraTransform != null)
            {
                rayDirection = _cameraTransform.forward;
                float camDistance = Vector3.Distance(_cameraTransform.position, transform.position);

                if (camDistance < 1.0f)
                {
                    rayOrigin = _cameraTransform.position + rayDirection * 0.1f;
                }
                else
                {
                    rayOrigin = transform.position + Vector3.up * 0.8f + rayDirection * 0.3f;
                }
            }
            else
            {
                rayOrigin = transform.position + Vector3.up * 0.8f;
                rayDirection = transform.forward;
            }

            int interactableLayerIndex = LayerMask.NameToLayer("Interactable");
            int defaultLayerIndex = LayerMask.NameToLayer("Default");
            int requiredBits = (1 << defaultLayerIndex);
            if (interactableLayerIndex != -1) requiredBits |= (1 << interactableLayerIndex);

            LayerMask mask = _interactableLayer.value != 0 
                ? (_interactableLayer.value | requiredBits)
                : ~0;

            Debug.DrawRay(rayOrigin, rayDirection * _interactionRange, Color.cyan, 0.1f);

            // 1. RaycastAll and SphereCastAll combined
            List<RaycastHit> allHits = new List<RaycastHit>();
            allHits.AddRange(Physics.RaycastAll(rayOrigin, rayDirection, _interactionRange, mask, QueryTriggerInteraction.Collide));
            allHits.AddRange(Physics.SphereCastAll(rayOrigin, 0.45f, rayDirection, _interactionRange, mask, QueryTriggerInteraction.Collide));
            allHits.Sort((a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in allHits)
            {
                if (hit.collider.transform.root == transform) continue;

                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    return interactable;
                }
            }

            // 2. Proximity Assist Check around diver
            Collider[] closeColliders = Physics.OverlapSphere(transform.position + Vector3.up * 0.5f, 3.0f, mask, QueryTriggerInteraction.Collide);
            IInteractable bestTarget = null;
            float closestDist = float.MaxValue;

            foreach (Collider col in closeColliders)
            {
                if (col.transform.root == transform) continue;

                IInteractable interactable = col.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    Vector3 toTarget = col.transform.position - transform.position;
                    float dist = toTarget.sqrMagnitude;
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = interactable;
                    }
                }
            }

            return bestTarget;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_currentPromptText)) return;

            // Render on-screen prompt overlay (works even if HUD Canvas isn't set up yet)
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;

            GUIContent content = new GUIContent(_currentPromptText);
            Vector2 size = style.CalcSize(content);
            float width = Mathf.Max(size.x + 40f, 260f);
            float height = 44f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 110f;

            GUI.backgroundColor = new Color(0.04f, 0.12f, 0.22f, 0.85f);
            GUI.Box(new Rect(x, y, width, height), _currentPromptText, style);
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
            if (evt.NewState == GameState.Exploring)
            {
                _movementEnabled = true;
            }
            else if (evt.NewState == GameState.InChallenge)
            {
                if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive)
                {
                    _movementEnabled = IsMoveableChallenge(ChallengeManager.Instance.ActiveChallengeType);
                }
                else
                {
                    _movementEnabled = true;
                }
            }
            else
            {
                _movementEnabled = false;
            }
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
