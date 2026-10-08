using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Dual-perspective camera controller.
    ///
    /// Third-person uses a spring arm: a pivot at shoulder height on the player, an arm of fixed
    /// length pointing back along the view, and a small socket offset (slightly above). The arm
    /// shortens when something is in the way and eases back out afterwards.
    ///
    /// Two styles (Inspector ▸ Style):
    ///  - BehindBack (default): you always see the diver's back. Horizontal look input (swipe,
    ///    mouse, right stick, gyro) turns the diver's body (yaw) and the camera together; vertical
    ///    look only tilts the camera (clamped). The joystick moves relative to the camera, so
    ///    diagonals and sideways input strafe while the body keeps facing where the camera points.
    ///  - FreeOrbit: swipe orbits the camera freely; the diver turns to face the way they swim and
    ///    the camera eases back behind them while swimming.
    ///
    /// First-person (challenges): locks to the player's head, look input controls the view.
    /// Smooth lerp transition between modes when challenges start/end.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        public enum CameraStyle { BehindBack, FreeOrbit }

        [Header("Style")]
        [Tooltip("BehindBack: look input turns the diver, camera always behind their back.\nFreeOrbit: camera orbits freely, diver faces the way they swim.")]
        [SerializeField] private CameraStyle _style = CameraStyle.BehindBack;

        [Header("Target")]
        [SerializeField] private Transform _playerTarget;

        [Header("Third-Person Spring Arm")]
        [Tooltip("Arm length: how far behind the pivot the camera trails.")]
        [SerializeField] private float _orbitDistance = 4.7f;
        [Tooltip("Pivot height above the player's origin (about shoulder height).")]
        [SerializeField] private float _orbitHeight = 1.0f;
        [Tooltip("Offset at the end of the arm, in camera space (x = right, y = up). Slightly above the shoulders.")]
        [SerializeField] private Vector3 _socketOffset = new Vector3(0f, 0.35f, 0f);
        [Tooltip("Radius of the collision probe that shortens the arm when something is in the way.")]
        [SerializeField] private float _probeRadius = 0.25f;
        [Tooltip("How fast (m/s) the arm extends back out after an obstacle clears.")]
        [SerializeField] private float _armRecoverSpeed = 6f;
        [Tooltip("What the arm collides with (triggers are always ignored).")]
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField] private float _orbitSmoothSpeed = 8.0f;
        [SerializeField] private float _minVerticalAngle = -20f;
        [SerializeField] private float _maxVerticalAngle = 60f;

        [Header("First-Person Settings")]
        [SerializeField] private Vector3 _firstPersonOffset = new Vector3(0f, 1.7f, 0f); // Eye height
        [SerializeField] private float _fpMinVerticalAngle = -80f;
        [SerializeField] private float _fpMaxVerticalAngle = 80f;

        [Header("Look Sensitivity")]
        [SerializeField] private float _horizontalSensitivity = GameConstants.Player.LOOK_SENSITIVITY;
        [SerializeField] private float _verticalSensitivity = GameConstants.Player.LOOK_SENSITIVITY;

        [Header("Transition")]
        [SerializeField] private float _transitionDuration = 0.5f;

        [Header("Orbit & follow (FreeOrbit style)")]
        [Tooltip("Camera degrees per degree the phone turns (Settings ▸ Gyroscope).")]
        [SerializeField] private float _gyroStrength = 1f;
        [Tooltip("How fast (°/s at full swim speed) the camera eases behind a swimming player. 0 = never.")]
        [SerializeField] private float _autoFollowSpeed = 25f;
        [Tooltip("Seconds after the last swipe before the camera starts easing behind the player.")]
        [SerializeField] private float _autoFollowDelay = 1.2f;
        [Tooltip("How quickly the camera moves to a requested extra distance (e.g. Bubble Barrier).")]
        [SerializeField] private float _extraDistanceSpeed = 3f;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public enum CameraMode { ThirdPerson, FirstPerson }
        public CameraMode CurrentMode { get; private set; } = CameraMode.ThirdPerson;

        private float _horizontalAngle = 0f; // camera orbit yaw around the player
        private float _verticalAngle = 15f;
        private bool _headingInitialized;
        private float _lastManualLookTime = -10f;

        private readonly System.Collections.Generic.Dictionary<object, float> _extraDistanceRequests = new System.Collections.Generic.Dictionary<object, float>();
        private float _currentExtraDistance;

        /// <summary>Camera yaw (degrees). The joystick moves the player relative to this.</summary>
        public float OrbitYaw => _horizontalAngle;

        public CameraStyle Style => _style;

        /// <summary>True when the diver's body yaw is locked to the camera (BehindBack, third-person).</summary>
        public bool LocksBodyToCamera => _style == CameraStyle.BehindBack && CurrentMode == CameraMode.ThirdPerson && !_isTransitioning;

        private float _armLength = -1f; // current (collision-shortened) arm length

        // Transition state
        private bool _isTransitioning = false;
        private float _transitionProgress = 0f;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private CameraMode _transitionTargetMode;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // The camera prefab can't reference the scene's player; find it if the scene didn't wire it.
            if (_playerTarget == null && PlayerController.Instance != null) _playerTarget = PlayerController.Instance.transform;
            SyncHeadingToPlayer();
        }

        /// <summary>Puts the camera behind wherever the player currently faces.</summary>
        public void SyncHeadingToPlayer()
        {
            if (_playerTarget == null) return;
            _horizontalAngle = _playerTarget.eulerAngles.y;
            _headingInitialized = true;
        }

        /// <summary>Points the first-person view at a world point (e.g. a minigame's table).</summary>
        public void AimFirstPersonAt(Vector3 point)
        {
            if (_playerTarget == null) return;
            Vector3 to = point - _playerTarget.TransformPoint(_firstPersonOffset);
            if (to.sqrMagnitude < 0.0001f) return;
            _horizontalAngle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float flat = new Vector2(to.x, to.z).magnitude;
            _verticalAngle = Mathf.Clamp(-Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg, _fpMinVerticalAngle, _fpMaxVerticalAngle);
            _headingInitialized = true;
        }

        /// <summary>Pulls the camera back by 'extraMeters' while 'source' asks for it (largest request wins).</summary>
        public void SetExtraDistance(object source, float extraMeters) => _extraDistanceRequests[source] = extraMeters;

        public void ClearExtraDistance(object source) => _extraDistanceRequests.Remove(source);

        private void OnEnable()
        {
            EventBus.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        // ─────────────────────────────────────────────
        // Dialogue shot (talking to a statue)
        // ─────────────────────────────────────────────

        private Transform _shot;

        /// <summary>
        /// Holds the camera on a fixed framing (e.g. a statue conversation) instead of following the
        /// player. <paramref name="snap"/> cuts straight to it (behind a fade); otherwise it eases there.
        /// </summary>
        public void SetShot(Transform shot, bool snap = false)
        {
            _shot = shot;
            if (snap && shot != null) transform.SetPositionAndRotation(shot.position, shot.rotation);
        }

        /// <summary>Back to following the player, from behind wherever they now face.</summary>
        public void ClearShot(bool snap = false)
        {
            if (_shot == null) return;
            _shot = null;
            SyncHeadingToPlayer();
            _verticalAngle = 10f;
            if (snap) SnapToCurrentMode();
        }

        public Transform Shot => _shot;

        public bool HasShot => _shot != null;

        private float _shakeAmplitude;
        private float _shakeUntil;
        private float _shakeLength = 1f;
        private float _shakeSeed;

        /// <summary>A short jolt (a bite, an impact). Stronger calls win; it fades out over <paramref name="seconds"/>.</summary>
        public void Shake(float amplitude, float seconds)
        {
            float remaining = Mathf.Max(0f, _shakeUntil - Time.time) / Mathf.Max(0.01f, _shakeLength) * _shakeAmplitude;
            if (amplitude < remaining) return;
            _shakeAmplitude = amplitude;
            _shakeLength = Mathf.Max(0.05f, seconds);
            _shakeUntil = Time.time + _shakeLength;
            _shakeSeed = UnityEngine.Random.value * 100f;
        }

        private void ApplyShake()
        {
            if (Time.time >= _shakeUntil || Time.timeScale <= 0f) return;
            float k = (_shakeUntil - Time.time) / _shakeLength;
            float t = Time.time * 28f;
            Vector3 offset = new Vector3(Mathf.PerlinNoise(_shakeSeed, t) - 0.5f, Mathf.PerlinNoise(t, _shakeSeed) - 0.5f, 0f) * 2f * _shakeAmplitude * k * k;
            transform.position += transform.rotation * offset;
        }

        private void LateUpdate()
        {
            if (_playerTarget == null) return;

            if (_shot != null)
            {
                // Unscaled, so the shot still settles while the dialogue is paused.
                float k = 1f - Mathf.Exp(-3.5f * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, _shot.position, k);
                transform.rotation = Quaternion.Slerp(transform.rotation, _shot.rotation, k);
                return;
            }

            HandleLookInput();

            if (_isTransitioning)
            {
                UpdateTransition();
            }
            else
            {
                switch (CurrentMode)
                {
                    case CameraMode.ThirdPerson:
                        UpdateThirdPerson();
                        break;

                    case CameraMode.FirstPerson:
                        UpdateFirstPerson();
                        break;
                }
            }
            ApplyShake();
        }

        // ─────────────────────────────────────────────
        // Look Input
        // ─────────────────────────────────────────────

        private void HandleLookInput()
        {
            if (InputManager.Instance == null) return;
            if (!_headingInitialized) SyncHeadingToPlayer();

            float extraTarget = 0f;
            foreach (float d in _extraDistanceRequests.Values) extraTarget = Mathf.Max(extraTarget, d);
            _currentExtraDistance = Mathf.MoveTowards(_currentExtraDistance, extraTarget, _extraDistanceSpeed * Time.deltaTime);

            // First-person minigames: the view is fixed on the work — no swiping or gyro look (a
            // drag there moves pieces, it must never pan the camera).
            if (InFirstPerson) return;

            // Player settings: look speed and inverted vertical look.
            GameSettings settings = SettingsManager.Active;
            Vector2 lookDelta = InputManager.Instance.LookDelta * settings.LookMultiplier;
            if (settings.invertCameraY) lookDelta.y = -lookDelta.y;

            bool looked = ApplyGyroLook(settings);
            if (lookDelta.sqrMagnitude >= 0.001f)
            {
                // Horizontal: turns the view (and, in BehindBack, the diver). Vertical: tilts only.
                _horizontalAngle += lookDelta.x * _horizontalSensitivity;
                _verticalAngle -= lookDelta.y * _verticalSensitivity;
                ClampVerticalAngle();
                looked = true;
            }

            if (looked) _lastManualLookTime = Time.unscaledTime;
            else if (_style == CameraStyle.FreeOrbit) AutoFollowPlayer();

            // BehindBack: the body turns with the camera in the same frame, so it never lags behind.
            if (LocksBodyToCamera && PlayerController.Instance != null)
                PlayerController.Instance.SnapBodyYaw(_horizontalAngle);
        }

        /// <summary>
        /// While the player swims and nobody is swiping, ease the camera round behind them —
        /// faster the faster they swim and the further off-axis they are. Swimming toward the
        /// camera (joystick down) never spins it round.
        /// </summary>
        private void AutoFollowPlayer()
        {
            if (_autoFollowSpeed <= 0f || CurrentMode != CameraMode.ThirdPerson || _isTransitioning) return;
            if (Time.unscaledTime - _lastManualLookTime < _autoFollowDelay) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            Vector3 v = player.Velocity;
            v.y = 0f;
            float speed = v.magnitude;
            if (speed < 0.3f) return;

            float moveYaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            float diff = Mathf.DeltaAngle(_horizontalAngle, moveYaw);
            if (Mathf.Abs(diff) > 135f) return;

            float rate = _autoFollowSpeed * Mathf.Clamp01(speed / 4f) * Mathf.Clamp01(Mathf.Abs(diff) / 90f);
            _horizontalAngle = Mathf.MoveTowardsAngle(_horizontalAngle, moveYaw, rate * Time.deltaTime);
        }

        /// <summary>
        /// Settings ▸ Gyroscope: turning / tilting the phone turns the camera by the same angle.
        /// Landscape only (the game never runs in portrait). Returns true if it moved the camera.
        /// </summary>
        private bool ApplyGyroLook(GameSettings settings)
        {
            if (!SystemInfo.supportsGyroscope) return false;
            if (Input.gyro.enabled != settings.gyroscope) Input.gyro.enabled = settings.gyroscope;
            if (!settings.gyroscope || Time.deltaTime <= 0f) return false;

            // Device axes in landscape: screen-up = device ±x, screen-right = device ∓y.
            Vector3 rate = Input.gyro.rotationRateUnbiased; // rad/s
            float sign = Screen.orientation == ScreenOrientation.LandscapeRight ? -1f : 1f;
            float yaw = -rate.x * sign * Mathf.Rad2Deg * Time.deltaTime * _gyroStrength;
            float pitch = -rate.y * sign * Mathf.Rad2Deg * Time.deltaTime * _gyroStrength;
            if (settings.invertCameraY) pitch = -pitch;
            if (Mathf.Abs(yaw) + Mathf.Abs(pitch) < 0.01f) return false;

            _horizontalAngle += yaw;
            _verticalAngle -= pitch;
            ClampVerticalAngle();
            return true;
        }

        private void ClampVerticalAngle()
        {
            // Clamp vertical angle based on current mode
            float minAngle = CurrentMode == CameraMode.FirstPerson ? _fpMinVerticalAngle : _minVerticalAngle;
            float maxAngle = CurrentMode == CameraMode.FirstPerson ? _fpMaxVerticalAngle : _maxVerticalAngle;
            _verticalAngle = Mathf.Clamp(_verticalAngle, minAngle, maxAngle);
        }

        // ─────────────────────────────────────────────
        // Third-Person
        // ─────────────────────────────────────────────

        private void UpdateThirdPerson()
        {
            Vector3 targetPos = CalculateThirdPersonPosition();
            Quaternion targetRot = CalculateThirdPersonRotation();

            if (_style == CameraStyle.BehindBack)
            {
                // Rigidly attached to the arm: the view is always exactly behind the diver.
                transform.SetPositionAndRotation(targetPos, targetRot);
                return;
            }

            float k = 1f - Mathf.Exp(-_orbitSmoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
        }

        /// <summary>Spring-arm pivot: shoulder height above the player.</summary>
        private Vector3 ArmPivot => _playerTarget.position + Vector3.up * _orbitHeight;

        /// <summary>
        /// Spring arm: from the pivot, back along the view for the arm length, then the socket offset.
        /// A sphere probe shortens the arm instantly when blocked; it extends again smoothly.
        /// </summary>
        private Vector3 CalculateThirdPersonPosition()
        {
            Quaternion rotation = CalculateThirdPersonRotation();
            Vector3 pivot = ArmPivot;
            Vector3 socket = rotation * _socketOffset;
            Vector3 back = rotation * Vector3.back;

            float wanted = _orbitDistance + _currentExtraDistance;
            float allowed = ProbeArm(pivot + socket, back, wanted);

            if (_armLength < 0f) _armLength = allowed;
            _armLength = allowed < _armLength
                ? allowed                                                        // blocked: pull in now
                : Mathf.MoveTowards(_armLength, allowed, _armRecoverSpeed * Time.deltaTime); // clear: ease out

            return pivot + socket + back * _armLength;
        }

        /// <summary>How long the arm can be before hitting level geometry (the player / dugong / triggers don't count).</summary>
        private float ProbeArm(Vector3 origin, Vector3 direction, float length)
        {
            float best = length;
            RaycastHit[] hits = Physics.SphereCastAll(origin, _probeRadius, direction, length, _collisionMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance <= 0f) continue; // started inside (e.g. the player's own capsule)
                Transform t = hit.collider.transform;
                if (t.IsChildOf(_playerTarget) || _playerTarget.IsChildOf(t)) continue;
                if (hit.collider.GetComponentInParent<Lalim.Abilities.DugongCompanion>() != null) continue;
                best = Mathf.Min(best, Mathf.Max(0.2f, hit.distance - 0.05f));
            }
            return best;
        }

        /// <summary>The camera looks exactly along the arm: yaw = view/body yaw, pitch = tilt.</summary>
        private Quaternion CalculateThirdPersonRotation() => Quaternion.Euler(_verticalAngle, _horizontalAngle, 0f);

        // ─────────────────────────────────────────────
        // First-Person
        // ─────────────────────────────────────────────

        private void UpdateFirstPerson()
        {
            // Position using local space offset so it can be pushed forward (Z) or up (Y) relative to player
            transform.position = _playerTarget.TransformPoint(_firstPersonOffset);

            // Rotation based on look input
            Quaternion targetRot = Quaternion.Euler(_verticalAngle, _horizontalAngle, 0f);
            transform.rotation = targetRot;
        }

        // ─────────────────────────────────────────────
        // Mode Transitions
        // ─────────────────────────────────────────────

        /// <summary>
        /// Switch camera mode with smooth transition.
        /// </summary>
        /// <summary>In (or going into) first person — the camera is fixed then.</summary>
        public bool InFirstPerson =>
            CurrentMode == CameraMode.FirstPerson || (_isTransitioning && _transitionTargetMode == CameraMode.FirstPerson)
            || (ChallengeManager.Instance != null && ChallengeManager.Instance.IsFirstPersonChallengeActive);

        /// <summary>Cuts straight to first person (behind a fade), with no camera swoop.</summary>
        public void SnapToFirstPerson()
        {
            _isTransitioning = false;
            CurrentMode = CameraMode.FirstPerson;
            SetBodyVisible(false);
            SnapToCurrentMode();
        }

        public void SwitchMode(CameraMode newMode)
        {
            if (CurrentMode == newMode && !_isTransitioning) return;

            _transitionStartPos = transform.position;
            _transitionStartRot = transform.rotation;
            _transitionTargetMode = newMode;
            _transitionProgress = 0f;
            _isTransitioning = true;

            Debug.Log($"[CameraController] Transitioning to {newMode}");
            if (newMode == CameraMode.ThirdPerson) SetBodyVisible(true);
        }

        // ─────────────────────────────────────────────
        // First person: the camera is the diver's eyes, so their own body isn't drawn
        // (it still casts its shadow).
        // ─────────────────────────────────────────────

        private readonly System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode> _hiddenBody =
            new System.Collections.Generic.Dictionary<Renderer, UnityEngine.Rendering.ShadowCastingMode>();

        private void SetBodyVisible(bool visible)
        {
            if (visible)
            {
                foreach (var pair in _hiddenBody) if (pair.Key != null) pair.Key.shadowCastingMode = pair.Value;
                _hiddenBody.Clear();
                return;
            }
            if (_hiddenBody.Count > 0 || PlayerController.Instance == null) return;
            foreach (Renderer r in PlayerController.Instance.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                _hiddenBody[r] = r.shadowCastingMode;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            }
        }

        private void UpdateTransition()
        {
            _transitionProgress += Time.deltaTime / _transitionDuration;

            // Smooth step for nicer easing
            float t = Mathf.SmoothStep(0f, 1f, _transitionProgress);

            // Calculate target position/rotation for the new mode
            Vector3 targetPos;
            Quaternion targetRot;

            if (_transitionTargetMode == CameraMode.FirstPerson)
            {
                targetPos = _playerTarget.position + _firstPersonOffset;
                targetRot = Quaternion.Euler(_verticalAngle, _horizontalAngle, 0f);
            }
            else
            {
                targetPos = CalculateThirdPersonPosition();
                targetRot = CalculateThirdPersonRotation();
            }

            // Interpolate
            transform.position = Vector3.Lerp(_transitionStartPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(_transitionStartRot, targetRot, t);

            // Going into first person: the diver's body vanishes as the camera reaches their head.
            if (_transitionTargetMode == CameraMode.FirstPerson && t > 0.6f) SetBodyVisible(false);

            // Complete transition
            if (_transitionProgress >= 1f)
            {
                _isTransitioning = false;
                CurrentMode = _transitionTargetMode;
                Debug.Log($"[CameraController] Now in {CurrentMode} mode");
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Set the player target transform.
        /// </summary>
        public void SetTarget(Transform target)
        {
            _playerTarget = target;
        }

        /// <summary>
        /// Snap camera to current mode position without transition.
        /// </summary>
        public void SnapToCurrentMode()
        {
            if (_playerTarget == null) return;

            _isTransitioning = false;

            if (CurrentMode == CameraMode.FirstPerson)
            {
                transform.position = _playerTarget.TransformPoint(_firstPersonOffset);
                transform.rotation = Quaternion.Euler(_verticalAngle, _horizontalAngle, 0f);
            }
            else
            {
                transform.position = CalculateThirdPersonPosition();
                transform.rotation = CalculateThirdPersonRotation();
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnPlayerRespawned(PlayerRespawnedEvent evt) => SyncHeadingToPlayer();

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            SwitchMode(evt.Perspective == ChallengePerspective.ThirdPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt) => LeaveFirstPerson();

        private void OnChallengeFailed(ChallengeFailedEvent evt) => LeaveFirstPerson();

        // Out of a first-person minigame through black (as into it), back behind the diver.
        // (Area minigames hand the camera back themselves — MinigameStage — while this is off.)
        private void LeaveFirstPerson()
        {
            bool firstPerson = CurrentMode == CameraMode.FirstPerson || (_isTransitioning && _transitionTargetMode == CameraMode.FirstPerson);
            if (!isActiveAndEnabled || !firstPerson) { SwitchMode(CameraMode.ThirdPerson); return; }
            StartCoroutine(FadeToThirdPerson());
        }

        private System.Collections.IEnumerator FadeToThirdPerson()
        {
            Lalim.UI.ScreenFader fader = Lalim.UI.ScreenFader.Instance;
            yield return fader.FadeOut(0.2f);
            _isTransitioning = false;
            CurrentMode = CameraMode.ThirdPerson;
            SetBodyVisible(true);
            SyncHeadingToPlayer();
            _verticalAngle = 10f;
            SnapToCurrentMode();
            yield return fader.FadeIn(0.3f);
        }
    }
}
