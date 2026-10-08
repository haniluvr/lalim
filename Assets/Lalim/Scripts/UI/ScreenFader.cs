using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Global singleton for scene transitions. Auto-instantiates itself so no editor setup is required.
    ///
    ///  - FadeOut / FadeIn: to and from black.
    ///  - Backdrop: a camera that only clears to black, on while a scene swap is under way, so
    ///    the frames between one scene's cameras going away and the next one's arriving are
    ///    black instead of whatever the screen last held (or the editor's "no cameras" display).
    ///
    /// Crossfades between scenes don't go through here: SceneLoader fades the loading screen
    /// itself in and out over the live scenes (see TransitionScreen).
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        public static ScreenFader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[ScreenFader]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ScreenFader>();
                    _instance.Initialize();
                }
                return _instance;
            }
        }

        private CanvasGroup _canvasGroup;     // black
        private Camera _backdrop;

        private void Initialize()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767; // Highest possible to cover everything

            GameObject bg = new GameObject("BlackScreen");
            bg.transform.SetParent(transform, false);
            Image img = bg.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;
            RectTransform rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _canvasGroup = bg.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            GameObject cam = new GameObject("BackdropCamera");
            cam.transform.SetParent(transform, false);
            _backdrop = cam.AddComponent<Camera>();
            _backdrop.clearFlags = CameraClearFlags.SolidColor;
            _backdrop.backgroundColor = Color.black;
            _backdrop.cullingMask = 0;
            _backdrop.depth = -100f;            // every scene camera draws over it
            _backdrop.useOcclusionCulling = false;
            _backdrop.allowHDR = false;
            _backdrop.allowMSAA = false;
            _backdrop.enabled = false;
        }

        /// <summary>Black behind everything while a scene swap is under way.</summary>
        public void SetBackdrop(bool on)
        {
            if (_backdrop != null) _backdrop.enabled = on;
        }

        /// <summary>Fades the screen to black.</summary>
        public IEnumerator FadeOut(float duration)
        {
            _canvasGroup.blocksRaycasts = true; // Block UI interaction during fade
            yield return Fade(_canvasGroup.alpha, 1f, duration);
        }

        /// <summary>Fades the screen from black to clear.</summary>
        public IEnumerator FadeIn(float duration)
        {
            yield return Fade(_canvasGroup.alpha, 0f, duration);
            _canvasGroup.blocksRaycasts = false; // Re-enable UI interaction
        }

        // A long frame (a scene just activated) never jumps the fade to its end.
        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < 1f;)
            {
                t += Lalim.Core.SceneLoader.SteadyDeltaTime / Mathf.Max(0.01f, duration);
                _canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
    }
}
