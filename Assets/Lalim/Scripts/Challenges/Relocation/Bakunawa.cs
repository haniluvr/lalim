using System;
using System.Collections;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Challenges.Gathering;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// Bakunawa, the moon-eating serpent of Padre Burgos (Minigame 3). It sleeps coiled on the
    /// abyss floor until the diver wakes it; then it roars and hunts the diver across the whole map.
    ///
    ///  - Chase: swims after the diver, a little faster than their normal swim, so it takes Surge,
    ///    Riptide, a gap narrower than its body (about 6 m), or cover to get away.
    ///  - Strike: close in, rear back (jaw open) for a moment, then lunge. A strike costs HP (Bubble
    ///    Barrier blocks it) and bursts the net-bag.
    ///  - The Radiant Conch played within reach calms it for a while: it drifts, glowing soft blue.
    ///  - After about a minute chasing outside Padre Burgos it hisses and swims home to its lair
    ///    (the rubber band), giving the diver a safe window. It rests there until the diver comes
    ///    back within its sense range.
    ///  - Appeased at the end, it settles back into its coil.
    /// </summary>
    [RequireComponent(typeof(BakunawaBody))]
    public class Bakunawa : MonoBehaviour
    {
        public enum State { Asleep, Roaring, Chase, Strike, Recover, Calmed, Returning, Resting, Appeased }

        [Header("Lair")]
        [Tooltip("Where it lies coiled (the abyss floor).")]
        [SerializeField] private Transform _lair;
        [Tooltip("Padre Burgos starts at this world X; chasing west of it starts the rubber-band clock.")]
        [SerializeField] private float _zoneMinX = 1300f;

        [Header("Hunting (the minigame sets these from its difficulty profile)")]
        [SerializeField, Min(1f)] private float _chaseSpeed = 6.2f;
        [SerializeField, Min(1f)] private float _cruiseSpeed = 4f;
        [SerializeField, Min(5f)] private float _senseRange = 45f;
        [SerializeField, Min(0f)] private float _strikeDamage = 20f;
        [SerializeField, Min(1f)] private float _calmSeconds = 10f;
        [SerializeField, Min(5f)] private float _returnAfterSeconds = 60f;
        [SerializeField, Min(5f)] private float _calmRange = 30f;
        [Tooltip("Seconds without seeing the diver (hidden, or behind rock) before it gives up and goes home.")]
        [SerializeField, Min(1f)] private float _loseAfterSeconds = 7f;

        [Header("Strike")]
        [SerializeField, Min(2f)] private float _strikeRange = 14f;
        [SerializeField, Min(1f)] private float _biteRadius = 4.5f;
        [SerializeField, Min(0.1f)] private float _rearSeconds = 0.7f;
        [SerializeField, Min(0.1f)] private float _lungeSeconds = 0.9f;
        [SerializeField, Min(1f)] private float _lungeSpeedScale = 2.4f;
        [SerializeField, Min(0.5f)] private float _recoverSeconds = 4f;

        [Header("Sound")]
        [SerializeField] private AudioClip _roarClip;
        [SerializeField] private AudioClip _hissClip;
        [SerializeField] private AudioClip _strikeClip;

        /// <summary>A strike landed (harmed = it got through Bubble Barrier).</summary>
        public static event Action<bool> Struck;
        /// <summary>It gave up the chase and is heading home (the rubber band, or lost the diver).</summary>
        public static event Action WentHome;

        public static Bakunawa Instance { get; private set; }
        public State Current { get; private set; } = State.Asleep;
        public bool IsHunting => Current == State.Chase || Current == State.Strike || Current == State.Recover;
        /// <summary>Extra sense range: each school the diver carries makes them easier to sense.</summary>
        public float SenseBonus { get; set; }

        private BakunawaBody _body;
        private Vector3 _velocity;
        private float _stateUntil;
        private float _outsideZone;
        private float _unseenFor;
        private Vector3 _lungeTarget;
        private Renderer[] _glowRenderers;
        private MaterialPropertyBlock _mpb;
        private float _calm01;

        public void Configure(float chaseSpeed, float senseRange, float strikeDamage, float calmSeconds, float returnAfter)
        {
            _chaseSpeed = chaseSpeed;
            _senseRange = senseRange;
            _strikeDamage = strikeDamage;
            _calmSeconds = calmSeconds;
            _returnAfterSeconds = returnAfter;
        }

        private void Awake()
        {
            Instance = this;
            _body = GetComponent<BakunawaBody>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            var glow = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r.name != "dragon-low") glow.Add(r);
            _glowRenderers = glow.ToArray();
            if (Current == State.Asleep) Sleep();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnEnable() => EventBus.Subscribe<RelicUsedEvent>(OnRelicUsed);
        private void OnDisable() => EventBus.Unsubscribe<RelicUsedEvent>(OnRelicUsed);

        // ───────────────────────── Story beats ─────────────────────────

        /// <summary>Coiled asleep on the abyss floor.</summary>
        public void Sleep()
        {
            Current = State.Asleep;
            _velocity = Vector3.zero;
            CoilAtLair();
        }

        /// <summary>The roar that wakes it: the jaw opens wide, the sea shakes, then the hunt begins.</summary>
        public IEnumerator Roar()
        {
            Current = State.Roaring;
            _body.SetJaw(1f);
            GameAudio.PlayAt(_roarClip, transform.position, 1f, AudioChannel.Sfx, 60f);
            CameraController.Instance?.Shake(0.6f, 2.4f);
            // It rears up out of its coil while it roars.
            Vector3 from = transform.position, up = from + Vector3.up * _body.Thickness * 3f;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, up, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                yield return null;
            }
            _body.SetJaw(0.15f);
            StartChase();
            _noStrikeUntil = Time.time + 5f;   // the diver gets a head start out of the abyss
        }

        private float _noStrikeUntil;

        /// <summary>Wakes already hunting (resuming a save after the roar): it starts at home, resting.</summary>
        public void WakeResting()
        {
            CoilAtLair();
            Current = State.Resting;
            _stateUntil = Time.time + 3f;
        }

        /// <summary>Every school is home: it swims back to its lair to wait for the conch.</summary>
        public void GoHomeAndWait()
        {
            if (Current == State.Appeased) return;
            Current = State.Returning;
            _stateUntil = float.MaxValue;
        }

        /// <summary>The conch played for it at the end: the anger leaves it and it coils to sleep.</summary>
        public void Appease()
        {
            Current = State.Appeased;
            _body.SetJaw(0f);
            StartCoroutine(SettleHome());
        }

        /// <summary>The diver respawned at a checkpoint: it goes back to its lair.</summary>
        public void ResetToLair()
        {
            if (Current == State.Asleep || Current == State.Appeased) return;
            WakeResting();
        }

        private void CoilAtLair()
        {
            Vector3 home = LairPoint();
            transform.position = home;
            _body.CoilAt(home, _lair != null ? _lair.forward : Vector3.forward);
        }

        private Vector3 LairPoint()
        {
            Vector3 p = _lair != null ? _lair.position : transform.position;
            float floor = SwimBody.SeabedY(p, p.y + 20f);
            return new Vector3(p.x, floor + _body.Thickness * 0.6f, p.z);
        }

        private IEnumerator SettleHome()
        {
            Vector3 home = LairPoint();
            while (Vector3.Distance(transform.position, home) > 3f)
            {
                Steer(home, _cruiseSpeed, Time.deltaTime, false);
                yield return null;
            }
            CoilAtLair();
        }

        // ───────────────────────── Brain ─────────────────────────

        private void StartChase()
        {
            Current = State.Chase;
            _outsideZone = 0f;
            _unseenFor = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateGlow(dt);
            if (Current == State.Asleep || Current == State.Roaring || Current == State.Appeased) return;

            PlayerController player = PlayerController.Instance;
            Vector3 pos = transform.position;
            Vector3 diver = player != null ? player.transform.position : pos;
            float dist = Vector3.Distance(pos, diver);

            switch (Current)
            {
                case State.Chase:
                {
                    bool seen = player != null && !HidingZone.PlayerHidden && CanSee(diver);
                    _unseenFor = seen ? 0f : _unseenFor + dt;
                    if (diver.x < _zoneMinX) _outsideZone += dt;
                    if (_outsideZone >= _returnAfterSeconds || _unseenFor >= _loseAfterSeconds)
                    {
                        GameAudio.PlayAt(_hissClip, pos, 1f, AudioChannel.Sfx, 40f);
                        Current = State.Returning;
                        WentHome?.Invoke();
                        break;
                    }
                    if (seen && dist <= _strikeRange && Time.time >= _noStrikeUntil)
                    {
                        Current = State.Strike;
                        _stateUntil = Time.time + _rearSeconds;
                        _lungeTarget = Vector3.zero;
                        _body.SetJaw(1f);
                    }
                    Steer(diver, _chaseSpeed, dt, true);
                    break;
                }
                case State.Strike:
                    if (_lungeTarget == Vector3.zero)
                    {
                        // Rearing back: slow, lining up.
                        Steer(diver, _chaseSpeed * 0.25f, dt, true);
                        if (Time.time >= _stateUntil)
                        {
                            Vector3 through = diver - pos;
                            _lungeTarget = diver + (through.sqrMagnitude > 0.01f ? through.normalized : transform.forward) * 6f;
                            _stateUntil = Time.time + _lungeSeconds;
                            GameAudio.PlayAt(_strikeClip, pos, 1f, AudioChannel.Sfx, 30f);
                        }
                    }
                    else
                    {
                        Steer(_lungeTarget, _chaseSpeed * _lungeSpeedScale, dt, true, snap: true);
                        if (player != null && dist <= _biteRadius) Bite(player);
                        else if (Time.time >= _stateUntil) Recover();
                    }
                    break;
                case State.Recover:
                    Steer(pos + (pos - diver).normalized * 20f + Vector3.up * 4f, _cruiseSpeed, dt, false);
                    if (Time.time >= _stateUntil) StartChase();
                    break;
                case State.Calmed:
                    Steer(pos + transform.forward * 5f, 1.2f, dt, false);
                    if (Time.time >= _stateUntil) StartChase();
                    break;
                case State.Returning:
                {
                    Vector3 home = LairPoint() + Vector3.up * _body.Thickness * 2f;
                    Steer(home, _cruiseSpeed * 1.3f, dt, false);
                    if (Vector3.Distance(pos, home) < 8f && _stateUntil != float.MaxValue) { Current = State.Resting; _stateUntil = Time.time + 4f; }
                    break;
                }
                case State.Resting:
                    Steer(LairPoint() + new Vector3(Mathf.Cos(Time.time * 0.2f), 0.6f, Mathf.Sin(Time.time * 0.2f)) * 12f, 2f, dt, false);
                    // It wakes to the diver coming back within reach (carrying schools makes it easier).
                    if (Time.time >= _stateUntil && player != null && diver.x >= _zoneMinX && dist <= _senseRange + SenseBonus && !HidingZone.PlayerHidden)
                        StartChase();
                    break;
            }
        }

        private bool CanSee(Vector3 diver)
        {
            Vector3 from = transform.position;
            Vector3 to = diver - from;
            if (to.magnitude > _senseRange * 3f + SenseBonus) return false;
            // Rock between us blocks its sight (it can't see into a cave or around a ridge).
            return !Physics.Raycast(from, to.normalized, out RaycastHit hit, to.magnitude - 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   || hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        private void Bite(PlayerController player)
        {
            PlayerHealth health = PlayerHealth.Instance;
            bool harmed = false;
            if (health != null && !health.IsInvulnerable)
            {
                float before = health.Current;
                health.TakeDamage(_strikeDamage, DamageKind.Contact);   // Bubble Barrier may block it
                harmed = health.Current < before;
            }
            Vector3 away = player.transform.position - transform.position;
            away.y *= 0.3f;
            if (harmed) player.Knockback(away, 5f, 0.35f);
            CameraController.Instance?.Shake(harmed ? 0.5f : 0.2f, 0.5f);
            Struck?.Invoke(harmed);
            Recover();
        }

        private void Recover()
        {
            Current = State.Recover;
            _stateUntil = Time.time + _recoverSeconds;
            _body.SetJaw(0.15f);
        }

        private void OnRelicUsed(RelicUsedEvent evt)
        {
            if (evt.ItemId != GameConstants.Relics.RADIANT_CONCH || !IsHunting) return;
            PlayerController player = PlayerController.Instance;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > _calmRange) return;
            Current = State.Calmed;
            _stateUntil = Time.time + _calmSeconds;
            _body.SetJaw(0f);
        }

        /// <summary>Within reach of the conch (the appeasement asks the diver to come this close).</summary>
        public bool InCalmRange(Vector3 p) => Vector3.Distance(p, transform.position) <= _calmRange * 1.5f;

        // ───────────────────────── Movement ─────────────────────────

        private void Steer(Vector3 goal, float speed, float dt, bool hunting, bool snap = false)
        {
            Vector3 pos = transform.position;
            Vector3 desired = Vector3.ClampMagnitude(goal - pos, 1f) * speed;
            desired += AvoidRock(pos, _velocity) * speed;
            float accel = snap ? speed * 3f : Mathf.Max(2f, speed * 0.7f);
            _velocity = Vector3.MoveTowards(_velocity, desired, dt * accel);
            Vector3 next = pos + _velocity * dt;
            // Its whole body stays off the seabed and under the surface.
            float r = _body.Thickness * 0.5f;
            float floor = SwimBody.SeabedY(next, next.y + 20f);
            next.y = Mathf.Clamp(next.y, floor + r, -2f - r);
            transform.position = next;
            if (_velocity.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_velocity), dt * 3f);
        }

        // It can't fit where its body can't: a sphere the size of its body, cast ahead.
        private Vector3 AvoidRock(Vector3 pos, Vector3 velocity)
        {
            if (velocity.sqrMagnitude < 0.01f) return Vector3.zero;
            Vector3 dir = velocity.normalized;
            float look = 6f + velocity.magnitude;
            if (Physics.SphereCast(pos, _body.Thickness * 0.5f, dir, out RaycastHit hit, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerController>() == null)
            {
                Vector3 away = Vector3.ProjectOnPlane(hit.normal, dir).normalized + Vector3.up * 0.5f;
                return away * (1f - hit.distance / look) * 2f;
            }
            return Vector3.zero;
        }

        // Calm: the glowing scales cool from angry ember to soft blue.
        private void UpdateGlow(float dt)
        {
            if (_glowRenderers == null) return;
            bool calm = Current == State.Calmed || Current == State.Appeased || Current == State.Asleep || Current == State.Resting;
            _calm01 = Mathf.MoveTowards(_calm01, calm ? 1f : 0f, dt * 0.8f);
            Color angry = new Color(1f, 0.45f, 0.25f), soft = new Color(0.45f, 0.8f, 1f);
            Color c = Color.Lerp(angry, soft, _calm01) * (Current == State.Strike ? 1.6f : 1f);
            foreach (Renderer r in _glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(_mpb);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform lair, AudioClip roar, AudioClip hiss, AudioClip strike)
        {
            _lair = lair;
            _roarClip = roar;
            _hissClip = hiss;
            _strikeClip = strike;
        }
#endif
    }
}
