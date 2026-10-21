using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Inventory;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A tide current: a tube of moving water along a path (this object's children, in order,
    /// smoothed into a curve). Manunubo's gift, Moontide, lets the diver see the tides and ride
    /// them: once the player owns it, every Moontide current glows as moonlit streaks, and swimming
    /// into one carries the diver along at <see cref="_carrySpeed"/> until it ends or they swim out
    /// of it. Before that, these currents are invisible and still.
    ///
    /// A current can also have a gentle push that is always on, with a few drifting motes, like the
    /// Trench A flow that nudges the diver east toward Ajuy (design doc: 0.5 to 1 m/s).
    /// The carry eases in at the start and out over the last metres, so it never snaps.
    /// </summary>
    public class TideCurrent : MonoBehaviour
    {
        private static readonly List<TideCurrent> _all = new List<TideCurrent>();
        public static IReadOnlyList<TideCurrent> All => _all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _all.Clear();

        /// <summary>The diver has Manunubo's gift: tide currents show and carry.</summary>
        public static bool MoontideOwned =>
            InventoryManager.Instance != null && InventoryManager.Instance.HasItem(GameConstants.Relics.MOONTIDE);

        [Header("Flow")]
        [Tooltip("Speed the current carries the diver once Moontide shows it (m/s; the diver swims 5).")]
        [SerializeField, Min(0f)] private float _carrySpeed = 9f;
        [Tooltip("Always-on push, even without Moontide (0 = none).")]
        [SerializeField, Min(0f)] private float _gentleSpeed;
        [Tooltip("Radius of the moving water around the path.")]
        [SerializeField, Min(0.5f)] private float _radius = 4f;
        [Tooltip("Off: the carry needs Moontide. On: it always carries (and always shows).")]
        [SerializeField] private bool _alwaysVisible;

        [Header("Look")]
        [SerializeField] private Material _streakMaterial;
        [SerializeField] private Color _moonlight = new Color(0.72f, 0.85f, 1f);

        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly List<float> _distance = new List<float>();
        private float _length;
        private Bounds _bounds;
        private ParticleSystem _streaks;
        private ParticleSystem _shadeStreaks;   // the same streaks for bright water (made when first needed)
        private float _contrast;
        private float _reveal;            // 0 hidden … 1 shown
        private float _ownedCheck;
        private bool _owned;
        private Vector3 _flow;
        private float _emitDebt;

        public float Length => _length;
        public Material StreakMaterial => _streakMaterial;
        public bool Revealed => _reveal > 0.5f;

        private void OnEnable() => _all.Add(this);
        private void OnDisable()
        {
            _all.Remove(this);
            if (PlayerController.Instance != null) PlayerController.Instance.ClearFlow(this);
        }

        private void Awake()
        {
            BuildPath();
            _streaks = CreateStreaks();
        }

        private void OnDestroy()
        {
            if (_streaks != null) Destroy(_streaks.gameObject);
            if (_shadeStreaks != null) Destroy(_shadeStreaks.gameObject);
            if (_entryMarker != null) Destroy(_entryMarker.gameObject);
        }

        // ───────────────────────── Path ─────────────────────────

        private void BuildPath()
        {
            _points.Clear();
            _distance.Clear();
            var nodes = new List<Vector3>();
            foreach (Transform c in transform) nodes.Add(c.position);
            if (nodes.Count < 2) { _length = 0f; return; }

            // Catmull-Rom through the nodes, about one point per metre.
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                Vector3 p0 = nodes[Mathf.Max(0, i - 1)], p1 = nodes[i], p2 = nodes[i + 1], p3 = nodes[Mathf.Min(nodes.Count - 1, i + 2)];
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2)));
                for (int s = 0; s < steps; s++) _points.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
            }
            _points.Add(nodes[nodes.Count - 1]);

            _length = 0f;
            _distance.Add(0f);
            _bounds = new Bounds(_points[0], Vector3.zero);
            for (int i = 1; i < _points.Count; i++)
            {
                _length += Vector3.Distance(_points[i - 1], _points[i]);
                _distance.Add(_length);
                _bounds.Encapsulate(_points[i]);
            }
            _bounds.Expand(_radius * 2f + 2f);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ───────────────────────── Route (Spirit Sense) ─────────────────────────

        private Transform _entryMarker;

        /// <summary>The diver is in this current's moving water.</summary>
        public bool IsInside(Vector3 p) => Closest(p, out _, out _, out float off) && off < _radius;

        /// <summary>Distance along the path of its nearest point to <paramref name="p"/> (anywhere, not just near it).</summary>
        private float NearestAlong(Vector3 p, out float off)
        {
            float best = float.MaxValue, along = 0f;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector3 a = _points[i], b = _points[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (p - (a + ab * t)).sqrMagnitude;
                if (d < best) { best = d; along = _distance[i] + ab.magnitude * t; }
            }
            off = Mathf.Sqrt(best);
            return along;
        }

        /// <summary>
        /// Where Spirit Sense should lead first on the way to <paramref name="goal"/>: the way into
        /// the current that gets there soonest (swim to it, ride, perhaps ride a second one on from
        /// where it ends, swim the rest), or null when swimming straight there is about as quick,
        /// Moontide isn't owned, or the diver is already riding one.
        /// </summary>
        public static Transform BestEntryToward(Vector3 from, Vector3 goal, float swimSpeed = 5f)
        {
            swimSpeed = Mathf.Max(0.5f, swimSpeed);
            var usable = new List<TideCurrent>();
            foreach (TideCurrent c in _all)
            {
                if (c == null || !c._owned || c._carrySpeed <= 0f || c._length < 10f) continue;
                if (c.IsInside(from)) return null;   // riding: head for the goal itself
                usable.Add(c);
            }
            if (usable.Count == 0) return null;

            // Each current's nearest pass to the goal (shared by both legs).
            var toGoalAlong = new float[usable.Count];
            var toGoalOff = new float[usable.Count];
            for (int i = 0; i < usable.Count; i++) toGoalAlong[i] = usable[i].NearestAlong(goal, out toGoalOff[i]);

            TideCurrent best = null;
            float bestTime = Vector3.Distance(from, goal) / swimSpeed * 0.8f;   // must save a fifth of the trip
            float bestAlong = 0f;
            for (int i = 0; i < usable.Count; i++)
            {
                TideCurrent c = usable[i];
                float enter = c.NearestAlong(from, out float toEntry);
                float approach = Mathf.Max(0f, toEntry - c._radius * 0.6f) / swimSpeed;   // into the tube's edge

                // Ride it, then swim from where it passes nearest the goal…
                float leave = toGoalAlong[i];
                float rest = float.MaxValue;
                if (leave - enter >= 10f) rest = (leave - enter) / c._carrySpeed + toGoalOff[i] / swimSpeed;

                // …or ride it to its end and on along a second one.
                Vector3 end = c._points[c._points.Count - 1];
                float toEnd = (c._length - enter) / c._carrySpeed;
                if (c._length - enter >= 10f)
                {
                    for (int j = 0; j < usable.Count; j++)
                    {
                        if (j == i) continue;
                        TideCurrent c2 = usable[j];
                        float enter2 = c2.NearestAlong(end, out float gap);
                        if (gap > 30f || toGoalAlong[j] - enter2 < 10f) continue;
                        float t2 = toEnd + Mathf.Max(0f, gap - c2._radius * 0.6f) / swimSpeed
                                 + (toGoalAlong[j] - enter2) / c2._carrySpeed + toGoalOff[j] / swimSpeed;
                        if (t2 < rest) rest = t2;
                    }
                }

                float time = approach + rest;
                if (time < bestTime) { bestTime = time; best = c; bestAlong = enter; }
            }
            if (best == null) return null;
            if (best._entryMarker == null)
            {
                // Not a child: the children are the path.
                best._entryMarker = new GameObject(best.name + "_SpiritSenseEntry").transform;
                best._entryMarker.SetParent(best.transform.parent, false);
            }
            best._entryMarker.position = best.PointAt(Mathf.Min(bestAlong + 2f, best._length), out _);
            return best._entryMarker;
        }

        /// <summary>Closest point on the path: its index (segment start), distance along, and how far off.</summary>
        private bool Closest(Vector3 p, out int segment, out float along, out float off)
        {
            segment = 0; along = 0f; off = float.MaxValue;
            if (_points.Count < 2 || !_bounds.Contains(p)) return false;
            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector3 a = _points[i], b = _points[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = (p - (a + ab * t)).sqrMagnitude;
                if (d < off) { off = d; segment = i; along = _distance[i] + ab.magnitude * t; }
            }
            off = Mathf.Sqrt(off);
            return true;
        }

        private Vector3 PointAt(float along, out Vector3 tangent)
        {
            along = Mathf.Clamp(along, 0f, _length);
            int i = 0;
            while (i < _distance.Count - 2 && _distance[i + 1] < along) i++;
            Vector3 a = _points[i], b = _points[i + 1];
            float seg = Mathf.Max(1e-5f, _distance[i + 1] - _distance[i]);
            tangent = (b - a).normalized;
            return Vector3.Lerp(a, b, (along - _distance[i]) / seg);
        }

        // ───────────────────────── Flow ─────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _length <= 0f) return;

            _ownedCheck -= dt;
            if (_ownedCheck <= 0f) { _ownedCheck = 0.5f; _owned = _alwaysVisible || MoontideOwned; _contrast = WaterBrightness.Contrast; }
            // A gentle-only flow (no carry) never turns into a Moontide ride.
            _reveal = Mathf.MoveTowards(_reveal, _owned && _carrySpeed > 0f ? 1f : 0f, dt * 0.6f);

            PlayerController pc = PlayerController.Instance;
            Vector3 want = Vector3.zero;
            if (pc != null && Closest(pc.transform.position, out int seg, out float along, out float off) && off < _radius)
            {
                Vector3 tangent = (_points[seg + 1] - _points[seg]).normalized;
                // Strongest in the middle of the tube, easing in over the first and out over the last metres.
                float edge = Mathf.Clamp01((_radius - off) / (_radius * 0.4f));
                float ends = Mathf.Clamp01(along / 4f) * Mathf.Clamp01((_length - along) / 8f);
                float speed = Mathf.Max(_gentleSpeed, _carrySpeed * _reveal);
                want = tangent * speed * edge * Mathf.Lerp(0.35f, 1f, ends);
                // Draw the diver back toward the middle a little, so the ride follows the bends.
                if (_reveal > 0.5f)
                {
                    Vector3 centre = _points[seg] + Vector3.Project(pc.transform.position - _points[seg], tangent);
                    want += (centre - pc.transform.position) * 0.8f;
                }
            }
            _flow = Vector3.MoveTowards(_flow, want, dt * 14f);
            if (pc != null)
            {
                if (_flow.sqrMagnitude > 0.0004f) pc.SetFlow(this, _flow);
                else pc.ClearFlow(this);
            }

            EmitStreaks(dt);
        }

        // ───────────────────────── Look ─────────────────────────

        private ParticleSystem CreateStreaks() => CreateStreaks(name + "_Streaks", _streakMaterial);

        private ParticleSystem CreateStreaks(string objectName, Material material)
        {
            // Not a child: the children are the path.
            var go = new GameObject(objectName);
            go.transform.SetParent(transform.parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.maxParticles = 1500;
            var emission = ps.emission;
            emission.rateOverTime = 0f;   // emitted by hand along the path
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.1f;
            r.lengthScale = 1.4f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (material != null) r.sharedMaterial = material;
            ps.Play();
            return ps;
        }

        /// <summary>
        /// The streaks for bright water: the glow can't show there, so they're drawn plain white
        /// (see-through, not added light), and longer (no bloom to smear them) to read as flowing water.
        /// </summary>
        private ParticleSystem CreateBrightWaterStreaks()
        {
            ParticleSystem ps = CreateStreaks(name + "_Streaks_BrightWater", WaterBrightness.ShadeOf(_streakMaterial));
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.velocityScale = 0.4f;   // long thin streaks, not dots
            r.lengthScale = 3f;
            return ps;
        }

        /// <summary>Moonlit streaks flowing along the tube once revealed; a few motes for a gentle current.</summary>
        private void EmitStreaks(float dt)
        {
            if (_streaks == null) return;
            float gentleShow = _gentleSpeed > 0f ? 0.25f : 0f;
            float show = Mathf.Max(_reveal, gentleShow);
            if (show <= 0.01f) return;
            // Only near the camera: no point flowing water nobody can see.
            Camera cam = Camera.main;
            if (cam != null && (_bounds.SqrDistance(cam.transform.position) > 90f * 90f)) return;

            float speed = Mathf.Max(_gentleSpeed, _carrySpeed * _reveal);
            float perMetre = Mathf.Lerp(0.08f, 0.45f, _reveal);
            _emitDebt += _length * perMetre * dt * show;
            int count = Mathf.Min(60, Mathf.FloorToInt(_emitDebt));
            _emitDebt -= count;
            if (count <= 0) return;
            var p = new ParticleSystem.EmitParams();
            Color c = _moonlight;
            // Favour the stretch near the camera (a little ahead of it).
            float camAlong = 0f;
            bool near = cam != null && Closest(cam.transform.position, out _, out camAlong, out _);
            for (int i = 0; i < count; i++)
            {
                float along = near ? Mathf.Clamp(camAlong + (Random.value - 0.35f) * 70f, 0f, _length) : Random.value * _length;
                Vector3 pos = PointAt(along, out Vector3 tangent);
                Vector3 side = Random.insideUnitSphere * _radius * 0.75f;
                side -= tangent * Vector3.Dot(side, tangent);
                p.position = pos + side;
                p.velocity = tangent * speed * Random.Range(0.7f, 1.1f);
                c.a = Mathf.Lerp(0.25f, 0.9f, _reveal) * Random.Range(0.6f, 1f);
                // Bright water: the glow would vanish, so this streak is drawn plain white instead.
                ParticleSystem target = _streaks;
                if (_contrast > 0.01f && Random.value < _contrast)
                {
                    if (_shadeStreaks == null) _shadeStreaks = CreateBrightWaterStreaks();
                    target = _shadeStreaks;
                    Color d = Color.white;
                    d.a = Mathf.Lerp(0.45f, 1f, _reveal) * Random.Range(0.75f, 1f);
                    p.startColor = d;
                }
                else p.startColor = c;
                target.Emit(p, 1);
            }
        }

        private void OnDrawGizmos()
        {
            var nodes = new List<Vector3>();
            foreach (Transform c in transform) nodes.Add(c.position);
            Gizmos.color = new Color(0.7f, 0.85f, 1f, 0.8f);
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                Gizmos.DrawLine(nodes[i], nodes[i + 1]);
                Vector3 dir = (nodes[i + 1] - nodes[i]).normalized;
                Vector3 mid = (nodes[i] + nodes[i + 1]) * 0.5f;
                Gizmos.DrawLine(mid, mid - dir * 2f + Vector3.Cross(Vector3.up, dir));
                Gizmos.DrawLine(mid, mid - dir * 2f - Vector3.Cross(Vector3.up, dir));
            }
            foreach (Vector3 n in nodes) Gizmos.DrawWireSphere(n, _radius);
        }

#if UNITY_EDITOR
        public void EditorSetup(float carrySpeed, float gentleSpeed, float radius, Material streaks)
        {
            _carrySpeed = carrySpeed;
            _gentleSpeed = gentleSpeed;
            _radius = radius;
            _streakMaterial = streaks;
        }
#endif
    }
}
