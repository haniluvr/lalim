using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Global singleton that manages a black screen overlay for smooth scene transitions.
    /// Auto-instantiates itself so no editor setup is required.
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

        private CanvasGroup _canvasGroup;

        private void Initialize()
        {
            // Create Canvas
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767; // Highest possible to cover everything

            // Create Black Image
            GameObject bg = new GameObject("BlackScreen");
            bg.transform.SetParent(transform, false);
            Image img = bg.AddComponent<Image>();
            img.color = Color.black;

            // Stretch to fill screen
            RectTransform rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Add CanvasGroup for fading
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Fades the screen to black.
        /// </summary>
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

        /// <summary>
        /// Fades the screen from black to clear.
        /// </summary>
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
    }
}
