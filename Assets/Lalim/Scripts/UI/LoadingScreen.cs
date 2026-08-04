using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Loading screen that displays during scene transitions.
    /// Shows a progress bar and optional region artwork/tips.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _tipText;
        [SerializeField] private Image _regionArtwork;

        [Header("Tips")]
        [SerializeField] private string[] _loadingTips = new string[]
        {
            "Use SpiritSense to find hidden relics nearby.",
            "The spirits guide those who listen carefully.",
            "Conservation tools help preserve cultural heritage.",
            "Explore every corner — some secrets are well hidden.",
            "Take your time. The land tells its own story."
        };

        [Header("Animation")]
        [SerializeField] private float _barSmoothSpeed = 5f;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private float _targetProgress;
        private float _displayProgress;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<SceneLoadProgressEvent>(OnLoadProgress);
            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);

            // Reset and show a random tip
            _targetProgress = 0f;
            _displayProgress = 0f;
            UpdateProgressUI(0f);
            ShowRandomTip();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SceneLoadProgressEvent>(OnLoadProgress);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        private void Update()
        {
            // Smoothly animate the progress bar
            if (Mathf.Abs(_displayProgress - _targetProgress) > 0.001f)
            {
                _displayProgress = Mathf.Lerp(_displayProgress, _targetProgress, _barSmoothSpeed * Time.unscaledDeltaTime);
                UpdateProgressUI(_displayProgress);
            }
        }

        // ─────────────────────────────────────────────
        // UI Updates
        // ─────────────────────────────────────────────

        private void UpdateProgressUI(float progress)
        {
            if (_progressBar != null)
            {
                _progressBar.value = progress;
            }

            if (_progressText != null)
            {
                _progressText.text = $"{Mathf.RoundToInt(progress * 100)}%";
            }
        }

        private void ShowRandomTip()
        {
            if (_tipText != null && _loadingTips.Length > 0)
            {
                _tipText.text = _loadingTips[Random.Range(0, _loadingTips.Length)];
            }
        }

        /// <summary>
        /// Set the region artwork image for the loading screen.
        /// </summary>
        public void SetRegionArtwork(Sprite artwork)
        {
            if (_regionArtwork != null && artwork != null)
            {
                _regionArtwork.sprite = artwork;
                _regionArtwork.enabled = true;
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnLoadProgress(SceneLoadProgressEvent evt)
        {
            _targetProgress = evt.Progress;
        }

        private void OnSceneLoaded(SceneLoadedEvent evt)
        {
            _targetProgress = 1f;
        }
    }
}
