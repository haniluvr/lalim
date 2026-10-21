using System.Collections.Generic;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Catching
{
    /// <summary>
    /// The glowing woven net-bag the diver tows: every school caught in Ajuy rides in it, small
    /// fish circling inside, until Minigame 3 lets them out at their sanctuaries. Trails behind
    /// and a little below the diver on a soft spring, and grows a little with each school. In
    /// Minigame 3 each school glows softly in its moon phase's colour, so it can be matched to the
    /// sanctuary that glows the same.
    /// </summary>
    public class NetBag : MonoBehaviour
    {
        public static NetBag Instance { get; private set; }

        private class Rider
        {
            public string Key;
            public List<Transform> Fish = new List<Transform>();
            public List<Renderer> Glows = new List<Renderer>();
            public Color Color;
            public float Phase;
            public float Flash;     // brightens when it's about to go home, dips on a mismatch
        }

        /// <summary>An unlit see-through material for the riders' glow (set by Minigame 3).</summary>
        public static Material GlowMaterial { get; set; }
        private static Material _glowInstance;
        private static MaterialPropertyBlock _mpb;

        private readonly List<Rider> _riders = new List<Rider>();
        private readonly List<LineRenderer> _weave = new List<LineRenderer>();
        private Material _lineMaterial;
        private Light _light;
        private Transform _mouth;
        private Vector3 _velocity;
        private float _radius = 0.55f;
        private float _pulse;

        public Transform Mouth => _mouth != null ? _mouth : transform;
        public int SchoolCount => _riders.Count;

        public IEnumerable<string> SchoolKeys { get { foreach (Rider r in _riders) yield return r.Key; } }

        public static NetBag Ensure(Material lineMaterial)
        {
            if (Instance == null)
            {
                var go = new GameObject("[NetBag]");
                Instance = go.AddComponent<NetBag>();
                Instance._lineMaterial = lineMaterial;
                Instance.Build();
                PlayerController pc = PlayerController.Instance;
                if (pc != null) go.transform.position = Instance.Rest(pc.transform);
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Build()
        {
            _mouth = new GameObject("Mouth").transform;
            _mouth.SetParent(transform, false);
            for (int i = 0; i < 3; i++) _weave.Add(Line(true, 0.035f));    // rings
            for (int i = 0; i < 6; i++) _weave.Add(Line(false, 0.03f));    // ribs
            _light = new GameObject("Glow").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.type = LightType.Point;
            _light.color = NetThrower.Woven;
            _light.range = 4f;
            _light.intensity = 1.6f;
            _light.shadows = LightShadows.None;
        }

        private LineRenderer Line(bool loop, float width)
        {
            var go = new GameObject("Weave");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (_lineMaterial != null) lr.sharedMaterial = _lineMaterial;
            return lr;
        }

        /// <summary>Adds a school's worth of little riders (model scaled down to fit inside).</summary>
        public void Add(string key, GameObject model, int count, float modelYaw) => Add(key, model, count, modelYaw, null);

        /// <summary>As above, with a soft glow of <paramref name="glow"/> round each little fish.</summary>
        public void Add(string key, GameObject model, int count, float modelYaw, Color? glow)
        {
            if (Has(key)) return;
            var rider = new Rider { Key = key, Phase = Random.value * 10f, Color = glow ?? Color.clear };
            int n = Mathf.Clamp(count, 1, 4);
            for (int i = 0; i < n; i++)
            {
                if (model == null) break;
                Transform t = SwimBody.Spawn(model, transform, 0.22f, modelYaw);
                foreach (Collider c in t.GetComponentsInChildren<Collider>()) Destroy(c);
                rider.Fish.Add(t);
                if (glow.HasValue) rider.Glows.Add(GlowQuad());
            }
            _riders.Add(rider);
            _radius = 0.55f + 0.08f * _riders.Count;
            _pulse = 1f;
        }

        public bool Has(string key)
        {
            foreach (Rider r in _riders) if (r.Key == key) return true;
            return false;
        }

        /// <summary>Lets a school out (Minigame 3: released at its sanctuary, or scattered).</summary>
        public void Remove(string key)
        {
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i].Key != key) continue;
                foreach (Transform t in _riders[i].Fish) if (t != null) Destroy(t.gameObject);
                foreach (Renderer g in _riders[i].Glows) if (g != null) Destroy(g.gameObject);
                _riders.RemoveAt(i);
            }
            _radius = 0.55f + 0.08f * _riders.Count;
        }

        public void Clear()
        {
            foreach (Rider r in _riders)
            {
                foreach (Transform t in r.Fish) if (t != null) Destroy(t.gameObject);
                foreach (Renderer g in r.Glows) if (g != null) Destroy(g.gameObject);
            }
            _riders.Clear();
            _radius = 0.55f;
        }

        /// <summary>Gives a school already in the bag its glow colour (schools caught before Minigame 3).</summary>
        public void Tint(string key, Color glow)
        {
            foreach (Rider r in _riders)
            {
                if (r.Key != key) continue;
                r.Color = glow;
                while (r.Glows.Count < r.Fish.Count) r.Glows.Add(GlowQuad());
            }
        }

        /// <summary>The school brightens (about to swim home).</summary>
        public void Brighten(string key)
        {
            foreach (Rider r in _riders) if (r.Key == key) r.Flash = 1f;
            _pulse = 1f;
        }

        /// <summary>Every school's glow dips for a moment (nothing here is theirs).</summary>
        public void Dim()
        {
            foreach (Rider r in _riders) r.Flash = -0.8f;
        }

        /// <summary>Where a school's fish are now (they leave the bag from here).</summary>
        public Vector3 PositionOf(string key)
        {
            foreach (Rider r in _riders)
                if (r.Key == key) foreach (Transform t in r.Fish) if (t != null) return t.position;
            return Mouth.position;
        }

        private Renderer GlowQuad()
        {
            if (GlowMaterial == null) return null;
            if (_glowInstance == null)
            {
                _glowInstance = new Material(GlowMaterial) { name = "NetBagGlow" };
                _glowInstance.SetTexture("_BaseMap", Lalim.Challenges.MoonRitual.MoonPhases.Disc);
            }
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Glow";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            q.transform.localScale = Vector3.one * 1.1f;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = _glowInstance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private void UpdateGlows(float dt)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Camera cam = Camera.main;
            foreach (Rider rider in _riders)
            {
                rider.Flash = Mathf.MoveTowards(rider.Flash, 0f, dt * 0.9f);
                float a = Mathf.Clamp01(0.65f + 0.1f * Mathf.Sin(Time.time * 2.2f + rider.Phase) + rider.Flash * 0.5f);
                Color c = rider.Color * (1f + Mathf.Max(0f, rider.Flash));
                c.a = a;
                for (int i = 0; i < rider.Glows.Count && i < rider.Fish.Count; i++)
                {
                    Renderer g = rider.Glows[i];
                    Transform f = rider.Fish[i];
                    if (g == null || f == null) continue;
                    g.transform.position = f.position;
                    if (cam != null) g.transform.rotation = Quaternion.LookRotation(g.transform.position - cam.transform.position);
                    g.GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", c);
                    g.SetPropertyBlock(_mpb);
                }
            }
        }

        // Close behind the diver's back (just clear of the body, towed on a short line).
        private Vector3 Rest(Transform diver) => diver.position - diver.forward * (0.55f + _radius) + Vector3.up * 0.35f;

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            PlayerController pc = PlayerController.Instance;
            if (pc != null)
            {
                // A soft spring behind the diver: it lags on turns like something being towed.
                Vector3 want = Rest(pc.transform) + Vector3.up * Mathf.Sin(Time.time * 1.4f) * 0.15f;
                Vector3 to = want - transform.position;
                if (to.sqrMagnitude > 400f) transform.position = want;   // teleported (checkpoint)
                _velocity = Vector3.Lerp(_velocity, to * 6f, dt * 4f);
                transform.position += _velocity * dt;
                Vector3 look = pc.transform.position - transform.position;
                if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 4f);
            }
            _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 1.5f);
            DrawWeave();
            MoveRiders();
            UpdateGlows(dt);
            if (_light != null) _light.intensity = 1.2f + 0.4f * Mathf.Sin(Time.time * 2f) + _pulse * 3f;
        }

        private void DrawWeave()
        {
            float r = _radius * (1f + 0.15f * _pulse);
            Color c = new Color(NetThrower.Woven.r, NetThrower.Woven.g, NetThrower.Woven.b, 0.85f);
            const int seg = 24;
            // A teardrop: round at the back, gathered to the mouth (the tow line end) at the front.
            for (int i = 0; i < 3; i++)
            {
                LineRenderer lr = _weave[i];
                float z = Mathf.Lerp(-0.6f, 0.5f, i / 2f) * r;
                float rr = Shape(z / r) * r;
                lr.positionCount = seg;
                for (int s = 0; s < seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f + Time.time * 0.3f;
                    lr.SetPosition(s, new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z));
                }
                lr.startColor = lr.endColor = c;
            }
            for (int i = 0; i < 6; i++)
            {
                LineRenderer lr = _weave[3 + i];
                float a = i / 6f * Mathf.PI * 2f + Time.time * 0.3f;
                const int pts = 12;
                lr.positionCount = pts;
                for (int s = 0; s < pts; s++)
                {
                    float z = Mathf.Lerp(-1f, 1f, s / (float)(pts - 1)) * r;
                    float rr = Shape(z / r) * r;
                    lr.SetPosition(s, new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z));
                }
                lr.startColor = lr.endColor = c;
            }
            if (_mouth != null) _mouth.localPosition = new Vector3(0f, 0f, r);
        }

        private static float Shape(float u)
        {
            // u from -1 (back) to 1 (mouth): full at the back, pinched at the mouth.
            float back = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            return Mathf.Lerp(back, back * 0.35f, Mathf.Clamp01((u + 0.2f) / 1.2f));
        }

        private void MoveRiders()
        {
            float r = _radius * 0.5f;
            for (int i = 0; i < _riders.Count; i++)
            {
                Rider rider = _riders[i];
                for (int f = 0; f < rider.Fish.Count; f++)
                {
                    Transform t = rider.Fish[f];
                    if (t == null) continue;
                    float a = Time.time * (0.9f + 0.15f * i) + rider.Phase + f * 1.7f;
                    Vector3 local = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.3f + i) * r * 0.5f, Mathf.Sin(a) * r * 0.6f - r * 0.3f);
                    Vector3 next = transform.TransformPoint(local);
                    Vector3 dir = next - t.position;
                    t.position = next;
                    if (dir.sqrMagnitude > 1e-6f) SwimBody.Face(t, dir / Mathf.Max(Time.deltaTime, 1e-4f), rider.Phase, Time.deltaTime, 10f);
                }
            }
        }
    }
}
