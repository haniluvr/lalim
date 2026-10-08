using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Inventory;
using Lalim.Mechanics;

namespace Lalim.Challenges.Catching
{
    /// <summary>
    /// The Woven Net Fragment, thrown. Hold the relic button (or 1) to aim: a dotted arc shows the
    /// real flight from the diver along the camera's view, with a ring where the net will open.
    /// Release to throw: the net flies that arc (underwater gravity and drag), opens where it lands
    /// (or where its flight runs out in open water) into a woven dome of <see cref="Radius"/> and
    /// closes. Whoever owns the catch (SelectiveCatchingStep) hears <see cref="Opened"/> and decides
    /// what the net holds.
    ///
    /// Only aims while <see cref="Armed"/> (a lesson or the test is running).
    /// </summary>
    public class NetThrower : MonoBehaviour
    {
        public static NetThrower Instance { get; private set; }

        /// <summary>The net opened at a point with a radius.</summary>
        public static event Action<Vector3, float> Opened;
        /// <summary>A throw left the diver's hands.</summary>
        public static event Action Thrown;

        private AudioClip _throwClip;   // the net leaving the hand
        private AudioClip _landClip;    // the net settling where it lands

        private const string NetId = GameConstants.Relics.WOVEN_NET_FRAGMENT;

        public static readonly Color Woven = new Color(1f, 0.8f, 0.45f);

        [Header("Flight")]
        [SerializeField] private float _throwSpeed = 12f;
        [SerializeField] private float _pitchUp = 10f;
        [SerializeField] private float _gravity = 7f;
        [SerializeField] private float _drag = 0.35f;
        [SerializeField] private float _maxFlight = 1.7f;

        public bool Armed { get; set; }
        public float Radius { get; set; } = 3.2f;
        public float Cooldown { get; set; } = 1.5f;
        public bool IsAiming => _aiming;

        private Material _lineMaterial;
        private GameObject _bundleModel;
        private LineRenderer _arc;
        private LineRenderer _ring;
        private bool _aiming;
        private float _aimStarted;
        private readonly List<Vector3> _path = new List<Vector3>();
        private Vector3 _landing;
        private Vector3 _landingNormal = Vector3.up;
        private int _mask;

        public static NetThrower Ensure(Material lineMaterial, GameObject bundleModel, AudioClip throwClip = null, AudioClip landClip = null)
        {
            if (Instance == null)
            {
                var go = new GameObject("[WovenNet]");
                Instance = go.AddComponent<NetThrower>();
            }
            Instance._lineMaterial = lineMaterial;
            Instance._bundleModel = bundleModel;
            Instance._throwClip = throwClip;
            Instance._landClip = landClip;
            Instance.BuildPreview();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _mask = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            int player = LayerMask.NameToLayer("Player");
            if (player >= 0) _mask &= ~(1 << player);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnEnable() => EventBus.Subscribe<RelicHoldEvent>(OnHold);
        private void OnDisable() => EventBus.Unsubscribe<RelicHoldEvent>(OnHold);

        private void BuildPreview()
        {
            if (_arc != null) return;
            _arc = NewLine("AimArc", 0.09f, false);
            _arc.textureMode = LineTextureMode.Tile;
            _ring = NewLine("AimRing", 0.07f, true);
            _arc.enabled = _ring.enabled = false;
        }

        private LineRenderer NewLine(string name, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (_lineMaterial != null) lr.sharedMaterial = _lineMaterial;
            lr.startColor = lr.endColor = Woven;
            return lr;
        }

        // ───────────────────────── Input ─────────────────────────

        private void OnHold(RelicHoldEvent evt)
        {
            if (evt.ItemId != NetId) return;
            if (evt.Down)
            {
                if (!Armed || RelicLoadout.CooldownRemaining(NetId) > 0f) return;
                _aiming = true;
                _aimStarted = Time.time;
            }
            else if (_aiming)
            {
                _aiming = false;
                HidePreview();
                if (Armed && Time.time - _aimStarted >= 0.08f) Throw();
            }
        }

        private void Update()
        {
            if (!_aiming) return;
            if (!Armed || Time.timeScale <= 0f || (DialogueManagerOpen())) { _aiming = false; HidePreview(); return; }
            Simulate(_path, out _landing, out _landingNormal);
            DrawPreview();
        }

        private static bool DialogueManagerOpen() => Lalim.UI.DialogueManager.Instance != null && Lalim.UI.DialogueManager.Instance.IsOpen;

        // ───────────────────────── Flight ─────────────────────────

        private bool Launch(out Vector3 origin, out Vector3 velocity)
        {
            origin = velocity = Vector3.zero;
            PlayerController pc = PlayerController.Instance;
            Camera cam = Camera.main;
            if (pc == null || cam == null) return false;
            Vector3 fwd = cam.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            if (right.sqrMagnitude < 0.001f) right = cam.transform.right;
            fwd = Quaternion.AngleAxis(-_pitchUp, right.normalized) * fwd;
            origin = pc.transform.position + Vector3.up * 0.4f + new Vector3(fwd.x, 0f, fwd.z).normalized * 0.9f;
            velocity = fwd.normalized * _throwSpeed;
            return true;
        }

        /// <summary>Runs the same flight the net will take; returns where it opens.</summary>
        private void Simulate(List<Vector3> path, out Vector3 landing, out Vector3 normal)
        {
            path.Clear();
            normal = Vector3.up;
            if (!Launch(out Vector3 p, out Vector3 v)) { landing = Vector3.zero; return; }
            const float step = 1f / 30f;
            path.Add(p);
            for (float t = 0f; t < _maxFlight; t += step)
            {
                Vector3 next;
                Step(ref p, ref v, step, out next);
                if (Physics.Linecast(p, next, out RaycastHit hit, _mask, QueryTriggerInteraction.Ignore))
                {
                    path.Add(hit.point);
                    landing = hit.point + hit.normal * 0.3f;
                    normal = hit.normal;
                    return;
                }
                p = next;
                path.Add(p);
            }
            landing = p;
        }

        private void Step(ref Vector3 p, ref Vector3 v, float dt, out Vector3 next)
        {
            v += Vector3.down * _gravity * dt;
            v *= 1f - _drag * dt;
            next = p + v * dt;
            if (next.y > -0.4f) { next.y = -0.4f; if (v.y > 0f) v.y = 0f; }   // stays under the surface
        }

        private void DrawPreview()
        {
            _arc.enabled = _ring.enabled = _path.Count > 1;
            _arc.positionCount = _path.Count;
            for (int i = 0; i < _path.Count; i++) _arc.SetPosition(i, _path[i]);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            Color c = new Color(Woven.r, Woven.g, Woven.b, 0.85f);
            _arc.startColor = new Color(c.r, c.g, c.b, 0.15f);
            _arc.endColor = c;
            const int seg = 40;
            _ring.positionCount = seg;
            Vector3 n = _landingNormal.y > 0.35f ? _landingNormal : Vector3.up;
            Vector3 a = Vector3.Cross(n, Vector3.forward);
            if (a.sqrMagnitude < 0.01f) a = Vector3.Cross(n, Vector3.right);
            a.Normalize();
            Vector3 b = Vector3.Cross(n, a);
            for (int i = 0; i < seg; i++)
            {
                float ang = i / (float)seg * Mathf.PI * 2f;
                _ring.SetPosition(i, _landing + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * Radius);
            }
            _ring.startColor = _ring.endColor = new Color(c.r, c.g, c.b, 0.55f + 0.35f * pulse);
        }

        private void HidePreview()
        {
            if (_arc != null) _arc.enabled = false;
            if (_ring != null) _ring.enabled = false;
        }

        private void Throw()
        {
            if (!Launch(out Vector3 origin, out Vector3 velocity)) return;
            RelicLoadout.StartCooldown(NetId, Cooldown);
            GameAudio.PlayFlat(_throwClip);
            Thrown?.Invoke();
            var go = new GameObject("NetInFlight");
            var flight = go.AddComponent<NetFlight>();
            flight.Begin(this, origin, velocity);
        }

        // ───────────────────────── The net in flight, then open ─────────────────────────

        private class NetFlight : MonoBehaviour
        {
            private NetThrower _owner;
            private Vector3 _p, _v;
            private float _t;
            private Transform _bundle;
            private bool _open;
            private float _openT;
            private Vector3 _at;
            private float _radius;
            private readonly List<LineRenderer> _lines = new List<LineRenderer>();
            private Light _light;

            public void Begin(NetThrower owner, Vector3 origin, Vector3 velocity)
            {
                _owner = owner;
                _p = origin;
                _v = velocity;
                transform.position = origin;
                _radius = owner.Radius;

                if (owner._bundleModel != null)
                {
                    GameObject m = Instantiate(owner._bundleModel, transform);
                    foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
                    foreach (MonoBehaviour b in m.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
                    _bundle = m.transform;
                    _bundle.localPosition = Vector3.zero;
                    FitTo(_bundle, 0.6f);
                }
                var trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.35f;
                trail.widthMultiplier = 0.18f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                if (owner._lineMaterial != null) trail.sharedMaterial = owner._lineMaterial;
                trail.startColor = new Color(Woven.r, Woven.g, Woven.b, 0.8f);
                trail.endColor = new Color(Woven.r, Woven.g, Woven.b, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = Woven;
                _light.range = 5f;
                _light.intensity = 2f;
                _light.shadows = LightShadows.None;
            }

            private static void FitTo(Transform t, float size)
            {
                Bounds b = new Bounds(t.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                float max = any ? Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) : 1f;
                if (max > 0.001f) t.localScale *= size / max;
            }

            private void Update()
            {
                float dt = Time.deltaTime;
                if (dt <= 0f) return;
                if (!_open) Fly(dt); else Animate(dt);
            }

            private void Fly(float dt)
            {
                _t += dt;
                _owner.Step(ref _p, ref _v, dt, out Vector3 next);
                if (Physics.Linecast(_p, next, out RaycastHit hit, _owner._mask, QueryTriggerInteraction.Ignore))
                {
                    Open(hit.point + hit.normal * 0.3f);
                    return;
                }
                _p = next;
                transform.position = _p;
                if (_bundle != null) _bundle.Rotate(new Vector3(260f, 410f, 90f) * dt, Space.Self);
                if (_t >= _owner._maxFlight) Open(_p);
            }

            private void Open(Vector3 at)
            {
                _open = true;
                _openT = 0f;
                _at = at;
                transform.position = at;
                if (_bundle != null) _bundle.gameObject.SetActive(false);
                // A woven dome: rings of latitude and ribs of longitude.
                for (int i = 0; i < 4; i++) _lines.Add(Line(true, 0.07f));
                for (int i = 0; i < 10; i++) _lines.Add(Line(false, 0.05f));
                GameAudio.PlayAt(_owner._landClip, at);
                Opened?.Invoke(at, _radius);
            }

            private LineRenderer Line(bool loop, float width)
            {
                LineRenderer lr = _owner.NewLine("Net", width, loop);
                lr.transform.SetParent(transform, false);
                return lr;
            }

            private void Animate(float dt)
            {
                _openT += dt;
                // 0–0.2 s spread, hold to 0.5 s, close to 0.9 s.
                float spread = _openT < 0.2f ? Mathf.SmoothStep(0.15f, 1f, _openT / 0.2f)
                             : _openT < 0.5f ? 1f
                             : Mathf.Lerp(1f, 0.08f, Mathf.SmoothStep(0f, 1f, (_openT - 0.5f) / 0.4f));
                float r = _radius * spread;
                float alpha = _openT < 0.9f ? 1f : Mathf.Clamp01(1f - (_openT - 0.9f) / 0.2f);
                Color c = new Color(Woven.r, Woven.g, Woven.b, 0.9f * alpha);
                const int seg = 36;
                for (int i = 0; i < 4; i++)
                {
                    LineRenderer lr = _lines[i];
                    float lat = (i + 0.5f) / 4f * Mathf.PI * 0.5f;          // latitude above the rim
                    float rr = r * Mathf.Cos(lat), h = r * 0.75f * Mathf.Sin(lat);
                    lr.positionCount = seg;
                    for (int s = 0; s < seg; s++)
                    {
                        float a = s / (float)seg * Mathf.PI * 2f + i * 0.2f;
                        lr.SetPosition(s, _at + new Vector3(Mathf.Cos(a) * rr, h - r * 0.25f, Mathf.Sin(a) * rr));
                    }
                    lr.startColor = lr.endColor = c;
                }
                for (int i = 0; i < 10; i++)
                {
                    LineRenderer lr = _lines[4 + i];
                    float a = i / 10f * Mathf.PI * 2f;
                    const int pts = 10;
                    lr.positionCount = pts;
                    for (int s = 0; s < pts; s++)
                    {
                        float lat = s / (float)(pts - 1) * Mathf.PI * 0.5f;
                        float rr = r * Mathf.Cos(lat), h = r * 0.75f * Mathf.Sin(lat);
                        lr.SetPosition(s, _at + new Vector3(Mathf.Cos(a) * rr, h - r * 0.25f, Mathf.Sin(a) * rr));
                    }
                    lr.startColor = lr.endColor = c;
                }
                if (_light != null) { _light.range = Mathf.Max(4f, r * 2.5f); _light.intensity = 3f * alpha; }
                if (_openT >= 1.1f) Destroy(gameObject);
            }
        }
    }
}
