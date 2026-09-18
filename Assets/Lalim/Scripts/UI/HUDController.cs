using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// HUD controller that displays gameplay information:
    /// - SpiritSense cooldown indicator
    /// - Active challenge timer
    /// - Hint counter
    /// Automatically shows/hides elements based on game state.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("SpiritSense Indicator")]
        [SerializeField] private Image _spiritSenseCooldownFill;
        [SerializeField] private Button _spiritSenseButton;
        [SerializeField] private Image _spiritSenseIcon;

        [Header("Moon Tide Ability")]
        [SerializeField] private Button _moonTideButton;
        [SerializeField] private Image _moonTideCooldownFill;

        [Header("HUD Action Buttons")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _navigationButton;
        [SerializeField] private Button _codexButton;
        [SerializeField] private Button _inventoryButton;

        [Header("Modals")]
        [SerializeField] private MapModalUI _mapModal;
        [SerializeField] private CodexModalUI _codexModal;

        [Header("Challenge Timer")]
        [SerializeField] private GameObject _timerGroup;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private Image _timerFill;

        [Header("Hint Counter")]
        [SerializeField] private GameObject _hintGroup;
        [SerializeField] private TMP_Text _hintCountText;
        [SerializeField] private Button _hintButton;

        [Header("Interaction Prompt")]
        [SerializeField] private GameObject _interactionPromptGroup;
        [SerializeField] private TMP_Text _interactionPromptText;

        [Header("Colors")]
        [SerializeField] private Color _timerNormalColor = Color.white;
        [SerializeField] private Color _timerWarningColor = Color.yellow;
        [SerializeField] private Color _timerCriticalColor = Color.red;
        [SerializeField] private float _timerWarningThreshold = 15f;
        [SerializeField] private float _timerCriticalThreshold = 5f;

        // ─────────────────────────────────────────────
        public static HUDController Instance { get; private set; }

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isInChallenge;
        private float _challengeTimeLimit;
        private float _challengeTimeRemaining;
        private int _currentHintBudget;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Subscribe<SpiritSenseCooldownEvent>(OnSpiritSenseCooldown);
            EventBus.Subscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Subscribe<HintRequestedEvent>(OnHintRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<SpiritSenseCooldownEvent>(OnSpiritSenseCooldown);
            EventBus.Unsubscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Unsubscribe<HintRequestedEvent>(OnHintRequested);
        }

        private void Start()
        {
            // Wire up buttons
            if (_spiritSenseButton != null)
            {
                _spiritSenseButton.onClick.AddListener(OnSpiritSenseButtonPressed);
            }

            if (_moonTideButton != null)
            {
                _moonTideButton.onClick.AddListener(OnMoonTideButtonPressed);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.AddListener(OnSettingsButtonPressed);
            }

            if (_navigationButton != null)
            {
                _navigationButton.onClick.AddListener(OnNavigationButtonPressed);
            }

            if (_codexButton != null)
            {
                _codexButton.onClick.AddListener(OnCodexButtonPressed);
            }

            if (_inventoryButton != null)
            {
                _inventoryButton.onClick.AddListener(OnInventoryButtonPressed);
            }

            if (_hintButton != null)
            {
                _hintButton.onClick.AddListener(OnHintButtonPressed);
            }

            // Start with challenge-specific elements hidden
            SetChallengeUIVisible(false);
            SetInteractionPromptVisible(false);
        }

        private void Update()
        {
            if (_isInChallenge && _challengeTimeLimit > 0)
            {
                UpdateTimer();
            }

            UpdateMoonTideDisplay();
        }

        private void UpdateMoonTideDisplay()
        {
            if (MoonTideAbility.Instance != null)
            {
                bool onCooldown = MoonTideAbility.Instance.IsOnCooldown;
                if (_moonTideCooldownFill != null)
                {
                    _moonTideCooldownFill.fillAmount = 1f - MoonTideAbility.Instance.CooldownNormalized;
                }
                if (_moonTideButton != null)
                {
                    _moonTideButton.interactable = !onCooldown;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Timer
        // ─────────────────────────────────────────────

        private void UpdateTimer()
        {
            _challengeTimeRemaining -= Time.deltaTime;
            _challengeTimeRemaining = Mathf.Max(0, _challengeTimeRemaining);

            // Update timer text
            if (_timerText != null)
            {
                int minutes = Mathf.FloorToInt(_challengeTimeRemaining / 60f);
                int seconds = Mathf.FloorToInt(_challengeTimeRemaining % 60f);
                _timerText.text = $"{minutes:00}:{seconds:00}";
            }

            // Update fill bar
            if (_timerFill != null && _challengeTimeLimit > 0)
            {
                _timerFill.fillAmount = _challengeTimeRemaining / _challengeTimeLimit;
            }

            // Color based on urgency
            if (_timerText != null)
            {
                if (_challengeTimeRemaining <= _timerCriticalThreshold)
                {
                    _timerText.color = _timerCriticalColor;
                }
                else if (_challengeTimeRemaining <= _timerWarningThreshold)
                {
                    _timerText.color = _timerWarningColor;
                }
                else
                {
                    _timerText.color = _timerNormalColor;
                }
            }

            // Check timeout
            if (_challengeTimeRemaining <= 0)
            {
                EventBus.Publish(new ChallengeFailedEvent
                {
                    ChallengeId = "",
                    Type = ChallengeType.WasteSegregation,
                    Reason = "timeout"
                });
            }
        }

        // ─────────────────────────────────────────────
        // UI Visibility
        // ─────────────────────────────────────────────

        private void SetChallengeUIVisible(bool visible)
        {
            if (_timerGroup != null) _timerGroup.SetActive(visible);
            if (_hintGroup != null) _hintGroup.SetActive(visible);
        }

        /// <summary>
        /// Show an interaction prompt to the player.
        /// </summary>
        public void ShowInteractionPrompt(string text)
        {
            if (_interactionPromptGroup != null)
            {
                _interactionPromptGroup.SetActive(true);
            }
            if (_interactionPromptText != null)
            {
                _interactionPromptText.text = text;
            }
        }

        /// <summary>
        /// Hide the interaction prompt.
        /// </summary>
        public void SetInteractionPromptVisible(bool visible)
        {
            if (_interactionPromptGroup != null)
            {
                _interactionPromptGroup.SetActive(visible);
            }
        }

        private void UpdateHintDisplay()
        {
            if (_hintCountText != null)
            {
                _hintCountText.text = $"×{_currentHintBudget}";
            }

            if (_hintButton != null)
            {
                _hintButton.interactable = _currentHintBudget > 0;
            }
        }

        // ─────────────────────────────────────────────
        // Button Handlers
        // ─────────────────────────────────────────────

        private void OnSpiritSenseButtonPressed()
        {
            if (SpiritSense.Instance != null)
            {
                SpiritSense.Instance.TryActivateReveal();
            }
            else if (InputManager.Instance != null)
            {
                InputManager.Instance.TriggerSpiritSense();
            }
        }

        private void OnMoonTideButtonPressed()
        {
            if (MoonTideAbility.Instance != null)
            {
                MoonTideAbility.Instance.TriggerMoonTide();
            }
        }

        private void OnSettingsButtonPressed()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowPause();
            }
            else
            {
                // Fallback for direct scene testing without UIManager (Boot Sequence)
                PauseMenu pauseMenu = FindObjectOfType<PauseMenu>(true);
                if (pauseMenu != null)
                {
                    pauseMenu.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning("[HUDController] No UIManager or PauseMenu found in scene! Cannot open settings.");
                }
                EventBus.Publish(new PauseRequestedEvent { IsPaused = true });
            }
        }

        private void OnNavigationButtonPressed()
        {
            if (_mapModal != null)
            {
                _mapModal.ToggleModal();
            }
            else if (MapModalUI.Instance != null)
            {
                MapModalUI.Instance.ToggleModal();
            }
        }

        private void OnCodexButtonPressed()
        {
            if (_codexModal != null)
            {
                _codexModal.ToggleModal();
            }
            else if (CodexModalUI.Instance != null)
            {
                CodexModalUI.Instance.ToggleModal();
            }
        }

        private void OnInventoryButtonPressed()
        {
            if (InventoryUI.Instance != null)
            {
                InventoryUI.Instance.ToggleInventory();
            }
        }

        private void OnHintButtonPressed()
        {
            if (_currentHintBudget > 0)
            {
                _currentHintBudget--;
                UpdateHintDisplay();

                EventBus.Publish(new HintRequestedEvent
                {
                    RemainingHints = _currentHintBudget
                });
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            bool showHUD = evt.NewState == GameState.Exploring || evt.NewState == GameState.InChallenge;
            gameObject.SetActive(showHUD);
        }

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            _isInChallenge = true;
            _challengeTimeLimit = evt.TimeLimit;
            _challengeTimeRemaining = evt.TimeLimit;
            _currentHintBudget = evt.HintBudget;

            SetChallengeUIVisible(true);
            UpdateHintDisplay();

            if (_timerText != null)
            {
                _timerText.color = _timerNormalColor;
            }
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            _isInChallenge = false;
            SetChallengeUIVisible(false);
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            _isInChallenge = false;
            SetChallengeUIVisible(false);
        }

        private void OnSpiritSenseCooldown(SpiritSenseCooldownEvent evt)
        {
            if (_spiritSenseCooldownFill != null)
            {
                _spiritSenseCooldownFill.fillAmount = 1f - (evt.RemainingTime / evt.TotalCooldown);
            }

            if (_spiritSenseButton != null)
            {
                _spiritSenseButton.interactable = evt.RemainingTime <= 0;
            }
        }

        private void OnDifficultyChanged(DifficultyChangedEvent evt)
        {
            _currentHintBudget = evt.HintBudget;
            UpdateHintDisplay();
        }

        private void OnHintRequested(HintRequestedEvent evt)
        {
            _currentHintBudget = evt.RemainingHints;
            UpdateHintDisplay();
        }
    }
}
