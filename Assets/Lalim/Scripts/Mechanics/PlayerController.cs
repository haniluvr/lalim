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

        // Speed multipliers registered by abilities (Riptide, Tidebond). Highest active value wins —
        // multipliers never stack multiplicatively, even if two sources are active at once due to a bug.
        private readonly Dictionary<object, float> _speedMultipliers = new Dictionary<object, float>();

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
        // True only for FIRST-person challenges. A 3rd-person challenge (trash collection) keeps
        // full exploration movement, facing and auto-swim.
        private bool _isInChallenge = false;
        private IInteractable _currentLookTarget;
        private string _currentPromptText = "";
        public string CurrentPromptText => _currentPromptText;

        /// <summary>The interactable the player is currently targeting (drives the HUD action button).</summary>
        public IInteractable CurrentInteractable => string.IsNullOrEmpty(_currentPromptText) ? null : _currentLookTarget;

        /// <summary>True while a HUD or floating prompt is drawing the target, so the debug OnGUI prompt stays off.</summary>
        public static bool HudPromptPresent => _hudPromptCount > 0;
        private static int _hudPromptCount;

        public static void RegisterHudPrompt(bool present) => _hudPromptCount = Mathf.Max(0, _hudPromptCount + (present ? 1 : -1));

        // Yaw the diver's body turns toward: the direction they last swam (or dashed).
        private float _facingYaw;
        private bool _facingInitialized;

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
                CameraController cam = FindAnyObjectByType<CameraController>();
                if (cam != null)
                {
                    _cameraTransform = cam.transform;
                }
                else
                {
                    Camera anyCam = FindAnyObjectByType<Camera>();
                    if (anyCam != null) _cameraTransform = anyCam.transform;
                }
            }

            // Initialize movement state based on current game state, in case we missed the event
            if (Core.GameManager.Instance != null)
            {
                var state = Core.GameManager.Instance.CurrentState;
                _movementEnabled = (state == Core.GameState.Exploring || state == Core.GameState.InChallenge);
            }
            else
            {
                _movementEnabled = true; // Fallback: always allow movement if no GameManager
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
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

            if (!_facingInitialized) SyncFacingToTransform();

            // --- Movement basis: the CAMERA ---
            // The joystick moves relative to where the camera looks: up = away from the camera,
            // down = toward it, left/right = sideways, diagonals = diagonal.
            //  - BehindBack camera: the body keeps facing the camera direction, so sideways and
            //    diagonal input strafe and you always see the diver's back.
            //  - FreeOrbit camera: the diver turns to face the way they swim.
            float cameraYaw = CameraController.Instance != null
                ? CameraController.Instance.OrbitYaw
                : (_cameraTransform != null ? _cameraTransform.eulerAngles.y : transform.eulerAngles.y);
            Quaternion basis = Quaternion.Euler(0f, cameraYaw, 0f);

            Vector3 camForward = basis * Vector3.forward;
            Vector3 camRight = basis * Vector3.right;

            // Keep the camera's pitch so looking up/down still swims diagonally.
            if (_cameraTransform != null)
            {
                float pitchY = Mathf.Clamp(_cameraTransform.forward.y, -0.95f, 0.95f);
                camForward = camForward * Mathf.Sqrt(1f - pitchY * pitchY) + Vector3.up * pitchY;
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

            // Settings ▸ Auto-Swim: keep swimming forward while the joystick is idle
            // (exploring only — never inside challenges or while input is taken away).
            if (moveInput.sqrMagnitude <= 0.01f && SettingsManager.Active.autoSwim && !_isInChallenge &&
                InputManager.Instance.InputEnabled &&
                GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Exploring)
            {
                moveInput = Vector2.up;
            }

            float verticalInput = InputManager.Instance.VerticalMoveInput; // +1 = Space (Up), -1 = Ctrl/C (Down)
            // Fix: removed the free LeftShift sprint multiplier — Shift now drives
            // Surge (tap) / Riptide (hold) via AbilityController, which calls
            // SetSpeedMultiplier(this, ...) instead of a hardcoded sprint flag.
            float currentSwimSpeed = _moveSpeed * GetHighestSpeedMultiplier();

            // --- Compute Target 3D Velocity ---
            Vector3 targetVelocity = Vector3.zero;

            // Camera-relative swimming (diagonals included).
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 moveDir = Vector3.ClampMagnitude(camForward * moveInput.y + camRight * moveInput.x, 1f);
                targetVelocity += moveDir * currentSwimSpeed;

                Vector3 flat = new Vector3(moveDir.x, 0f, moveDir.z);
                if (flat.sqrMagnitude > 0.0004f && !_isInChallenge && !BodyLockedToCamera)
                    _facingYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
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

            // --- Fluid Acceleration & Water Drag (overridden while dashing) ---
            if (IsDashing)
            {
                _currentVelocity = TickDash();
            }
            else if (targetVelocity.sqrMagnitude > 0.01f)
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
            // Vertical pitch: tilt diver body up when ascending, down when descending
            // (swim motion only — looking up/down never pitches the body).
            float normalizedVertical = Mathf.Clamp(_currentVelocity.y / _swimVerticalSpeed, -1f, 1f);
            float targetPitch = -normalizedVertical * _swimPitchTilt;
            _currentPitchAngle = Mathf.Lerp(_currentPitchAngle, targetPitch, _pitchSmoothSpeed * Time.deltaTime);

            if (BodyLockedToCamera)
            {
                // BehindBack: yaw is exactly the camera's — no turning lag, the back always faces the camera.
                _facingYaw = CameraController.Instance.OrbitYaw;
                ApplyBodyRotation();
                return;
            }

            // FreeOrbit: turn smoothly toward the direction the diver last swam (kept while idle).
            _targetYaw = Quaternion.Euler(0f, _facingYaw, 0f);
            Quaternion desiredRotation = _targetYaw * Quaternion.Euler(_currentPitchAngle, 0f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, _rotationSpeed * Time.deltaTime);
        }

        /// <summary>True when the camera owns the body's yaw (BehindBack style, third-person, not in a challenge).</summary>
        private bool BodyLockedToCamera =>
            !_isInChallenge && CameraController.Instance != null && CameraController.Instance.LocksBodyToCamera;

        private void ApplyBodyRotation() =>
            transform.rotation = Quaternion.Euler(0f, _facingYaw, 0f) * Quaternion.Euler(_currentPitchAngle, 0f, 0f);

        /// <summary>
        /// Called by CameraController right after it applies look input, so the body turns in the
        /// same frame as the camera (BehindBack style). Pitch from swimming is kept.
        /// </summary>
        public void SnapBodyYaw(float yaw)
        {
            if (!BodyLockedToCamera || !_movementEnabled) return;
            _facingYaw = yaw;
            _facingInitialized = true;
            ApplyBodyRotation();
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
                    CameraController cam = FindAnyObjectByType<CameraController>();
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
                // Skip things with nothing to offer right now (e.g. the coral station sharing the
                // water station's spot): otherwise they hide the one that's actually active.
                if (interactable != null && HasPrompt(interactable))
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
                if (interactable != null && HasPrompt(interactable))
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

        private static bool HasPrompt(IInteractable interactable) => !string.IsNullOrEmpty(interactable.GetInteractionPrompt());

        private void OnGUI()
        {
            if (HudPromptPresent || string.IsNullOrEmpty(_currentPromptText)) return;

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
            // Sit above the bottom-centre HP bar + stamina orbs (~150px tall at 1080p).
            float y = Screen.height - (44f + 170f) * (Screen.height / 1080f);

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

        /// <summary>Makes the diver keep facing its current rotation (after a teleport / respawn / cutscene).</summary>
        public void SyncFacingToTransform()
        {
            _facingYaw = transform.eulerAngles.y;
            _facingInitialized = true;
        }

        /// <summary>Current world-space velocity, exposed for abilities (Bubble Barrier proximity checks, VFX, etc).</summary>
        public Vector3 Velocity => _currentVelocity;

        /// <summary>True while riding the dugong via Tidebond.</summary>
        public bool IsMounted { get; private set; }

        /// <summary>True while player-driven movement is currently allowed.</summary>
        public bool IsMovementEnabled => _movementEnabled;

        /// <summary>
        /// Registers a speed multiplier from an ability (Riptide, Tidebond). Multipliers never
        /// stack multiplicatively — the HIGHEST currently-registered value wins, so Riptide and
        /// Tidebond can never combine into an unintended super-speed even if both are active
        /// due to a bug elsewhere.
        /// </summary>
        public void SetSpeedMultiplier(object source, float value)
        {
            _speedMultipliers[source] = value;
        }

        /// <summary>Removes a previously-registered speed multiplier. Call when an ability ends or is cancelled.</summary>
        public void ClearSpeedMultiplier(object source)
        {
            _speedMultipliers.Remove(source);
        }

        private float GetHighestSpeedMultiplier()
        {
            float highest = 1.0f;
            foreach (var kvp in _speedMultipliers)
            {
                if (kvp.Value > highest) highest = kvp.Value;
            }
            return highest;
        }

        // Dash state — while dashing, HandleUnderwaterMovement uses the dash velocity instead of
        // the normal acceleration/drag, but still moves through the CharacterController so walls
        // and UnderwaterBounds clamping keep working.
        private Vector3 _dashDir;
        private float _dashDuration;
        private float _dashElapsed;
        private float _dashPeakSpeed;
        private float _dashExitSpeed;

        public bool IsDashing => _dashDuration > 0f;

        /// <summary>
        /// Dashes 'distance' meters along world-space 'dir' over 'duration' seconds with a linear
        /// ease-out. When the dash ends, the player keeps exitVelocityRetain × the dash's AVERAGE
        /// speed as glide velocity so it flows into normal swimming instead of stopping dead.
        /// </summary>
        public void Dash(Vector3 dir, float distance, float duration, float exitVelocityRetain = 0.4f)
        {
            if (!_movementEnabled) return;
            if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

            duration = Mathf.Max(duration, 0.01f);
            _dashDir = dir.normalized;
            if (new Vector2(_dashDir.x, _dashDir.z).sqrMagnitude > 0.0004f)
                _facingYaw = Mathf.Atan2(_dashDir.x, _dashDir.z) * Mathf.Rad2Deg;
            _dashDuration = duration;
            _dashElapsed = 0f;
            _dashPeakSpeed = 2f * distance / duration; // linear ease-out: avg = peak / 2
            _dashExitSpeed = (distance / duration) * exitVelocityRetain;
        }

        public void CancelDash()
        {
            _dashDuration = 0f;
        }

        /// <summary>Advances the dash and returns the velocity to use this frame.</summary>
        private Vector3 TickDash()
        {
            _dashElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_dashElapsed / _dashDuration);
            if (t >= 1f)
            {
                _dashDuration = 0f;
                return _dashDir * _dashExitSpeed;
            }
            return _dashDir * (_dashPeakSpeed * (1f - t));
        }

        /// <summary>
        /// Mounts/dismounts the player onto the dugong. The player's own CharacterController
        /// keeps doing all the movement (reusing already-tuned collisions/bounds) — the dugong
        /// model just snaps to a seat offset under the player and plays its swim animation.
        /// </summary>
        public void SetMounted(bool mounted)
        {
            if (IsMounted == mounted) return;
            IsMounted = mounted;
            EventBus.Publish(new DugongMountChangedEvent { Mounted = mounted });
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
                CancelDash();
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            _isInChallenge = evt.Perspective == ChallengePerspective.FirstPerson;
            _movementEnabled = IsMoveableChallenge(evt.Type);
            if (!_movementEnabled) CancelDash();
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

        /// <summary>True for challenges where the player swims freely (abilities that move the player are allowed).</summary>
        public static bool IsMoveableChallenge(ChallengeType type)
        {
            switch (type)
            {
                case ChallengeType.WasteSegregation:
                case ChallengeType.CoralReplanting:
                case ChallengeType.WaterSampling:
                case ChallengeType.PollutionScan:
                case ChallengeType.HazardDefusal:
                case ChallengeType.GhostNetRescue:
                case ChallengeType.TrashCollection:
                    return true;

                case ChallengeType.SustainableFishing:
                case ChallengeType.FrequencyMatching:
                case ChallengeType.TrenchDescent:
                case ChallengeType.RitualOffering:
                default:
                    return false;
            }
        }

        private void OnPlayerRespawned(PlayerRespawnedEvent evt) => SyncFacingToTransform();

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
