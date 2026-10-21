using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// A school released at its home (Minigame 3): it pours out of the net-bag and swims in a
    /// circle around the diver and the dugong, head first along the circle and playing its own
    /// swim, then swims off into the sanctuary. Crabs and lobsters walk the circle on the sand
    /// (crabs sideways). The callback gets where each one is when it ends, so the sanctuary's
    /// residents carry on from there.
    /// </summary>
    public class SchoolReleaseSwirl : MonoBehaviour
    {
        public enum Gait { Swim, Walk, Sideways }

        private const float GatherSeconds = 0.9f;   // out of the bag and onto the eight
        private const float TurnSeconds = 5f;       // once round the circle

        private Transform _a, _b;
        private float _duration, _age;
        private Vector3 _from;
        private readonly List<Transform> _fish = new List<Transform>();
        private readonly List<Vector3> _velocity = new List<Vector3>();
        private readonly List<float> _lead = new List<float>();     // how far along the eight each fish swims
        private readonly List<float> _widen = new List<float>();
        private readonly List<float> _rise = new List<float>();
        private Transform _glyph;
        private Material _glyphMaterial;
        private Color _glyphColor;
        private Action<List<Vector3>> _done;
        private Gait _gait;

        /// <summary>
        /// Swirls <paramref name="count"/> fish of <paramref name="model"/> from <paramref name="from"/>
        /// around <paramref name="a"/> and <paramref name="b"/> for <paramref name="seconds"/>, under
        /// <paramref name="glyph"/> (a see-through material showing the moon phase; may be null).
        /// </summary>
        public static SchoolReleaseSwirl Play(GameObject model, int count, float length, float modelYaw, Vector3 from,
                                              Transform a, Transform b, Material glyph, float seconds, Action<List<Vector3>> done, Gait gait = Gait.Swim)
        {
            var go = new GameObject("[SchoolRelease]");
            var swirl = go.AddComponent<SchoolReleaseSwirl>();
            swirl._a = a;
            swirl._b = b;
            swirl._from = from;
            swirl._duration = Mathf.Max(GatherSeconds + 1f, seconds);
            swirl._done = done;
            swirl._gait = gait;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                Transform t = SwimBody.Spawn(model, go.transform, length, modelYaw);
                t.position = from + UnityEngine.Random.insideUnitSphere * 0.4f;
                // Close to the camera: always animate (its own swim), never culled into a stiff pose.
                foreach (Animator an in t.GetComponentsInChildren<Animator>()) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (SkinnedMeshRenderer sm in t.GetComponentsInChildren<SkinnedMeshRenderer>()) sm.updateWhenOffscreen = true;
                swirl._fish.Add(t);
                swirl._velocity.Add(Vector3.zero);
                // Spread round the circle in two loose bands, so it reads as a school, not a queue.
                swirl._lead.Add(i * (Mathf.PI * 2f / Mathf.Max(1, count)) * 0.6f + (i % 2) * 0.3f);
                swirl._widen.Add(UnityEngine.Random.Range(-0.5f, 0.6f));
                swirl._rise.Add(UnityEngine.Random.Range(-0.35f, 0.45f));
            }
            swirl.BuildGlyph(glyph);
            return swirl;
        }

        private void BuildGlyph(Material glyph)
        {
            if (glyph == null) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "MoonSymbol";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(transform, false);
            var r = q.GetComponent<Renderer>();
            _glyphMaterial = glyph;
            _glyphColor = glyph.HasProperty("_BaseColor") ? glyph.GetColor("_BaseColor") : Color.white;
            r.sharedMaterial = _glyphMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _glyph = q.transform;
            SetGlyphAlpha(0f);
        }

        private void SetGlyphAlpha(float k)
        {
            if (_glyphMaterial == null || !_glyphMaterial.HasProperty("_BaseColor")) return;
            Color c = _glyphColor;
            c.a = _glyphColor.a * Mathf.Clamp01(k);
            _glyphMaterial.SetColor("_BaseColor", c);
        }

        private Vector3 A => _a != null ? _a.position + Vector3.up * 0.55f : _from;
        private Vector3 B => _b != null ? _b.position + Vector3.up * 0.4f : A;

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            Vector3 pa = A, pb = B, mid = (pa + pb) * 0.5f;
            float gather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / GatherSeconds));

            // The circle: round the middle of the two of them, wide enough to clear both bodies.
            Vector3 flat = pb - pa; flat.y = 0f;
            float radius = Mathf.Max(2.6f, flat.magnitude * 0.5f + 2f);
            bool walking = _gait != Gait.Swim;
            float speed = radius * Mathf.PI * 2f / TurnSeconds * (walking ? 0.55f : 1f);
            for (int i = 0; i < _fish.Count; i++)
            {
                Transform f = _fish[i];
                if (f == null) continue;
                float a = _age * speed / radius + _lead[i];
                float r = radius + _widen[i];
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));   // the way round it swims
                Vector3 onCircle = mid + radial * r + Vector3.up * (_rise[i] + Mathf.Sin(a * 2f + _lead[i]) * 0.2f);
                if (walking) onCircle.y = SwimBody.SeabedY(onCircle, mid.y + 2f) + 0.08f;   // on the sand
                Vector3 want = Vector3.Lerp(_from, onCircle, gather);
                f.position = Vector3.Lerp(f.position, want, 1f - Mathf.Exp(-10f * dt));
                // Head first along the circle (crabs: side on, facing out, as they scuttle).
                Vector3 heading = gather < 0.99f ? (want - f.position) + tangent * 0.5f : tangent;
                if (_gait == Gait.Sideways) heading = radial;
                _velocity[i] = heading.normalized * speed;
                if (_gait == Gait.Swim) SwimBody.Face(f, _velocity[i], _lead[i] * 3f, dt, 8f);
                else if (heading.sqrMagnitude > 1e-4f)
                {
                    heading.y = 0f;
                    f.rotation = Quaternion.Slerp(f.rotation, Quaternion.LookRotation(heading.normalized, Vector3.up), dt * 6f);
                }
            }

            // The moon symbol glows above the two of them, turning to the camera, then fades.
            if (_glyph != null)
            {
                float k = Mathf.Clamp01(_age / 0.8f) * Mathf.Clamp01((_duration - _age) / 1f);
                SetGlyphAlpha(k);
                _glyph.position = mid + Vector3.up * (2.4f + Mathf.Sin(_age * 1.6f) * 0.15f);
                _glyph.localScale = Vector3.one * (1.7f + Mathf.Sin(_age * 2.4f) * 0.08f);
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Vector3 look = _glyph.position - cam.transform.position;
                    if (look.sqrMagnitude > 0.01f) _glyph.rotation = Quaternion.LookRotation(look);
                }
            }

            if (_age < _duration) return;
            var at = new List<Vector3>();
            foreach (Transform f in _fish) if (f != null) at.Add(f.position);
            Action<List<Vector3>> done = _done;
            _done = null;
            Destroy(gameObject);
            done?.Invoke(at);
        }

        private void OnDestroy()
        {
            if (_glyphMaterial != null) Destroy(_glyphMaterial);
        }
    }
}
