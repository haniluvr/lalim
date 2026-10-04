using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lalim.UI
{
    /// <summary>
    /// Standalone boot sequence using OnGUI rendering (bypasses Canvas and URP entirely).
    /// Guaranteed to appear on top of everything.
    /// </summary>
    public class BootSequenceUI : MonoBehaviour
    {
        [Header("UI Elements (use Sprite or Texture2D)")]
        [SerializeField] private Texture2D _unityBannerTex;
        [SerializeField] private Texture2D _teamLogoTex;

        [Header("Timings")]
        [SerializeField] private float _fadeDuration = 1.0f;
        [SerializeField] private float _displayDuration = 2.0f;

        [Header("Next Scene")]
        [SerializeField] private string _nextSceneName = Lalim.Core.GameConstants.Scenes.MAIN_MENU;

        private float _currentAlpha = 0f;
        private Texture2D _activeTex = null;
        private bool _isComplete = false;

        private void Start()
        {
            if (_unityBannerTex == null)
                Debug.LogError("[Boot] Unity Banner Texture is NOT assigned! Assign your logo in the Inspector as a Texture2D.");
            if (_teamLogoTex == null)
                Debug.LogError("[Boot] Team Logo Texture is NOT assigned! Assign your logo in the Inspector as a Texture2D.");

            Debug.Log("[Boot] Starting boot sequence via OnGUI...");
            StartCoroutine(RunSequence());
        }

        private void OnGUI()
        {
            if (_isComplete)
            {
                // Just draw black so it seamlessly blends into the SceneLoader's fade without flashing the skybox
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                return;
            }

            if (_activeTex == null || _currentAlpha <= 0f) return;

            // Draw a solid black background first
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            // Ensure smooth filtering
            _activeTex.filterMode = FilterMode.Trilinear;
            _activeTex.anisoLevel = 16;

            // Draw the active logo centered on screen with current alpha
            float texW = _activeTex.width;
            float texH = _activeTex.height;
            float maxW = Screen.width * 0.5f;
            float maxH = Screen.height * 0.5f;

            float scale = Mathf.Min(maxW / texW, maxH / texH);
            float drawW = texW * scale;
            float drawH = texH * scale;
            float x = (Screen.width - drawW) / 2f;
            float y = (Screen.height - drawH) / 2f;

            GUI.color = new Color(1f, 1f, 1f, _currentAlpha);
            GUI.DrawTexture(new Rect(x, y, drawW, drawH), _activeTex, ScaleMode.ScaleToFit, true);

            GUI.color = Color.white;
        }

        private IEnumerator RunSequence()
        {
            yield return null; // one frame buffer

            // --- Unity Banner ---
            if (_unityBannerTex != null)
            {
                _activeTex = _unityBannerTex;
                Debug.Log("[Boot] Fading in Unity Banner...");
                yield return FadeAlpha(0f, 1f);
                yield return new WaitForSeconds(_displayDuration);
                yield return FadeAlpha(1f, 0f);
            }

            _activeTex = null;
            _currentAlpha = 0f;

            // --- Team Logo ---
            if (_teamLogoTex != null)
            {
                _activeTex = _teamLogoTex;
                Debug.Log("[Boot] Fading in Team Logo...");
                yield return FadeAlpha(0f, 1f);
                yield return new WaitForSeconds(_displayDuration);
                yield return FadeAlpha(1f, 0f);
            }

            _activeTex = null;
            _currentAlpha = 0f;

            // Stop drawing OnGUI so the new global fader is visible
            _isComplete = true;

            // Load next scene via SceneLoader (this will trigger the fade and Loading Screen)
            Debug.Log("[Boot] Sequence complete. Loading: " + _nextSceneName);
            if (Lalim.Core.SceneLoader.Instance != null)
            {
                Lalim.Core.SceneLoader.Instance.LoadScene(_nextSceneName, true);
            }
            else
            {
                // Fallback just in case SceneLoader isn't in the scene
                SceneManager.LoadScene(_nextSceneName);
            }
        }

        private IEnumerator FadeAlpha(float from, float to)
        {
            float elapsed = 0f;
            _currentAlpha = from;

            if (_fadeDuration <= 0f)
            {
                _currentAlpha = to;
                yield break;
            }

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _currentAlpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
                yield return null;
            }
            _currentAlpha = to;
        }
    }
}
