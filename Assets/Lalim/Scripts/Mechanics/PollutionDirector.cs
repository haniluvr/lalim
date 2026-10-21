using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Makes pollution visible and lets it recede gradually as zones are cleared.
    ///
    /// Level cleanliness = cleared zones / total zones (0, 0.33, 0.67, 1), tweened over several
    /// seconds. Each zone's own "look" is:
    ///   cleared zone   → lerp(clearedFloor, 1, levelCleanliness)   (vivid)
    ///   uncleared zone → levelCleanliness × unclearedShare           (murky, but less so as others clear)
    /// So the more zones are cleared, the cleaner everything looks, and cleared zones look cleanest.
    ///
    /// Fog and colour grading follow the look of the zone the player is in; coral tint, murk
    /// particles and marine life follow each zone's own look.
    /// </summary>
    [DisallowMultipleComponent]
    public class PollutionDirector : MonoBehaviour
    {
        public static PollutionDirector Instance { get; private set; }

        [Serializable]
        public class ZoneLook
        {
            public string zoneId;
            [Tooltip("Bounds used to know which zone the player is in.")]
            public Collider bounds;
            [Tooltip("Reef renderers tinted from bleached (polluted) to their real colour (clean).")]
            public List<Renderer> reefRenderers = new List<Renderer>();
            [Tooltip("Silt / floating plastic particles: emission scales down as the zone gets cleaner.")]
            public List<ParticleSystem> murk = new List<ParticleSystem>();
            [Tooltip("Marine life that returns once the zone's look reaches its threshold.")]
            public List<TieredObject> life = new List<TieredObject>();

            [Header("Optional: this zone's own clean water (else the scene's fog)")]
            public bool overrideCleanFog;
            public Color cleanFogColor = new Color(0f, 0.35f, 0.65f);
            [Min(0f)] public float cleanFogDensity = 0.015f;
            [Tooltip("Scales the depth fog's density here: above 1 = murkier, shorter visibility.")]
            [Min(0.05f)] public float depthFogScale = 1f;

            // Cached at Awake: the bounds collider is the zone's toxic volume, which is disabled
            // once the zone is cleansed (a disabled collider reports empty bounds).
            [NonSerialized] public Bounds cachedBounds;
            [NonSerialized] public float look;
            [NonSerialized] public float appliedLook = -1f;
        }

        [Serializable]
        public class TieredObject
        {
            public GameObject target;
            [Range(0f, 1f)] public float appearsAtLook = 0.5f;
        }

        [Header("Zones (in level order)")]
        [SerializeField] private List<ZoneLook> _zones = new List<ZoneLook>();

        [Header("Look rules")]
        [Tooltip("A cleared zone's look never drops below this.")]
        [SerializeField, Range(0f, 1f)] private float _clearedFloor = 0.8f;
        [Tooltip("How much of the level-wide cleanliness reaches zones that are still polluted.")]
        [SerializeField, Range(0f, 1f)] private float _unclearedShare = 0.6f;

        [Header("Timing")]
        [Tooltip("Seconds for the level-wide tween after a zone is cleansed.")]
        [SerializeField, Min(0.1f)] private float _cleanseSeconds = 8f;
        [Tooltip("Seconds for fog/grading to follow the player into another zone.")]
        [SerializeField, Min(0.1f)] private float _zoneBlendSeconds = 3f;

        [Header("Fog")]
        [SerializeField] private Color _pollutedFogColor = new Color(0.16f, 0.22f, 0.12f);
        [SerializeField, Min(0f)] private float _pollutedFogDensity = 0.035f;
        [Tooltip("The camera's far plane follows the fog (where it hides 98% of the scene), up to this. 0 leaves the camera alone.")]
        [SerializeField, Min(0f)] private float _fogFarClipMax = 500f;

        [Header("Colour grading (polluted volume, weight = 1 − look)")]
        [SerializeField, Range(-100f, 0f)] private float _pollutedSaturation = -45f;
        [SerializeField, Range(-100f, 100f)] private float _pollutedContrast = -15f;
        [SerializeField] private Color _pollutedTint = new Color(0.86f, 0.9f, 0.7f);
        [SerializeField, Range(0f, 1f)] private float _pollutedVignette = 0.3f;

        [Header("Light")]
        [SerializeField] private List<Light> _lights = new List<Light>();
        [SerializeField, Range(0f, 1f)] private float _pollutedLightFactor = 0.55f;

        [Header("Reef tint")]
        [SerializeField] private Color _bleachedTint = new Color(0.78f, 0.76f, 0.72f);
        [SerializeField, Range(0f, 1f)] private float _bleachStrength = 0.75f;

        // Own record of cleared zones: the event can arrive before RegionProgressTracker updates.
        private readonly HashSet<string> _cleared = new HashSet<string>();
        private float _levelTarget;
        private float _level;
        private float _playerLook;

        private Color _cleanFogColor;
        private float _cleanFogDensity;
        private Color _sceneFogColor;
        private float _sceneFogDensity;
        private float _depthFogScale = 1f;
        private Volume _volume;
        private readonly Dictionary<Light, float> _lightBase = new Dictionary<Light, float>();
        private readonly Dictionary<ParticleSystem, float> _murkBase = new Dictionary<ParticleSystem, float>();
        private readonly Dictionary<Renderer, Color[]> _reefBase = new Dictionary<Renderer, Color[]>();
        private MaterialPropertyBlock _mpb;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Level-wide cleanliness 0..1 (tweened).</summary>
        public float Cleanliness => _level;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _cleanFogColor = RenderSettings.fogColor;
            _cleanFogDensity = RenderSettings.fogDensity;
            _sceneFogColor = _cleanFogColor;
            _sceneFogDensity = _cleanFogDensity;
            _mpb = new MaterialPropertyBlock();

            foreach (Light l in _lights) if (l != null) _lightBase[l] = l.intensity;
            foreach (ZoneLook z in _zones)
            {
                if (z.bounds != null) z.cachedBounds = z.bounds.bounds;
                foreach (ParticleSystem p in z.murk) if (p != null) _murkBase[p] = p.emission.rateOverTimeMultiplier;
                foreach (Renderer r in z.reefRenderers) CacheReefColors(r);
            }

            CreatePollutedVolume();
        }

        private void OnDestroy()
        {
            UnderwaterDepthFog.ZoneDensityScale = 1f;
            if (Instance == this) Instance = null;
            if (_volume != null && _volume.profile != null) Destroy(_volume.profile);
        }

        private void OnEnable() => EventBus.Subscribe<ZoneCompletedEvent>(OnZoneCompleted);
        private void OnDisable() => EventBus.Unsubscribe<ZoneCompletedEvent>(OnZoneCompleted);

        private System.Collections.IEnumerator Start()
        {
            // Wait one frame so the save (and RegionProgressTracker) has been restored first.
            yield return null;

            // Saved progress applies instantly — no tween on load.
            foreach (ZoneLook z in _zones)
            {
                if (RegionProgressTracker.Instance != null && RegionProgressTracker.Instance.IsZoneCompleted(z.zoneId)) _cleared.Add(z.zoneId);
            }
            _levelTarget = ComputeLevelTarget();
            _level = _levelTarget;
            RecomputeZoneLooks();
            _playerLook = LookAtPlayer();
            BlendCleanWater(instant: true);
            ApplyAll(force: true);
            _initialised = true;
        }

        private void OnZoneCompleted(ZoneCompletedEvent evt)
        {
            _cleared.Add(evt.ZoneId);
            _levelTarget = ComputeLevelTarget();
            EventBus.Publish(new CleanlinessChangedEvent { Target = _levelTarget, Instant = false });
        }

        private float ComputeLevelTarget()
        {
            if (_zones.Count == 0) return 1f;
            int cleared = 0;
            foreach (ZoneLook z in _zones) if (IsCleared(z.zoneId)) cleared++;
            return (float)cleared / _zones.Count;
        }

        private bool IsCleared(string zoneId) => _cleared.Contains(zoneId);

        private bool _initialised;

        private void Update()
        {
            if (!_initialised) return;
            if (!Mathf.Approximately(_level, _levelTarget))
            {
                _level = Mathf.MoveTowards(_level, _levelTarget, Time.deltaTime / _cleanseSeconds);
            }
            RecomputeZoneLooks();
            _playerLook = Mathf.MoveTowards(_playerLook, LookAtPlayer(), Time.deltaTime / _zoneBlendSeconds);
            BlendCleanWater(instant: false);
            ApplyAll(force: false);
        }

        /// <summary>
        /// Eases the clean water colour, fog density and depth-fog scale toward the zone the player
        /// is in (zones without an override use the scene's own fog).
        /// </summary>
        private void BlendCleanWater(bool instant)
        {
            Color color = _sceneFogColor;
            float density = _sceneFogDensity;
            float scale = 1f;
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                Vector3 p = player.transform.position;
                foreach (ZoneLook z in _zones)
                {
                    if (!z.overrideCleanFog || z.cachedBounds.size == Vector3.zero || !z.cachedBounds.Contains(p)) continue;
                    color = z.cleanFogColor;
                    density = z.cleanFogDensity;
                    scale = z.depthFogScale;
                    break;
                }
            }

            float t = instant ? 1f : Mathf.Clamp01(Time.deltaTime / _zoneBlendSeconds);
            _cleanFogColor = Color.Lerp(_cleanFogColor, color, t);
            _cleanFogDensity = Mathf.Lerp(_cleanFogDensity, density, t);
            _depthFogScale = Mathf.Lerp(_depthFogScale, scale, t);
            UnderwaterDepthFog.ZoneDensityScale = _depthFogScale;
        }

        private void RecomputeZoneLooks()
        {
            // Smoothstep the level value so each cleanse eases in and out.
            float eased = Mathf.SmoothStep(0f, 1f, _level);
            foreach (ZoneLook z in _zones)
            {
                z.look = IsCleared(z.zoneId)
                    ? Mathf.Lerp(_clearedFloor, 1f, eased)
                    : eased * _unclearedShare;
            }
        }

        private const float MinFarClip = 120f;

        /// <summary>
        /// The camera stops drawing where the fog has already hidden the scene: exponential fog lets
        /// exp(-density·d) of an object through, 2% at d = 3.9 / density. Nothing visible is lost, and
        /// phones draw far less (Ajuy's village view: about a quarter of the objects at 500 m).
        /// </summary>
        private void FitFarClipToFog(float density)
        {
            if (_fogFarClipMax <= 0f || density <= 0f || RenderSettings.fogMode != FogMode.Exponential) return;
            Camera cam = Camera.main;
            if (cam != null) cam.farClipPlane = Mathf.Clamp(3.9f / density, MinFarClip, _fogFarClipMax);
        }

        /// <summary>Look of the zone containing the player (falls back to the level value).</summary>
        private float LookAtPlayer()
        {
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                Vector3 p = player.transform.position;
                foreach (ZoneLook z in _zones)
                {
                    if (z.cachedBounds.size != Vector3.zero && z.cachedBounds.Contains(p)) return z.look;
                }
            }
            return Mathf.SmoothStep(0f, 1f, _level) * _unclearedShare;
        }

        private void ApplyAll(bool force)
        {
            float look = _playerLook;

            RenderSettings.fogColor = Color.Lerp(_pollutedFogColor, _cleanFogColor, look);
            RenderSettings.fogDensity = Mathf.Lerp(_pollutedFogDensity, _cleanFogDensity, look);
            FitFarClipToFog(RenderSettings.fogDensity);
            if (_volume != null) _volume.weight = 1f - look;

            float lightFactor = Mathf.Lerp(_pollutedLightFactor, 1f, look);
            foreach (var kv in _lightBase) if (kv.Key != null) kv.Key.intensity = kv.Value * lightFactor;

            foreach (ZoneLook z in _zones)
            {
                if (!force && Mathf.Abs(z.look - z.appliedLook) < 0.005f) continue;
                z.appliedLook = z.look;

                foreach (ParticleSystem p in z.murk)
                {
                    if (p == null || !_murkBase.TryGetValue(p, out float baseRate)) continue;
                    var emission = p.emission;
                    emission.rateOverTimeMultiplier = baseRate * Mathf.Lerp(1f, 0.1f, z.look);
                }

                foreach (TieredObject o in z.life)
                {
                    if (o != null && o.target != null) o.target.SetActive(z.look >= o.appearsAtLook);
                }

                float bleach = (1f - z.look) * _bleachStrength;
                foreach (Renderer r in z.reefRenderers) ApplyReefTint(r, bleach);
            }
        }

        private void CacheReefColors(Renderer r)
        {
            if (r == null || _reefBase.ContainsKey(r)) return;
            Material[] mats = r.sharedMaterials;
            var colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) colors[i] = Color.white;
                else if (m.HasProperty(BaseColorId)) colors[i] = m.GetColor(BaseColorId);
                else if (m.HasProperty(ColorId)) colors[i] = m.GetColor(ColorId);
                else colors[i] = Color.white;
            }
            _reefBase[r] = colors;
        }

        private void ApplyReefTint(Renderer r, float bleach)
        {
            if (r == null || !_reefBase.TryGetValue(r, out Color[] colors)) return;
            for (int i = 0; i < colors.Length; i++)
            {
                Color baseColor = colors[i];
                float grey = baseColor.grayscale;
                Color bleached = new Color(grey, grey, grey, baseColor.a) * _bleachedTint;
                bleached.a = baseColor.a;

                r.GetPropertyBlock(_mpb, i);
                if (bleach <= 0.001f)
                {
                    _mpb.Clear();
                }
                else
                {
                    Color c = Color.Lerp(baseColor, bleached, bleach);
                    _mpb.SetColor(BaseColorId, c);
                    _mpb.SetColor(ColorId, c);
                }
                r.SetPropertyBlock(_mpb, i);
            }
        }

        private void CreatePollutedVolume()
        {
            var go = new GameObject("[PollutedLook_Volume]");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 50f;
            _volume.weight = 1f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(_pollutedSaturation);
            color.contrast.Override(_pollutedContrast);
            color.colorFilter.Override(_pollutedTint);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(_pollutedVignette);
            vignette.color.Override(new Color(0.12f, 0.14f, 0.06f));
            _volume.profile = profile;
        }
    }
}
