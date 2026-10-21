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
        [Header("Bright water")]
        [Tooltip("In cleansed (bright) water the additive glow can't show: a deep halo behind the core keeps Manunubo visible.")]
        [SerializeField] private Color _brightWaterHalo = new Color(0.06f, 0.14f, 0.42f, 0.8f);
        [SerializeField, Min(0.1f)] private float _haloSize = 3.6f;
        [SerializeField] private Color _brightWaterTrail = new Color(0.08f, 0.3f, 0.6f, 0.7f);

        private Vector3 _home;
        private Vector3 _velocity;
        private float _phase;
        private float _calm;
        private float _shownBrightness;
        private float _brightnessTarget;
        private Vector3 _coreScale = Vector3.one;
        private float _trailWidth = 1f;
        private Transform _halo;
        private Material _haloMat;
        private Material _trailGlow, _trailShade;
        private Color _trailStart, _trailEnd;
        private float _contrast, _contrastCheck;

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
            if (_trail != null)
            {
                _trailWidth = _trail.widthMultiplier;
                _trailGlow = _trail.sharedMaterial;
                _trailStart = _trail.startColor;
                _trailEnd = _trail.endColor;
            }
        }

        private void OnDestroy() { if (_haloMat != null) Destroy(_haloMat); }

        /// <summary>The deep halo for bright water, built the first time it's needed.</summary>
        private void BuildHalo()
        {
            Material glow = null;
            foreach (ParticleSystemRenderer r in GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("M_Manunubo_Glow")) { glow = r.sharedMaterial; break; }
            Material shade = WaterBrightness.ShadeOf(glow);
            if (shade == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BrightWaterHalo";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.AddComponent<FaceCamera>();
            var r2 = go.GetComponent<MeshRenderer>();
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r2.receiveShadows = false;
            _haloMat = new Material(shade);
            r2.sharedMaterial = _haloMat;
            _halo = go.transform;
        }

        private void UpdateBrightWater(float dt, float glow)
        {
            _contrastCheck -= dt;
            if (_contrastCheck <= 0f) { _contrastCheck = 0.25f; _contrast = Mathf.MoveTowards(_contrast, WaterBrightness.Contrast, 0.25f); }
            if (_contrast > 0.01f && _halo == null) BuildHalo();
            if (_halo != null)
            {
                bool show = _contrast > 0.01f;
                if (_halo.gameObject.activeSelf != show) _halo.gameObject.SetActive(show);
                if (show)
                {
                    _halo.position = _core != null ? _core.position : transform.position;
                    _halo.localScale = Vector3.one * _haloSize * Mathf.Lerp(0.8f, 1.1f, glow);
                    Color c = _brightWaterHalo;
                    c.a *= _contrast;
                    _haloMat.SetColor("_BaseColor", c);
                }
            }
            // The trail: glowing in dark water, deep and see-through in bright water.
            if (_trail != null && _trailGlow != null)
            {
                bool bright = _contrast > 0.5f;
                if (bright && _trailShade == null) _trailShade = WaterBrightness.ShadeOf(_trailGlow);
                Material want = bright && _trailShade != null ? _trailShade : _trailGlow;
                if (_trail.sharedMaterial != want)
                {
                    _trail.sharedMaterial = want;
                    Color end = _brightWaterTrail; end.a = 0f;
                    _trail.startColor = want == _trailGlow ? _trailStart : _brightWaterTrail;
                    _trail.endColor = want == _trailGlow ? _trailEnd : end;
                }
            }
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
            UpdateBrightWater(dt, glow);

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
