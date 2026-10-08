using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Manunubo, the Caluya spirit of the sea-herds: a wisp of light with no body. It drifts around
    /// its home on slow looping curves so its trail draws long ribbons through the water, and
    /// settles into a calm hover when the player comes close to talk. Its glow is dim while the
    /// herds are scattered; Minigame 1 raises it with SetBrightness as herds come home.
    /// Sits on the talkable object (StatueDialogueRouter + collider), so the talk target moves with it.
    /// </summary>
    public class ManunuboWisp : MonoBehaviour
    {
        [Header("Drift")]
        [Tooltip("How far the wisp wanders from home on each axis (metres).")]
        [SerializeField] private Vector3 _wanderRadius = new Vector3(7f, 1.5f, 7f);
        [Tooltip("Seconds for one full loop of the wander curve.")]
        [SerializeField, Min(1f)] private float _loopSeconds = 18f;
        [SerializeField, Min(0.05f)] private float _smoothTime = 0.9f;
        [SerializeField, Min(0.1f)] private float _maxSpeed = 6f;

        [Header("Calm near the player")]
        [Tooltip("Within this distance the wisp stops wandering and hovers for the conversation.")]
        [SerializeField, Min(0f)] private float _calmRadius = 12f;
        [Tooltip("Wander left while calm, as a share of the full radius.")]
        [SerializeField, Range(0f, 1f)] private float _calmShare = 0.08f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.3f;

        [Header("Glow")]
        [SerializeField] private Transform _core;
        [SerializeField] private Light _light;
        [SerializeField] private TrailRenderer _trail;
        [SerializeField, Min(0f)] private float _lightIntensityBright = 6f;
        [SerializeField, Min(0f)] private float _pulseSpeed = 1.3f;
        [SerializeField, Range(0f, 1f)] private float _pulseDepth = 0.15f;
        [Tooltip("Starting brightness: the herds are scattered, so Manunubo is dim.")]
        [SerializeField, Range(0f, 1f)] private float _brightness = 0.45f;
        [SerializeField, Min(0.1f)] private float _brightnessEaseSeconds = 2f;

        private Vector3 _home;
        private Vector3 _velocity;
        private float _phase;
        private float _calm;
        private float _shownBrightness;
        private float _brightnessTarget;
        private Vector3 _coreScale = Vector3.one;
        private float _trailWidth = 1f;

        /// <summary>Glow level 0..1 the wisp eases toward.</summary>
        public float Brightness => _brightnessTarget;

        /// <summary>Raises or lowers the glow (e.g. herds delivered / herds needed).</summary>
        public void SetBrightness(float value) => _brightnessTarget = Mathf.Clamp01(value);

        private void Awake()
        {
            _home = transform.position;
            _phase = Random.value * 100f;
            _brightnessTarget = _brightness;
            _shownBrightness = _brightness;
            if (_core != null) _coreScale = _core.localScale;
            if (_trail != null) _trailWidth = _trail.widthMultiplier;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time + _phase;

            // Calm down near the player so the conversation has a steady target.
            float targetCalm = 0f;
            PlayerController player = PlayerController.Instance;
            if (player != null && (player.transform.position - _home).sqrMagnitude < _calmRadius * _calmRadius) targetCalm = 1f;
            _calm = Mathf.MoveTowards(_calm, targetCalm, dt * 0.5f);

            // A slow Lissajous loop: wide sweeps, gentle rises, never the same line twice in a row.
            float w = 2f * Mathf.PI / _loopSeconds;
            Vector3 loop = new Vector3(
                Mathf.Sin(t * w) * _wanderRadius.x,
                Mathf.Sin(t * w * 1.7f) * _wanderRadius.y,
                Mathf.Sin(t * w * 2f + 1.3f) * _wanderRadius.z);
            float share = Mathf.Lerp(1f, _calmShare, _calm);
            Vector3 bob = Vector3.up * Mathf.Sin(t * 1.1f) * _bobAmplitude;
            Vector3 goal = _home + loop * share + bob;
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, _smoothTime, _maxSpeed, dt);

            // Glow: ease toward the target brightness, with a slow breathing pulse.
            _shownBrightness = Mathf.MoveTowards(_shownBrightness, _brightnessTarget, dt / _brightnessEaseSeconds);
            float pulse = 1f + Mathf.Sin(t * _pulseSpeed * 2f * Mathf.PI) * _pulseDepth;
            float glow = Mathf.Lerp(0.35f, 1f, _shownBrightness) * pulse;
            if (_core != null) _core.localScale = _coreScale * glow;
            if (_light != null) _light.intensity = _lightIntensityBright * glow;

            // The trail only streams while moving; a still wisp shouldn't leave a smear.
            if (_trail != null)
            {
                float speed01 = Mathf.InverseLerp(0.1f, 2.5f, _velocity.magnitude);
                _trail.widthMultiplier = _trailWidth * Mathf.Lerp(0.25f, 1f, speed01) * glow;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 home = Application.isPlaying ? _home : transform.position;
            Gizmos.color = new Color(0.4f, 1f, 0.9f, 0.4f);
            Gizmos.DrawWireCube(home, _wanderRadius * 2f);
            Gizmos.color = new Color(1f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(home, _calmRadius);
        }
    }
}
