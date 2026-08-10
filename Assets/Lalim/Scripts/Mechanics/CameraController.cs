using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Dual-perspective camera controller.
    /// - Third-person: orbits behind the player, swipe rotates around character.
    /// - First-person: locks to player's head position, swipe controls free look.
    /// Smooth lerp transition between modes when challenges start/end.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _playerTarget;

        [Header("Third-Person Settings")]
        [SerializeField] private float _orbitDistance = 5.0f;
        [SerializeField] private float _orbitHeight = 2.5f;
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

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public enum CameraMode { ThirdPerson, FirstPerson }
        public CameraMode CurrentMode { get; private set; } = CameraMode.ThirdPerson;

        private float _horizontalAngle = 0f;
        private float _verticalAngle = 15f;

        // Transition state
        private bool _isTransitioning = false;
        private float _transitionProgress = 0f;
        private Vector3 _transitionStartPos;
        private Quaternion _transitionStartRot;
        private CameraMode _transitionTargetMode;

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

        private void LateUpdate()
        {
            if (_playerTarget == null) return;

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
        }

        // ─────────────────────────────────────────────
        // Look Input
        // ─────────────────────────────────────────────

        private void HandleLookInput()
        {
            if (InputManager.Instance == null) return;

            Vector2 lookDelta = InputManager.Instance.LookDelta;

            if (lookDelta.sqrMagnitude < 0.001f) return;

            _horizontalAngle += lookDelta.x * _horizontalSensitivity;
            _verticalAngle -= lookDelta.y * _verticalSensitivity;

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

            transform.position = Vector3.Lerp(transform.position, targetPos, _orbitSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _orbitSmoothSpeed * Time.deltaTime);
        }

        private Vector3 CalculateThirdPersonPosition()
        {
            // Calculate orbit position using spherical coordinates
            Quaternion rotation = Quaternion.Euler(_verticalAngle, _horizontalAngle, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -_orbitDistance);

            Vector3 targetPoint = _playerTarget.position + Vector3.up * _orbitHeight;
            Vector3 desiredPosition = targetPoint + offset;

            // Obstacle avoidance: raycast from target to desired position
            Vector3 direction = desiredPosition - targetPoint;
            if (Physics.Raycast(targetPoint, direction.normalized, out RaycastHit hit, direction.magnitude))
            {
                // Pull camera in front of the obstacle
                desiredPosition = hit.point - direction.normalized * 0.2f;
            }

            return desiredPosition;
        }

        private Quaternion CalculateThirdPersonRotation()
        {
            // Look at the player from the orbit position
            Vector3 lookTarget = _playerTarget.position + Vector3.up * (_orbitHeight * 0.5f);
            return Quaternion.LookRotation(lookTarget - transform.position);
        }

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
        public void SwitchMode(CameraMode newMode)
        {
            if (CurrentMode == newMode && !_isTransitioning) return;

            _transitionStartPos = transform.position;
            _transitionStartRot = transform.rotation;
            _transitionTargetMode = newMode;
            _transitionProgress = 0f;
            _isTransitioning = true;

            Debug.Log($"[CameraController] Transitioning to {newMode}");
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

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // All challenges use first-person camera
            SwitchMode(CameraMode.FirstPerson);
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            SwitchMode(CameraMode.ThirdPerson);
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            SwitchMode(CameraMode.ThirdPerson);
        }
    }
}
