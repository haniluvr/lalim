using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lalim.UI;

namespace Lalim.Core
{
    /// <summary>
    /// Handles async scene loading with progress tracking and linear progression gating.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float _minimumLoadTime = 1.0f; // Prevents jarring instant loads

        public bool IsLoading { get; private set; }
        public string CurrentSceneName { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentSceneName = SceneManager.GetActiveScene().name;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SceneLoadRequestedEvent>(OnSceneLoadRequested);
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Load a scene by name with optional loading screen.
        /// </summary>
        public void LoadScene(string sceneName, bool showLoadingScreen = true)
        {
            if (IsLoading)
            {
                Debug.LogWarning("[SceneLoader] Already loading a scene, ignoring request.");
                return;
            }

            // Enforce linear progression for region scenes
            Region? targetRegion = GameConstants.Scenes.GetRegionForScene(sceneName);
            if (targetRegion.HasValue && GameManager.Instance != null)
            {
                if (!GameManager.Instance.IsRegionAccessible(targetRegion.Value))
                {
                    Debug.LogWarning($"[SceneLoader] Region {targetRegion.Value} is locked. Cannot load {sceneName}.");
                    return;
                }
            }

            StartCoroutine(LoadSceneAsync(sceneName, showLoadingScreen));
        }

        /// <summary>
        /// Reload the current scene.
        /// </summary>
        public void ReloadCurrentScene()
        {
            LoadScene(CurrentSceneName, true);
        }

        // ─────────────────────────────────────────────
        // Async Loading
        // ─────────────────────────────────────────────

        private IEnumerator LoadSceneAsync(string sceneName, bool showLoadingScreen)
        {
            IsLoading = true;
            float startTime = Time.unscaledTime;

            Debug.Log($"[SceneLoader] Loading scene: {sceneName}");

            // 1. Fade out current scene to black
            yield return ScreenFader.Instance.FadeOut(0.5f);

            // 2. Notify systems to show Loading Screen (UIManager listens to this)
            EventBus.Publish(new SceneLoadProgressEvent { Progress = 0f });

            // 3. Fade in from black to the Loading Screen (if applicable)
            if (showLoadingScreen)
            {
                yield return ScreenFader.Instance.FadeIn(0.2f);
            }

            // 4. Begin async load
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            asyncOp.allowSceneActivation = false;

            // Track progress until 90% (Unity holds at 0.9 until activation)
            while (asyncOp.progress < 0.9f)
            {
                float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                EventBus.Publish(new SceneLoadProgressEvent { Progress = progress });
                yield return null;
            }

            // Ensure minimum load time (so loading screen doesn't flash)
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < _minimumLoadTime && showLoadingScreen)
            {
                float remaining = _minimumLoadTime - elapsed;
                float waitStart = Time.unscaledTime;

                while (Time.unscaledTime - waitStart < remaining)
                {
                    float progress = Mathf.Lerp(0.9f, 1.0f, (Time.unscaledTime - waitStart) / remaining);
                    EventBus.Publish(new SceneLoadProgressEvent { Progress = progress });
                    yield return null;
                }
            }

            // 5. Fade out Loading Screen to black
            if (showLoadingScreen)
            {
                yield return ScreenFader.Instance.FadeOut(0.5f);
            }
            else
            {
                // Already black if we didn't show loading screen, but just ensure
                yield return ScreenFader.Instance.FadeOut(0.1f);
            }

            // 6. Activate the new scene
            EventBus.Publish(new SceneLoadProgressEvent { Progress = 1f });
            asyncOp.allowSceneActivation = true;

            // Wait for activation to complete
            while (!asyncOp.isDone)
            {
                yield return null;
            }

            CurrentSceneName = sceneName;
            
            Debug.Log($"[SceneLoader] Scene loaded: {sceneName}");
            EventBus.Publish(new SceneLoadedEvent { SceneName = sceneName });

            // 7. Fade in from black to the newly loaded scene
            yield return ScreenFader.Instance.FadeIn(0.5f);

            IsLoading = false;
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnSceneLoadRequested(SceneLoadRequestedEvent evt)
        {
            LoadScene(evt.SceneName, evt.ShowLoadingScreen);
        }
    }
}
