using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// HUD controller that displays gameplay information:
    /// - Active challenge timer
    /// - Hint counter
    /// Automatically shows/hides elements based on game state.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("HUD Root")]
        [Tooltip("CanvasGroup on this HUD root, used to hide/show instead of SetActive so event subscriptions in OnDisable never get lost.")]
        [SerializeField] private CanvasGroup _rootCanvasGroup;

        // Ability buttons, HP and stamina are driven by AbilityButtonUI / HealthBarUI / StaminaOrbsUI.

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
        [SerializeField] private Image _interactionPromptIcon;

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
            EventBus.Subscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Subscribe<HintRequestedEvent>(OnHintRequested);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
            EventBus.Unsubscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Unsubscribe<HintRequestedEvent>(OnHintRequested);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void Start()
        {
            // Wire up buttons
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
        }

        // ─────────────────────────────────────────────
        // Timer
        // ─────────────────────────────────────────────

        private void UpdateTimer()
        {
            // Fix: this used to keep its own countdown and publish a duplicate
            // ChallengeFailedEvent with a hardcoded ChallengeType.WasteSegregation.
            // ChallengeManager is the single source of truth for the timer and
            // for timeout failure; the HUD now only displays its state.
            if (Mechanics.ChallengeManager.Instance == null) return;

            _challengeTimeRemaining = Mechanics.ChallengeManager.Instance.TimeRemaining;

            if (_timerText != null)
            {
                int minutes = Mathf.FloorToInt(_challengeTimeRemaining / 60f);
                int seconds = Mathf.FloorToInt(_challengeTimeRemaining % 60f);
                _timerText.text = $"{minutes:00}:{seconds:00}";

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

            if (_timerFill != null && _challengeTimeLimit > 0)
            {
                _timerFill.fillAmount = _challengeTimeRemaining / _challengeTimeLimit;
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
            if (_interactionPromptIcon != null)
            {
                _interactionPromptIcon.gameObject.SetActive(true);
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

        private void OnSettingsButtonPressed()
        {
            if (PauseModalUI.Instance != null) PauseModalUI.Instance.Open();
            else EventBus.Publish(new PauseRequestedEvent { IsPaused = true });
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

        // The placeholder inventory window was removed; the button is kept for the real one.
        private void OnInventoryButtonPressed()
        {
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

        private bool _stateAllowsHud = true;
        private bool _dialogueOpen;
        private readonly System.Collections.Generic.HashSet<object> _suppressors = new System.Collections.Generic.HashSet<object>();

        /// <summary>Hide the game HUD while a full-screen minigame (scanner, coral site, ghost net) is up.</summary>
        public void SetSuppressed(object source, bool suppressed)
        {
            if (suppressed) _suppressors.Add(source); else _suppressors.Remove(source);
            ApplyHudVisibility();
        }

        // No game HUD while a dialogue is on screen.
        private void OnDialogueStarted(DialogueStartedEvent evt) { _dialogueOpen = true; ApplyHudVisibility(); }
        private void OnDialogueEnded(DialogueEndedEvent evt) { _dialogueOpen = false; ApplyHudVisibility(); }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _stateAllowsHud = evt.NewState == GameState.Exploring || evt.NewState == GameState.InChallenge;
            ApplyHudVisibility();
        }

        /// <summary>True while the game HUD is on screen (not in dialogue, menus or a full-screen minigame).</summary>
        public bool IsShowing => _stateAllowsHud && !_dialogueOpen && _suppressors.Count == 0;

        private void ApplyHudVisibility()
        {
            bool showHUD = IsShowing;

            // Fix: this component used to SetActive(false) on itself, which meant OnDisable()
            // unsubscribed it from GameStateChangedEvent — so it could never receive the event
            // that would turn it back on again. Use a CanvasGroup instead so the object (and its
            // subscriptions) stay alive at all times.
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = showHUD ? 1f : 0f;
                _rootCanvasGroup.interactable = showHUD;
                _rootCanvasGroup.blocksRaycasts = showHUD;
            }
            else
            {
                Debug.LogWarning("[HUDController] No CanvasGroup assigned — falling back to SetActive, " +
                                  "which will stop this component from receiving future show/hide events.");
                gameObject.SetActive(showHUD);
            }
        }

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            _isInChallenge = true;
            _challengeTimeLimit = evt.TimeLimit;
            _challengeTimeRemaining = evt.TimeLimit;
            _currentHintBudget = evt.HintBudget;

            SetChallengeUIVisible(true);
            UpdateHintDisplay();
            // First-person minigames lock movement: the swim controls only get in the way.
            SetSwimControlsVisible(evt.Perspective != ChallengePerspective.FirstPerson);

            if (_timerText != null)
            {
                _timerText.color = _timerNormalColor;
            }
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            _isInChallenge = false;
            SetChallengeUIVisible(false);
            SetSwimControlsVisible(true);
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            _isInChallenge = false;
            SetChallengeUIVisible(false);
            SetSwimControlsVisible(true);
        }

        // The joystick, skill buttons, dugong card and action button (found by name, so hand edits to the HUD are kept).
        private static readonly string[] SwimControls = { "Joystick_Movement", "BottomRight_Cluster", "Btn_SeagrassMend", "ActionButton" };
        private readonly System.Collections.Generic.List<GameObject> _hiddenControls = new System.Collections.Generic.List<GameObject>();

        private void SetSwimControlsVisible(bool visible)
        {
            if (visible)
            {
                foreach (GameObject go in _hiddenControls) if (go != null) go.SetActive(true);
                _hiddenControls.Clear();
                return;
            }
            foreach (string name in SwimControls)
            {
                Transform t = transform.Find(name);
                if (t == null || !t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(false);
                _hiddenControls.Add(t.gameObject);
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
