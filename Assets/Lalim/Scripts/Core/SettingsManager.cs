using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Global manager for saving, loading, and applying user settings via PlayerPrefs.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Audio Settings
        // ─────────────────────────────────────────────
        public float MusicVolume { get; private set; }
        public float VoiceVolume { get; private set; }
        public float SfxVolume { get; private set; }
        public float AmbientVolume { get; private set; }
        public bool SpatialAudio { get; private set; }
        public bool HeadphoneOptimization { get; private set; }

        // ─────────────────────────────────────────────
        // Quality Settings
        // ─────────────────────────────────────────────
        public int GraphicsQuality { get; private set; } // 0=Smooth, 1=Medium, 2=High, 3=Ultra, 4=Customize
        public int TextureQuality { get; private set; }  // 0=C, 1=B, 2=A
        public int MappingQuality { get; private set; }
        public int ShadowQuality { get; private set; }
        public int AmbientQuality { get; private set; }
        public int SfxQuality { get; private set; }
        public int ResolutionLevel { get; private set; }
        public bool SssQuality { get; private set; }

        // ─────────────────────────────────────────────
        // Controls Settings
        // ─────────────────────────────────────────────
        public bool Gyroscope { get; private set; }
        public bool RightHandedMode { get; private set; }
        public bool AutoSwim { get; private set; }
        public bool HapticFeedback { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        public void LoadSettings()
        {
            // Audio (Default values based on screenshots)
            MusicVolume = PlayerPrefs.GetFloat("MusicVolume", 2f);
            VoiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 10f);
            SfxVolume = PlayerPrefs.GetFloat("SfxVolume", 5f);
            AmbientVolume = PlayerPrefs.GetFloat("AmbientVolume", 8f);
            SpatialAudio = PlayerPrefs.GetInt("SpatialAudio", 1) == 1;
            HeadphoneOptimization = PlayerPrefs.GetInt("HeadphoneOptimization", 1) == 1;

            // Quality
            GraphicsQuality = PlayerPrefs.GetInt("GraphicsQuality", 4); 
            TextureQuality = PlayerPrefs.GetInt("TextureQuality", 2); 
            MappingQuality = PlayerPrefs.GetInt("MappingQuality", 2);
            ShadowQuality = PlayerPrefs.GetInt("ShadowQuality", 2);
            AmbientQuality = PlayerPrefs.GetInt("AmbientQuality", 1);
            SfxQuality = PlayerPrefs.GetInt("SfxQuality", 1);
            ResolutionLevel = PlayerPrefs.GetInt("ResolutionLevel", 1);
            SssQuality = PlayerPrefs.GetInt("SssQuality", 1) == 1;

            // Controls
            Gyroscope = PlayerPrefs.GetInt("Gyroscope", 1) == 1;
            RightHandedMode = PlayerPrefs.GetInt("RightHandedMode", 1) == 1;
            AutoSwim = PlayerPrefs.GetInt("AutoSwim", 1) == 1;
            HapticFeedback = PlayerPrefs.GetInt("HapticFeedback", 1) == 1;

            ApplySettings();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("MusicVolume", MusicVolume);
            PlayerPrefs.SetFloat("VoiceVolume", VoiceVolume);
            PlayerPrefs.SetFloat("SfxVolume", SfxVolume);
            PlayerPrefs.SetFloat("AmbientVolume", AmbientVolume);
            PlayerPrefs.SetInt("SpatialAudio", SpatialAudio ? 1 : 0);
            PlayerPrefs.SetInt("HeadphoneOptimization", HeadphoneOptimization ? 1 : 0);

            PlayerPrefs.SetInt("GraphicsQuality", GraphicsQuality);
            PlayerPrefs.SetInt("TextureQuality", TextureQuality);
            PlayerPrefs.SetInt("MappingQuality", MappingQuality);
            PlayerPrefs.SetInt("ShadowQuality", ShadowQuality);
            PlayerPrefs.SetInt("AmbientQuality", AmbientQuality);
            PlayerPrefs.SetInt("SfxQuality", SfxQuality);
            PlayerPrefs.SetInt("ResolutionLevel", ResolutionLevel);
            PlayerPrefs.SetInt("SssQuality", SssQuality ? 1 : 0);

            PlayerPrefs.SetInt("Gyroscope", Gyroscope ? 1 : 0);
            PlayerPrefs.SetInt("RightHandedMode", RightHandedMode ? 1 : 0);
            PlayerPrefs.SetInt("AutoSwim", AutoSwim ? 1 : 0);
            PlayerPrefs.SetInt("HapticFeedback", HapticFeedback ? 1 : 0);

            PlayerPrefs.Save();
            ApplySettings();
        }

        public void SetAudioSettings(float music, float voice, float sfx, float amb, bool spatial, bool headphone)
        {
            MusicVolume = music;
            VoiceVolume = voice;
            SfxVolume = sfx;
            AmbientVolume = amb;
            SpatialAudio = spatial;
            HeadphoneOptimization = headphone;
            SaveSettings();
        }

        public void SetQualitySettings(int graphics, int texture, int mapping, int shadow, int ambient, int sfxQ, int res, bool sss)
        {
            GraphicsQuality = graphics;
            TextureQuality = texture;
            MappingQuality = mapping;
            ShadowQuality = shadow;
            AmbientQuality = ambient;
            SfxQuality = sfxQ;
            ResolutionLevel = res;
            SssQuality = sss;
            SaveSettings();
        }

        public void SetControlsSettings(bool gyro, bool rightHanded, bool autoSwim, bool haptic)
        {
            Gyroscope = gyro;
            RightHandedMode = rightHanded;
            AutoSwim = autoSwim;
            HapticFeedback = haptic;
            SaveSettings();
        }

        private void ApplySettings()
        {
            // TODO: Hook into Unity's AudioMixer and QualitySettings here.
            // Example: AudioListener.volume = MusicVolume / 10f;
            // Example: QualitySettings.SetQualityLevel(GraphicsQuality);
        }
    }
}
