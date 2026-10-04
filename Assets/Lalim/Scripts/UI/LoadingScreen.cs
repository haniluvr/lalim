using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// The loading scene's screen (PF_LoadingScreen): a looping video background, the loading
    /// percentage, a small progress bar under it moving with the number, and a random fun fact
    /// underneath — a deity, relic, tool, game mechanic, ocean fact or tip. A long load swaps
    /// to another fact every few seconds. Progress comes from SceneLoader (SceneLoadProgressEvent).
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [Serializable]
        public class Fact
        {
            [Tooltip("Small heading above the fact, e.g. DEITY, RELIC, TOOL, MECHANIC, TIP.")]
            public string category;
            [TextArea(2, 4)] public string text;
        }

        [Header("UI References")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _tipText;
        [SerializeField] private TMP_Text _categoryText;
        [Tooltip("Fades when the fact changes.")]
        [SerializeField] private CanvasGroup _factGroup;
        [SerializeField] private TMP_Text _statusText; // optional "Loading..." cycling text
        [SerializeField] private Image _regionArtwork;

        [Header("Fun facts (one is picked at random each load)")]
        [SerializeField] private List<Fact> _facts = new List<Fact>();
        [Tooltip("During a long load, show another fact after this many seconds.")]
        [SerializeField, Min(2f)] private float _factSeconds = 7f;
        [SerializeField, Min(0.05f)] private float _factFadeSeconds = 0.35f;

        [Header("Status Text Cycling")]
        [SerializeField] private string _baseLoadingText = "Loading";
        [SerializeField] private float _statusCycleInterval = 0.4f;

        [Header("Animation")]
        [SerializeField] private float _barSmoothSpeed = 5f;

        public IReadOnlyList<Fact> Facts => _facts;

        private static int _lastFact = -1;   // not the same fact twice in a row, across loads

        private float _targetProgress;
        private float _displayProgress;

        private void OnEnable()
        {
            EventBus.Subscribe<SceneLoadProgressEvent>(OnLoadProgress);
            EventBus.Subscribe<SceneLoadedEvent>(OnSceneLoaded);

            _targetProgress = 0f;
            _displayProgress = 0f;
            UpdateProgressUI(0f);
            ShowFact(PickFact());
            StartCoroutine(CycleFacts());
            if (_statusText != null) StartCoroutine(CycleStatusTextRoutine());
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<SceneLoadProgressEvent>(OnLoadProgress);
            EventBus.Unsubscribe<SceneLoadedEvent>(OnSceneLoaded);
        }

        private void Update()
        {
            if (Mathf.Abs(_displayProgress - _targetProgress) > 0.001f)
            {
                _displayProgress = Mathf.Lerp(_displayProgress, _targetProgress, _barSmoothSpeed * Time.unscaledDeltaTime);
                if (_targetProgress >= 1f && _displayProgress > 0.995f) _displayProgress = 1f;
                UpdateProgressUI(_displayProgress);
            }
        }

        // ─────────────────────────────────────────────
        // Progress
        // ─────────────────────────────────────────────

        // The number and the bar always show the same value.
        private void UpdateProgressUI(float progress)
        {
            if (_progressBar != null) _progressBar.value = progress;
            if (_progressText != null) _progressText.text = $"{Mathf.FloorToInt(progress * 100f + 0.0001f)}%";
        }

        private void OnLoadProgress(SceneLoadProgressEvent evt) => _targetProgress = Mathf.Max(_targetProgress, evt.Progress);

        private void OnSceneLoaded(SceneLoadedEvent evt) => _targetProgress = 1f;

        // ─────────────────────────────────────────────
        // Fun facts
        // ─────────────────────────────────────────────

        private int PickFact()
        {
            if (_facts.Count == 0) return -1;
            int i = UnityEngine.Random.Range(0, _facts.Count);
            if (_facts.Count > 1 && i == _lastFact) i = (i + 1 + UnityEngine.Random.Range(0, _facts.Count - 1)) % _facts.Count;
            _lastFact = i;
            return i;
        }

        private void ShowFact(int index)
        {
            if (index < 0) return;
            Fact f = _facts[index];
            if (_categoryText != null) _categoryText.text = string.IsNullOrEmpty(f.category) ? "DID YOU KNOW?" : f.category.ToUpperInvariant();
            if (_tipText != null) _tipText.text = f.text;
        }

        private IEnumerator CycleFacts()
        {
            if (_facts.Count < 2) yield break;
            while (true)
            {
                yield return new WaitForSecondsRealtime(_factSeconds);
                yield return Fade(1f, 0f);
                ShowFact(PickFact());
                yield return Fade(0f, 1f);
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            if (_factGroup == null) yield break;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / _factFadeSeconds)
            {
                _factGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            _factGroup.alpha = to;
        }

        private IEnumerator CycleStatusTextRoutine()
        {
            int dots = 0;
            while (true)
            {
                _statusText.text = _baseLoadingText + new string('.', dots);
                dots = (dots + 1) % 4;
                yield return new WaitForSecondsRealtime(_statusCycleInterval);
            }
        }

        /// <summary>Set the region artwork image for the loading screen.</summary>
        public void SetRegionArtwork(Sprite artwork)
        {
            if (_regionArtwork != null && artwork != null)
            {
                _regionArtwork.sprite = artwork;
                _regionArtwork.enabled = true;
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Slider bar, TMP_Text percent, TMP_Text category, TMP_Text fact, CanvasGroup factGroup, List<Fact> facts)
        {
            _progressBar = bar;
            _progressText = percent;
            _categoryText = category;
            _tipText = fact;
            _factGroup = factGroup;
            if (_facts.Count == 0) _facts = facts;   // keep hand-edited facts
        }
#endif
    }
}
