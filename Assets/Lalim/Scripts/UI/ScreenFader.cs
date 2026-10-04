using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Global singleton for scene transitions. Auto-instantiates itself so no editor setup is required.
    ///
    ///  - FadeOut / FadeIn: to and from black.
    ///  - CaptureForCrossfade + CrossfadeOut: freezes the current frame on top of everything, so
    ///    the scene underneath can be swapped and then revealed by fading the frozen frame away
    ///    (the loading screen dissolves straight into the next scene, never through black).
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
        private RawImage _snapshot;
        private CanvasGroup _snapshotGroup;
        private Texture2D _snapshotTexture;

        private void Initialize()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767; // Highest possible to cover everything

            // Frozen frame for crossfades (under the black).
            GameObject snap = new GameObject("Snapshot");
            snap.transform.SetParent(transform, false);
            _snapshot = snap.AddComponent<RawImage>();
            Stretch(snap.GetComponent<RectTransform>());
            _snapshotGroup = snap.AddComponent<CanvasGroup>();
            _snapshotGroup.alpha = 0f;
            _snapshotGroup.blocksRaycasts = false;
            snap.SetActive(false);

            GameObject bg = new GameObject("BlackScreen");
            bg.transform.SetParent(transform, false);
            Image img = bg.AddComponent<Image>();
            img.color = Color.black;
            Stretch(bg.GetComponent<RectTransform>());
            _canvasGroup = bg.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Fades the screen to black.</summary>
        public IEnumerator FadeOut(float duration)
        {
            _canvasGroup.blocksRaycasts = true; // Block UI interaction during fade
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
        }

        /// <summary>Fades the screen from black to clear.</summary>
        public IEnumerator FadeIn(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false; // Re-enable UI interaction
        }

        /// <summary>Freezes the current frame (UI included) over the screen. Call before swapping scenes.</summary>
        public IEnumerator CaptureForCrossfade()
        {
            yield return new WaitForEndOfFrame();
            ReleaseSnapshot();
            _snapshotTexture = ScreenCapture.CaptureScreenshotAsTexture();
            _snapshot.texture = _snapshotTexture;
            _snapshot.gameObject.SetActive(true);
            _snapshotGroup.alpha = 1f;
            _snapshotGroup.blocksRaycasts = true;
        }

        /// <summary>Fades the frozen frame away, revealing whatever is underneath now.</summary>
        public IEnumerator CrossfadeOut(float duration)
        {
            if (_snapshotTexture == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _snapshotGroup.alpha = 1f - t * t * (3f - 2f * t);
                yield return null;
            }
            ReleaseSnapshot();
        }

        private void ReleaseSnapshot()
        {
            _snapshotGroup.alpha = 0f;
            _snapshotGroup.blocksRaycasts = false;
            _snapshot.texture = null;
            _snapshot.gameObject.SetActive(false);
            if (_snapshotTexture != null) Destroy(_snapshotTexture);
            _snapshotTexture = null;
        }
    }
}
