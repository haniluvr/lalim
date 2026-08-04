using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Pause menu with Resume, Save, Settings, and Quit options.
    /// Sets Time.timeScale = 0 when paused.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitToMenuButton;

        [Header("Feedback")]
        [SerializeField] private TMP_Text _saveStatusText;
        [SerializeField] private float _saveStatusDisplayTime = 2f;

        private float _saveStatusTimer;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            SetupButtons();
            if (_saveStatusText != null) _saveStatusText.text = "";
        }

        private void Update()
        {
            // Auto-hide save status after delay
            if (_saveStatusTimer > 0)
            {
                _saveStatusTimer -= Time.unscaledDeltaTime;
                if (_saveStatusTimer <= 0 && _saveStatusText != null)
                {
                    _saveStatusText.text = "";
                }
            }

            // Listen for pause input (escape key / back button on Android)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnResumePressed();
            }
        }

        // ─────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────

        private void SetupButtons()
        {
            if (_resumeButton != null)
            {
                _resumeButton.onClick.RemoveAllListeners();
                _resumeButton.onClick.AddListener(OnResumePressed);
            }

            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveAllListeners();
                _saveButton.onClick.AddListener(OnSavePressed);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveAllListeners();
                _settingsButton.onClick.AddListener(OnSettingsPressed);
            }

            if (_quitToMenuButton != null)
            {
                _quitToMenuButton.onClick.RemoveAllListeners();
                _quitToMenuButton.onClick.AddListener(OnQuitToMenuPressed);
            }
        }

        // ─────────────────────────────────────────────
        // Button Actions
        // ─────────────────────────────────────────────

        private void OnResumePressed()
        {
            EventBus.Publish(new PauseRequestedEvent { IsPaused = false });
        }

        private void OnSavePressed()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame();

                if (_saveStatusText != null)
                {
                    _saveStatusText.text = "Game Saved!";
                    _saveStatusTimer = _saveStatusDisplayTime;
                }
            }
        }

        private void OnSettingsPressed()
        {
            // TODO: Open settings panel
            Debug.Log("[PauseMenu] Settings pressed — not yet implemented");
        }

        private void OnQuitToMenuPressed()
        {
            // Save before quitting
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SaveGame();
            }

            // Resume time and go to menu
            EventBus.Publish(new PauseRequestedEvent { IsPaused = false });

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMenu();
            }
        }
    }
}
