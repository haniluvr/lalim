using UnityEngine;

namespace Lalim.Abilities
{
    /// <summary>
    /// Spirit Sense's guiding light. It stays for the whole Spirit Sense duration and leads the
    /// player to the next thing to do:
    ///  - Leading:  floats a few meters ahead of the player on the way to the target, so it's
    ///              always in front of them and keeps pace as they swim.
    ///  - Arrived:  once the player is close, it moves over the target and circles above it.
    ///  - Idle:     with nothing to point at, it circles around the player.
    /// Spirit Sense retargets it as things are collected/finished and dismisses it when the skill ends.
    /// </summary>
    public class SpiritWisp : MonoBehaviour
    {
        [Tooltip("How far ahead of the player the light leads while guiding.")]
        [SerializeField] private float _leadDistance = 6f;
        [Tooltip("Height above the player/target it floats at.")]
        [SerializeField] private float _height = 1.2f;
        [Tooltip("Smoothing time for movement (lower = snappier).")]
        [SerializeField] private float _smoothTime = 0.45f;
        [SerializeField] private float _maxSpeed = 14f;
        [SerializeField] private float _circleRadius = 1.1f;
        [SerializeField] private float _circleSpeed = 2.2f;
        [SerializeField] private float _bobAmplitude = 0.25f;
        [Tooltip("If the target is further than this from where the camera looks (dot product; 0.57 ≈ 55°), the light stays in view on the side to turn toward.")]
        [SerializeField, Range(-1f, 1f)] private float _visibleDot = 0.57f;

        private Transform _player;
        private Transform _target;
        private Vector3 _velocity;
        private float _fadeOut = -1f;
        private float _fadeIn;
        private Light _light;
        private float _lightIntensity;
        private float _phase;

        public Transform Target => _target;

        public void Init(Transform player, Transform target)
        {
            _player = player;
            _target = target;
            _light = GetComponentInChildren<Light>();
            if (_light != null) _lightIntensity = _light.intensity;
            _phase = Random.value * 10f;
            _fadeIn = 0f;
        }

        public void Retarget(Transform target) => _target = target;

        /// <summary>Fades the light out and destroys it (called when Spirit Sense ends).</summary>
        public void Dismiss()
        {
            if (_fadeOut < 0f) _fadeOut = 0.6f;
        }

        private void Update()
        {
            if (_fadeOut >= 0f)
            {
                _fadeOut -= Time.deltaTime;
                float k = Mathf.Clamp01(_fadeOut / 0.6f);
                if (_light != null) _light.intensity = _lightIntensity * k;
                transform.localScale = Vector3.one * k;
                if (_fadeOut <= 0f) Destroy(gameObject);
                return;
            }

            if (_player == null) { Dismiss(); return; }

            _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, Time.deltaTime * 2.5f);
            float t = Time.time + _phase;
            Vector3 goal;
            float pulse = 1f;

            if (_target == null)
            {
                // Nothing to point at: circle the player.
                goal = _player.position + Vector3.up * _height + Circle(t, _circleRadius * 1.6f);
            }
            else
            {
                Vector3 from = _player.position;
                Vector3 to = _target.position;
                Vector3 toTarget = to - from;
                float distance = toTarget.magnitude;

                if (distance <= _leadDistance)
                {
                    // Close: hover and circle right above the target — "it's here".
                    goal = to + Vector3.up * _height + Circle(t, _circleRadius);
                    pulse = 1.25f + Mathf.Sin(t * 6f) * 0.25f;
                }
                else if (TryTurnHint(from, toTarget, out Vector3 turnGoal))
                {
                    // Target is off to the side / behind: stay in view, on the side to turn toward.
                    goal = turnGoal;
                    pulse = 1.3f + Mathf.Sin(t * 5f) * 0.3f;
                }
                else
                {
                    // Lead the way: a few meters ahead of the player, on the line to the target.
                    goal = from + toTarget / distance * _leadDistance + Vector3.up * _height;
                }
            }

            goal += Vector3.up * (Mathf.Sin(t * 2.4f) * _bobAmplitude);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, _smoothTime, _maxSpeed);

            if (_light != null) _light.intensity = _lightIntensity * _fadeIn * pulse;
            transform.localScale = Vector3.one * _fadeIn;
        }

        /// <summary>
        /// When the target isn't roughly in front of the camera, the lead position would be off-screen.
        /// Instead, park the light in front of the camera, offset toward the side the player should turn.
        /// </summary>
        private bool TryTurnHint(Vector3 from, Vector3 toTarget, out Vector3 goal)
        {
            goal = default;
            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            Vector3 dirFlat = toTarget;
            dirFlat.y = 0f;
            if (camForward.sqrMagnitude < 0.001f || dirFlat.sqrMagnitude < 0.001f) return false;
            camForward.Normalize();
            dirFlat.Normalize();

            // Within ~55° of where the camera looks: normal leading is visible.
            if (Vector3.Dot(camForward, dirFlat) > _visibleDot) return false;

            Vector3 right = Vector3.Cross(Vector3.up, camForward);
            float side = Mathf.Sign(Vector3.Dot(right, dirFlat));
            goal = from + camForward * 3.5f + right * (side * 2.5f) + Vector3.up * _height;
            return true;
        }

        private Vector3 Circle(float t, float radius)
        {
            float a = t * _circleSpeed;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }

        /// <summary>Builds a simple light (point light + trail + small glowing orb) when no prefab is assigned.</summary>
        public static SpiritWisp CreateRuntime(Color color)
        {
            GameObject root = new GameObject("SpiritWisp (runtime)");

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 8f;
            light.intensity = 6f;
            light.shadows = LightShadows.None;

            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(root.transform, false);
            orb.transform.localScale = Vector3.one * 0.3f;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var mat = new Material(unlit);
                mat.SetColor("_BaseColor", color * 1.5f);
                orb.GetComponent<Renderer>().sharedMaterial = mat;
            }
            orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.8f;
            trail.startWidth = 0.2f;
            trail.endWidth = 0f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particle != null) trail.sharedMaterial = new Material(particle);
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);

            return root.AddComponent<SpiritWisp>();
        }
    }
}
