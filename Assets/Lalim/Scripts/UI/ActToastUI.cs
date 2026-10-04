using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lalim.UI
{
    /// <summary>What a toast is for; when several are waiting they play in this order.</summary>
    public enum ToastKind { Act = 0, QuestComplete = 1, Reward = 2, Achievement = 3 }

    /// <summary>
    /// The achievement-title toast (achievement-title-toast.png). Used ONLY for: the act title when
    /// a level is first entered ("ACT I"), "Quest Complete" with the zone's rewards, a tool received
    /// mid-quest, and achievements.
    ///
    /// Toasts never overlap: they wait while a dialogue is open and play one at a time, in
    /// ToastKind order (act, quest complete, reward, achievement). ZoneBannerUI waits for them.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActToastUI : MonoBehaviour
    {
        public static ActToastUI Instance { get; private set; }

        private struct PendingToast { public string Title; public string Body; public ToastKind Kind; }
        private readonly List<PendingToast> _queue = new List<PendingToast>();
        private bool _playing;

        /// <summary>A toast is showing or waiting to (banners hold back until it's done).</summary>
        public static bool IsBusy => Instance != null && (Instance._playing || Instance._queue.Count > 0);

        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _bodyText;

        [SerializeField, Min(0f)] private float _panelFadeIn = 0.6f;
        [SerializeField, Min(0f)] private float _textFadeIn = 1.0f;
        [SerializeField, Min(0f)] private float _hold = 3f;
        [SerializeField, Min(0f)] private float _fadeOut = 1.2f;
        [SerializeField] private bool _playOnStart = true;

        private void Awake()
        {
            Instance = this;
            if (_panelGroup == null) _panelGroup = GetComponentInChildren<CanvasGroup>(true);
            SetHidden();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            if (_playOnStart && ShouldShowAct()) Show();
        }

        /// <summary>
        /// The act title plays once per save, on the first fresh entry into the level: not when
        /// resuming a saved game, and not on later visits.
        /// </summary>
        private bool ShouldShowAct()
        {
            Lalim.Core.SaveManager sm = Lalim.Core.SaveManager.Instance;
            if (sm == null) return true;
            if (sm.Origin == Lalim.Core.SaveManager.SessionOrigin.LoadedGame) return false;
            Lalim.Core.SaveData save = sm.CurrentSave;
            if (save == null) return true;
            string flag = "seen_act_" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (save.tutorialFlags.Contains(flag)) return false;
            save.tutorialFlags.Add(flag);
            return true;
        }

        /// <summary>Plays the toast with the texts currently in the scene (the level's act title).</summary>
        public void Show()
        {
            if (_panelGroup == null || _titleText == null) return;
            Add(new PendingToast { Title = null, Body = null, Kind = ToastKind.Act });
        }

        /// <summary>Queue a toast with new title/body text.</summary>
        public void Enqueue(string title, string body, ToastKind kind = ToastKind.Reward)
        {
            if (_panelGroup == null || _titleText == null) return;
            Add(new PendingToast { Title = title, Body = body, Kind = kind });
        }

        private void Add(PendingToast toast)
        {
            _queue.Add(toast);
            if (!_playing) StartCoroutine(RunQueue());
        }

        /// <summary>Static convenience that tolerates a missing toast in the scene.</summary>
        public static void Post(string title, string body, ToastKind kind = ToastKind.Reward)
        {
            if (Instance != null) Instance.Enqueue(title, body, kind);
            else Debug.Log($"[Toast] {title} — {body}");
        }

        private IEnumerator RunQueue()
        {
            _playing = true;
            while (_queue.Count > 0)
            {
                // Never cover a dialogue; then give anything posted in the same moment a frame to
                // arrive, so the order below holds (e.g. Quest Complete before its achievement).
                while (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) yield return null;
                yield return null;
                if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) continue;

                int pick = 0;
                for (int i = 1; i < _queue.Count; i++) if (_queue[i].Kind < _queue[pick].Kind) pick = i;
                PendingToast next = _queue[pick];
                _queue.RemoveAt(pick);
                if (next.Title != null) _titleText.text = next.Title;
                if (_bodyText != null && next.Body != null) _bodyText.text = next.Body;
                yield return Sequence();
            }
            _playing = false;
        }

        private IEnumerator Sequence()
        {
            _panelGroup.alpha = 0f;
            _titleText.alpha = 0f;
            if (_bodyText != null) _bodyText.alpha = 0f;

            yield return Fade(0f, 1f, _panelFadeIn, a => _panelGroup.alpha = a);
            yield return Fade(0f, 1f, _textFadeIn, a => {
                _titleText.alpha = a;
                if (_bodyText != null) _bodyText.alpha = a;
            });
            if (_hold > 0f) yield return new WaitForSecondsRealtime(_hold);
            yield return Fade(1f, 0f, _fadeOut, a => { 
                _panelGroup.alpha = a; 
                _titleText.alpha = a; 
                if (_bodyText != null) _bodyText.alpha = a;
            });

            SetHidden();
        }

        private static IEnumerator Fade(float from, float to, float duration, System.Action<float> apply)
        {
            if (duration <= 0f) { apply(to); yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            apply(to);
        }

        private void SetHidden()
        {
            if (_panelGroup != null) { _panelGroup.alpha = 0f; _panelGroup.blocksRaycasts = false; _panelGroup.interactable = false; }
            if (_titleText != null) _titleText.alpha = 0f;
        }
    }
}
