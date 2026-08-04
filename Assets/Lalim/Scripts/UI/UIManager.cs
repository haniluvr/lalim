using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// UI panel stack manager. Handles push/pop navigation between UI screens.
    /// Manages: HUD, PauseMenu, InventoryPanel, RegionSelect, LoadingScreen.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Panel References")]
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _loadingPanel;
        [SerializeField] private GameObject _regionSelectPanel;
        [SerializeField] private GameObject _inventoryPanel;

        // ─────────────────────────────────────────────
        // Panel Stack
        // ─────────────────────────────────────────────

        private Stack<GameObject> _panelStack = new Stack<GameObject>();
        private GameObject _activePanelBeforePause;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<SceneLoadProgressEvent>(OnSceneLoadProgress);
            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<SceneLoadProgressEvent>(OnSceneLoadProgress);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        private void Start()
        {
            // Hide all panels initially
            HideAllPanels();
        }

        // ─────────────────────────────────────────────
        // Panel Stack Operations
        // ─────────────────────────────────────────────

        /// <summary>
        /// Push a panel onto the stack (shows it, hides previous).
        /// </summary>
        public void PushPanel(GameObject panel)
        {
            if (panel == null) return;

            // Hide current top panel
            if (_panelStack.Count > 0)
            {
                GameObject current = _panelStack.Peek();
                if (current != null)
                {
                    current.SetActive(false);
                }
            }

            _panelStack.Push(panel);
            panel.SetActive(true);

            Debug.Log($"[UIManager] Pushed: {panel.name} (stack depth: {_panelStack.Count})");
        }

        /// <summary>
        /// Pop the top panel (hides it, shows previous).
        /// </summary>
        public void PopPanel()
        {
            if (_panelStack.Count == 0) return;

            GameObject popped = _panelStack.Pop();
            if (popped != null)
            {
                popped.SetActive(false);
            }

            // Show previous panel
            if (_panelStack.Count > 0)
            {
                GameObject previous = _panelStack.Peek();
                if (previous != null)
                {
                    previous.SetActive(true);
                }
            }

            Debug.Log($"[UIManager] Popped: {popped?.name} (stack depth: {_panelStack.Count})");
        }

        /// <summary>
        /// Pop all panels (clear the stack).
        /// </summary>
        public void PopAll()
        {
            while (_panelStack.Count > 0)
            {
                GameObject panel = _panelStack.Pop();
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Hide all known panels.
        /// </summary>
        private void HideAllPanels()
        {
            SetPanelActive(_hudPanel, false);
            SetPanelActive(_pausePanel, false);
            SetPanelActive(_loadingPanel, false);
            SetPanelActive(_regionSelectPanel, false);
            SetPanelActive(_inventoryPanel, false);
        }

        private void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        // ─────────────────────────────────────────────
        // Direct Panel Access
        // ─────────────────────────────────────────────

        /// <summary>
        /// Show the HUD (does not use stack — HUD is always-visible overlay).
        /// </summary>
        public void ShowHUD()
        {
            SetPanelActive(_hudPanel, true);
        }

        /// <summary>
        /// Hide the HUD.
        /// </summary>
        public void HideHUD()
        {
            SetPanelActive(_hudPanel, false);
        }

        /// <summary>
        /// Show the loading screen.
        /// </summary>
        public void ShowLoading()
        {
            SetPanelActive(_loadingPanel, true);
        }

        /// <summary>
        /// Hide the loading screen.
        /// </summary>
        public void HideLoading()
        {
            SetPanelActive(_loadingPanel, false);
        }

        /// <summary>
        /// Show region selection UI.
        /// </summary>
        public void ShowRegionSelect()
        {
            PushPanel(_regionSelectPanel);
        }

        /// <summary>
        /// Show the inventory panel.
        /// </summary>
        public void ShowInventory()
        {
            PushPanel(_inventoryPanel);
        }

        /// <summary>
        /// Show the pause menu.
        /// </summary>
        public void ShowPause()
        {
            PushPanel(_pausePanel);
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            switch (evt.NewState)
            {
                case GameState.Menu:
                    PopAll();
                    HideHUD();
                    ShowRegionSelect();
                    break;

                case GameState.Exploring:
                    PopAll();
                    ShowHUD();
                    break;

                case GameState.InChallenge:
                    PopAll();
                    ShowHUD();
                    break;

                case GameState.Paused:
                    ShowPause();
                    break;

                case GameState.Results:
                    PopAll();
                    HideHUD();
                    break;
            }
        }

        private void OnSceneLoadProgress(SceneLoadProgressEvent evt)
        {
            if (evt.Progress < 1f)
            {
                ShowLoading();
            }
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            HideLoading();
        }
    }
}
