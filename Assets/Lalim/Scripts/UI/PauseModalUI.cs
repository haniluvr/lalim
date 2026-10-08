using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Abilities;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// In-game pause modal: Resume, Save, Exit (back to the main menu), and a settings icon (top-right).
    ///
    /// Save:
    ///  - Loaded game (or already saved once this session) → saves straight to its slot.
    ///  - New game that was never saved → opens the slot picker; overwriting an occupied slot asks first.
    ///  - Not allowed mid-challenge or while dead/respawning → short message instead.
    /// Exit (labelled "Exit" in the prefab; returns to the main menu):
    ///  - Nothing unsaved → goes straight back.
    ///  - Unsaved progress → "Save & Exit / Exit Without Saving / Cancel".
    ///
    /// Opened by the HUD pause button, Escape / Android back (InputManager.TriggerPause), or any
    /// PauseRequestedEvent. Escape inside a sub-panel goes back one step instead of resuming.
    /// Lives in its own canvas that ignores the HUD's CanvasGroup (the HUD hides while paused).
    /// </summary>
    public class PauseModalUI : MonoBehaviour
    {
        public static PauseModalUI Instance { get; private set; }

        [Header("Root")]
        [SerializeField] private CanvasGroup _root;
        [SerializeField] private Button _hudPauseButton;

        [Header("Pause panel")]
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TMP_Text _statusText;

        [Header("Save slots")]
        [SerializeField] private GameObject _slotsPanel;
        [SerializeField] private Button[] _slotButtons = new Button[3];
        [SerializeField] private TMP_Text[] _slotLabels = new TMP_Text[3];
        [SerializeField] private Button _slotsBackButton;

        [Header("Confirm dialog")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmTitle;
        [SerializeField] private TMP_Text _confirmMessage;
        [SerializeField] private Button[] _confirmButtons = new Button[3];
        [SerializeField] private TMP_Text[] _confirmLabels = new TMP_Text[3];

        [Header("Settings")]
        [SerializeField] private SettingsModalUI _settings;

        [Header("Toast")]
        [SerializeField] private float _statusSeconds = 2.2f;

        public bool IsOpen { get; private set; }

        private Action _afterSlotSave;
        private Action[] _confirmActions = new Action[3];
        private GameObject _panelBeforeConfirm;
        private float _statusTimer;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;

            Bind(_hudPauseButton, Open);
            Bind(_resumeButton, Resume);
            Bind(_saveButton, OnSavePressed);
            Bind(_mainMenuButton, OnMainMenuPressed);
            Bind(_settingsButton, OpenSettings);
            Bind(_slotsBackButton, () => ShowOnly(_pausePanel));

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int slot = i;
                Bind(_slotButtons[i], () => OnSlotChosen(slot));
            }
            for (int i = 0; i < _confirmButtons.Length; i++)
            {
                int index = i;
                Bind(_confirmButtons[i], () => OnConfirmChosen(index));
            }

            HideAll();
        }

        private void OnDestroy()
        {
            if (IsOpen) PauseClock.SetPaused(false);
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        private void OnDisable() => EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

        private void Update()
        {
            if (_statusTimer <= 0f || _statusText == null) return;
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer <= 0f) _statusText.text = "";
        }

        private static void Bind(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
        }

        // ─────────────────────────────────────────────
        // Open / close
        // ─────────────────────────────────────────────

        public void Open()
        {
            if (IsOpen) return;
            if (GameManager.Instance != null) EventBus.Publish(new PauseRequestedEvent { IsPaused = true });
            else Time.timeScale = 0f; // scene started directly, no GameManager
            ShowPausePanel();
        }

        public void Resume()
        {
            HideAll();
            if (GameManager.Instance != null) EventBus.Publish(new PauseRequestedEvent { IsPaused = false });
            else Time.timeScale = 1f;
        }

        /// <summary>Escape / Android back: step back out of sub-panels, otherwise toggle pause.</summary>
        public void OnPauseKey()
        {
            if (!IsOpen) { Open(); return; }
            if (_confirmPanel != null && _confirmPanel.activeSelf) { ShowOnly(_panelBeforeConfirm != null ? _panelBeforeConfirm : _pausePanel); return; }
            if (_settings != null && _settings.IsOpen) { _settings.Back(); return; }
            if (_slotsPanel != null && _slotsPanel.activeSelf) { ShowOnly(_pausePanel); return; }
            Resume();
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState == GameState.Paused && !IsOpen) ShowPausePanel();
            else if (evt.PreviousState == GameState.Paused && evt.NewState != GameState.Paused && IsOpen) HideAll();
        }

        private void ShowPausePanel()
        {
            IsOpen = true;
            PauseClock.SetPaused(true);   // the HUD freezes behind the menu too
            SetRootVisible(true);
            if (_statusText != null) _statusText.text = "";
            ShowOnly(_pausePanel);
        }

        private void HideAll()
        {
            IsOpen = false;
            PauseClock.SetPaused(false);
            SetActive(_pausePanel, false);
            SetActive(_slotsPanel, false);
            SetActive(_confirmPanel, false);
            if (_settings != null) _settings.ForceClose();
            SetRootVisible(false);
        }

        private void SetRootVisible(bool visible)
        {
            if (_root == null) return;
            _root.alpha = visible ? 1f : 0f;
            _root.interactable = visible;
            _root.blocksRaycasts = visible;
        }

        private void ShowOnly(GameObject panel)
        {
            SetActive(_pausePanel, panel == _pausePanel);
            SetActive(_slotsPanel, panel == _slotsPanel);
            SetActive(_confirmPanel, panel == _confirmPanel);
            if (_settings != null && panel != _settings.gameObject) _settings.gameObject.SetActive(false);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private void ShowStatus(string message)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusTimer = _statusSeconds;
        }

        // ─────────────────────────────────────────────
        // Settings
        // ─────────────────────────────────────────────

        private void OpenSettings()
        {
            if (_settings == null) return;
            ShowOnly(null);
            _settings.Open(SettingsContext.InGame, () => ShowOnly(_pausePanel));
        }

        // ─────────────────────────────────────────────
        // Save
        // ─────────────────────────────────────────────

        /// <summary>Why saving isn't possible right now, or null if it is.</summary>
        private static string SaveBlockedReason()
        {
            if (SaveManager.Instance == null) return "Saving isn't available here.";
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) return "You can't save during a challenge.";
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return "You can't save right now.";
            return null;
        }

        private void OnSavePressed()
        {
            string blocked = SaveBlockedReason();
            if (blocked != null) { ShowStatus(blocked); return; }

            SaveManager save = SaveManager.Instance;
            if (save.IsSlotBound)
            {
                // Loaded game / already saved once: straight to its slot.
                SaveInto(save.CurrentSlot);
                return;
            }

            // New game that has never been saved: let the player choose a slot.
            OpenSlotPicker(null);
        }

        private void OpenSlotPicker(Action afterSave)
        {
            _afterSlotSave = afterSave;
            RefreshSlotLabels();
            ShowOnly(_slotsPanel);
        }

        private void RefreshSlotLabels()
        {
            SaveManager save = SaveManager.Instance;
            for (int i = 0; i < _slotLabels.Length; i++)
            {
                if (_slotLabels[i] == null) continue;
                SaveData data = save != null ? save.PeekSlot(i) : null;
                string title = $"Slot {i + 1}";
                if (save != null && save.IsSlotBound && save.CurrentSlot == i) title += "  (current)";
                _slotLabels[i].text = $"{title}\n<size=70%>{DescribeSlot(data)}</size>";
            }
        }

        private static string DescribeSlot(SaveData data)
        {
            if (data == null) return "Empty";

            string region = data.lastRegion >= 0 && Enum.IsDefined(typeof(Region), data.lastRegion)
                ? ((Region)data.lastRegion).ToString()
                : "Start";
            int zones = data.completedZoneIds != null ? data.completedZoneIds.Count : 0;
            string when = "";
            if (DateTime.TryParse(data.lastSaveTimestamp, null, DateTimeStyles.RoundtripKind, out DateTime utc))
            {
                when = " · " + utc.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);
            }
            string name = string.IsNullOrEmpty(data.playerName) ? "Player" : data.playerName;
            return $"{name} · {region} · {zones} zone{(zones == 1 ? "" : "s")}{when}";
        }

        private void OnSlotChosen(int slot)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null) return;

            bool isOwnSlot = save.IsSlotBound && save.CurrentSlot == slot;
            if (save.HasSave(slot) && !isOwnSlot)
            {
                Confirm($"Overwrite Slot {slot + 1}?",
                        $"This replaces:\n<size=85%>{DescribeSlot(save.PeekSlot(slot))}</size>",
                        ("Overwrite", () => SaveInto(slot)),
                        ("Cancel", () => ShowOnly(_slotsPanel)));
                return;
            }

            SaveInto(slot);
        }

        private void SaveInto(int slot)
        {
            bool ok = SaveManager.Instance != null && SaveManager.Instance.SaveToSlot(slot);
            ShowOnly(_pausePanel);
            ShowStatus(ok ? $"Saved to Slot {slot + 1}" : "Save failed. Please try again.");

            Action after = _afterSlotSave;
            _afterSlotSave = null;
            if (ok) after?.Invoke();
        }

        // ─────────────────────────────────────────────
        // Main menu
        // ─────────────────────────────────────────────

        private void OnMainMenuPressed()
        {
            SaveManager save = SaveManager.Instance;
            if (save == null || !save.HasUnsavedProgress)
            {
                GoToMainMenu();
                return;
            }

            string blocked = SaveBlockedReason();
            if (blocked != null)
            {
                // Can't save right now (e.g. mid-challenge): only leaving without saving is possible.
                Confirm("Unsaved progress",
                        $"{blocked}\nLeave without saving?",
                        ("Exit Without Saving", GoToMainMenu),
                        ("Cancel", () => ShowOnly(_pausePanel)));
                return;
            }

            Confirm("Unsaved progress",
                    "You have progress that isn't saved.\nSave before returning to the main menu?",
                    ("Save & Exit", SaveThenExit),
                    ("Exit Without Saving", GoToMainMenu),
                    ("Cancel", () => ShowOnly(_pausePanel)));
        }

        private void SaveThenExit()
        {
            SaveManager save = SaveManager.Instance;
            if (save.IsSlotBound)
            {
                if (save.SaveToSlot(save.CurrentSlot)) GoToMainMenu();
                else { ShowOnly(_pausePanel); ShowStatus("Save failed. Please try again."); }
                return;
            }
            OpenSlotPicker(GoToMainMenu);
        }

        private void GoToMainMenu()
        {
            HideAll();
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.ReturnToMenu();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scenes.MAIN_MENU);
        }

        // ─────────────────────────────────────────────
        // Confirm dialog (up to 3 choices; null label = hidden)
        // ─────────────────────────────────────────────

        private void Confirm(string title, string message, params (string label, Action action)[] choices)
        {
            _panelBeforeConfirm = _slotsPanel != null && _slotsPanel.activeSelf ? _slotsPanel : _pausePanel;
            if (_confirmTitle != null) _confirmTitle.text = title;
            if (_confirmMessage != null) _confirmMessage.text = message;

            for (int i = 0; i < _confirmButtons.Length; i++)
            {
                bool used = i < choices.Length && !string.IsNullOrEmpty(choices[i].label);
                if (_confirmButtons[i] != null) _confirmButtons[i].gameObject.SetActive(used);
                if (used && _confirmLabels[i] != null) _confirmLabels[i].text = choices[i].label;
                _confirmActions[i] = used ? choices[i].action : null;
            }
            ShowOnly(_confirmPanel);
        }

        private void OnConfirmChosen(int index)
        {
            Action action = index < _confirmActions.Length ? _confirmActions[index] : null;
            action?.Invoke();
        }
    }
}
