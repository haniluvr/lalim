using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;
using System.Collections.Generic;

namespace Lalim.UI
{
    /// <summary>
    /// Manages the Settings UI Panel. Connects sliders, toggles, and buttons to the SettingsManager.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button _tabAudio;
        [SerializeField] private Button _tabQuality;
        [SerializeField] private Button _tabControl;
        [SerializeField] private Button _tabSystem;
        
        [SerializeField] private GameObject _panelAudio;
        [SerializeField] private GameObject _panelQuality;
        [SerializeField] private GameObject _panelControl;
        [SerializeField] private GameObject _panelSystem;

        [Header("Audio Settings")]
        [SerializeField] private Slider _sliderMusic;
        [SerializeField] private Slider _sliderVoice;
        [SerializeField] private Slider _sliderSfx;
        [SerializeField] private Slider _sliderAmbient;
        [SerializeField] private Toggle _toggleSpatialAudio;
        [SerializeField] private Toggle _toggleHeadphoneOpt;

        [Header("Quality Settings (Requires UI Toggles in ToggleGroups)")]
        [SerializeField] private ToggleGroup _groupGraphics;
        [SerializeField] private ToggleGroup _groupTexture;
        [SerializeField] private ToggleGroup _groupMapping;
        [SerializeField] private ToggleGroup _groupShadow;
        [SerializeField] private ToggleGroup _groupAmbient;
        [SerializeField] private ToggleGroup _groupSfxQuality;
        [SerializeField] private ToggleGroup _groupResolution;
        [SerializeField] private Toggle _toggleSss; // Just a simple ON/OFF toggle

        [Header("Control Settings")]
        [SerializeField] private Toggle _toggleGyroscope;
        [SerializeField] private Toggle _toggleRightHanded;
        [SerializeField] private Toggle _toggleAutoSwim;
        [SerializeField] private Toggle _toggleHaptic;

        [Header("Main Buttons")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _closeButton;

        private void Start()
        {
            // Bind Tabs
            if (_tabAudio != null) _tabAudio.onClick.AddListener(() => SwitchTab(_panelAudio));
            if (_tabQuality != null) _tabQuality.onClick.AddListener(() => SwitchTab(_panelQuality));
            if (_tabControl != null) _tabControl.onClick.AddListener(() => SwitchTab(_panelControl));
            if (_tabSystem != null) _tabSystem.onClick.AddListener(() => SwitchTab(_panelSystem));

            // Bind Action Buttons
            if (_saveButton != null) _saveButton.onClick.AddListener(SaveAllSettings);
            if (_closeButton != null) _closeButton.onClick.AddListener(CloseSettings);
        }

        private void OnEnable()
        {
            // Load settings into UI every time the panel opens
            if (SettingsManager.Instance != null)
            {
                LoadDataIntoUI();
            }
            
            SwitchTab(_panelAudio); // Default tab
        }

        private void SwitchTab(GameObject activePanel)
        {
            if (_panelAudio != null) _panelAudio.SetActive(false);
            if (_panelQuality != null) _panelQuality.SetActive(false);
            if (_panelControl != null) _panelControl.SetActive(false);
            if (_panelSystem != null) _panelSystem.SetActive(false);

            if (activePanel != null) activePanel.SetActive(true);
        }

        /// <summary>
        /// Reads current values from SettingsManager and updates the UI elements to match.
        /// </summary>
        private void LoadDataIntoUI()
        {
            var sm = SettingsManager.Instance;

            // Audio
            if (_sliderMusic != null) _sliderMusic.value = sm.MusicVolume;
            if (_sliderVoice != null) _sliderVoice.value = sm.VoiceVolume;
            if (_sliderSfx != null) _sliderSfx.value = sm.SfxVolume;
            if (_sliderAmbient != null) _sliderAmbient.value = sm.AmbientVolume;
            if (_toggleSpatialAudio != null) _toggleSpatialAudio.isOn = sm.SpatialAudio;
            if (_toggleHeadphoneOpt != null) _toggleHeadphoneOpt.isOn = sm.HeadphoneOptimization;

            // Controls
            if (_toggleGyroscope != null) _toggleGyroscope.isOn = sm.Gyroscope;
            if (_toggleRightHanded != null) _toggleRightHanded.isOn = sm.RightHandedMode;
            if (_toggleAutoSwim != null) _toggleAutoSwim.isOn = sm.AutoSwim;
            if (_toggleHaptic != null) _toggleHaptic.isOn = sm.HapticFeedback;

            // Quality
            if (_toggleSss != null) _toggleSss.isOn = sm.SssQuality;
            
            // Note: Toggle Groups (like C, B, A for texture) require a bit more manual setup in Unity.
            // You will need to ensure the correct toggle in the group is set to 'isOn = true' based on the integer index (sm.TextureQuality).
            // A helper function would typically find the toggle by index in the group's active toggles.
            SetToggleGroupIndex(_groupGraphics, sm.GraphicsQuality);
            SetToggleGroupIndex(_groupTexture, sm.TextureQuality);
            SetToggleGroupIndex(_groupMapping, sm.MappingQuality);
            SetToggleGroupIndex(_groupShadow, sm.ShadowQuality);
            SetToggleGroupIndex(_groupAmbient, sm.AmbientQuality);
            SetToggleGroupIndex(_groupSfxQuality, sm.SfxQuality);
            SetToggleGroupIndex(_groupResolution, sm.ResolutionLevel);
        }

        /// <summary>
        /// Gathers values from UI elements and saves them to SettingsManager.
        /// </summary>
        public void SaveAllSettings()
        {
            if (SettingsManager.Instance == null) return;

            var sm = SettingsManager.Instance;

            // Save Audio
            sm.SetAudioSettings(
                _sliderMusic != null ? _sliderMusic.value : sm.MusicVolume,
                _sliderVoice != null ? _sliderVoice.value : sm.VoiceVolume,
                _sliderSfx != null ? _sliderSfx.value : sm.SfxVolume,
                _sliderAmbient != null ? _sliderAmbient.value : sm.AmbientVolume,
                _toggleSpatialAudio != null ? _toggleSpatialAudio.isOn : sm.SpatialAudio,
                _toggleHeadphoneOpt != null ? _toggleHeadphoneOpt.isOn : sm.HeadphoneOptimization
            );

            // Save Controls
            sm.SetControlsSettings(
                _toggleGyroscope != null ? _toggleGyroscope.isOn : sm.Gyroscope,
                _toggleRightHanded != null ? _toggleRightHanded.isOn : sm.RightHandedMode,
                _toggleAutoSwim != null ? _toggleAutoSwim.isOn : sm.AutoSwim,
                _toggleHaptic != null ? _toggleHaptic.isOn : sm.HapticFeedback
            );

            // Save Quality
            sm.SetQualitySettings(
                GetToggleGroupIndex(_groupGraphics, sm.GraphicsQuality),
                GetToggleGroupIndex(_groupTexture, sm.TextureQuality),
                GetToggleGroupIndex(_groupMapping, sm.MappingQuality),
                GetToggleGroupIndex(_groupShadow, sm.ShadowQuality),
                GetToggleGroupIndex(_groupAmbient, sm.AmbientQuality),
                GetToggleGroupIndex(_groupSfxQuality, sm.SfxQuality),
                GetToggleGroupIndex(_groupResolution, sm.ResolutionLevel),
                _toggleSss != null ? _toggleSss.isOn : sm.SssQuality
            );

            Debug.Log("[SettingsUI] Settings Saved Globally!");
        }

        private void CloseSettings()
        {
            // Usually the UIManager handles popping the panel, but we can also just tell UIManager to pop.
            if (UIManager.Instance != null)
            {
                UIManager.Instance.PopPanel();
            }
            else
            {
                gameObject.SetActive(false); // Fallback
            }
        }

        // --- Helper Methods for ToggleGroups ---

        private void SetToggleGroupIndex(ToggleGroup group, int index)
        {
            if (group == null) return;
            var toggles = group.GetComponentsInChildren<Toggle>(true); // Get all toggles in order
            if (index >= 0 && index < toggles.Length)
            {
                toggles[index].isOn = true;
            }
        }

        private int GetToggleGroupIndex(ToggleGroup group, int fallback)
        {
            if (group == null) return fallback;
            var toggles = group.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i].isOn) return i;
            }
            return fallback;
        }
    }
}
