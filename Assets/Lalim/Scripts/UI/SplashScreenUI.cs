using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Drives the loading bar on the Splash Screen.
    /// Listens to SceneLoadProgressEvent from the EventBus and updates a progress bar UI.
    /// </summary>
    public class SplashScreenUI : MonoBehaviour
    {
        [Header("Loading Bar (Option A: Slider)")]
        [Tooltip("Assign a Unity Slider component as your loading bar. Leave empty to use the Image Fill method instead.")]
        [SerializeField] private Slider _loadingSlider;

        [Header("Loading Bar (Option B: Image Fill)")]
        [Tooltip("Assign a UI Image set to Image Type: Filled. Leave empty to use the Slider method instead.")]
        [SerializeField] private Image _loadingBarFill;

        [Header("Loading Text (Optional)")]
        [SerializeField] private TMP_Text _loadingText;

        [Header("Settings")]
        [Tooltip("How smoothly the bar animates toward the target progress value.")]
        [SerializeField] private float _smoothSpeed = 5f;

        private float _targetProgress = 0f;
        private float _currentProgress = 0f;
        private string _phase = "Starting up...";

        private void OnEnable()
        {
            EventBus.Subscribe<SceneLoadProgressEvent>(OnLoadProgress);
            if (_loadingText != null) _loadingText.text = _phase;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SceneLoadProgressEvent>(OnLoadProgress);
        }

        private void OnLoadProgress(SceneLoadProgressEvent evt)
        {
            _targetProgress = evt.Progress;
            if (!string.IsNullOrEmpty(evt.Phase)) _phase = evt.Phase;   // what the loader is doing right now
        }

        private void Update()
        {
            // Smoothly animate the bar
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, _smoothSpeed * Time.unscaledDeltaTime);

            // Update Slider (Option A)
            if (_loadingSlider != null)
            {
                _loadingSlider.value = _currentProgress;
            }

            // Update Image Fill (Option B)
            if (_loadingBarFill != null)
            {
                _loadingBarFill.fillAmount = _currentProgress;
            }

            // Loading text: the current loader phase, swapped instantly (no fade)
            if (_loadingText != null && _loadingText.text != _phase)
            {
                _loadingText.text = _phase;
            }
        }
    }
}
