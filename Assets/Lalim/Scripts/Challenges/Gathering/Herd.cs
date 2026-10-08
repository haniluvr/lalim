using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Gathering
{
    /// <summary>
    /// One of Manunubo's lost herds: a few fish of one kind.
    ///  - Scattered: panicked, darting around their spot, glowing yellow.
    ///  - Following: called by the Radiant Conch — calm blue, trailing behind the diver in the ribbon.
    ///  - Delivered: swim into Manunubo's light and dissolve.
    /// A predator bite scatters a following herd again around where it happened.
    /// Fish have no colliders, so they never block the diver.
    /// </summary>
    public class Herd : MonoBehaviour
    {
        public enum HerdState { Scattered, Following, Delivered }

        public static readonly Color PanicColor = new Color(1f, 0.82f, 0.28f);
        public static readonly Color CalmColor = new Color(0.45f, 0.9f, 1f);

        private static readonly List<Herd> _all = new List<Herd>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();

        /// <summary>Every herd in the scene (the conch looks for scattered ones in range).</summary>
        public static IReadOnlyList<Herd> All => _all;

        /// <summary>A scattered herd heard the conch and turned to follow.</summary>
        public static event Action<Herd> Calmed;
        /// <summary>A calmed herd caught up and fell into the ribbon behind the diver.</summary>
        public static event Action<Herd> Joined;

        private bool _joinedRibbon;

        private class Fish
        {
            public Transform Body;
            public Renderer[] Renderers;
            public Color[] BaseColors;
            public Vector3 Velocity;
            public Vector3 Target;
            public float NextDart;
            public float Phase;
            public float Shrink = 1f;
            public float BaseScale = 1f;
        }

        private readonly List<Fish> _fish = new List<Fish>();
        private MaterialPropertyBlock _mpb;
        private ParticleSystem _motes;
        private ParticleSystem _aura;
        private ParticleSystem.Particle[] _auraParticles;
        private Transform _marker;
        private Vector3 _home;
        private float _homeRadius = 3.5f;
        private float _tint;            // 0 panicked yellow … 1 calm blue
        private Transform _deliverTo;
        private Action _onDelivered;

        public HerdState State { get; private set; } = HerdState.Scattered;
        public int FishCount => _fish.Count;
        public int Index { get; private set; }

        /// <summary>Follows the herd's middle (for navigation and range checks).</summary>
        public Transform Marker => _marker;
        public Vector3 Center => _marker != null ? _marker.position : transform.position;

        // ───────────────────────── Spawning ─────────────────────────

        public static Herd Spawn(int index, Vector3 home, GameObject model, int count, float length, float modelYaw, Material moteMaterial, Transform parent)
        {
            var go = new GameObject($"Herd_{index}");
            go.transform.SetParent(parent, false);
            go.transform.position = home;
            var herd = go.AddComponent<Herd>();
            herd.Index = index;
            herd.Build(home, model, count, length, modelYaw, moteMaterial);
            return herd;
        }

        private void Build(Vector3 home, GameObject model, int count, float length, float modelYaw, Material moteMaterial)
        {
            _mpb = new MaterialPropertyBlock();
            _home = KeepOffSeabed(home);
            _marker = new GameObject("Marker").transform;
            _marker.SetParent(transform, false);
            _marker.position = _home;

            for (int i = 0; i < count; i++)
            {
                Transform body = SwimBody.Spawn(model, transform, length, modelYaw);
                body.position = _home + UnityEngine.Random.insideUnitSphere * _homeRadius;
                var f = new Fish
                {
                    Body = body,
                    Renderers = body.GetComponentsInChildren<Renderer>(),
                    Phase = UnityEngine.Random.value * 10f,
                    BaseScale = body.localScale.x,
                };
                f.BaseColors = new Color[f.Renderers.Length];
                for (int r = 0; r < f.Renderers.Length; r++)
                {
                    Material m = f.Renderers[r].sharedMaterial;
                    f.BaseColors[r] = m == null ? Color.white : m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                }
                f.Target = body.position;
                _fish.Add(f);
            }

            _motes = CreateMotes(moteMaterial);
            _aura = CreateAura(moteMaterial, count);
            ApplyTint(force: true);
            _all.Add(this);
        }

        private ParticleSystem CreateMotes(Material material)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;   // emitted by hand, at the fish
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            ps.Play();
            return ps;
        }

        /// <summary>One soft glow per fish, kept on the fish by hand: the herd's colour reads from afar.</summary>
        private ParticleSystem CreateAura(Material material, int count)
        {
            var go = new GameObject("Aura");
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
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            ps.Emit(count);
            _auraParticles = new ParticleSystem.Particle[count];
            ps.Play();
            return ps;
        }

        private void UpdateAura()
        {
            if (_aura == null) return;
            int n = _aura.GetParticles(_auraParticles);
            Color c = CurrentColor;
            for (int i = 0; i < n && i < _fish.Count; i++)
            {
                Fish f = _fish[i];
                if (f.Body == null) continue;
                _auraParticles[i].position = f.Body.position;
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * (State == HerdState.Scattered ? 9f : 3f) + f.Phase);
                _auraParticles[i].startSize = 1.4f * pulse * f.Shrink;
                _auraParticles[i].startColor = new Color(c.r, c.g, c.b, 0.6f);
                _auraParticles[i].remainingLifetime = 1000f;
            }
            _aura.SetParticles(_auraParticles, n);
        }

        private void OnDestroy() => _all.Remove(this);

        // ───────────────────────── State changes ─────────────────────────

        /// <summary>The conch reached this herd: it calms and joins the ribbon.</summary>
        public bool Call()
        {
            if (State != HerdState.Scattered) return false;
            State = HerdState.Following;
            HerdRibbon.Instance.Join(this);
            _joinedRibbon = false;
            Calmed?.Invoke(this);
            return true;
        }

        /// <summary>A predator struck: the herd bolts and settles, panicked, around <paramref name="at"/>.</summary>
        public void Scatter(Vector3 at)
        {
            if (State != HerdState.Following) return;
            State = HerdState.Scattered;
            HerdRibbon.Instance.Leave(this);
            _home = KeepOffSeabed(at + UnityEngine.Random.insideUnitSphere * 4f);
            _homeRadius = 5f;
            foreach (Fish f in _fish)
            {
                Vector3 away = f.Body.position - at;
                away.y = Mathf.Abs(away.y) * 0.3f;
                f.Velocity = (away.sqrMagnitude > 0.01f ? away.normalized : UnityEngine.Random.onUnitSphere) * 7f;
                f.NextDart = Time.time + 0.6f;
            }
        }

        /// <summary>Swim into <paramref name="target"/> (Manunubo) and dissolve; then <paramref name="onDone"/>.</summary>
        public void Deliver(Transform target, Action onDone)
        {
            if (State != HerdState.Following) return;
            State = HerdState.Delivered;
            HerdRibbon.Instance.Leave(this);
            _deliverTo = target;
            _onDelivered = onDone;
        }

        // ───────────────────────── Swimming ─────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 sum = Vector3.zero;
            int alive = 0;

            for (int i = 0; i < _fish.Count; i++)
            {
                Fish f = _fish[i];
                if (f.Body == null) continue;
                switch (State)
                {
                    case HerdState.Scattered: Panic(f, dt); break;
                    case HerdState.Following: Follow(f, i, dt); break;
                    case HerdState.Delivered: Dissolve(f, dt); break;
                }
                f.Body.position += f.Velocity * dt;
                SwimBody.Face(f.Body, f.Velocity, f.Phase, dt, State == HerdState.Scattered ? 22f : 10f);
                sum += f.Body.position;
                alive++;
            }

            if (alive > 0) _marker.position = sum / alive;
            if (State == HerdState.Following && !_joinedRibbon && alive > 0)
            {
                HerdRibbon ribbon = HerdRibbon.Instance;
                if ((ribbon.SlotPosition(ribbon.SlotOf(this, 0)) - _marker.position).sqrMagnitude < 9f)
                {
                    _joinedRibbon = true;
                    Joined?.Invoke(this);
                }
            }
            ApplyTint(force: false);
            EmitMotes(dt);
            UpdateAura();

            if (State == HerdState.Delivered && AllDissolved())
            {
                Action done = _onDelivered;
                _onDelivered = null;
                done?.Invoke();
                Destroy(gameObject);
            }
        }

        private void Panic(Fish f, float dt)
        {
            // Jittery: a new dart target every fraction of a second, quick and twitchy.
            if (Time.time >= f.NextDart || (f.Body.position - f.Target).sqrMagnitude < 0.25f)
            {
                f.Target = _home + UnityEngine.Random.insideUnitSphere * _homeRadius;
                f.Target.y = Mathf.Min(f.Target.y, -5f);   // never darting up out of the diver's reach
                f.NextDart = Time.time + UnityEngine.Random.Range(0.35f, 0.9f);
            }
            Steer(f, f.Target, 3.6f, 9f, dt);
            f.Velocity += SwimBody.Avoidance(f.Body.position, f.Velocity, 1.2f, 0.8f) * dt;
        }

        private void Follow(Fish f, int index, float dt)
        {
            HerdRibbon ribbon = HerdRibbon.Instance;
            Vector3 slot = ribbon.SlotPosition(ribbon.SlotOf(this, index));
            // A gentle side-to-side shimmer so the ribbon twinkles instead of marching.
            Vector3 side = Vector3.Cross(Vector3.up, f.Velocity.sqrMagnitude > 0.01f ? f.Velocity.normalized : Vector3.forward);
            Vector3 shimmer = side * Mathf.Sin(Time.time * 2.2f + f.Phase) * 0.45f + Vector3.up * Mathf.Sin(Time.time * 1.7f + f.Phase * 1.3f) * 0.25f;
            float far = Vector3.Distance(f.Body.position, slot);
            Steer(f, slot + shimmer, Mathf.Lerp(4f, 12f, Mathf.InverseLerp(1f, 10f, far)), 14f, dt);
        }

        private void Dissolve(Fish f, float dt)
        {
            Vector3 target = _deliverTo != null ? _deliverTo.position : f.Body.position;
            Steer(f, target, 8f, 16f, dt);
            if ((f.Body.position - target).sqrMagnitude < 6.25f) f.Shrink = Mathf.MoveTowards(f.Shrink, 0f, dt * 2.5f);
            f.Body.localScale = Vector3.one * f.BaseScale * f.Shrink;
        }

        private static void Steer(Fish f, Vector3 target, float maxSpeed, float accel, float dt)
        {
            Vector3 desired = Vector3.ClampMagnitude((target - f.Body.position) * 2.5f, maxSpeed);
            f.Velocity = Vector3.MoveTowards(f.Velocity, desired, accel * dt);
        }

        private bool AllDissolved()
        {
            foreach (Fish f in _fish) if (f.Body != null && f.Shrink > 0.01f) return false;
            return true;
        }

        /// <summary>Highest a herd's home sits: the diver can only rise to about 4 m under the surface,
        /// and the fish spread a few metres around their home.</summary>
        private const float MaxHomeY = -7.5f;

        private static Vector3 KeepOffSeabed(Vector3 p)
        {
            float floor = SwimBody.SeabedY(p, p.y + 20f);
            p.y = Mathf.Max(floor + 1.2f, Mathf.Min(p.y, MaxHomeY));
            return p;
        }

        // ───────────────────────── Look ─────────────────────────

        private Color CurrentColor => Color.Lerp(PanicColor, CalmColor, _tint);

        private void ApplyTint(bool force)
        {
            float target = State == HerdState.Scattered ? 0f : 1f;
            float prev = _tint;
            _tint = force ? target : Mathf.MoveTowards(_tint, target, Time.deltaTime / 0.6f);
            if (!force && Mathf.Approximately(prev, _tint)) return;

            // Light enough not to darken the fish; the aura carries most of the colour.
            Color tint = Color.Lerp(Color.white, CurrentColor, 0.35f) * 1.15f;
            foreach (Fish f in _fish)
            {
                if (f.Body == null) continue;
                for (int r = 0; r < f.Renderers.Length; r++)
                {
                    Renderer rend = f.Renderers[r];
                    if (rend == null) continue;
                    rend.GetPropertyBlock(_mpb);
                    Color c = f.BaseColors[r] * tint;
                    c.a = f.BaseColors[r].a;
                    _mpb.SetColor(BaseColorId, c);
                    _mpb.SetColor(ColorId, c);
                    rend.SetPropertyBlock(_mpb);
                }
            }
        }

        private float _moteBudget;

        private void EmitMotes(float dt)
        {
            if (_motes == null || _fish.Count == 0 || State == HerdState.Delivered) return;
            _moteBudget += dt * _fish.Count * (State == HerdState.Following ? 4f : 2.5f);
            var p = new ParticleSystem.EmitParams { startColor = CurrentColor };
            while (_moteBudget >= 1f)
            {
                _moteBudget -= 1f;
                Fish f = _fish[UnityEngine.Random.Range(0, _fish.Count)];
                if (f.Body == null) continue;
                p.position = f.Body.position + UnityEngine.Random.insideUnitSphere * 0.25f;
                p.velocity = -f.Velocity * 0.15f + UnityEngine.Random.insideUnitSphere * 0.1f;
                _motes.Emit(p, 1);
            }
        }
    }
}
