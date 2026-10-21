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
        /// <summary>The scene being loaded right now (the loading screen's destination), or null.</summary>
        public string TargetSceneName { get; private set; }

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

        /// <summary>Frame time for fades: a long frame (a scene activating) counts as a short one.</summary>
        public static float SteadyDeltaTime => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        /// <summary>
        /// With a loading screen:  the loading screen fades in from black — or, leaving a game
        ///                         scene, dissolves in over it live → the real work, in stages
        ///                         (LoadingStages) → the loading screen dissolves away over the new
        ///                         scene, once that scene is running smoothly (never through black).
        /// Without one:            fade to black → load → fade in (no minimum wait).
        /// From Boot the loading screen is the splash, and boot → splash fades out and in.
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName, bool showLoadingScreen, bool crossfadeIn)
        {
            IsLoading = true;
            TargetSceneName = sceneName;
            bool isFromBoot = CurrentSceneName == GameConstants.Scenes.BOOT;
            Debug.Log($"[SceneLoader] Loading scene: {sceneName}");
            ScreenFader.Instance.SetBackdrop(true);
            TransitionScreen screen = null;

            // 1–2. The loading screen (the splash only plays once, at boot; every other transition —
            //      new game, teleport, levels, back to the menu — uses 04_Loading).
            float startTime = Time.unscaledTime;
            if (showLoadingScreen)
            {
                string intermediateScene = isFromBoot ? GameConstants.Scenes.SPLASH : GameConstants.Scenes.LOADING;
                if (crossfadeIn)
                {
                    // Loaded beside the scene we're leaving, which keeps playing underneath while
                    // the loading screen dissolves in; then the old scene is unloaded.
                    Scene leaving = SceneManager.GetActiveScene();
                    yield return LoadParked(intermediateScene, s => screen = s);
                    if (screen != null)
                    {
                        EventBus.Publish(new SceneLoadProgressEvent { Progress = 0f, Phase = "Starting up..." });
                        yield return screen.Fade(0f, 1f, _crossfadeSeconds);
                        SceneManager.SetActiveScene(SceneManager.GetSceneByName(intermediateScene));
                        yield return SceneManager.UnloadSceneAsync(leaving);
                        screen.Unpark();
                    }
                    else crossfadeIn = false;   // couldn't load it beside: fall back to black
                }
                if (!crossfadeIn)
                {
                    // Loading it on its own also unloads the scene we're leaving.
                    yield return ScreenFader.Instance.FadeOut(isFromBoot ? _bootFadeSeconds : _fadeSeconds);
                    EventBus.Publish(new SceneLoadProgressEvent { Progress = 0f, Phase = "Starting up..." });
                    AsyncOperation interOp = SceneManager.LoadSceneAsync(intermediateScene);
                    if (interOp == null)
                        Debug.LogError($"[SceneLoader] Failed to load intermediate scene '{intermediateScene}'. Please ensure it is added to your Build Settings.");
                    else
                    {
                        yield return interOp;
                        screen = TransitionScreen.Of(SceneManager.GetSceneByName(intermediateScene));
                    }
                    yield return ScreenFader.Instance.FadeIn(isFromBoot ? _bootFadeSeconds : _loadingFadeInSeconds);
                }
                startTime = Time.unscaledTime;
            }
            else
            {
                yield return ScreenFader.Instance.FadeOut(_fadeSeconds);
            }

            // While the loading screen is up, loading gets priority over the (simple) loading-screen frame rate.
            int uploadSlice = QualitySettings.asyncUploadTimeSlice;
            if (showLoadingScreen)
            {
                Application.backgroundLoadingPriority = ThreadPriority.High;
                QualitySettings.asyncUploadTimeSlice = Mathf.Max(uploadSlice, 8);
            }

            float shown = 0f;
            void Report(LoadingStages.Stage stage, float t, string phase = null)
            {
                // The bar never runs ahead of the minimum time, and never goes backwards.
                float minimum = showLoadingScreen ? _minimumLoadTime : 0f;
                float timeProgress = minimum > 0f ? Mathf.Clamp01((Time.unscaledTime - startTime) / minimum) : 1f;
                shown = Mathf.Max(shown, Mathf.Min(timeProgress, LoadingStages.Progress(stage, t)));
                EventBus.Publish(new SceneLoadProgressEvent { Progress = shown, Phase = phase ?? LoadingStages.Label(stage) });
            }

            // 3a. Free the memory the old scene used. A single-mode load already unloaded the old
            //     scene's unused assets (textures, meshes, audio); after an additive swap that's
            //     done here (with a time limit, so a stuck unload can't hold the bar). Then collect
            //     the garbage it left behind too.
            if (showLoadingScreen)
            {
                if (crossfadeIn)
                {
                    AsyncOperation unload = Resources.UnloadUnusedAssets();
                    float until = Time.unscaledTime + 3f;
                    while (!unload.isDone && Time.unscaledTime < until)
                    {
                        Report(LoadingStages.Stage.FreeMemory, 0.5f);
                        yield return null;
                    }
                }
                GC.Collect();
                Report(LoadingStages.Stage.FreeMemory, 1f);
                yield return null;
            }

            // The scene's shader recording is fetched first: once the scene below is waiting for
            // activation, Unity holds every other async load back.
            UnityEngine.Rendering.GraphicsStateCollection shaders = null;
            if (showLoadingScreen)
            {
                EventBus.Publish(new SceneLoadProgressEvent { Progress = shown, Phase = "Retrieving shader data..." });
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
                ScreenFader.Instance.SetBackdrop(false);
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
                Report(LoadingStages.Stage.WarmShaders, 1f, "Verifying resources...");
                yield return null;
            }
            EventBus.Publish(new SceneLoadProgressEvent { Progress = 1f, Phase = "Almost ready..." });

            // 4. Swap in the new scene. The loading screen is kept through the swap (it covers it),
            //    the bar sitting at 100% for a moment first.
            if (showLoadingScreen)
            {
                yield return new WaitForSecondsRealtime(0.5f);
                if (screen != null) screen.Detach();
            }

            // Activation: every object in the new scene is created and set up (Awake/Start: the
            // save is restored, the player placed, quests and systems initialised), and its
            // textures and meshes are uploaded to the GPU — all behind the loading screen.
            if (showLoadingScreen) EventBus.Publish(new SceneLoadProgressEvent { Progress = 1f, Phase = "Setting things up..." });
            asyncOp.allowSceneActivation = true;
            while (!asyncOp.isDone) yield return null;
            RestoreLoadingSettings(uploadSlice);

            CurrentSceneName = sceneName;
            Debug.Log($"[SceneLoader] Scene loaded: {sceneName}");
            EventBus.Publish(new SceneLoadedEvent { SceneName = sceneName });

            // 5. Reveal it once its first, heavy frames are over (otherwise the fade would be
            //    spent on a couple of long frames and look like a cut): the loading screen
            //    dissolves into the new scene, or black fades away.
            yield return WaitForSteadyFrames();
            if (screen != null && !screen.IsEmpty)
            {
                yield return screen.Fade(1f, 0f, _crossfadeSeconds);
                screen.Destroy();
            }
            else yield return ScreenFader.Instance.FadeIn(_fadeSeconds);

            ScreenFader.Instance.SetBackdrop(false);
            IsLoading = false;
            TargetSceneName = null;
        }

        /// <summary>
        /// Loads <paramref name="sceneName"/> next to the current scene, parked and invisible
        /// (see TransitionScreen), and hands back its screen — null if it couldn't be loaded.
        /// </summary>
        private static IEnumerator LoadParked(string sceneName, Action<TransitionScreen> done)
        {
            TransitionScreen screen = null;
            // Runs as soon as the scene's objects exist, before they're ever rendered.
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = null;
            onLoaded = (scene, mode) =>
            {
                if (scene.name != sceneName) return;
                SceneManager.sceneLoaded -= onLoaded;
                screen = TransitionScreen.Of(scene);
                screen.Park(scene);
                screen.SetAlpha(0f);
            };
            SceneManager.sceneLoaded += onLoaded;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                SceneManager.sceneLoaded -= onLoaded;
                Debug.LogError($"[SceneLoader] Failed to load intermediate scene '{sceneName}'. Please ensure it is added to your Build Settings.");
                done(null);
                yield break;
            }
            yield return op;
            SceneManager.sceneLoaded -= onLoaded;
            done(screen);
        }

        /// <summary>Waits until frames are short again after a scene activation (at most ~2 s).</summary>
        private static IEnumerator WaitForSteadyFrames()
        {
            int steady = 0;
            float until = Time.unscaledTime + 2f;
            yield return null;
            while (steady < 3 && Time.unscaledTime < until)
            {
                yield return null;
                steady = Time.unscaledDeltaTime < 0.1f ? steady + 1 : 0;
            }
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
