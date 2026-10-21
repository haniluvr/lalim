using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Manunubo's gift made visible: for a few seconds the Moontide current (the same moonlit
    /// streaks the tide currents show) swirls around the diver and the dugong in a figure of
    /// eight, one loop around each, then fades. Plays when Moontide is given.
    /// </summary>
    public class MoontideGiftSwirl : MonoBehaviour
    {
        private const int Ribbons = 3;
        private const float FadeSeconds = 1.5f;

        private Transform _a, _b;
        private float _duration;
        private float _age;
        private float _turnSeconds = 2.6f;   // one trip around the eight
        private Material _glow, _shade;
        private Color _moonlight = new Color(0.72f, 0.85f, 1f);
        private readonly List<TrailRenderer> _trails = new List<TrailRenderer>();
        private ParticleSystem _streaks;
        private float _contrast;
        private float _emitDebt;

        /// <summary>Swirls around <paramref name="a"/> and <paramref name="b"/> (b may be null: one loop around a).</summary>
        public static MoontideGiftSwirl Play(Transform a, Transform b, float seconds = 8f)
        {
            if (a == null) return null;
            var go = new GameObject("[MoontideGiftSwirl]");
            var swirl = go.AddComponent<MoontideGiftSwirl>();
            swirl._a = a;
            swirl._b = b;
            swirl._duration = Mathf.Max(FadeSeconds + 0.5f, seconds);
            Material streak = null;
            foreach (TideCurrent c in TideCurrent.All) if (c != null && c.StreakMaterial != null) { streak = c.StreakMaterial; break; }
            swirl.Build(streak);
            return swirl;
        }

        private void Build(Material streak)
        {
            _contrast = WaterBrightness.Contrast;
            _glow = streak;
            _shade = WaterBrightness.ShadeOf(streak);
            bool bright = _contrast > 0.5f;
            Material mat = bright && _shade != null ? _shade : _glow;
            Color col = bright ? Color.white : _moonlight;   // white either way

            for (int i = 0; i < Ribbons; i++)
            {
                var go = new GameObject("Ribbon_" + i);
                go.transform.SetParent(transform, false);
                var trail = go.AddComponent<TrailRenderer>();
                trail.time = 0.9f;
                trail.minVertexDistance = 0.08f;
                trail.widthMultiplier = 0.32f - i * 0.06f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.alignment = LineAlignment.View;
                if (mat != null) trail.sharedMaterial = mat;
                Color start = col; start.a = bright ? 0.8f : 0.9f;
                Color end = col; end.a = 0f;
                trail.startColor = start;
                trail.endColor = end;
                trail.emitting = false;
                _trails.Add(trail);
            }

            var ps = new GameObject("Streaks").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform, false);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.12f;
            r.lengthScale = 1.2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mat != null) r.sharedMaterial = mat;
            ps.Play();
            _streaks = ps;
            _moonlight = col;

            // Start the ribbons on the curve, so they don't streak in from the origin.
            for (int i = 0; i < _trails.Count; i++)
            {
                _trails[i].transform.position = PointOnEight(Phase(i, 0f));
                _trails[i].Clear();
                _trails[i].emitting = true;
            }
        }

        private float Phase(int ribbon, float age) => age / _turnSeconds * 2f * Mathf.PI + ribbon * (2f * Mathf.PI / Ribbons);

        /// <summary>
        /// A figure of eight (lemniscate of Bernoulli) lying around the two of them, one lobe around
        /// each, rising and dipping a little so it reads as water winding round them.
        /// </summary>
        private Vector3 PointOnEight(float t)
        {
            Vector3 pa = _a.position + Vector3.up * 0.55f;
            Vector3 pb = _b != null ? _b.position + Vector3.up * 0.4f : pa;
            return FigureEight(pa, pb, t);
        }

        /// <summary>
        /// A point on a figure of eight lying around <paramref name="pa"/> and <paramref name="pb"/>
        /// (one lobe around each), at <paramref name="t"/> radians along it (2π: once round).
        /// </summary>
        public static Vector3 FigureEight(Vector3 pa, Vector3 pb, float t, float widen = 1f)
        {
            Vector3 mid = (pa + pb) * 0.5f;
            Vector3 axis = pb - pa;
            axis.y = 0f;
            float half = axis.magnitude * 0.5f;
            if (half < 0.4f) { axis = Vector3.right; half = 0.8f; }
            axis.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            // A lobe's middle sits at ~0.6 of the curve's reach: put each of them there.
            float reach = Mathf.Max(1.6f, half / 0.6f) * widen;
            float s = Mathf.Sin(t), c = Mathf.Cos(t);
            float k = 1f + s * s;
            float x = reach * c / k;
            float z = reach * s * c / k * 1.6f;   // wider lobes, so the loop clears the bodies
            float y = Mathf.Sin(t * 2f) * 0.35f;
            return mid + axis * x + side * z + Vector3.up * y;
        }

        private void Update()
        {
            if (_a == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _age += dt;
            float fade = Mathf.Clamp01((_duration - _age) / FadeSeconds) * Mathf.Clamp01(_age / 0.6f);

            for (int i = 0; i < _trails.Count; i++)
            {
                TrailRenderer trail = _trails[i];
                if (trail == null) continue;
                float t = Phase(i, _age);
                trail.transform.position = PointOnEight(t);
                trail.widthMultiplier = (0.32f - i * 0.06f) * fade;

                // Streaks shed along each ribbon, flowing the way it goes.
                if (_streaks != null && fade > 0.05f)
                {
                    _emitDebt += dt * 22f * fade;
                    int n = Mathf.FloorToInt(_emitDebt);
                    _emitDebt -= n;
                    if (n <= 0) continue;
                    Vector3 here = trail.transform.position;
                    Vector3 ahead = PointOnEight(t + 0.05f);
                    Vector3 flow = (ahead - here) / Mathf.Max(0.0001f, 0.05f / (2f * Mathf.PI / _turnSeconds));
                    var p = new ParticleSystem.EmitParams();
                    for (int j = 0; j < n; j++)
                    {
                        p.position = here + Random.insideUnitSphere * 0.3f;
                        p.velocity = flow * Random.Range(0.6f, 0.9f);
                        Color c = _moonlight;
                        c.a = Random.Range(0.5f, 0.9f) * fade;
                        p.startColor = c;
                        _streaks.Emit(p, 1);
                    }
                }
            }

            if (_age >= _duration + 1f) Destroy(gameObject);
            else if (_age >= _duration) foreach (TrailRenderer trail in _trails) if (trail != null) trail.emitting = false;
        }
    }
}
