using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using System.Collections.Generic;

namespace Lalim.UI
{
    /// <summary>
    /// Dynamic Main Menu that changes based on whether save data exists.
    /// Handles multiple save slots.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Main Panel")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private Button _playButton;
        [SerializeField] private TMP_Text _playButtonText;
        [SerializeField] private float _playButtonYWhenHasSave = 0f;
        [SerializeField] private float _playButtonYWhenNoSave = -50f;
        [SerializeField] private Button _loadGameButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;
        [SerializeField] private Button _exitButton;

        [Header("Load Game Sub-Menu")]
        [SerializeField] private GameObject _loadGamePanel;
        [SerializeField] private Button _closeLoadGameButton;
        [SerializeField] private List<Button> _slotButtons;
        [SerializeField] private List<TMP_Text> _slotTexts;

        [Header("Credits Sub-Menu")]
        [SerializeField] private GameObject _creditsPanel;
        [SerializeField] private Button _closeCreditsButton;

        private int _pendingNewGameSlot = -1;

        private void Start()
        {
            // Bind main menu buttons
            if (_playButton != null) _playButton.onClick.AddListener(OnPlayClicked);
            if (_loadGameButton != null) _loadGameButton.onClick.AddListener(OnLoadGameMenuOpened);
            if (_exitButton != null) _exitButton.onClick.AddListener(OnExit);
            if (_closeLoadGameButton != null) _closeLoadGameButton.onClick.AddListener(OnLoadGameMenuClosed);

            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsMenuOpened);

            if (_creditsButton != null) _creditsButton.onClick.AddListener(OnCreditsMenuOpened);
            if (_closeCreditsButton != null) _closeCreditsButton.onClick.AddListener(OnCreditsMenuClosed);

            // Bind slot buttons
            for (int i = 0; i < _slotButtons.Count; i++)
            {
                int slotIndex = i; // local capture for lambda
                if (_slotButtons[i] != null)
                {
                    _slotButtons[i].onClick.AddListener(() => OnSlotSelected(slotIndex));
                }
            }

            // Set game state to Menu
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Menu);
            }

            SetupMenuState();
        }

        private void SetupMenuState()
        {
            // Guard: if buttons haven't been assigned in Inspector yet, skip gracefully
            if (_mainPanel == null)
            {
                Debug.LogWarning("[MainMenuUI] _mainPanel is not assigned in the Inspector. Skipping menu setup.");
                return;
            }

            _mainPanel.SetActive(true);
            if (_loadGamePanel != null) _loadGamePanel.SetActive(false);
            if (_creditsPanel != null) _creditsPanel.SetActive(false);

            bool hasAnySave = SaveManager.Instance != null && SaveManager.Instance.HasAnySave();

            // Toggle visibility, text, and position based on player state
            if (_playButton != null)
            {
                if (_playButtonText != null)
                {
                    _playButtonText.text = hasAnySave ? "CONTINUE" : "NEW GAME";
                }

                RectTransform playRect = _playButton.GetComponent<RectTransform>();
                if (playRect != null)
                {
                    float targetY = hasAnySave ? _playButtonYWhenHasSave : _playButtonYWhenNoSave;
                    playRect.anchoredPosition = new Vector2(playRect.anchoredPosition.x, targetY);
                }
            }

            if (_loadGameButton != null) _loadGameButton.gameObject.SetActive(hasAnySave);
        }

        private void OnPlayClicked()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.HasAnySave())
            {
                OnContinue();
            }
            else
            {
                OnNewGame(0); // Default to slot 0 for fresh new game
            }
        }

        private void OnNewGame(int slotIndex)
        {
            Debug.Log($"[MainMenu] New Game on Slot {slotIndex + 1} - Routing to Name Input Scene");
            
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.DeleteSave(slotIndex); // Ensure it's clean
                SaveManager.Instance.SetCurrentSlot(slotIndex);
                SaveManager.Instance.SaveGame(); // Create new base save
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(GameConstants.Scenes.NAME_INPUT);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scenes.NAME_INPUT);
            }
        }

        private void OnContinue()
        {
            Debug.Log("[MainMenu] Continue pressed");

            if (SaveManager.Instance != null)
            {
                // Simple logic: Just load the last used slot (CurrentSlot)
                // A robust system might check timestamps across slots.
                SaveManager.Instance.LoadGame();
            }

            StartGame();
        }

        private void OnLoadGameMenuOpened()
        {
            _mainPanel.SetActive(false);
            if (_loadGamePanel != null)
            {
                _loadGamePanel.SetActive(true);
                RefreshSlotsUI();
            }
        }

        private void OnLoadGameMenuClosed()
        {
            if (_loadGamePanel != null) _loadGamePanel.SetActive(false);
            _mainPanel.SetActive(true);
        }

        private void OnSettingsMenuOpened()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowSettings();
            }
        }

        private void OnCreditsMenuOpened()
        {
            _mainPanel.SetActive(false);
            if (_creditsPanel != null) _creditsPanel.SetActive(true);
        }

        private void OnCreditsMenuClosed()
        {
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
            _mainPanel.SetActive(true);
        }

        private void RefreshSlotsUI()
        {
            if (SaveManager.Instance == null) return;

            for (int i = 0; i < _slotButtons.Count; i++)
            {
                if (i >= SaveManager.MAX_SLOTS) break;

                bool slotHasSave = SaveManager.Instance.HasSave(i);
                if (_slotTexts.Count > i && _slotTexts[i] != null)
                {
                    _slotTexts[i].text = slotHasSave ? $"Save Slot {i + 1}\n<size=80%>(Load Data)</size>" : $"Save Slot {i + 1}\n<size=80%>(Create New Game)</size>";
                }
            }
        }

        private void OnSlotSelected(int slotIndex)
        {
            Debug.Log($"[MainMenu] Slot {slotIndex + 1} selected");
            
            if (SaveManager.Instance != null)
            {
                if (SaveManager.Instance.HasSave(slotIndex))
                {
                    // Load existing
                    SaveManager.Instance.SetCurrentSlot(slotIndex);
                    SaveManager.Instance.LoadGame();
                    StartGame();
                }
                else
                {
                    // Create new
                    OnNewGame(slotIndex);
                }
            }
        }

        private void StartGame()
        {
            // Transition to Luzon or Region Select
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowRegionSelect();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.TryEnterRegion(Region.Luzon);
            }
            else
            {
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.LoadScene(GameConstants.Scenes.LUZON);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scenes.LUZON);
                }
            }
        }

        private void OnExit()
        {
            Debug.Log("[MainMenu] Exit pressed");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
