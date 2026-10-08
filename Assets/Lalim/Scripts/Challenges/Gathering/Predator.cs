using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges.Gathering
{
    /// <summary>
    /// A shark or hammerhead patrolling an open sand lane on a loop of waypoints.
    ///  - Patrol: cruises the loop.
    ///  - Chase: spotted the diver in the open (in range and roughly ahead) — closes in, faster
    ///    than the diver's normal swim, so it takes cover, Tidebond, Surge or Riptide to escape.
    ///  - Search: lost sight (the diver hid, rode the dugong, or got away) — circles where it last
    ///    saw them for a moment, then returns to its lane.
    ///  - Retreat: after a bite it turns away for a few seconds.
    /// A bite costs HP (Bubble Barrier absorbs one) and scatters every herd in the ribbon.
    /// Its body is wide, so it can't follow the diver into narrow crevices. It never enters the
    /// spawn lagoon (west of <see cref="_safeBeforeX"/>).
    /// </summary>
    public class Predator : MonoBehaviour
    {
        public enum Mode { Patrol, Chase, Search, Retreat }

        [Header("Body")]
        [SerializeField] private GameObject _model;
        [SerializeField, Min(0.5f)] private float _length = 3.5f;
        [SerializeField] private float _modelYaw;

        [Header("Lane")]
        [Tooltip("Waypoints of the patrol loop (children of this transform, in order).")]
        [SerializeField] private Transform _path;
        [SerializeField, Min(0.5f)] private float _patrolSpeed = 3.2f;

        [Header("Hunting (the minigame sets these from its difficulty profile)")]
        [SerializeField, Min(0.5f)] private float _chaseSpeed = 5.6f;
        [SerializeField, Min(2f)] private float _sightRange = 16f;
        [SerializeField, Range(0f, 180f)] private float _sightHalfAngle = 110f;
        [SerializeField, Min(2f)] private float _giveUpRange = 28f;
        [SerializeField, Min(1f)] private float _maxChaseSeconds = 12f;
        [SerializeField, Min(0.5f)] private float _biteRange = 2.2f;
        [SerializeField, Min(0f)] private float _biteDamage = 14f;
        [SerializeField, Min(0.5f)] private float _retreatSeconds = 5f;
        [SerializeField, Min(0.5f)] private float _searchSeconds = 3f;
        [Tooltip("Body radius used to steer around rocks (it won't squeeze through gaps narrower than about twice this).")]
        [SerializeField, Min(0.2f)] private float _bodyRadius = 1.1f;
        [Tooltip("World X west of which the diver is safe and the predator never goes (Caluya's spawn lagoon).")]
        [SerializeField] private float _safeBeforeX = 110f;
        [Tooltip("After a chase times out, it won't start another for this long.")]
        [SerializeField, Min(0f)] private float _rechaseDelay = 4f;

        private static readonly List<Predator> _active = new List<Predator>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _active.Clear(); Bit = null; ChaseStarted = null; ClosingIn = null; GaveUp = null; }

        /// <summary>Raised when a bite lands (after Bubble Barrier): the minigame scatters the ribbon and counts it.</summary>
        public static event Action<Predator, bool> Bit;   // (predator, harmedPlayer)
        /// <summary>A predator spotted the diver and started a chase.</summary>
        public static event Action<Predator> ChaseStarted;
        /// <summary>A chasing predator came within <see cref="ClosingInRange"/> (once per chase).</summary>
        public static event Action<Predator> ClosingIn;
        /// <summary>A chase ended without a bite: the diver hid, got away or the predator tired.</summary>
        public static event Action<Predator> GaveUp;

        private const float ClosingInRange = 7f;
        private bool _closedIn;

        // The attack, made readable: close in, coil (slow down and line up) for a moment, then
        // lunge through the diver's position. A bite only lands during the lunge; a miss sails
        // past and it has to turn and try again.
        private enum Lunge { None, Coil, Strike }
        private const float LungeRange = 6.5f;
        private const float CoilSeconds = 0.45f;
        private const float StrikeSeconds = 0.75f;
        private const float StrikeSpeedScale = 2.3f;
        private const float MissCooldown = 1.3f;
        private Lunge _lunge;
        private float _lungeUntil;
        private float _nextLunge;
        private Vector3 _strikeTarget;
        private float _thrashUntil;

        /// <summary>Coiling or lunging right now (the alert UI marks it).</summary>
        public bool IsAttacking => _lunge != Lunge.None;

        public static IReadOnlyList<Predator> Active => _active;

        /// <summary>0..1: how close the nearest chasing predator is (drives the red screen edge).</summary>
        public static float Threat
        {
            get
            {
                float best = 0f;
                PlayerController player = PlayerController.Instance;
                if (player == null) return 0f;
                foreach (Predator p in _active)
                {
                    if (p.CurrentMode != Mode.Chase) continue;
                    float d = Vector3.Distance(p.transform.position, player.transform.position);
                    best = Mathf.Max(best, Mathf.InverseLerp(p._giveUpRange, p._biteRange, d));
                }
                return best;
            }
        }

        public Mode CurrentMode { get; private set; } = Mode.Patrol;

        private Transform _body;
        private Vector3 _velocity;
        private int _waypoint;
        private float _modeUntil;
        private float _chaseStarted;
        private Vector3 _lastSeen;
        private float _phase;
        private float _noChaseUntil;

        public void Configure(float chaseSpeed, float sightRange, float biteDamage)
        {
            _chaseSpeed = chaseSpeed;
            _sightRange = sightRange;
            _giveUpRange = Mathf.Max(_giveUpRange, sightRange + 10f);
            _biteDamage = biteDamage;
        }

        private void Awake()
        {
            _phase = UnityEngine.Random.value * 10f;
            if (_model != null) _body = SwimBody.Spawn(_model, transform, _length, _modelYaw);
            // The root moves; the body is a child that only turns.
            if (_body != null) _body.localPosition = Vector3.zero;
            _waypoint = NearestWaypoint();
        }

        private void OnEnable() { if (!_active.Contains(this)) _active.Add(this); SetMode(Mode.Patrol); }
        private void OnDisable() => _active.Remove(this);

        // ───────────────────────── Brain ─────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            PlayerController player = PlayerController.Instance;
            Vector3 pos = transform.position;
            Vector3 diver = player != null ? player.transform.position : pos + Vector3.forward * 1000f;
            float dist = Vector3.Distance(pos, diver);
            bool exposed = player != null && !HidingZone.PlayerHidden && diver.x >= _safeBeforeX;
            bool canSee = exposed && Time.time >= _noChaseUntil && dist <= _sightRange && InView(diver - pos);
            bool canTrack = exposed && dist <= _giveUpRange;

            Vector3 goal;
            float speed;
            switch (CurrentMode)
            {
                case Mode.Chase:
                    bool tired = Time.time - _chaseStarted > _maxChaseSeconds;
                    if (tired) _noChaseUntil = Time.time + _rechaseDelay;
                    if (!canTrack || tired)
                    {
                        SetMode(Mode.Search);
                        if (Time.time - _chaseStarted >= 1.5f) GaveUp?.Invoke(this);   // not for a chase that never got going
                        goto case Mode.Search;
                    }
                    if (!_closedIn && dist <= ClosingInRange) { _closedIn = true; ClosingIn?.Invoke(this); }
                    _lastSeen = diver;
                    goal = diver;
                    speed = _chaseSpeed;
                    Attack(player, pos, diver, dist, ref goal, ref speed);
                    break;

                case Mode.Search:
                    if (canSee) { StartChase(); goal = diver; speed = _chaseSpeed; break; }
                    if (Time.time >= _modeUntil) { SetMode(Mode.Patrol); goal = Waypoint(); speed = _patrolSpeed; break; }
                    float a = (Time.time + _phase) * 1.4f;
                    goal = _lastSeen + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 5f;
                    speed = _patrolSpeed * 1.2f;
                    break;

                case Mode.Retreat:
                    if (Time.time >= _modeUntil) { SetMode(Mode.Patrol); }
                    goal = pos + (pos - diver).normalized * 10f;
                    speed = _chaseSpeed * 0.8f;
                    break;

                default:
                    if (canSee) { StartChase(); goal = diver; speed = _chaseSpeed; break; }
                    goal = Waypoint();
                    speed = _patrolSpeed;
                    if (Vector3.Distance(pos, goal) < 3f) _waypoint = (_waypoint + 1) % Mathf.Max(1, WaypointCount);
                    break;
            }

            Move(goal, speed, dt);
        }

        private void Attack(PlayerController player, Vector3 pos, Vector3 diver, float dist, ref Vector3 goal, ref float speed)
        {
            switch (_lunge)
            {
                case Lunge.None:
                    if (dist <= LungeRange && Time.time >= _nextLunge)
                    {
                        _lunge = Lunge.Coil;
                        _lungeUntil = Time.time + CoilSeconds;
                    }
                    break;
                case Lunge.Coil:
                    speed = _chaseSpeed * 0.3f;   // checks its speed and lines up
                    if (Time.time >= _lungeUntil)
                    {
                        Vector3 through = diver - pos;
                        _strikeTarget = diver + (through.sqrMagnitude > 0.01f ? through.normalized : transform.forward) * 3f;
                        _lunge = Lunge.Strike;
                        _lungeUntil = Time.time + StrikeSeconds;
                    }
                    break;
                case Lunge.Strike:
                    goal = _strikeTarget;
                    speed = _chaseSpeed * StrikeSpeedScale;
                    if (dist <= _biteRange + 0.6f) { _lunge = Lunge.None; Bite(player); }
                    else if (Time.time >= _lungeUntil) { _lunge = Lunge.None; _nextLunge = Time.time + MissCooldown; }
                    break;
            }
        }

        private void StartChase()
        {
            SetMode(Mode.Chase);
            _chaseStarted = Time.time;
            _closedIn = false;
            ChaseStarted?.Invoke(this);
        }

        private void SetMode(Mode mode)
        {
            CurrentMode = mode;
            if (mode != Mode.Chase) _lunge = Lunge.None;
            if (mode == Mode.Search) _modeUntil = Time.time + _searchSeconds;
            if (mode == Mode.Retreat) _modeUntil = Time.time + _retreatSeconds;
            if (mode == Mode.Patrol) _waypoint = NearestWaypoint();
        }

        private bool InView(Vector3 toDiver)
        {
            Vector3 fwd = _velocity.sqrMagnitude > 0.01f ? _velocity.normalized : transform.forward;
            return Vector3.Angle(fwd, toDiver) <= _sightHalfAngle;
        }

        private void Bite(PlayerController player)
        {
            PlayerHealth health = PlayerHealth.Instance;
            bool harmed = false;
            if (health != null && !health.IsInvulnerable)
            {
                float before = health.Current;
                health.TakeDamage(_biteDamage, DamageKind.Contact);   // Bubble Barrier may absorb it
                harmed = health.Current < before;
            }
            // Make it felt and seen: the diver is shoved away, the camera jolts, the shark thrashes.
            _thrashUntil = Time.time + 0.6f;
            if (player != null)
            {
                Vector3 away = player.transform.position - transform.position;
                away.y *= 0.3f;
                if (harmed) player.Knockback(away, 3f, 0.3f);
            }
            CameraController.Instance?.Shake(harmed ? 0.35f : 0.15f, 0.4f);
            Bit?.Invoke(this, harmed);
            SetMode(Mode.Retreat);
        }

        // ───────────────────────── Movement ─────────────────────────

        private void Move(Vector3 goal, float speed, float dt)
        {
            Vector3 pos = transform.position;
            // Never into the lagoon.
            if (goal.x < _safeBeforeX + 5f) goal.x = _safeBeforeX + 5f;

            Vector3 desired = Vector3.ClampMagnitude(goal - pos, 1f) * speed;
            desired += AvoidRocks(pos, _velocity) * speed;
            float accel = _lunge == Lunge.Strike ? speed * 4f : Mathf.Max(3f, speed * 1.2f);   // the lunge snaps forward
            _velocity = Vector3.MoveTowards(_velocity, desired, dt * accel);
            Vector3 next = pos + _velocity * dt;

            // Stay off the seabed and under the surface.
            float floor = SwimBody.SeabedY(next, next.y + 15f);
            next.y = Mathf.Clamp(next.y, floor + 1.5f, -1.5f);
            transform.position = next;
            if (_body != null) SwimBody.Face(_body, _velocity, _phase, dt, Time.time < _thrashUntil ? 35f : 7f);
        }

        private Vector3 AvoidRocks(Vector3 pos, Vector3 velocity)
        {
            if (velocity.sqrMagnitude < 0.01f) return Vector3.zero;
            Vector3 dir = velocity.normalized;
            float look = 3f + velocity.magnitude * 0.6f;
            if (Physics.SphereCast(pos, _bodyRadius, dir, out RaycastHit hit, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerController>() == null)
            {
                Vector3 away = Vector3.ProjectOnPlane(hit.normal, dir).normalized + Vector3.up * 0.4f;
                return away * (1f - hit.distance / look) * 1.6f;
            }
            return Vector3.zero;
        }

        // ───────────────────────── Path ─────────────────────────

        private int WaypointCount => _path != null ? _path.childCount : 0;

        private Vector3 Waypoint() =>
            WaypointCount > 0 ? _path.GetChild(Mathf.Clamp(_waypoint, 0, WaypointCount - 1)).position : transform.position;

        private int NearestWaypoint()
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < WaypointCount; i++)
            {
                float d = (_path.GetChild(i).position - transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private void OnDrawGizmosSelected()
        {
            if (_path == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            for (int i = 0; i < _path.childCount; i++)
                Gizmos.DrawLine(_path.GetChild(i).position, _path.GetChild((i + 1) % _path.childCount).position);
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _sightRange);
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject model, float length, float modelYaw, Transform path)
        {
            _model = model;
            _length = length;
            _modelYaw = modelYaw;
            _path = path;
        }
#endif
    }
}
