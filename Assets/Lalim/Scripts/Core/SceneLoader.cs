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
        [SerializeField] private float _minimumLoadTime = 3.0f; // Prevents jarring instant loads
        [Tooltip("Fade to/from black for transitions without a loading screen, and into the loading screen.")]
        [SerializeField] private float _fadeSeconds = 0.5f;
        [Tooltip("Loading screen fading in from black.")]
        [SerializeField] private float _loadingFadeInSeconds = 0.3f;
        [Tooltip("Loading screen dissolving into the next scene.")]
        [SerializeField] private float _crossfadeSeconds = 0.8f;
        [Tooltip("Boot fading out, and the splash fading in.")]
        [SerializeField] private float _bootFadeSeconds = 0.6f;

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
        /// The loader, created if this session has none yet (it normally comes from the Boot
        /// scene; pressing Play straight on a level in the Editor skips Boot).
        /// </summary>
        public static SceneLoader Ensure()
        {
            if (Instance == null) new GameObject("[SceneLoader]").AddComponent<SceneLoader>();
            return Instance;
        }

        /// <summary>
        /// Load a scene by name with optional loading screen. <paramref name="crossfadeIn"/>: the
        /// current frame dissolves into the loading screen instead of going through black (always
        /// the case when leaving a game scene — see IsGameScene).
        /// </summary>
        public void LoadScene(string sceneName, bool showLoadingScreen = true, bool crossfadeIn = false)
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

            // Leaving a game scene always dissolves into the loading screen (never through black).
            crossfadeIn |= IsGameScene(SceneManager.GetActiveScene().name);
            StartCoroutine(LoadSceneAsync(sceneName, showLoadingScreen, crossfadeIn && showLoadingScreen));
        }

        /// <summary>The scenes you play in: the tutorial, the teleport hub and the region levels.</summary>
        public static bool IsGameScene(string sceneName) =>
            sceneName == GameConstants.Scenes.TUTORIAL ||
            sceneName == GameConstants.Scenes.TELEPORT ||
            GameConstants.Scenes.GetRegionForScene(sceneName).HasValue;

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

        /// <summary>
        /// With a loading screen:  fade the current scene to black (or crossfade it) → loading
        ///                         screen → the real work, in stages (LoadingStages) → the loading
        ///                         screen crossfades straight into the new scene (no black).
        /// Without one:            fade to black → load → fade in (no minimum wait).
        /// From Boot the loading screen is the splash, and boot → splash fades out and in.
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName, bool showLoadingScreen, bool crossfadeIn)
        {
            IsLoading = true;
            bool isFromBoot = CurrentSceneName == GameConstants.Scenes.BOOT;
            Debug.Log($"[SceneLoader] Loading scene: {sceneName}");

            // 1. Leave the current scene: fade to black, or freeze its last frame to dissolve from.
            if (crossfadeIn) yield return ScreenFader.Instance.CaptureForCrossfade();
            else yield return ScreenFader.Instance.FadeOut(isFromBoot ? _bootFadeSeconds : _fadeSeconds);
            EventBus.Publish(new SceneLoadProgressEvent { Progress = 0f });

            // 2. The loading screen (the splash only plays once, at boot; every other transition —
            //    new game, teleport, levels, back to the menu — uses 04_Loading). Loading it on its
            //    own also unloads the scene we're leaving and the assets only it used.
            float startTime = Time.unscaledTime;
            if (showLoadingScreen)
            {
                string intermediateScene = isFromBoot ? GameConstants.Scenes.SPLASH : GameConstants.Scenes.LOADING;
                AsyncOperation interOp = SceneManager.LoadSceneAsync(intermediateScene);
                if (interOp == null)
                    Debug.LogError($"[SceneLoader] Failed to load intermediate scene '{intermediateScene}'. Please ensure it is added to your Build Settings.");
                else
                    yield return interOp;

                startTime = Time.unscaledTime;
                if (crossfadeIn)
                {
                    yield return null;   // one frame so the loading screen has rendered under the frozen frame
                    yield return ScreenFader.Instance.CrossfadeOut(_crossfadeSeconds);
                }
                else yield return ScreenFader.Instance.FadeIn(isFromBoot ? _bootFadeSeconds : _loadingFadeInSeconds);
            }

            // While the loading screen is up, loading gets priority over the (simple) loading-screen frame rate.
            int uploadSlice = QualitySettings.asyncUploadTimeSlice;
            if (showLoadingScreen)
            {
                Application.backgroundLoadingPriority = ThreadPriority.High;
                QualitySettings.asyncUploadTimeSlice = Mathf.Max(uploadSlice, 8);
            }

            float shown = 0f;
            void Report(LoadingStages.Stage stage, float t)
            {
                // The bar never runs ahead of the minimum time, and never goes backwards.
                float minimum = showLoadingScreen ? _minimumLoadTime : 0f;
                float timeProgress = minimum > 0f ? Mathf.Clamp01((Time.unscaledTime - startTime) / minimum) : 1f;
                shown = Mathf.Max(shown, Mathf.Min(timeProgress, LoadingStages.Progress(stage, t)));
                EventBus.Publish(new SceneLoadProgressEvent { Progress = shown });
            }

            // 3a. Free the memory the old scene used: loading the loading screen on its own already
            //     unloaded the old scene and its unused assets (textures, meshes, audio); collect
            //     the garbage it left behind too.
            if (showLoadingScreen)
            {
                GC.Collect();
                Report(LoadingStages.Stage.FreeMemory, 1f);
                yield return null;
            }

            // The scene's shader recording is fetched first: once the scene below is waiting for
            // activation, Unity holds every other async load back.
            UnityEngine.Rendering.GraphicsStateCollection shaders = null;
            if (showLoadingScreen)
            {
                ResourceRequest shaderRequest = ShaderWarmup.Request(sceneName);
                while (!shaderRequest.isDone) yield return null;
                shaders = shaderRequest.asset as UnityEngine.Rendering.GraphicsStateCollection;
            }

            // 3b. Read the target scene's data from storage into memory, decompressing and
            //     deserialising it in the background (its objects aren't created yet).
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                Debug.LogError($"[SceneLoader] Failed to load target scene '{sceneName}'. Please ensure it is added to your Build Settings.");
                RestoreLoadingSettings(uploadSlice);
                yield return ScreenFader.Instance.FadeIn(_fadeSeconds);
                IsLoading = false;
                yield break;
            }
            asyncOp.allowSceneActivation = false;
            while (asyncOp.progress < 0.9f)
            {
                Report(LoadingStages.Stage.LoadData, asyncOp.progress / 0.9f);
                yield return null;
            }
            Report(LoadingStages.Stage.LoadData, 1f);

            // 3c. Compile the scene's shaders now, so the first look at a new material doesn't hitch.
            if (showLoadingScreen)
                yield return ShaderWarmup.WarmUp(shaders, t => Report(LoadingStages.Stage.WarmShaders, t));

            // 3d. Hold for the minimum time (facts stay readable), filling the bar to 100%.
            float hold = showLoadingScreen ? _minimumLoadTime : 0f;
            while (Time.unscaledTime - startTime < hold)
            {
                Report(LoadingStages.Stage.WarmShaders, 1f);
                yield return null;
            }
            EventBus.Publish(new SceneLoadProgressEvent { Progress = 1f });

            // 4. Swap in the new scene.
            if (showLoadingScreen)
            {
                // Let the bar sit at 100% for a moment, then freeze the loading screen's last frame
                // over everything so the swap underneath can't be seen.
                yield return new WaitForSecondsRealtime(0.5f);
                yield return ScreenFader.Instance.CaptureForCrossfade();
            }

            // Activation: every object in the new scene is created and set up (Awake/Start: the
            // save is restored, the player placed, quests and systems initialised), and its
            // textures and meshes are uploaded to the GPU — all behind the frozen frame.
            asyncOp.allowSceneActivation = true;
            while (!asyncOp.isDone) yield return null;
            RestoreLoadingSettings(uploadSlice);

            CurrentSceneName = sceneName;
            Debug.Log($"[SceneLoader] Scene loaded: {sceneName}");
            EventBus.Publish(new SceneLoadedEvent { SceneName = sceneName });

            // 5. Reveal it: the loading screen dissolves into the new scene, or black fades away.
            yield return null;   // one frame so the new scene has rendered under the overlay
            if (showLoadingScreen) yield return ScreenFader.Instance.CrossfadeOut(_crossfadeSeconds);
            else yield return ScreenFader.Instance.FadeIn(_fadeSeconds);

            IsLoading = false;
        }

        private static void RestoreLoadingSettings(int uploadSlice)
        {
            // Back to the default, so gameplay frames come first again.
            Application.backgroundLoadingPriority = ThreadPriority.Normal;
            QualitySettings.asyncUploadTimeSlice = uploadSlice;
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
