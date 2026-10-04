using System;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// The player's dugong companion. One instance per region scene (PF_Dugong).
    ///
    /// States:
    ///  - Follow:      swims BESIDE the player (a couple of meters to the side), catching up smoothly.
    ///                 While Bubble Barrier is up it tucks in closer so it's inside the bubble too.
    ///                 If it falls more than 40m behind it reappears out of the camera's view.
    ///  - Approaching: Tidebond called it; swims straight to the player (or reappears just
    ///                 off-camera behind them first if it's far away), then mounts.
    ///  - Mounted:     snaps so its SeatAnchor sits under the player and matches their heading.
    ///                 The player's CharacterController does all the movement.
    ///  - Resting:     Tidebond ran out — swims slowly nearby for the cooldown so "the dugong
    ///                 is tired" is something the player can see.
    ///
    /// No CharacterController or Rigidbody — it moves its own transform, clamped by UnderwaterBounds.
    /// </summary>
    public class DugongCompanion : MonoBehaviour
    {
        public enum DugongState { Follow, Approaching, Mounted, Resting }

        public static DugongCompanion Instance { get; private set; }

        [Header("References")]
        [Tooltip("Child transform where the player sits. The dugong is positioned so this lands on the player.")]
        [SerializeField] private Transform _seatAnchor;
        [SerializeField] private Animator _animator;
        [SerializeField] private ParticleSystem _healVfx;
        [SerializeField] private ParticleSystem _rideVfx;

        [Header("Follow")]
        [Tooltip("How far behind the player's shoulder it swims (small = alongside).")]
        [SerializeField] private float _followBackDistance = 0.4f;
        [Tooltip("How far to the side of the player it swims.")]
        [SerializeField] private float _followSideDistance = 2.4f;
        [SerializeField] private float _followHeightOffset = -0.2f;
        [Tooltip("Side distance while Bubble Barrier is up, so it fits inside the bubble with the player.")]
        [SerializeField] private float _barrierSideDistance = 1.5f;
        [SerializeField] private float _maxFollowSpeed = 14f;
        [SerializeField] private float _catchUpGain = 1.5f;
        [SerializeField] private float _turnSpeed = 4f;
        [SerializeField] private float _lostDistance = 40f;

        [Header("Approach (Tidebond call)")]
        [SerializeField] private float _approachSpeed = 18f;
        [SerializeField] private float _mountSnapDistance = 1.5f;
        [Tooltip("Safety net: if the dugong hasn't arrived after this long it snaps to the player.")]
        [SerializeField] private float _approachTimeout = 2.5f;

        [Header("Resting")]
        [SerializeField] private float _restFollowDistance = 9f;
        [SerializeField] private float _restSpeed = 2f;

        [Header("Animation")]
        [Tooltip("Swim speed at which the swim clip plays at 1× speed.")]
        [SerializeField] private float _animReferenceSpeed = 5f;

        [Header("Model")]
        [Tooltip("Rotation applied on top of the travel heading, to correct the imported model's forward axis.")]
        [SerializeField] private Vector3 _modelRotationOffset = Vector3.zero;

        public DugongState State { get; private set; } = DugongState.Follow;

        private Transform _player;
        private Vector3 _velocity;
        private Action _onArrived;
        private float _approachTimer;
        private float _restTimer;
        private float _sideSign = 1f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MountedHash = Animator.StringToHash("Mounted");
        private static readonly int HealHash = Animator.StringToHash("Heal");
        private static readonly int TiredHash = Animator.StringToHash("Tired");
        private bool _hasSpeed, _hasMounted, _hasHeal, _hasTired;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            CacheAnimatorParameters();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void ResolvePlayer()
        {
            if (_player == null && PlayerController.Instance != null) _player = PlayerController.Instance.transform;
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>True if the dugong is within 'radius' meters of the player (or mounted).</summary>
        public bool IsWithin(float radius)
        {
            ResolvePlayer();
            if (_player == null) return false;
            if (State == DugongState.Mounted) return true;
            return (transform.position - _player.position).sqrMagnitude <= radius * radius;
        }

        /// <summary>Called by Tidebond. Swims to 'rider' and invokes onArrived when mounted.</summary>
        public void BeginMount(Transform rider, float teleportDistance, Action onArrived)
        {
            _player = rider;
            _onArrived = onArrived;
            _approachTimer = 0f;
            _restTimer = 0f;
            SetTired(false);

            if ((transform.position - rider.position).sqrMagnitude > teleportDistance * teleportDistance)
            {
                TeleportOutOfView(8f);
            }

            State = DugongState.Approaching;
        }

        public void Dismount()
        {
            if (State == DugongState.Mounted || State == DugongState.Approaching)
            {
                State = DugongState.Follow;
                _onArrived = null;
                // Give it a little forward momentum so it doesn't stop dead under the player.
                if (PlayerController.Instance != null) _velocity = PlayerController.Instance.Velocity * 0.5f;
            }
            SetMountedAnim(false);
            if (_rideVfx != null) _rideVfx.Stop();
        }

        /// <summary>Dismounts and swims slowly nearby for 'seconds' (Tidebond's cooldown).</summary>
        public void Rest(float seconds)
        {
            Dismount();
            State = DugongState.Resting;
            _restTimer = seconds;
            SetTired(true);
        }

        /// <summary>Seagrass Mend flourish: plays the heal animation/VFX. The heal itself never depends on this.</summary>
        public void PlayHeal()
        {
            if (_healVfx != null) _healVfx.Play();
            if (_animator != null && _hasHeal) _animator.SetTrigger(HealHash);
        }

        /// <summary>
        /// Parks the dugong at <paramref name="spot"/> (facing its forward) until <see cref="ReleaseHold"/>,
        /// e.g. beside the diver while talking to a deity. Ignored while the player is riding it.
        /// </summary>
        public void HoldAt(Transform spot)
        {
            if (spot == null || State == DugongState.Mounted || State == DugongState.Approaching) return;
            _holdSpot = spot;
            _velocity = Vector3.zero;
            transform.SetPositionAndRotation(spot.position, Quaternion.LookRotation(FlatForward(spot)) * Quaternion.Euler(_modelRotationOffset));
            UpdateAnimSpeed(0f);
        }

        public void ReleaseHold() => _holdSpot = null;

        public bool IsHeld => _holdSpot != null;

        private Transform _holdSpot;

        // ─────────────────────────────────────────────
        // Update
        // ─────────────────────────────────────────────

        private void Update()
        {
            ResolvePlayer();
            if (_player == null) return;
            if (_holdSpot != null && State != DugongState.Mounted && State != DugongState.Approaching)
            {
                UpdateAnimSpeed(0f);   // a slow idle sway in place
                return;
            }

            switch (State)
            {
                case DugongState.Follow: TickFollow(_followBackDistance, _maxFollowSpeed); break;
                case DugongState.Resting: TickResting(); break;
                case DugongState.Approaching: TickApproach(); break;
            }
        }

        private void LateUpdate()
        {
            // Mounted positioning runs after the player has moved this frame so there's no lag.
            if (State == DugongState.Mounted && _player != null) SnapToSeat();
        }

        private void TickFollow(float backDistance, float maxSpeed)
        {
            Vector3 playerFwd = FlatForward(_player);
            Vector3 playerRight = Vector3.Cross(Vector3.up, playerFwd);
            bool shielded = InsideBarrier();
            float side = shielded ? _barrierSideDistance : _followSideDistance;
            if (shielded)
            {
                backDistance = Mathf.Min(backDistance, 0.2f);  // even a resting dugong comes in close
                maxSpeed = Mathf.Max(maxSpeed, _maxFollowSpeed); // ...and hurries to get inside
            }
            Vector3 target = _player.position
                             - playerFwd * backDistance
                             + playerRight * (side * _sideSign)
                             + Vector3.up * _followHeightOffset;

            Vector3 toTarget = target - transform.position;
            float dist = toTarget.magnitude;

            if ((transform.position - _player.position).sqrMagnitude > _lostDistance * _lostDistance)
            {
                TeleportOutOfView(backDistance + 6f);
                return;
            }

            // Proportional catch-up: slow when close, fast when far, never faster than maxSpeed.
            float desiredSpeed = Mathf.Min(maxSpeed, dist * _catchUpGain);
            Vector3 desiredVelocity = dist > 0.05f ? toTarget / dist * desiredSpeed : Vector3.zero;
            _velocity = Vector3.Lerp(_velocity, desiredVelocity, 1f - Mathf.Exp(-3f * Time.deltaTime));

            Move(_velocity * Time.deltaTime);
            // Swimming alongside: match the player's heading instead of jittering toward tiny moves.
            if (_velocity.sqrMagnitude < 1f) FaceDirection(playerFwd);
            else FaceTravelDirection();
        }

        private static bool InsideBarrier()
        {
            BubbleBarrierAbility barrier = PlayerHealth.Instance != null ? PlayerHealth.Instance.Barrier : null;
            return barrier != null && barrier.IsActive;
        }

        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(_modelRotationOffset);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
        }

        // ─────────────────────────────────────────────
        // Body size (so Bubble Barrier can wrap around the dugong)
        // ─────────────────────────────────────────────

        private Vector3 _bodyCenterLocal;
        private float _bodyRadius = -1f;

        /// <summary>World-space centre of the dugong's body.</summary>
        public Vector3 BodyCenter
        {
            get { EnsureBodyBounds(); return transform.TransformPoint(_bodyCenterLocal); }
        }

        /// <summary>Radius of a sphere that roughly contains the dugong's body.</summary>
        public float BodyRadius
        {
            get { EnsureBodyBounds(); return _bodyRadius; }
        }

        private void EnsureBodyBounds()
        {
            if (_bodyRadius > 0f) return;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            _bodyCenterLocal = transform.InverseTransformPoint(b.center);
            // Longest half-axis, slightly trimmed (the model's bounds include fins/tail tips).
            _bodyRadius = first ? 1.5f : Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)) * 0.9f;
        }

        private void TickResting()
        {
            _restTimer -= Time.deltaTime;
            if (_restTimer <= 0f)
            {
                State = DugongState.Follow;
                SetTired(false);
                return;
            }
            TickFollow(_restFollowDistance, _restSpeed);
        }

        private void TickApproach()
        {
            _approachTimer += Time.deltaTime;
            Vector3 seatOffset = _seatAnchor != null ? _seatAnchor.position - transform.position : Vector3.zero;
            Vector3 target = _player.position - seatOffset;
            Vector3 toTarget = target - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= _mountSnapDistance || _approachTimer >= _approachTimeout)
            {
                State = DugongState.Mounted;
                _velocity = Vector3.zero;
                SnapToSeat();
                SetMountedAnim(true);
                if (_rideVfx != null) _rideVfx.Play();

                Action callback = _onArrived;
                _onArrived = null;
                callback?.Invoke();
                return;
            }

            _velocity = toTarget / dist * Mathf.Min(_approachSpeed, dist / Mathf.Max(Time.deltaTime, 0.0001f));
            Move(_velocity * Time.deltaTime);
            FaceTravelDirection();
        }

        private void SnapToSeat()
        {
            Vector3 heading = PlayerController.Instance != null ? PlayerController.Instance.Velocity : Vector3.zero;
            if (heading.sqrMagnitude < 0.25f) heading = FlatForward(_player);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(_modelRotationOffset), 1f - Mathf.Exp(-10f * Time.deltaTime));

            Vector3 seatOffset = _seatAnchor != null ? _seatAnchor.position - transform.position : Vector3.zero;
            transform.position = _player.position - seatOffset;

            float speed = PlayerController.Instance != null ? PlayerController.Instance.Velocity.magnitude : 0f;
            UpdateAnimSpeed(speed);
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private void Move(Vector3 delta)
        {
            Vector3 pos = transform.position + delta;
            if (UnderwaterBounds.Instance != null) pos = UnderwaterBounds.Instance.ClampPosition(pos, 1f);
            transform.position = pos;
            UpdateAnimSpeed(_velocity.magnitude);
        }

        private void FaceTravelDirection()
        {
            if (_velocity.sqrMagnitude < 0.04f) return;
            Quaternion target = Quaternion.LookRotation(_velocity.normalized) * Quaternion.Euler(_modelRotationOffset);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
        }

        /// <summary>Places the dugong behind the player where the camera can't see it, so recalls never pop in on screen.</summary>
        private void TeleportOutOfView(float distanceBehind)
        {
            Camera cam = Camera.main;
            Vector3 back = cam != null ? -FlatForward(cam.transform) : -FlatForward(_player);
            Vector3 right = Vector3.Cross(Vector3.up, -back);
            _sideSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;

            Vector3 pos = _player.position + back * distanceBehind + right * (_followSideDistance * _sideSign) + Vector3.down * 1.5f;
            if (UnderwaterBounds.Instance != null) pos = UnderwaterBounds.Instance.ClampPosition(pos, 1f);

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(-back) * Quaternion.Euler(_modelRotationOffset);
            _velocity = Vector3.zero;
        }

        private static Vector3 FlatForward(Transform t)
        {
            Vector3 f = t.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        private void CacheAnimatorParameters()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                if (p.nameHash == SpeedHash) _hasSpeed = true;
                else if (p.nameHash == MountedHash) _hasMounted = true;
                else if (p.nameHash == HealHash) _hasHeal = true;
                else if (p.nameHash == TiredHash) _hasTired = true;
            }
        }

        private void UpdateAnimSpeed(float speed)
        {
            if (_animator == null) return;
            if (_hasSpeed) _animator.SetFloat(SpeedHash, speed);
            // Until dedicated clips exist, drive playback rate so the swim cycle matches movement.
            float rate = Mathf.Clamp(speed / Mathf.Max(0.1f, _animReferenceSpeed), 0.3f, 2.5f);
            if (State == DugongState.Resting) rate *= 0.6f;
            _animator.speed = rate;
        }

        private void SetMountedAnim(bool mounted)
        {
            if (_animator != null && _hasMounted) _animator.SetBool(MountedHash, mounted);
        }

        private void SetTired(bool tired)
        {
            if (_animator != null && _hasTired) _animator.SetBool(TiredHash, tired);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_seatAnchor == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_seatAnchor.position, 0.3f);
        }
#endif
    }
}
