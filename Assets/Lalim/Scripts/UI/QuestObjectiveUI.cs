using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// The quest box (quest-textbox.png) with its key button (quest-btn.png), top-left of the
    /// screen: "QUEST", the objective, and an "x/y" counter in the corner while a step counts
    /// something.
    ///
    /// Every quest step's text shows here. A new objective opens the box for actual quests and
    /// leaves it closed for travel/return steps (QuestStep "box starts open"); tapping the key
    /// button closes or reopens it either way. Steps aside during dialogue and first-person minigames.
    ///
    /// Lives in PF_QuestObjectiveUI — edit the prefab and every level picks it up.
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestObjectiveUI : MonoBehaviour
    {
        public static QuestObjectiveUI Instance { get; private set; }

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _objectiveText;
        [Tooltip("The \"0/3\" in the box's top-right corner.")]
        [SerializeField] private TMP_Text _counterText;
        [Tooltip("Counter format. Steps may pass longer formats; the corner always uses this one.")]
        [SerializeField] private string _counterFormat = "{0}/{1}";

        [Header("Toggleable Quest Box")]
        [Tooltip("The text box that opens and closes (quest-textbox.png).")]
        [SerializeField] private RectTransform _questBoxContent;
        [Tooltip("The key button (quest-btn.png).")]
        [SerializeField] private Button _toggleButton;
        [SerializeField, Min(0.01f)] private float _toggleSeconds = 0.18f;

        private CanvasGroup _boxGroup;
        private bool _isBoxExpanded = true;
        private bool _hasObjective;
        private bool _shown;
        private bool _dialogueOpen;
        private bool _minigameOpen;
        private Coroutine _anim;

        public bool IsShown => _shown;
        public bool IsBoxExpanded => _isBoxExpanded;

        private void Awake()
        {
            Instance = this;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_questBoxContent != null)
            {
                _boxGroup = _questBoxContent.GetComponent<CanvasGroup>();
                if (_boxGroup == null) _boxGroup = _questBoxContent.gameObject.AddComponent<CanvasGroup>();
            }
            if (_toggleButton != null) _toggleButton.onClick.AddListener(ToggleQuestBox);
            ClearCounter();
            _shown = false;
            ApplyAlpha();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            Lalim.Core.EventBus.Subscribe<Lalim.Core.DialogueStartedEvent>(OnDialogueStarted);
            Lalim.Core.EventBus.Subscribe<Lalim.Core.DialogueEndedEvent>(OnDialogueEnded);
            Lalim.Core.EventBus.Subscribe<Lalim.Core.ChallengeStartedEvent>(OnChallengeStarted);
            Lalim.Core.EventBus.Subscribe<Lalim.Core.ChallengeCompletedEvent>(OnChallengeEnded);
            Lalim.Core.EventBus.Subscribe<Lalim.Core.ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            Lalim.Core.EventBus.Unsubscribe<Lalim.Core.DialogueStartedEvent>(OnDialogueStarted);
            Lalim.Core.EventBus.Unsubscribe<Lalim.Core.DialogueEndedEvent>(OnDialogueEnded);
            Lalim.Core.EventBus.Unsubscribe<Lalim.Core.ChallengeStartedEvent>(OnChallengeStarted);
            Lalim.Core.EventBus.Unsubscribe<Lalim.Core.ChallengeCompletedEvent>(OnChallengeEnded);
            Lalim.Core.EventBus.Unsubscribe<Lalim.Core.ChallengeFailedEvent>(OnChallengeFailed);
        }

        // First-person minigames have their own status line; the quest box steps aside.
        private void OnChallengeStarted(Lalim.Core.ChallengeStartedEvent evt) { _minigameOpen = evt.Perspective == Lalim.Core.ChallengePerspective.FirstPerson; ApplyAlpha(); }
        private void OnChallengeEnded(Lalim.Core.ChallengeCompletedEvent evt) { _minigameOpen = false; ApplyAlpha(); }
        private void OnChallengeFailed(Lalim.Core.ChallengeFailedEvent evt) { _minigameOpen = false; ApplyAlpha(); }

        private void OnDialogueStarted(Lalim.Core.DialogueStartedEvent evt) { _dialogueOpen = true; ApplyAlpha(); }
        private void OnDialogueEnded(Lalim.Core.DialogueEndedEvent evt) { _dialogueOpen = false; ApplyAlpha(); }

        private void ApplyAlpha()
        {
            if (_group == null) return;
            bool visible = _shown && !_dialogueOpen && !_minigameOpen;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        /// <summary>Shows the quest UI (whether its box is open is up to the objective).</summary>
        public void Show()
        {
            _shown = true;
            ApplyAlpha();
        }

        public void Hide()
        {
            _shown = false;
            ApplyAlpha();
        }

        public void ToggleQuestBox() => SetExpanded(!_isBoxExpanded, instant: false);

        /// <summary>Sets the objective. A new objective (a newly given quest) opens the box.</summary>
        public void SetObjective(string text) => SetObjective(text, true);

        /// <summary>
        /// Sets the objective; when it's new, the box opens (an actual quest) or closes (travel —
        /// the player can still open it). The same text again keeps the player's choice.
        /// </summary>
        public void SetObjective(string text, bool openBox)
        {
            if (_objectiveText == null) return;
            bool changed = _objectiveText.text != text || !_hasObjective;
            _objectiveText.text = text;
            _hasObjective = true;
            if (changed) SetExpanded(openBox, instant: !isActiveAndEnabled || _group == null || _group.alpha < 0.01f);
        }

        public void SetCounter(int current, int total)
        {
            if (_counterText == null) return;
            _counterText.text = string.Format(_counterFormat, current, total);
            _counterText.gameObject.SetActive(true);
        }

        /// <summary>Kept for steps that pass their own wording; the corner badge stays compact.</summary>
        public void SetCounter(int current, int total, string format) => SetCounter(current, total);

        /// <summary>Hides the counter (steps without a count, e.g. a dialogue).</summary>
        public void ClearCounter()
        {
            if (_counterText == null) return;
            _counterText.text = "";
            _counterText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Screen area (pixels) the quest widget covers with its box fully open — even while it's
        /// closed or animating — so other HUD can keep clear of it. False when the widget is hidden.
        /// </summary>
        public bool TryGetOpenScreenRect(out Rect rect)
        {
            rect = default;
            if (!_shown || _questBoxContent == null) return false;
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            bool any = false;
            Vector2 min = Vector2.zero, max = Vector2.zero;
            void Add(RectTransform rt, bool unscaled)
            {
                if (rt == null) return;
                Rect r = rt.rect;
                foreach (Vector2 c in new[] { r.min, r.max, new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMin) })
                {
                    // The box scales while it opens and closes: measure it at full size.
                    Vector3 world = unscaled && rt.parent != null
                        ? rt.parent.TransformPoint(rt.localPosition + (Vector3)c)
                        : rt.TransformPoint(c);
                    Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, world);
                    if (!any) { min = max = s; any = true; }
                    else { min = Vector2.Min(min, s); max = Vector2.Max(max, s); }
                }
            }
            Add(_questBoxContent, true);
            if (_toggleButton != null) Add((RectTransform)_toggleButton.transform, false);
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return any;
        }

        // ───────────────────────── Box open / close ─────────────────────────

        private void SetExpanded(bool expanded, bool instant)
        {
            _isBoxExpanded = expanded;
            if (_questBoxContent == null) return;
            if (_anim != null) StopCoroutine(_anim);
            if (instant || !isActiveAndEnabled)
            {
                ApplyBox(expanded ? 1f : 0f);
                return;
            }
            _anim = StartCoroutine(AnimateBox(expanded));
        }

        // Grows out from the key button (the box's pivot sits by the button).
        private IEnumerator AnimateBox(bool open)
        {
            if (open) _questBoxContent.gameObject.SetActive(true);
            float from = _boxGroup != null ? _boxGroup.alpha : (open ? 0f : 1f);
            float to = open ? 1f : 0f;
            float t = 0f;
            while (t < 1f)
            {
                t += Lalim.Core.PauseClock.DeltaTime / _toggleSeconds;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                ApplyBox(Mathf.Lerp(from, to, e));
                yield return null;
            }
            ApplyBox(to);
            _anim = null;
        }

        private void ApplyBox(float k)
        {
            if (_boxGroup != null)
            {
                _boxGroup.alpha = k;
                _boxGroup.blocksRaycasts = k > 0.5f;
            }
            _questBoxContent.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), Mathf.Lerp(0.85f, 1f, k), 1f);
            _questBoxContent.gameObject.SetActive(k > 0.001f);
        }
    }
}
