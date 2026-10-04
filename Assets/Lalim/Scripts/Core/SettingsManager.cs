using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Lalim.Core
{
    /// <summary>
    /// Owns the player's settings (GameSettings), persists them in PlayerPrefs and applies them:
    ///
    ///  Audio     volumes / mutes / 3D spatial / headphone mode → GameAudio
    ///  Quality   preset → project quality level; per-row overrides:
    ///            Texture (mip limit), Mapping (anisotropic filtering + LOD detail),
    ///            Shadow (off / hard / soft), Ambient Effect (post-processing, caustics, god rays),
    ///            SFX (overlapping sounds, in GameAudio), Resolution (render scale), frame rate.
    ///            SSS is stored for a future subsurface-scattering shader; nothing uses it yet.
    ///  Controls  read directly by CameraController (speed, invert, gyro), PlayerController
    ///            (auto-swim) and HudHandedness (right-handed mode); haptics pulse from here.
    ///
    /// Created automatically before the first scene loads. The settings modal edits a copy:
    /// Preview() applies it live, Save() persists it, Revert() restores the saved copy.
    /// In the editor, quality/pipeline changes made during Play mode are undone on exit.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        private const string PrefsKey = "Lalim.Settings.v1";

        /// <summary>Preset index → project quality level name (see Project Settings ▸ Quality).</summary>
        private static readonly string[] PresetQualityNames = { "Low", "Medium", "High", "Ultra" };

        private static readonly int[] TextureMipLimit = { 2, 1, 0 };
        private static readonly AnisotropicFiltering[] Anisotropic =
            { AnisotropicFiltering.Disable, AnisotropicFiltering.Enable, AnisotropicFiltering.ForceEnable };
        private static readonly float[] LodDetail = { 0.7f, 1f, 1.5f };
        private static readonly float[] RenderScale = { 0.7f, 0.85f, 1f };

        public static SettingsManager Instance { get; private set; }

        /// <summary>What's stored in PlayerPrefs.</summary>
        public GameSettings Saved { get; private set; }

        /// <summary>What's applied right now (differs from Saved while the modal previews changes).</summary>
        public GameSettings Current { get; private set; }

        private static GameSettings _fallback;

        /// <summary>The applied settings, or defaults when no manager exists (edit-mode tests).</summary>
        public static GameSettings Active => Instance != null ? Instance.Current : (_fallback ??= new GameSettings());

        private int _appliedQualityLevel = -1;

        // Authored render scale of each pipeline asset we scale (Resolution row multiplies it).
        private readonly Dictionary<UniversalRenderPipelineAsset, float> _authoredRenderScale =
            new Dictionary<UniversalRenderPipelineAsset, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            EditorQualitySnapshot.Capture();
#endif
            Ensure();
        }

        public static SettingsManager Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[SettingsManager]");
            return go.AddComponent<SettingsManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventBus.Subscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnPlayerHealthChanged);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            // Put pipeline assets back the way they were authored (matters in the editor).
            foreach (var kv in _authoredRenderScale)
                if (kv.Key != null) kv.Key.renderScale = kv.Value;
        }

        // ─────────────────────────────────────────────
        // Load / Save
        // ─────────────────────────────────────────────

        public void Load()
        {
            GameSettings loaded = null;
            string json = PlayerPrefs.GetString(PrefsKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                try { loaded = JsonUtility.FromJson<GameSettings>(json); }
                catch (System.Exception e) { Debug.LogWarning($"[SettingsManager] Couldn't read saved settings, using defaults: {e.Message}"); }
            }
            loaded ??= new GameSettings();
            loaded.Sanitize();

            Saved = loaded;
            Current = loaded.Clone();
            Apply();
        }

        /// <summary>Apply without persisting (live preview while the modal is open).</summary>
        public void Preview(GameSettings settings)
        {
            Current = settings.Clone();
            Current.Sanitize();
            Apply();
        }

        /// <summary>Persist and apply.</summary>
        public void Save(GameSettings settings)
        {
            Saved = settings.Clone();
            Saved.Sanitize();
            Current = Saved.Clone();
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Saved));
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Throw away previewed changes.</summary>
        public void Revert() => Preview(Saved);

        public bool HasUnsavedPreview => !Current.SameAs(Saved);

        // ─────────────────────────────────────────────
        // Apply
        // ─────────────────────────────────────────────

        private void Apply()
        {
            ApplyQualityLevel();

            GameSettings s = Current;
            QualitySettings.globalTextureMipmapLimit = TextureMipLimit[s.textureQuality];
            QualitySettings.anisotropicFiltering = Anisotropic[s.mappingQuality];
            QualitySettings.lodBias = LodDetail[s.mappingQuality];
            ApplyRenderScale();

            // Mobile ignores vSync; on desktop vSync would override the frame-rate cap.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = s.TargetFrameRate;

            ApplyToScene();
            GameAudio.RefreshBoundSources();
            EventBus.Publish(new SettingsChangedEvent { Settings = Current });
        }

        private void ApplyQualityLevel()
        {
            int level = QualityLevelForPreset(Current.EffectivePreset);
            if (level < 0 || level == _appliedQualityLevel) return;
            _appliedQualityLevel = level;
            if (QualitySettings.GetQualityLevel() != level)
                QualitySettings.SetQualityLevel(level, true);
        }

        /// <summary>Finds the quality level by name, or spreads the presets across whatever levels exist.</summary>
        public static int QualityLevelForPreset(int preset)
        {
            string[] names = QualitySettings.names;
            if (names.Length == 0) return -1;
            preset = Mathf.Clamp(preset, 0, PresetQualityNames.Length - 1);

            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], PresetQualityNames[preset], System.StringComparison.OrdinalIgnoreCase))
                    return i;

            float t = preset / (float)(PresetQualityNames.Length - 1);
            return Mathf.RoundToInt(t * (names.Length - 1));
        }

        /// <summary>Resolution row: scales the active pipeline's authored render scale.</summary>
        private void ApplyRenderScale()
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)) return;
            if (!_authoredRenderScale.TryGetValue(urp, out float authored))
            {
                authored = urp.renderScale;
                _authoredRenderScale[urp] = authored;
            }
            float target = authored * RenderScale[Current.resolutionQuality];
            if (!Mathf.Approximately(urp.renderScale, target)) urp.renderScale = target;
        }

        /// <summary>Per-camera and per-scene switches: shadows, post-processing, ambient effects.</summary>
        private void ApplyToScene()
        {
            GameSettings s = Current;

            Camera cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                data.renderShadows = s.shadowQuality > 0;
                data.renderPostProcessing = s.ambientQuality > 0;
            }

            // Shadow B = hard shadows, A = soft shadows (only on lights that cast shadows at all).
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                if (!_lightShadows.TryGetValue(light, out LightShadows authored))
                {
                    authored = light.shadows;
                    _lightShadows[light] = authored;
                }
                if (authored == LightShadows.None) continue;
                light.shadows = s.shadowQuality == 0 ? LightShadows.None
                              : s.shadowQuality == 1 ? LightShadows.Hard
                              : authored;
            }

            AmbientEffect.ApplyTier(s.ambientQuality);
        }

        private readonly Dictionary<Light, LightShadows> _lightShadows = new Dictionary<Light, LightShadows>();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lightShadows.Clear();
            AmbientEffect.TagKnownObjects(scene);
            ApplyRenderScale(); // quality level may have swapped the pipeline asset
            ApplyToScene();
        }

        // ─────────────────────────────────────────────
        // Haptics
        // ─────────────────────────────────────────────

        private float _lastPulseTime = -10f;

        /// <summary>Short vibration if haptics are on (phones only; ignored in the editor).</summary>
        public static void Pulse()
        {
            if (Instance == null || !Instance.Current.haptics) return;
            if (Time.unscaledTime - Instance._lastPulseTime < 0.35f) return;
            Instance._lastPulseTime = Time.unscaledTime;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        // Only real hits buzz — per-frame toxic damage is far below this.
        private void OnPlayerHealthChanged(PlayerHealthChangedEvent evt)
        {
            if (evt.Delta <= -5f) Pulse();
        }

        private void OnPlayerDied(PlayerDiedEvent evt) => Pulse();

#if UNITY_EDITOR
        /// <summary>
        /// QualitySettings changed during Play mode stick in the editor. Snapshot them when Play
        /// starts and put them back when it ends, so testing settings never edits the project.
        /// </summary>
        private static class EditorQualitySnapshot
        {
            private struct Level
            {
                public int MipLimit;
                public AnisotropicFiltering Aniso;
                public float LodBias;
                public int VSync;
            }

            private static int _level = -1;
            private static Level[] _levels;
            private static bool _hooked;

            public static void Capture()
            {
                _level = QualitySettings.GetQualityLevel();
                int count = QualitySettings.names.Length;
                _levels = new Level[count];
                for (int i = 0; i < count; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    _levels[i] = new Level
                    {
                        MipLimit = QualitySettings.globalTextureMipmapLimit,
                        Aniso = QualitySettings.anisotropicFiltering,
                        LodBias = QualitySettings.lodBias,
                        VSync = QualitySettings.vSyncCount,
                    };
                }
                QualitySettings.SetQualityLevel(_level, false);

                if (_hooked) return;
                _hooked = true;
                UnityEditor.EditorApplication.playModeStateChanged += state =>
                {
                    if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) Restore();
                };
            }

            private static void Restore()
            {
                if (_levels == null || _level < 0) return;
                for (int i = 0; i < _levels.Length && i < QualitySettings.names.Length; i++)
                {
                    QualitySettings.SetQualityLevel(i, false);
                    QualitySettings.globalTextureMipmapLimit = _levels[i].MipLimit;
                    QualitySettings.anisotropicFiltering = _levels[i].Aniso;
                    QualitySettings.lodBias = _levels[i].LodBias;
                    QualitySettings.vSyncCount = _levels[i].VSync;
                }
                QualitySettings.SetQualityLevel(_level, true);
                _levels = null;
            }
        }
#endif
    }
}
