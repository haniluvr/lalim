using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Mechanics;

namespace Lalim.Challenges.Catching
{
    /// <summary>
    /// Something in a Root Pool the Woven Net can close around: a school of fish that may be taken,
    /// or a protected animal (turtle, stingray, giant clam) that must be left alone.
    ///
    /// Readability (design doc): targets stay low and dart fast; protected animals swim slow and
    /// high. Spirit Sense colour-codes them (green target, red protected) with a soft aura on each
    /// body. A protected animal caught in the net is released and bolts before drifting back.
    ///
    /// Movement: wander inside the pool's circle and depth band, or, with a patrol line, glide back
    /// and forth along it (lesson 3: a turtle that keeps crossing in front of the school).
    /// </summary>
    public class CatchableSchool : MonoBehaviour, ISpiritSenseTarget
    {
        public static readonly Color TargetColor = new Color(0.35f, 1f, 0.45f);
        public static readonly Color ProtectedColor = new Color(1f, 0.3f, 0.25f);

        private static readonly List<CatchableSchool> _all = new List<CatchableSchool>();
        public static IReadOnlyList<CatchableSchool> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();

        public enum SchoolState { Free, Netted, Caught, Fleeing }

        private class Body
        {
            public Transform T;
            public Vector3 Offset;      // place in the school
            public Vector3 Velocity;
            public float Phase;
            public float Scale;
        }

        private readonly List<Body> _bodies = new List<Body>();
        private ParticleSystem _aura;
        private ParticleSystem.Particle[] _auraParticles;
        private Vector3 _centre;          // the school's own centre (moves)
        private Vector3 _velocity;
        private Vector3 _target;
        private float _nextTurn;
        private float _glow;              // Spirit Sense aura 0..1
        private float _glowTarget;
        private float _glowIntensity = 1f;
        private float _fleeUntil;
        private float _noiseSeed;   // so schools don't speed up and slow down in step
        private Transform _pullTo;
        private Action _onPulled;

        // Pool and behaviour
        private Vector3 _poolCentre;
        private float _poolRadius = 10f;
        private float _minHeight = 0.8f, _maxHeight = 2.5f;   // above the seabed
        private float _speed = 2.2f;
        private bool _static;                                  // the giant clam
        private Vector3 _patrolA, _patrolB;
        private bool _patrol;
        private float _patrolT;

        public string SpeciesKey { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsProtected { get; private set; }
        public SchoolState State { get; private set; } = SchoolState.Free;
        public bool IsFree => State == SchoolState.Free;
        public Vector3 Centre => _centre;
        public GameObject Model { get; private set; }
        public int Count => _bodies.Count;
        public float BodyLength { get; private set; }

        /// <summary>Lesson 2 counts a throw only after the player has looked with Spirit Sense.</summary>
        public static bool AnyGlowing
        {
            get { foreach (CatchableSchool s in _all) if (s._glowTarget > 0f) return true; return false; }
        }

        // ───────────────────────── Spawning ─────────────────────────

        public struct Spec
        {
            public string Key;
            public string Name;
            public bool Protected;
            public GameObject Model;
            public int Count;
            public float Length;
            public float ModelYaw;
            public float Speed;
            public float MinHeight, MaxHeight;
            public bool Static;
        }

        public static CatchableSchool Spawn(Spec spec, Vector3 poolCentre, float poolRadius, Vector3 at, Material auraMaterial, Transform parent)
        {
            var go = new GameObject((spec.Protected ? "Protected_" : "School_") + spec.Key);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<CatchableSchool>();
            s.Build(spec, poolCentre, poolRadius, at, auraMaterial);
            return s;
        }

        /// <summary>Glide back and forth between two points (in front of the school, for lesson 3).</summary>
        public void SetPatrol(Vector3 a, Vector3 b)
        {
            _patrol = true;
            _patrolA = a;
            _patrolB = b;
            _patrolT = 0f;
        }

        public void SetSpeed(float speed) => _speed = Mathf.Max(0.2f, speed);

        public static readonly Color PanicColor = new Color(1f, 0.85f, 0.2f);
        private bool _panicked;
        private float _calmSpeed;

        /// <summary>
        /// Minigame 3: scattered from a burst net-bag, the school darts about glowing panicked yellow
        /// (always visible, no Spirit Sense needed). The conch calms it: it slows and the glow fades.
        /// </summary>
        public void SetPanicked(bool panicked)
        {
            if (_calmSpeed <= 0f) _calmSpeed = _speed;
            _panicked = panicked;
            _speed = panicked ? _calmSpeed * 1.7f : _calmSpeed * 0.6f;
            _glowTarget = panicked ? 1f : 0f;
            _glowIntensity = 1f;
            _nextTurn = 0f;
        }

        public bool IsPanicked => _panicked;

        private bool _midwater;

        /// <summary>Keep to the depth of its water's centre instead of hugging the seabed (fish scattered over the abyss stay where the bag burst).</summary>
        public void SetMidwater(bool on)
        {
            _midwater = on;
            if (!on) return;
            // Spawned on the seabed far below: lift the whole school to its water's depth at once.
            float dy = _poolCentre.y + UnityEngine.Random.Range(-1f, 1f) - _centre.y;
            if (dy <= 0f) return;
            _centre.y += dy;
            _target = _centre;
            foreach (Body b in _bodies) if (b.T != null) b.T.position += Vector3.up * dy;
            transform.position = _centre;
        }

        /// <summary>Moves the water the school keeps to (the conch draws scattered fish in close).</summary>
        public void MoveHome(Vector3 centre, float radius)
        {
            _poolCentre = centre;
            _poolRadius = Mathf.Max(1f, radius);
            _nextTurn = 0f;
            _homing = true;   // swim there, rather than snapping inside the new water
        }

        private bool _homing;

        private void Build(Spec spec, Vector3 poolCentre, float poolRadius, Vector3 at, Material auraMaterial)
        {
            SpeciesKey = spec.Key;
            DisplayName = spec.Name;
            IsProtected = spec.Protected;
            Model = spec.Model;
            BodyLength = spec.Length;
            _poolCentre = poolCentre;
            _poolRadius = poolRadius;
            _minHeight = spec.MinHeight;
            _maxHeight = Mathf.Max(spec.MinHeight + 0.2f, spec.MaxHeight);
            _speed = spec.Speed;
            _static = spec.Static;
            _noiseSeed = UnityEngine.Random.Range(0f, 100f);

            float floor = SwimBody.SeabedY(at, Mathf.Min(-0.3f, at.y + 6f));   // from near its own depth (the abyss is deeper than the ray)
            _centre = _static ? new Vector3(at.x, floor + spec.Length * 0.25f, at.z)
                              : new Vector3(at.x, floor + UnityEngine.Random.Range(_minHeight, _maxHeight), at.z);
            _target = _centre;
            transform.position = _centre;

            int count = Mathf.Max(1, spec.Count);
            float spread = count == 1 ? 0f : Mathf.Lerp(0.6f, 1.4f, Mathf.InverseLerp(2, 8, count));
            for (int i = 0; i < count; i++)
            {
                Transform t = spec.Model != null ? SwimBody.Spawn(spec.Model, transform, spec.Length, spec.ModelYaw) : new GameObject("Body").transform;
                if (spec.Model == null) t.SetParent(transform, false);
                Vector3 off = count == 1 ? Vector3.zero : UnityEngine.Random.insideUnitSphere * spread;
                off.y *= 0.5f;
                var b = new Body { T = t, Offset = off, Phase = UnityEngine.Random.value * 10f, Scale = t.localScale.x };
                t.position = _centre + off;
                if (_static) t.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                _bodies.Add(b);
            }
            _aura = CreateAura(auraMaterial, count);

            // Spirit Sense finds us through SpiritGlow (its light in our colour); we add the aura.
            SpiritGlow.EnsureOn(this, IsProtected ? ProtectedColor : TargetColor, SpiritGlow.RelevanceMode.Auto);
            _all.Add(this);
        }

        private ParticleSystem CreateAura(Material material, int count)
        {
            var go = new GameObject("SenseAura");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            main.startLifetime = 1000f;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) r.sharedMaterial = material;
            ps.Emit(count);
            _auraParticles = new ParticleSystem.Particle[count];
            ps.Play();
            return ps;
        }

        private void OnDestroy() => _all.Remove(this);

        // ───────────────────────── Spirit Sense ─────────────────────────

        public bool IsRelevant(SpiritSenseContext ctx) => isActiveAndEnabled && State == SchoolState.Free;

        public void SetGlow(bool on, float intensity)
        {
            _glowTarget = on || _panicked ? 1f : 0f;   // panicked fish glow regardless
            _glowIntensity = Mathf.Max(0.3f, intensity);
        }

        // ───────────────────────── The net ─────────────────────────

        /// <summary>Is any body within <paramref name="radius"/> of <paramref name="point"/>?</summary>
        public bool Inside(Vector3 point, float radius)
        {
            if (State != SchoolState.Free) return false;
            float r = radius + BodyLength * 0.4f;
            foreach (Body b in _bodies)
                if (b.T != null && (b.T.position - point).sqrMagnitude <= r * r) return true;
            return false;
        }

        /// <summary>The net closed around us: hold still inside it.</summary>
        public void Net() { if (State == SchoolState.Free) State = SchoolState.Netted; }

        /// <summary>Taken: swim into <paramref name="bag"/>, shrinking, then call back and go.</summary>
        public void PullInto(Transform bag, Action onDone)
        {
            State = SchoolState.Caught;
            _pullTo = bag;
            _onPulled = onDone;
            _glowTarget = 0f;
        }

        /// <summary>Released from the net (protected, or a spoiled throw): bolt away from it, then settle.</summary>
        public void Release(Vector3 from)
        {
            if (State == SchoolState.Caught) return;
            State = SchoolState.Fleeing;
            _fleeUntil = Time.time + (_static ? 0.6f : 2.2f);
            if (_static) return;
            Vector3 away = _centre - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = UnityEngine.Random.onUnitSphere;
            _velocity = away.normalized * _speed * 3f + Vector3.up * 0.6f;
        }

        // ───────────────────────── Swimming ─────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (State)
            {
                case SchoolState.Free: Wander(dt); break;
                case SchoolState.Netted: _velocity = Vector3.Lerp(_velocity, Vector3.zero, dt * 6f); break;
                case SchoolState.Fleeing:
                    _velocity = Vector3.Lerp(_velocity, Vector3.zero, dt * 0.8f);
                    if (Time.time >= _fleeUntil) { State = SchoolState.Free; _nextTurn = 0f; }
                    break;
                case SchoolState.Caught: Pull(dt); break;
            }

            if (State != SchoolState.Caught)
            {
                if (!_static) _centre += _velocity * dt;
                KeepInPool();
            }
            transform.position = _centre;
            MoveBodies(dt);
            UpdateAura(dt);
        }

        private void Wander(float dt)
        {
            if (_static) { _velocity = Vector3.zero; return; }
            if (_patrol)
            {
                // Slow glide along the line, easing at each end.
                _patrolT += dt * _speed / Mathf.Max(1f, Vector3.Distance(_patrolA, _patrolB));
                float k = 0.5f - 0.5f * Mathf.Cos(_patrolT * Mathf.PI);
                Vector3 want = Vector3.Lerp(_patrolA, _patrolB, k);
                _velocity = (want - _centre) / Mathf.Max(dt, 0.0001f);
                _velocity = Vector3.ClampMagnitude(_velocity, _speed * 2f);
                return;
            }

            if (_homing)
            {
                Vector3 flatTo = _poolCentre - _centre;
                flatTo.y = 0f;
                if (flatTo.magnitude <= _poolRadius) _homing = false;
                else { _target = new Vector3(_poolCentre.x, _centre.y, _poolCentre.z); _nextTurn = Time.time + 0.5f; }
            }
            if (!_homing && (Time.time >= _nextTurn || (_centre - _target).sqrMagnitude < 0.5f))
            {
                Vector2 c = UnityEngine.Random.insideUnitCircle * _poolRadius * 0.8f;
                Vector3 p = new Vector3(_poolCentre.x + c.x, 0f, _poolCentre.z + c.y);
                float floor = SwimBody.SeabedY(p, Mathf.Min(-0.3f, _centre.y + 6f));
                p.y = _midwater ? Mathf.Max(floor + _minHeight, _poolCentre.y + UnityEngine.Random.Range(-2f, 2f))
                                : floor + UnityEngine.Random.Range(_minHeight, _maxHeight);
                _target = p;
                // Targets dart (short, quick legs); protected animals cruise.
                _nextTurn = Time.time + (IsProtected ? UnityEngine.Random.Range(5f, 9f) : _panicked ? UnityEngine.Random.Range(0.5f, 1.1f) : UnityEngine.Random.Range(1.2f, 2.6f));
            }
            Vector3 want2 = (_target - _centre);
            float speed = IsProtected ? _speed : _speed * (0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 0.8f, _noiseSeed));
            Vector3 desired = want2.sqrMagnitude > 0.01f ? want2.normalized * speed : Vector3.zero;
            _velocity = Vector3.Lerp(_velocity, desired, dt * (IsProtected ? 0.8f : 2.5f));
        }

        private void KeepInPool()
        {
            Vector3 flat = _centre - _poolCentre;
            flat.y = 0f;
            float max = _poolRadius;
            if (flat.magnitude > max && !_homing)
            {
                flat = flat.normalized * max;
                _centre = new Vector3(_poolCentre.x + flat.x, _centre.y, _poolCentre.z + flat.z);
            }
            float floor = SwimBody.SeabedY(_centre, Mathf.Min(-0.3f, _centre.y + 6f));
            float lo = _static ? floor + BodyLength * 0.25f : floor + _minHeight * 0.6f;
            if (_centre.y < lo) _centre.y = lo;
            if (_centre.y > -0.8f) _centre.y = -0.8f;   // stay under the surface
        }

        private void MoveBodies(float dt)
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                Body b = _bodies[i];
                if (b.T == null) continue;
                if (_static) continue;
                Vector3 wobble = new Vector3(Mathf.Sin(Time.time * 1.3f + b.Phase), Mathf.Sin(Time.time * 0.9f + b.Phase * 2f) * 0.4f, Mathf.Cos(Time.time * 1.1f + b.Phase)) * 0.25f;
                Vector3 want = _centre + b.Offset + (_bodies.Count > 1 ? wobble : Vector3.zero);
                Vector3 to = want - b.T.position;
                b.Velocity = Vector3.Lerp(b.Velocity, to * 4f + _velocity, dt * 5f);
                b.T.position += b.Velocity * dt;
                Vector3 face = _velocity.sqrMagnitude > 0.04f ? _velocity : b.Velocity;
                SwimBody.Face(b.T, face, b.Phase, dt, IsProtected ? 4f : 14f);
            }
        }

        private void Pull(float dt)
        {
            if (_pullTo == null) { Finish(); return; }
            bool allIn = true;
            foreach (Body b in _bodies)
            {
                if (b.T == null) continue;
                b.T.position = Vector3.MoveTowards(b.T.position, _pullTo.position, dt * 9f);
                float d = Vector3.Distance(b.T.position, _pullTo.position);
                b.T.localScale = Vector3.one * b.Scale * Mathf.Clamp01(d / 2.5f);
                if (d > 0.15f) allIn = false;
            }
            _centre = _pullTo.position;
            if (allIn) Finish();
        }

        private void Finish()
        {
            Action done = _onPulled;
            _onPulled = null;
            done?.Invoke();
            Destroy(gameObject);
        }

        private void UpdateAura(float dt)
        {
            _glow = Mathf.MoveTowards(_glow, _glowTarget, dt * 3f);
            if (_aura == null) return;
            int n = _aura.GetParticles(_auraParticles);
            Color c = _panicked ? PanicColor : IsProtected ? ProtectedColor : TargetColor;
            float size = Mathf.Max(1.4f, BodyLength * 2.4f);   // reads through Spirit Sense's grey
            for (int i = 0; i < n && i < _bodies.Count; i++)
            {
                Body b = _bodies[i];
                if (b.T == null) continue;
                _auraParticles[i].position = b.T.position;
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f + b.Phase);
                _auraParticles[i].startSize = size * pulse;
                _auraParticles[i].startColor = new Color(c.r, c.g, c.b, Mathf.Clamp01(0.95f * _glow * _glowIntensity));
                _auraParticles[i].remainingLifetime = 1000f;
            }
            _aura.SetParticles(_auraParticles, n);
        }
    }
}
