using System;
using UnityEngine;

namespace Lalim.Core
{
    public enum AudioChannel { Music, Voice, Sfx, Ambient }

    /// <summary>The C / B / A rows on the Quality tab.</summary>
    public enum QualityRow { Texture, Mapping, Shadow, Ambient, Sfx, Resolution }

    /// <summary>
    /// Every player-facing setting, stored as one JSON blob in PlayerPrefs by SettingsManager.
    /// Plain data so the settings modal can edit a copy, preview it live, then save or discard.
    /// Tiers on the Quality tab are 0 = C (low), 1 = B, 2 = A (high).
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        public const int VolumeSteps = 10;
        public const int SensitivitySteps = 10;
        public const int TierCount = 3;

        /// <summary>Graphics presets shown on the Quality tab; the last one is "Customize".</summary>
        public static readonly string[] GraphicsPresetNames = { "Smooth", "Medium", "High", "Ultra", "Customize" };
        public static int CustomizePreset => GraphicsPresetNames.Length - 1;
        public static readonly string[] TierNames = { "C", "B", "A" };

        /// <summary>Row tiers each preset sets: Texture, Mapping, Shadow, Ambient, SFX, Resolution, then SSS.</summary>
        private static readonly int[][] PresetTiers =
        {
            new[] { 0, 0, 0, 0, 1, 1 }, // Smooth
            new[] { 1, 1, 1, 1, 1, 1 }, // Medium
            new[] { 2, 2, 2, 1, 2, 2 }, // High
            new[] { 2, 2, 2, 2, 2, 2 }, // Ultra
        };
        private static readonly bool[] PresetSss = { false, false, true, true };

        // ── Audio (0–10) ────────────────────────────
        public int music = 7;
        public int voice = 10;
        public int sfx = 8;
        public int ambient = 7;
        public bool musicMuted;
        public bool voiceMuted;
        public bool sfxMuted;
        public bool ambientMuted;
        public bool spatialAudio = true;          // sounds come from where they happen (3D)
        public bool headphoneOptimization = true; // sharp left/right placement; off = softer, for phone speakers

        // ── Quality ─────────────────────────────────
        public int graphicsPreset = 1;
        public int textureQuality = 1;
        public int mappingQuality = 1;
        public int shadowQuality = 1;
        public int ambientQuality = 1;
        public int sfxQuality = 1;
        public int resolutionQuality = 1;
        public bool sssQuality;
        public bool highFrameRate = true;         // 60 fps; off = 30 fps (battery saver)

        // ── Controls ────────────────────────────────
        public bool gyroscope;                    // tilt the phone to look around
        public bool rightHandedMode = true;       // off = HUD mirrored for left-handed play
        public bool autoSwim;                     // keep swimming forward while the joystick is idle
        public bool haptics = true;
        public int cameraSensitivity = 5;         // 1–10, 5 = default speed
        public bool invertCameraY;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        public bool SameAs(GameSettings other) =>
            other != null && JsonUtility.ToJson(this) == JsonUtility.ToJson(other);

        /// <summary>Clamps everything into range (old or hand-edited PlayerPrefs can hold anything).</summary>
        public void Sanitize()
        {
            music = Mathf.Clamp(music, 0, VolumeSteps);
            voice = Mathf.Clamp(voice, 0, VolumeSteps);
            sfx = Mathf.Clamp(sfx, 0, VolumeSteps);
            ambient = Mathf.Clamp(ambient, 0, VolumeSteps);
            graphicsPreset = Mathf.Clamp(graphicsPreset, 0, CustomizePreset);
            foreach (QualityRow row in Enum.GetValues(typeof(QualityRow)))
                SetTier(row, GetTier(row));
            cameraSensitivity = Mathf.Clamp(cameraSensitivity, 1, SensitivitySteps);
        }

        // ── Audio helpers ───────────────────────────

        public int GetVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return music;
                case AudioChannel.Voice: return voice;
                case AudioChannel.Ambient: return ambient;
                default: return sfx;
            }
        }

        public void SetVolume(AudioChannel channel, int value)
        {
            value = Mathf.Clamp(value, 0, VolumeSteps);
            switch (channel)
            {
                case AudioChannel.Music: music = value; break;
                case AudioChannel.Voice: voice = value; break;
                case AudioChannel.Ambient: ambient = value; break;
                default: sfx = value; break;
            }
        }

        public bool IsMuted(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return musicMuted;
                case AudioChannel.Voice: return voiceMuted;
                case AudioChannel.Ambient: return ambientMuted;
                default: return sfxMuted;
            }
        }

        public void SetMuted(AudioChannel channel, bool muted)
        {
            switch (channel)
            {
                case AudioChannel.Music: musicMuted = muted; break;
                case AudioChannel.Voice: voiceMuted = muted; break;
                case AudioChannel.Ambient: ambientMuted = muted; break;
                default: sfxMuted = muted; break;
            }
        }

        /// <summary>0–1 gain for a channel. Squared so the slider feels even to the ear.</summary>
        public float Gain(AudioChannel channel)
        {
            if (IsMuted(channel)) return 0f;
            float t = GetVolume(channel) / (float)VolumeSteps;
            return t * t;
        }

        // ── Quality helpers ─────────────────────────

        public int GetTier(QualityRow row)
        {
            switch (row)
            {
                case QualityRow.Texture: return textureQuality;
                case QualityRow.Mapping: return mappingQuality;
                case QualityRow.Shadow: return shadowQuality;
                case QualityRow.Ambient: return ambientQuality;
                case QualityRow.Sfx: return sfxQuality;
                default: return resolutionQuality;
            }
        }

        public void SetTier(QualityRow row, int tier)
        {
            tier = Mathf.Clamp(tier, 0, TierCount - 1);
            switch (row)
            {
                case QualityRow.Texture: textureQuality = tier; break;
                case QualityRow.Mapping: mappingQuality = tier; break;
                case QualityRow.Shadow: shadowQuality = tier; break;
                case QualityRow.Ambient: ambientQuality = tier; break;
                case QualityRow.Sfx: sfxQuality = tier; break;
                default: resolutionQuality = tier; break;
            }
        }

        /// <summary>Picks a preset and sets every row to match it. "Customize" leaves the rows as they are.</summary>
        public void ApplyPreset(int preset)
        {
            graphicsPreset = Mathf.Clamp(preset, 0, CustomizePreset);
            if (graphicsPreset == CustomizePreset) return;
            int[] tiers = PresetTiers[graphicsPreset];
            for (int i = 0; i < tiers.Length; i++) SetTier((QualityRow)i, tiers[i]);
            sssQuality = PresetSss[graphicsPreset];
        }

        /// <summary>Changing a single row turns the preset into "Customize".</summary>
        public void SetTierCustom(QualityRow row, int tier)
        {
            SetTier(row, tier);
            graphicsPreset = CustomizePreset;
        }

        /// <summary>Quality level to use for the project's quality ladder: the preset, or the closest one for Customize.</summary>
        public int EffectivePreset
        {
            get
            {
                if (graphicsPreset != CustomizePreset) return graphicsPreset;
                float avg = (textureQuality + mappingQuality + shadowQuality + ambientQuality + resolutionQuality) / 5f;
                return Mathf.Clamp(Mathf.RoundToInt(avg * 1.5f), 0, CustomizePreset - 1); // 0..2 → 0..3
            }
        }

        // ── Controls helpers ────────────────────────

        /// <summary>Look-speed multiplier: 1 → 0.3×, 5 → 1×, 10 → 2×.</summary>
        public float LookMultiplier =>
            cameraSensitivity <= 5
                ? Mathf.Lerp(0.3f, 1f, (cameraSensitivity - 1) / 4f)
                : Mathf.Lerp(1f, 2f, (cameraSensitivity - 5) / 5f);

        public int TargetFrameRate => highFrameRate ? 60 : 30;
    }
}
