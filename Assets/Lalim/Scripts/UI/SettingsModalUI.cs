using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>Where the settings modal was opened from — a few options only make sense in one place.</summary>
    public enum SettingsContext { MainMenu, InGame }

    /// <summary>
    /// Settings modal shared by the main menu and the pause menu (one prefab, SettingsModal.prefab).
    ///
    ///  Audio     Music / Voice / SFX / Amb. (slider + switch), 3D Spatial Audio, Headphone Optimization
    ///  Quality   Smooth · Medium · High · Ultra · Customize; Texture / Mapping / Shadow / Ambient Effect /
    ///            SFX / Resolution (C·B·A), SSS (OFF·ON), Frame Rate (30·60)
    ///  Controls  Gyroscope, Right Handed Mode, Auto-Swim/Auto-Forward, Haptic Feedback,
    ///            Camera Speed, Invert Camera
    ///  System    User Agreement, Privacy Policy, App Version; Manage Resource, Quick Cleanup,
    ///            Performance Fix, Reset Settings, Erase Save Data (main menu only)
    ///
    /// Every change is previewed live but only kept when Save is pressed. Close / Back with unsaved
    /// changes asks Save / Discard / Keep Editing.
    /// </summary>
    public class SettingsModalUI : MonoBehaviour
    {
        [Serializable]
        public class VolumeRow
        {
            public AudioChannel channel;
            public Slider slider;
            public TMP_Text value;
            public PillSwitch enabled; // on = audible, off = muted (keeps its level)
        }

        [Serializable]
        public class TierRow
        {
            public QualityRow row;
            public Button[] tiers = new Button[3]; // C, B, A
        }

        [Header("Tabs (Audio, Quality, Controls, System)")]
        [SerializeField] private Button[] _tabButtons = new Button[4];
        [SerializeField] private GameObject[] _pages = new GameObject[4];
        [SerializeField] private ScrollRect[] _pageScrolls = new ScrollRect[4];

        [Header("Audio")]
        [SerializeField] private VolumeRow[] _volumes = new VolumeRow[4];
        [SerializeField] private PillSwitch _spatialAudio;
        [SerializeField] private PillSwitch _headphones;

        [Header("Quality")]
        [SerializeField] private Button[] _presetButtons = new Button[5];
        [SerializeField] private TierRow[] _tierRows = new TierRow[6];
        [SerializeField] private Button[] _sssButtons = new Button[2];       // OFF, ON
        [SerializeField] private Button[] _frameRateButtons = new Button[2]; // 30, 60

        [Header("Controls")]
        [SerializeField] private PillSwitch _gyroscope;
        [SerializeField] private PillSwitch _rightHanded;
        [SerializeField] private PillSwitch _autoSwim;
        [SerializeField] private PillSwitch _haptics;
        [SerializeField] private Slider _sensitivity;
        [SerializeField] private TMP_Text _sensitivityValue;
        [SerializeField] private PillSwitch _invertY;

        [Header("System")]
        [SerializeField] private Button _userAgreementButton;
        [SerializeField] private Button _privacyPolicyButton;
        [SerializeField] private TMP_Text _versionText;
        [SerializeField] private Button _manageResourceButton;
        [SerializeField] private Button _quickCleanupButton;
        [SerializeField] private Button _performanceFixButton;
        [SerializeField] private Button _resetDefaultsButton;
        [SerializeField] private Button _eraseSavesButton;
        [SerializeField] private TMP_Text _eraseNote;
        [SerializeField] private TextAsset _userAgreementText;
        [SerializeField] private TextAsset _privacyPolicyText;

        [Header("Footer")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Confirm")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmTitle;
        [SerializeField] private TMP_Text _confirmMessage;
        [SerializeField] private Button[] _confirmButtons = new Button[3];
        [SerializeField] private TMP_Text[] _confirmLabels = new TMP_Text[3];

        [Header("Info (documents, storage)")]
        [SerializeField] private GameObject _infoPanel;
        [SerializeField] private TMP_Text _infoTitle;
        [SerializeField] private TMP_Text _infoBody;
        [SerializeField] private ScrollRect _infoScroll;
        [SerializeField] private Button _infoActionButton;
        [SerializeField] private TMP_Text _infoActionLabel;
        [SerializeField] private Button _infoCloseButton;

        [Header("Look")]
        [SerializeField] private Sprite _onSprite;
        [SerializeField] private Sprite _offSprite;
        [SerializeField] private Sprite _warningSprite;
        [SerializeField] private Color _tabOnText = Color.white;
        [SerializeField] private Color _tabOffText = new Color(0.8f, 0.93f, 0.96f, 0.6f);
        [SerializeField] private Color _choiceSelected = new Color(0.3f, 0.6f, 0.64f, 1f);
        [SerializeField] private float _statusSeconds = 2.4f;

        /// <summary>Raised after save slots are erased (main menu refreshes its Play / Load buttons).</summary>
        public event Action SaveDataErased;

        public bool IsOpen => gameObject.activeSelf;
        public SettingsContext Context { get; private set; }

        private enum Choice { Primary, Danger, Cancel }

        private GameSettings _edit;
        private Action _onClosed;
        private Action _infoAction;
        private int _tab;
        private bool _refreshing;
        private bool _snapSwitches; // opening: switches jump to their state instead of sliding
        private float _statusTimer;
        private readonly Action[] _confirmActions = new Action[3];

        private static SettingsManager Manager => SettingsManager.Ensure();

        private void Awake()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int tab = i;
                Bind(_tabButtons[i], () => ShowTab(tab));
            }

            // Audio
            foreach (VolumeRow row in _volumes)
            {
                if (row == null) continue;
                VolumeRow r = row;
                SetupSlider(r.slider, 0, GameSettings.VolumeSteps, v => Edit(s => s.SetVolume(r.channel, v)));
                BindSwitch(r.enabled, on => Edit(s => s.SetMuted(r.channel, !on)));
            }
            BindSwitch(_spatialAudio, on => Edit(s => s.spatialAudio = on));
            BindSwitch(_headphones, on => Edit(s => s.headphoneOptimization = on));

            // Quality
            for (int i = 0; i < _presetButtons.Length; i++)
            {
                int preset = i;
                Bind(_presetButtons[i], () => Edit(s => s.ApplyPreset(preset)));
            }
            foreach (TierRow row in _tierRows)
            {
                if (row == null) continue;
                for (int t = 0; t < row.tiers.Length; t++)
                {
                    QualityRow q = row.row;
                    int tier = t;
                    Bind(row.tiers[t], () => Edit(s => s.SetTierCustom(q, tier)));
                }
            }
            if (_sssButtons.Length > 1)
            {
                Bind(_sssButtons[0], () => Edit(s => { s.sssQuality = false; s.graphicsPreset = GameSettings.CustomizePreset; }));
                Bind(_sssButtons[1], () => Edit(s => { s.sssQuality = true; s.graphicsPreset = GameSettings.CustomizePreset; }));
            }
            if (_frameRateButtons.Length > 1)
            {
                Bind(_frameRateButtons[0], () => Edit(s => s.highFrameRate = false));
                Bind(_frameRateButtons[1], () => Edit(s => s.highFrameRate = true));
            }

            // Controls
            BindSwitch(_gyroscope, on =>
            {
                Edit(s => s.gyroscope = on);
                if (on && !SystemInfo.supportsGyroscope) ShowStatus("This device has no gyroscope.");
            });
            BindSwitch(_rightHanded, on => Edit(s => s.rightHandedMode = on));
            BindSwitch(_autoSwim, on => Edit(s => s.autoSwim = on));
            BindSwitch(_haptics, on =>
            {
                Edit(s => s.haptics = on);
                if (on) SettingsManager.Pulse(); // feel what you just turned on
            });
            SetupSlider(_sensitivity, 1, GameSettings.SensitivitySteps, v => Edit(s => s.cameraSensitivity = v));
            BindSwitch(_invertY, on => Edit(s => s.invertCameraY = on));

            // System
            Bind(_userAgreementButton, () => ShowDocument("User Agreement", _userAgreementText));
            Bind(_privacyPolicyButton, () => ShowDocument("Privacy Policy", _privacyPolicyText));
            Bind(_manageResourceButton, ShowManageResource);
            Bind(_quickCleanupButton, OnQuickCleanup);
            Bind(_performanceFixButton, OnPerformanceFix);
            Bind(_resetDefaultsButton, OnResetDefaults);
            Bind(_eraseSavesButton, OnEraseSaves);

            // Footer / dialogs
            Bind(_saveButton, SaveAndClose);
            Bind(_closeButton, RequestClose);
            Bind(_infoCloseButton, HideInfo);
            Bind(_infoActionButton, () => _infoAction?.Invoke());
            for (int i = 0; i < _confirmButtons.Length; i++)
            {
                int index = i;
                Bind(_confirmButtons[i], () =>
                {
                    Action action = _confirmActions[index];
                    HideConfirm();
                    action?.Invoke();
                });
            }

            if (_versionText != null) _versionText.text = "v" + Application.version;
        }

        private void Update()
        {
            if (_statusTimer <= 0f) return;
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f && _statusText != null) _statusText.text = "";
        }

        // ─────────────────────────────────────────────
        // Open / close
        // ─────────────────────────────────────────────

        /// <summary>Pause-menu entry point (kept for existing callers).</summary>
        public void Open(Action onClosed) => Open(SettingsContext.InGame, onClosed);

        public void Open(SettingsContext context, Action onClosed)
        {
            Context = context;
            _onClosed = onClosed;

            SettingsManager sm = Manager;
            sm.Revert();                 // drop any stale preview
            _edit = sm.Saved.Clone();

            bool inGame = context == SettingsContext.InGame;
            if (_eraseSavesButton != null) _eraseSavesButton.interactable = !inGame;
            if (_eraseNote != null)
                _eraseNote.text = inGame ? "Only available from the main menu" : "Delete every save slot for good";

            if (_statusText != null) _statusText.text = "";
            HideConfirm();
            HideInfo();
            gameObject.SetActive(true);
            ShowTab(0);
            _snapSwitches = true;
            Refresh();
            _snapSwitches = false;
        }

        /// <summary>Escape / Android back: leave a dialog first, then close the modal.</summary>
        public void Back()
        {
            if (_confirmPanel != null && _confirmPanel.activeSelf) { HideConfirm(); return; }
            if (_infoPanel != null && _infoPanel.activeSelf) { HideInfo(); return; }
            RequestClose();
        }

        /// <summary>Close, asking first if there are unsaved changes.</summary>
        public void RequestClose()
        {
            if (!IsDirty) { Finish(); return; }
            Confirm("Unsaved changes",
                    "Save your new settings before closing?",
                    ("Save", Choice.Primary, SaveAndClose),
                    ("Discard", Choice.Danger, DiscardAndClose),
                    ("Keep Editing", Choice.Cancel, null));
        }

        /// <summary>Hide immediately, dropping unsaved changes (e.g. the pause menu closed underneath).</summary>
        public void ForceClose()
        {
            if (!IsOpen) return;
            Manager.Revert();
            HideConfirm();
            HideInfo();
            _onClosed = null;
            gameObject.SetActive(false);
        }

        /// <summary>Kept for older callers: same as pressing Close.</summary>
        public void Close() => RequestClose();

        private bool IsDirty => _edit != null && !_edit.SameAs(Manager.Saved);

        private void SaveAndClose()
        {
            Manager.Save(_edit);
            Finish();
        }

        private void DiscardAndClose()
        {
            Manager.Revert();
            Finish();
        }

        private void Finish()
        {
            HideConfirm();
            HideInfo();
            gameObject.SetActive(false);
            Action closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        }

        // ─────────────────────────────────────────────
        // Editing
        // ─────────────────────────────────────────────

        private void Edit(Action<GameSettings> change)
        {
            if (_refreshing || _edit == null) return;
            change(_edit);
            _edit.Sanitize();
            Manager.Preview(_edit);
            Refresh();
        }

        // ─────────────────────────────────────────────
        // System actions
        // ─────────────────────────────────────────────

        private void ShowDocument(string title, TextAsset text)
        {
            ShowInfo(title, text != null ? text.text : "Not available yet.", null, null);
        }

        private void ShowManageResource()
        {
            ShowInfo("Manage Resource", DescribeStorage(), "Clear Cache", () =>
            {
                long freed = StorageTools.ClearCache();
                if (_infoBody != null) _infoBody.text = DescribeStorage();
                ShowStatus(freed > 0 ? $"Cleared {StorageTools.FormatBytes(freed)} of cache." : "The cache is already empty.");
            });
        }

        private static string DescribeStorage()
        {
            int slots = StorageTools.SaveSlotsUsed();
            return "All of Lalim's content is built into the app, so there is nothing to download or delete.\n\n" +
                   $"<b>Save data</b>\n{slots} of {SaveManager.MAX_SLOTS} slots used · {StorageTools.FormatBytes(StorageTools.SaveDataBytes())}\n" +
                   "Your saves are never touched by cleanup.\n\n" +
                   $"<b>Cached files</b>\n{StorageTools.FormatBytes(StorageTools.CacheBytes())}\n" +
                   "Temporary files the game can rebuild. Safe to clear.";
        }

        private void OnQuickCleanup()
        {
            long freed = StorageTools.QuickCleanup();
            ShowStatus(freed > 0 ? $"Cleanup done — freed {StorageTools.FormatBytes(freed)}." : "Cleanup done — memory tidied up.");
        }

        private void OnPerformanceFix()
        {
            Confirm("Performance Fix",
                    "Switch to Smooth graphics at 30 FPS and free unused memory?\nThis saves your settings.",
                    ("Apply", Choice.Primary, () =>
                    {
                        _edit.ApplyPreset(0);
                        _edit.highFrameRate = false;
                        Manager.Save(_edit);
                        StorageTools.QuickCleanup();
                        Refresh();
                        ShowStatus("Performance Fix applied.");
                    }),
                    ("Cancel", Choice.Cancel, null));
        }

        private void OnResetDefaults()
        {
            Confirm("Reset settings",
                    "Put every setting back to its default?\nYou can still Discard before saving.",
                    ("Reset", Choice.Danger, () =>
                    {
                        _edit = new GameSettings();
                        Manager.Preview(_edit);
                        Refresh();
                        ShowStatus("Defaults restored — press Save to keep them.");
                    }),
                    ("Cancel", Choice.Cancel, null));
        }

        private void OnEraseSaves()
        {
            if (Context == SettingsContext.InGame) return;
            SaveManager save = SaveManager.Instance;
            if (save == null || !save.HasAnySave()) { ShowStatus("There's no save data to erase."); return; }

            Confirm("Erase save data",
                    "Delete ALL save slots?\nYour progress will be lost for good.",
                    ("Erase Everything", Choice.Danger, () =>
                    {
                        for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
                            if (save.HasSave(i)) save.DeleteSave(i);
                        ShowStatus("All save data erased.");
                        SaveDataErased?.Invoke();
                    }),
                    ("Cancel", Choice.Cancel, null));
        }

        // ─────────────────────────────────────────────
        // View
        // ─────────────────────────────────────────────

        private void ShowTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, _pages.Length - 1);
            for (int i = 0; i < _pages.Length; i++)
                if (_pages[i] != null) _pages[i].SetActive(i == _tab);
            if (_tab < _pageScrolls.Length && _pageScrolls[_tab] != null) _pageScrolls[_tab].verticalNormalizedPosition = 1f;
            for (int i = 0; i < _tabButtons.Length; i++)
                StyleTab(_tabButtons[i], i == _tab);
        }

        private void Refresh()
        {
            if (_edit == null) return;
            _refreshing = true;
            GameSettings s = _edit;

            foreach (VolumeRow row in _volumes)
            {
                if (row == null) continue;
                int v = s.GetVolume(row.channel);
                if (row.slider != null) row.slider.SetValueWithoutNotify(v);
                if (row.value != null) row.value.text = v.ToString();
                SetSwitch(row.enabled, !s.IsMuted(row.channel));
            }
            SetSwitch(_spatialAudio, s.spatialAudio);
            SetSwitch(_headphones, s.headphoneOptimization);

            for (int i = 0; i < _presetButtons.Length; i++) StyleChoice(_presetButtons[i], i == s.graphicsPreset);
            foreach (TierRow row in _tierRows)
            {
                if (row == null) continue;
                int tier = s.GetTier(row.row);
                for (int t = 0; t < row.tiers.Length; t++) StyleChoice(row.tiers[t], t == tier);
            }
            if (_sssButtons.Length > 1) { StyleChoice(_sssButtons[0], !s.sssQuality); StyleChoice(_sssButtons[1], s.sssQuality); }
            if (_frameRateButtons.Length > 1) { StyleChoice(_frameRateButtons[0], !s.highFrameRate); StyleChoice(_frameRateButtons[1], s.highFrameRate); }

            SetSwitch(_gyroscope, s.gyroscope);
            SetSwitch(_rightHanded, s.rightHandedMode);
            SetSwitch(_autoSwim, s.autoSwim);
            SetSwitch(_haptics, s.haptics);
            if (_sensitivity != null) _sensitivity.SetValueWithoutNotify(s.cameraSensitivity);
            if (_sensitivityValue != null) _sensitivityValue.text = s.cameraSensitivity.ToString();
            SetSwitch(_invertY, s.invertCameraY);

            if (_saveButton != null) _saveButton.interactable = IsDirty;
            _refreshing = false;
        }

        private void SetSwitch(PillSwitch sw, bool on)
        {
            if (sw != null) sw.SetOn(on, !_snapSwitches);
        }

        /// <summary>Tabs: bright pill when selected, darker pill otherwise.</summary>
        private void StyleTab(Button button, bool selected)
        {
            if (button == null) return;
            if (button.targetGraphic is Image img && _onSprite != null && _offSprite != null)
                img.sprite = selected ? _onSprite : _offSprite;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.color = selected ? _tabOnText : _tabOffText;
        }

        /// <summary>Choices (presets, C/B/A, OFF/ON): plain text, with a pill behind the selected one.</summary>
        private void StyleChoice(Button button, bool selected)
        {
            if (button == null) return;
            if (button.targetGraphic is Image img) img.color = selected ? _choiceSelected : new Color(1f, 1f, 1f, 0f);
        }

        private void ShowStatus(string message)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusTimer = _statusSeconds;
        }

        // ─────────────────────────────────────────────
        // Info panel (User Agreement, Privacy Policy, Manage Resource)
        // ─────────────────────────────────────────────

        private void ShowInfo(string title, string body, string actionLabel, Action action)
        {
            if (_infoPanel == null) return;
            if (_infoTitle != null) _infoTitle.text = title;
            if (_infoBody != null) _infoBody.text = body;
            _infoAction = action;
            if (_infoActionButton != null) _infoActionButton.gameObject.SetActive(action != null);
            if (_infoActionLabel != null && actionLabel != null) _infoActionLabel.text = actionLabel;
            if (_infoCloseButton != null && _infoCloseButton.transform is RectTransform closeRt)
                closeRt.anchoredPosition = new Vector2(action != null ? 170f : 0f, closeRt.anchoredPosition.y);
            _infoPanel.SetActive(true);
            _infoPanel.transform.SetAsLastSibling();
            if (_infoScroll != null) _infoScroll.verticalNormalizedPosition = 1f;
        }

        private void HideInfo()
        {
            if (_infoPanel != null) _infoPanel.SetActive(false);
            _infoAction = null;
        }

        // ─────────────────────────────────────────────
        // Confirm (up to 3 choices; a null action just closes the dialog)
        // ─────────────────────────────────────────────

        private void Confirm(string title, string message, params (string label, Choice kind, Action action)[] choices)
        {
            if (_confirmPanel == null)
            {
                // No dialog built: fall back to the first choice (Save / Reset / Erase).
                if (choices.Length > 0) choices[0].action?.Invoke();
                return;
            }

            if (_confirmTitle != null) _confirmTitle.text = title;
            if (_confirmMessage != null) _confirmMessage.text = message;
            for (int i = 0; i < _confirmButtons.Length; i++)
            {
                bool used = i < choices.Length;
                _confirmActions[i] = used ? choices[i].action : null;
                if (_confirmButtons[i] != null) _confirmButtons[i].gameObject.SetActive(used);
                if (!used) continue;
                if (_confirmLabels.Length > i && _confirmLabels[i] != null) _confirmLabels[i].text = choices[i].label;
                if (_confirmButtons[i] != null && _confirmButtons[i].targetGraphic is Image img)
                {
                    Sprite sprite = choices[i].kind == Choice.Danger ? _warningSprite
                                  : choices[i].kind == Choice.Cancel ? _offSprite : _onSprite;
                    if (sprite != null) img.sprite = sprite;
                }
            }
            _confirmPanel.SetActive(true);
            _confirmPanel.transform.SetAsLastSibling();
        }

        private void HideConfirm()
        {
            if (_confirmPanel != null) _confirmPanel.SetActive(false);
            for (int i = 0; i < _confirmActions.Length; i++) _confirmActions[i] = null;
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private void BindSwitch(PillSwitch sw, Action<bool> onToggled)
        {
            if (sw != null) sw.Toggled += on => { if (!_refreshing) onToggled(on); };
        }

        private static void SetupSlider(Slider slider, int min, int max, Action<int> onChanged)
        {
            if (slider == null) return;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(v => onChanged(Mathf.RoundToInt(v)));
        }
    }
}
