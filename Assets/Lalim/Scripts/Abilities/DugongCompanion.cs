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
    ///                 While Bubble Barrier is up it tucks in closer so it's inside the bubble too,
    ///                 and moves with the player (a Surge or Riptide can't leave it behind).
    ///                 If it falls more than 40m behind it reappears out of the camera's view.
    ///  - Approaching: Tidebond called it; swims straight to the player's side (or reappears just
    ///                 off-camera behind them first if it's far away).
    ///  - Mounted:     Tidebond — the player isn't on its back: it swims right alongside, matching
    ///                 their heading, with the player's near hand on its flank (player on its left
    ///                 → right hand on its left side, and the other way round). Whichever side it
    ///                 arrived on is kept. The player's CharacterController does all the movement.
    ///                 (The same pose can hide the player from predators in a later minigame.)
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
        [Tooltip("Unused since Tidebond became swimming alongside (see the Tidebond header); kept for the gizmo.")]
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
        [Tooltip("Top speed above the player's own while it swims over (it slows as it arrives).")]
        [SerializeField] private float _approachSpeed = 18f;
        [Tooltip("Close enough to count as beside the player: from here it settles in smoothly.")]
        [SerializeField] private float _mountSnapDistance = 1.5f;
        [Tooltip("Safety net: if the dugong hasn't arrived after this long it settles in anyway (smoothly).")]
        [SerializeField] private float _approachTimeout = 5f;
        [Tooltip("How long the last stretch (settling beside the player) takes before the hand reaches for it.")]
        [SerializeField, Min(0.05f)] private float _settleSeconds = 0.5f;

        [Header("Tidebond (hand on its side)")]
        [Tooltip("Half the body's width at the flank the player touches, in the model's own units.")]
        [SerializeField] private float _flankHalfWidth = 0.42f;
        [Tooltip("Height (model units) of the touched point on the flank.")]
        [SerializeField] private float _flankHeight = 0.85f;
        [Tooltip("Forward offset (model units) of the touched point; negative = behind the middle.")]
        [SerializeField] private float _flankForward = -0.1f;
        [Tooltip("How much of the arm's length the hand reaches out (1 = fully straight).")]
        [SerializeField, Range(0.5f, 1f)] private float _reachFraction = 0.85f;
        [Tooltip("Where the hand rests, relative to the shoulder (x out to the side, y up, z forward), in arm reaches: down and a little forward is a relaxed arm on the dugong's back, not one stuck straight out.")]
        [SerializeField] private Vector3 _handRest = new Vector3(0.55f, -0.6f, 0.3f);
        [SerializeField] private float _handBlendSeconds = 0.6f;
        [Tooltip("How far the wrist stays off the dugong's skin, so the palm rests on it (metres).")]
        [SerializeField, Min(0f)] private float _palmThickness = 0.03f;
        [Tooltip("Which way the right hand bone's local X points from the palm: +1 = palm on +X (this rig; the left hand is mirrored).")]
        [SerializeField] private float _palmAxisSign = 1f;

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
        private Vector3 _lastPlayerPos;
        private bool _hasLastPlayerPos;
        private float _handWeight;
        private float _seated;   // 0 just arrived … 1 settled beside the player (then the hand reaches)
        private Arm _rightArm, _leftArm;
        private bool _armsResolved;

        /// <summary>The diver's arm bones (generic rig, found by name).</summary>
        private struct Arm
        {
            public Transform Upper, Fore, Hand;
            public bool Valid => Upper != null && Fore != null && Hand != null;
            public float Length => Valid ? Vector3.Distance(Upper.position, Fore.position) + Vector3.Distance(Fore.position, Hand.position) : 0f;
        }

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
            _handWeight = 0f;
            _seated = 0f;
            SetTired(false);
            // Keeps the side it's on: on the player's right → the player's right hand touches it.
            Vector3 right = Vector3.Cross(Vector3.up, FlatForward(rider));
            _sideSign = Vector3.Dot(transform.position - rider.position, right) >= 0f ? 1f : -1f;

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

        /// <summary>The green heal particles (Seagrass Mend copies them onto the player).</summary>
        public ParticleSystem HealVfx => _healVfx;

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
            Vector3 playerMoved = _hasLastPlayerPos ? _player.position - _lastPlayerPos : Vector3.zero;
            _lastPlayerPos = _player.position;
            _hasLastPlayerPos = true;
            if (_holdSpot != null && State != DugongState.Mounted && State != DugongState.Approaching)
            {
                UpdateAnimSpeed(0f);   // a slow idle sway in place
                return;
            }

            switch (State)
            {
                case DugongState.Follow: CarryInsideBarrier(playerMoved); TickFollow(_followBackDistance, _maxFollowSpeed); break;
                case DugongState.Resting: TickResting(); break;
                case DugongState.Approaching: TickApproach(); break;
            }
        }

        private void LateUpdate()
        {
            // Mounted positioning runs after the player has moved (and been animated) this frame,
            // so there's no lag and the hand lands on the flank.
            if (State == DugongState.Mounted && _player != null)
            {
                SnapToSeat();
                ReachHand();
            }
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

        /// <summary>
        /// Sharing the bubble, the dugong travels with the player: a surge (8 m in a third of a
        /// second) is faster than it can swim, so the player's own movement is applied to it too.
        /// </summary>
        private void CarryInsideBarrier(Vector3 playerMoved)
        {
            if (!InsideBarrier() || playerMoved.sqrMagnitude > 25f) return;   // > 5 m in a frame: a respawn, not a dash
            BubbleBarrierAbility barrier = PlayerHealth.Instance.Barrier;
            float inside = Mathf.Max(barrier.CurrentRadius, 6f) + 2f;
            if ((transform.position - _player.position).sqrMagnitude > inside * inside) return;   // it's still catching up
            Move(playerMoved);
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
            Vector3 target = BesidePosition(transform.rotation);
            Vector3 toTarget = target - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= _mountSnapDistance || _approachTimer >= _approachTimeout)
            {
                State = DugongState.Mounted;
                _velocity = Vector3.zero;
                _seated = 0f;   // the last stretch is eased in SnapToSeat, never a jump; the ride starts once settled
                SnapToSeat();
                SetMountedAnim(true);
                return;
            }

            // Swim over at a natural pace: as fast as the player plus a margin (so it catches up),
            // slowing to a glide over the last few metres; turning to the player's heading as it nears.
            float playerSpeed = PlayerController.Instance != null ? PlayerController.Instance.Velocity.magnitude : 0f;
            float cruise = Mathf.Min(_approachSpeed, playerSpeed + 6f);
            float speed = Mathf.Min(cruise, playerSpeed + dist * 1.6f + 0.5f);
            Vector3 want = toTarget / dist * speed;
            _velocity = Vector3.MoveTowards(_velocity, want, Time.deltaTime * 14f);
            Move(_velocity * Time.deltaTime);
            float near = Mathf.InverseLerp(4f, 1f, dist);
            if (near > 0f) TurnTo(FlatForward(_player), near);
            else FaceTravelDirection();
        }

        private void TurnTo(Vector3 heading, float amount)
        {
            if (heading.sqrMagnitude < 1e-4f) return;
            Quaternion along = Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(_modelRotationOffset);
            Quaternion travel = _velocity.sqrMagnitude > 0.04f ? Quaternion.LookRotation(_velocity.normalized) * Quaternion.Euler(_modelRotationOffset) : along;
            Quaternion target = Quaternion.Slerp(travel, along, amount);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
        }

        private void SnapToSeat()
        {
            Vector3 heading = PlayerController.Instance != null ? PlayerController.Instance.Velocity : Vector3.zero;
            if (heading.sqrMagnitude < 0.25f) heading = FlatForward(_player);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(_modelRotationOffset), 1f - Mathf.Exp(-10f * Time.deltaTime));
            // Settling in: glide the last bit to the player's side, then stay locked there.
            Vector3 beside = BesidePosition(transform.rotation);
            _seated = Mathf.MoveTowards(_seated, 1f, Time.deltaTime / _settleSeconds);
            transform.position = _seated >= 1f ? beside : Vector3.Lerp(transform.position, beside, 1f - Mathf.Exp(-8f * Time.deltaTime));
            // Beside the player now: the ride begins (Tidebond's speed and pose) as the hand reaches for it.
            if (_seated >= 0.8f && _onArrived != null)
            {
                if (_rideVfx != null) _rideVfx.Play();
                Action callback = _onArrived;
                _onArrived = null;
                callback.Invoke();
            }

            float speed = PlayerController.Instance != null ? PlayerController.Instance.Velocity.magnitude : 0f;
            UpdateAnimSpeed(speed);
        }

        // ─────────────────────────────────────────────
        // Tidebond: alongside, hand on the flank
        // ─────────────────────────────────────────────

        /// <summary>Where the dugong goes (at <paramref name="rotation"/>) so its flank is at arm's reach of the player.</summary>
        private Vector3 BesidePosition(Quaternion rotation)
        {
            ResolveArms();
            Arm arm = _sideSign > 0f ? _rightArm : _leftArm;
            Vector3 heading = rotation * Quaternion.Inverse(Quaternion.Euler(_modelRotationOffset)) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, heading);
            right = right.sqrMagnitude > 1e-4f ? right.normalized : _player.right;

            Vector3 shoulder;
            float reach;
            if (arm.Valid) { shoulder = arm.Upper.position; reach = arm.Length * _reachFraction; }
            else { shoulder = _player.position + Vector3.up * 0.45f; reach = 0.25f; }

            // The hand rests out, down and a little ahead of the shoulder (elbow bent): the dugong
            // sits there, lower than the diver, its back under the hand.
            Vector3 rest = _handRest.normalized * reach;
            Vector3 flankTarget = shoulder + right * (_sideSign * rest.x) + Vector3.up * rest.y + heading * rest.z;
            return flankTarget - rotation * Vector3.Scale(FlankLocal(), transform.lossyScale);
        }

        /// <summary>The touched point on the flank facing the player, in the model's own space.</summary>
        private Vector3 FlankLocal() => new Vector3(-_sideSign * _flankHalfWidth, _flankHeight, _flankForward);

        private void ReachHand()
        {
            // The hand reaches only once the dugong is beside the player, not while it's still arriving.
            float handTarget = _seated >= 0.8f ? 1f : 0f;
            _handWeight = Mathf.MoveTowards(_handWeight, handTarget, Time.deltaTime / Mathf.Max(0.01f, _handBlendSeconds));
            Arm arm = _sideSign > 0f ? _rightArm : _leftArm;
            if (!arm.Valid) return;
            Vector3 flank = transform.TransformPoint(FlankLocal());
            // Into the dugong's body from the touched point (a little down: the hand lies on its upper flank).
            Vector3 core = transform.TransformPoint(new Vector3(0f, _flankHeight * 0.4f, _flankForward));
            Vector3 inward = core - flank;
            inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : _player.right * _sideSign;
            // The wrist stays a palm's thickness off the skin, so the palm rests on it rather than sinking in.
            Vector3 wrist = flank - inward * _palmThickness;
            // The elbow bends out and back, as when leaning a hand on something beside you.
            Vector3 side = _player.right * _sideSign;
            TwoBoneIK.Solve(arm.Upper, arm.Fore, arm.Hand, wrist, side * 0.6f - _player.forward * 0.8f, _handWeight);
            LayPalm(arm, inward);
        }

        /// <summary>
        /// Turns the hand so its palm (not its edge or back) lies on the dugong: the palm faces
        /// <paramref name="inward"/> and the fingers run on along the forearm's line, over its skin.
        /// The diver's rig has its palms on the hand bones' local +X (right) / −X (left), fingers along +Y.
        /// </summary>
        private void LayPalm(Arm arm, Vector3 inward)
        {
            if (arm.Hand == null || arm.Fore == null) return;
            Vector3 fingers = arm.Hand.position - arm.Fore.position;
            fingers -= inward * Vector3.Dot(fingers, inward);   // flat along the skin
            if (fingers.sqrMagnitude < 1e-6f) fingers = Vector3.ProjectOnPlane(_player.forward, inward);
            if (fingers.sqrMagnitude < 1e-6f) return;
            fingers.Normalize();
            Vector3 localX = (_sideSign > 0f ? 1f : -1f) * _palmAxisSign * inward;   // the palm's side of the hand
            Quaternion lay = Quaternion.LookRotation(Vector3.Cross(localX, fingers), fingers);
            arm.Hand.rotation = Quaternion.Slerp(arm.Hand.rotation, lay, _handWeight);
        }

        private void ResolveArms()
        {
            if (_armsResolved || _player == null) return;
            _armsResolved = true;
            _rightArm = FindArm('R');
            _leftArm = FindArm('L');
        }

        /// <summary>The diver's arm bones on one side, in either rig's naming ("R_Upperarm" or "UpperArm.R").</summary>
        private Arm FindArm(char side)
        {
            var arm = new Arm();
            string old = side + "_", suffix = "." + side;
            foreach (Transform t in _player.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n == old + "Upperarm" || n == "UpperArm" + suffix) arm.Upper = t;
                else if (n == old + "Forearm" || n == "LowerArm" + suffix) arm.Fore = t;
                else if (n == old + "Hand" || n == "Hand" + suffix) arm.Hand = t;
            }
            return arm;
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
