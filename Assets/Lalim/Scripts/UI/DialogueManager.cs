using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;

namespace Lalim.UI
{
    [System.Serializable]
    public struct DialogueLine
    {
        public string speakerName;
        [TextArea(3, 5)]
        public string text;
        public bool isPlayer; // Player box: name top-right; NPC box: name top-left
        public Sprite portrait; // Optional NPC portrait
    }

    /// <summary>
    /// Runs dialogue: one long box that swaps between the NPC sprite (name in the shaded box,
    /// top-left) and the Player sprite (name top-right). Text always starts from the left.
    ///
    /// Dialogue HUD state lives here so the buttons stay thin:
    ///  - Review: log of the lines shown so far in the CURRENT conversation (never earlier ones,
    ///    never future lines); the scene is paused (timeScale 0) while it's open.
    ///  - Auto: advances by itself after each line finishes typing.
    ///  - Speed: 1 → 1.2 → 1.5 → 2× typing (and auto delay). Never skips lines.
    ///  Auto and Speed only last for the conversation they were set in: every dialogue starts at
    ///  the defaults (auto off, 1×), and nothing is saved.
    ///
    /// The body text scrolls (drag) when a line is longer than the box, following the typing; a
    /// small ▼ fades in (and stays still) at the box's bottom-right once a line has finished, to
    /// say "tap for the next".
    /// A tap (press and release without dragging) advances.
    ///  - Pause: freezes everything (world, sound, typing, auto, tap-to-advance) and turns into a
    ///    play button that resumes (does not open the pause menu).
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        public struct HistoryEntry
        {
            public string Speaker;
            public string Text;
            public bool IsPlayer;
        }

        public static readonly float[] Speeds = { 1f, 1.2f, 1.5f, 2f };
        // Auto/Speed used to be remembered here; they aren't any more (cleared on start).
        private const string PrefAuto = "lalim.dialogue.auto";
        private const string PrefSpeed = "lalim.dialogue.speedIndex";

        [Header("Box")]
        [SerializeField] private GameObject _dialoguePanel;
        [SerializeField] private Image _boxImage;
        [SerializeField] private Sprite _npcBoxSprite;
        [SerializeField] private Sprite _playerBoxSprite;
        [SerializeField] private RectTransform _nameBox;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private Image _npcPortraitImage;
        [SerializeField] private GameObject _pausedBadge;

        [System.Serializable]
        public struct SpeakerPortrait
        {
            public string speaker;
            public Sprite sprite;
            [Tooltip("Size of this bust (0 or 1 = normal). Grows up and out to the left, clear of the text.")]
            [Min(0f)] public float scale;
        }

        [Header("NPC portraits (bottom-left of the box)")]
        [Tooltip("Bust shown for an NPC's lines, matched by speaker name (case-insensitive). A line's own portrait wins.")]
        [SerializeField] private List<SpeakerPortrait> _speakerPortraits = new List<SpeakerPortrait>();
        [Tooltip("Where the body text starts while a portrait shows, as a fraction of the box width (lines up with the name).")]
        [SerializeField, Range(0f, 0.5f)] private float _bodyLeftWithPortrait = 0.142f;
        [SerializeField] private float _bodyInsetWithPortrait = 28f;

        [Header("Name box placement (fractions of the box, from the sprites' shaded strip)")]
        [SerializeField] private Vector2 _npcNameMin = new Vector2(0.142f, 0.77f);
        [SerializeField] private Vector2 _npcNameMax = new Vector2(0.5f, 1f);
        [SerializeField] private Vector2 _playerNameMin = new Vector2(0.615f, 0.77f);
        [SerializeField] private Vector2 _playerNameMax = new Vector2(0.973f, 1f);

        [Header("Typing")]
        [SerializeField, Min(1f)] private float _charsPerSecond = 45f;
        [SerializeField] private AudioClip _typeSound;

        [Header("Next indicator (▼ bottom-right of the box)")]
        [Tooltip("Position inside the box, as fractions of its size.")]
        [SerializeField] private Vector2 _nextIndicatorAnchor = new Vector2(0.95f, 0.13f);
        [SerializeField, Min(8f)] private float _nextIndicatorSize = 26f;
        [SerializeField] private Color _nextIndicatorColor = new Color(0.16f, 0.32f, 0.5f, 0.9f);
        [SerializeField, Min(0.01f)] private float _nextIndicatorFadeSeconds = 0.3f;
        [Tooltip("How far the ▼ floats up and down (pixels) and how fast.")]
        [SerializeField, Min(0f)] private float _nextIndicatorBob = 5f;
        [SerializeField, Min(0.1f)] private float _nextIndicatorBobSpeed = 4f;

        [Header("Auto")]
        [SerializeField, Min(0f)] private float _autoDelayBase = 1.2f;
        [SerializeField, Min(0f)] private float _autoDelayPerChar = 0.03f;

        private readonly Queue<DialogueLine> _queue = new Queue<DialogueLine>();
        private readonly List<HistoryEntry> _history = new List<HistoryEntry>();
        private AudioSource _audioSource;
        private Action _onDialogueComplete;
        private int _lineIndex;

        private string _currentSentence = "";
        private bool _isTyping;
        private float _typedChars;
        private float _autoTimer;
        private int _speedIndex;
        private float _timeScaleBeforeReview = 1f;

        private RectTransform _bodyFrame;     // the scroll viewport the body text sits in
        private ScrollRect _bodyScroll;
        private RectTransform _nextIndicator;
        private Image _nextIndicatorImage;
        private float _nextIndicatorAlpha;
        private Vector2 _pressPosition;
        private bool _pressed;
        private static Sprite s_triangle;

        public bool IsOpen => _dialoguePanel != null && _dialoguePanel.activeSelf;
        public bool IsAuto { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsReviewOpen { get; private set; }
        public int SpeedIndex => _speedIndex;
        public float Speed => Speeds[_speedIndex];
        public IReadOnlyList<HistoryEntry> History => _history;
        /// <summary>Goes up with every new conversation (the review rebuilds for it).</summary>
        public int ConversationId { get; private set; }

        /// <summary>Raised whenever auto / speed / pause / review / the shown line changes (HUD refresh).</summary>
        public event Action StateChanged;

        /// <summary>Raised as each line of the current conversation appears (0-based index in that conversation).</summary>
        public event Action<int> LineStarted;

        /// <summary>Replaces {player} with the saved player name ("Player" until the name scene sets one).</summary>
        public static string ResolveTokens(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("{player}")) return text;
            string name = SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null
                ? SaveManager.Instance.CurrentSave.playerName : null;
            return text.Replace("{player}", string.IsNullOrEmpty(name) ? "Player" : name);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            PlayerPrefs.DeleteKey(PrefAuto);
            PlayerPrefs.DeleteKey(PrefSpeed);
            ResetPlaybackDefaults();

            SetupBodyScroll();
            SetupNextIndicator();

            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            if (_pausedBadge != null) _pausedBadge.SetActive(false);   // legacy "PAUSED" label: never shown
        }

        // The body text goes into a masked viewport in its place, so a long line can scroll.
        private void SetupBodyScroll()
        {
            if (_bodyText == null) return;
            RectTransform body = _bodyText.rectTransform;
            var frameGo = new GameObject("BodyViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect));
            _bodyFrame = (RectTransform)frameGo.transform;
            _bodyFrame.SetParent(body.parent, false);
            _bodyFrame.SetSiblingIndex(body.GetSiblingIndex());
            _bodyFrame.anchorMin = body.anchorMin;
            _bodyFrame.anchorMax = body.anchorMax;
            _bodyFrame.offsetMin = body.offsetMin;
            _bodyFrame.offsetMax = body.offsetMax;
            _bodyFrame.pivot = body.pivot;
            var hit = frameGo.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);   // invisible, only catches drags

            float size = _bodyText.enableAutoSizing ? Mathf.Max(_bodyText.fontSizeMax, _bodyText.fontSize) : _bodyText.fontSize;
            if (size > 48f) size = 34f;
            _bodyText.enableAutoSizing = false;   // long lines scroll instead of shrinking
            _bodyText.fontSize = size;
            _bodyText.overflowMode = TextOverflowModes.Overflow;
            _bodyText.raycastTarget = false;
            body.SetParent(_bodyFrame, false);
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = body.offsetMax = Vector2.zero;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _bodyScroll = frameGo.GetComponent<ScrollRect>();
            _bodyScroll.viewport = _bodyFrame;
            _bodyScroll.content = body;
            _bodyScroll.horizontal = false;
            _bodyScroll.vertical = true;
            _bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            _bodyScroll.scrollSensitivity = 30f;
        }

        private void SetupNextIndicator()
        {
            if (_boxImage == null) return;
            var go = new GameObject("NextIndicator", typeof(RectTransform), typeof(Image));
            _nextIndicator = (RectTransform)go.transform;
            _nextIndicator.SetParent(_boxImage.transform, false);
            _nextIndicator.anchorMin = _nextIndicator.anchorMax = _nextIndicatorAnchor;
            _nextIndicator.sizeDelta = new Vector2(_nextIndicatorSize, _nextIndicatorSize * 0.75f);
            var img = go.GetComponent<Image>();
            img.sprite = TriangleSprite();
            img.color = new Color(_nextIndicatorColor.r, _nextIndicatorColor.g, _nextIndicatorColor.b, 0f);
            img.raycastTarget = false;
            _nextIndicatorImage = img;
            go.SetActive(false);
        }

        /// <summary>A soft-edged ▼, generated once.</summary>
        private static Sprite TriangleSprite()
        {
            if (s_triangle != null) return s_triangle;
            const int w = 64, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Apex at the bottom middle, flat edge at the top.
                    float fy = (y + 0.5f) / h;                 // 0 bottom … 1 top
                    float half = fy * 0.5f * w;                // half-width at this height
                    float dx = Mathf.Abs(x + 0.5f - w * 0.5f);
                    float edge = Mathf.Clamp01(half - dx + 0.5f) * Mathf.Clamp01((h - 1 - y) + 0.5f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(255f * edge));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            s_triangle = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return s_triangle;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_timeFrozen) Time.timeScale = _timeScaleBeforeReview;
            if (IsPaused) AudioListener.pause = false;
        }

        // ───────────────────────── Public API ─────────────────────────

        public void StartDialogue(List<DialogueLine> dialogue, Action onComplete = null)
        {
            if (_dialoguePanel == null)
            {
                Debug.LogWarning("[DialogueManager] Dialogue panel not assigned!");
                onComplete?.Invoke();
                return;
            }

            _onDialogueComplete = onComplete;
            ResetPlaybackDefaults();   // every dialogue starts with auto off at 1×
            _queue.Clear();
            foreach (DialogueLine line in dialogue) _queue.Enqueue(line);
            _lineIndex = 0;

            Mechanics.PlayerController.Instance?.SetMovementEnabled(false);
            Mechanics.InputManager.Instance?.SetInputEnabled(false);
            Mechanics.DialogueCameraPanner.Instance?.StartDialogueSequence();

            bool wasOpen = _dialoguePanel.activeSelf;
            _dialoguePanel.SetActive(true);
            if (!wasOpen)
            {
                // A new conversation: the review only ever shows this one.
                _history.Clear();
                ConversationId++;
                SetPaused(false);
                EventBus.Publish(new DialogueStartedEvent());
            }
            DisplayNextSentence();
        }

        public void DisplayNextSentence()
        {
            if (_queue.Count == 0)
            {
                EndDialogue();
                return;
            }

            DialogueLine line = _queue.Dequeue();
            _currentSentence = ResolveTokens(line.text);
            string speaker = ResolveTokens(string.IsNullOrEmpty(line.speakerName) ? (line.isPlayer ? "{player}" : "") : line.speakerName);

            ApplyLayout(line.isPlayer);
            if (_nameText != null) _nameText.text = speaker;
            ApplyPortrait(line.isPlayer ? null : (line.portrait != null ? line.portrait : PortraitFor(speaker)), line.isPlayer ? 1f : PortraitScaleFor(speaker));

            _history.Add(new HistoryEntry { Speaker = speaker, Text = _currentSentence, IsPlayer = line.isPlayer });

            Mechanics.DialogueCameraPanner.Instance?.PanToSpeaker(line.isPlayer);

            if (_bodyText != null)
            {
                _bodyText.text = _currentSentence;
                _bodyText.maxVisibleCharacters = 0;
            }
            if (_bodyScroll != null) { _bodyScroll.StopMovement(); _bodyScroll.verticalNormalizedPosition = 1f; }
            _typedChars = 0f;
            _isTyping = true;
            _autoTimer = 0f;
            StateChanged?.Invoke();
            int index = _lineIndex++;   // counted even with no listeners (?. would skip the ++)
            LineStarted?.Invoke(index);
        }

        public void ToggleAuto()
        {
            IsAuto = !IsAuto;
            _autoTimer = 0f;
            StateChanged?.Invoke();
        }

        public void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            StateChanged?.Invoke();
        }

        private void ResetPlaybackDefaults()
        {
            IsAuto = false;
            _speedIndex = 0;
            _autoTimer = 0f;
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>
        /// Pause freezes everything: the world (timeScale 0), all sound, typing and auto-advance.
        /// The pause button turns into play; pressing it resumes exactly where things were.
        /// </summary>
        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) { StateChanged?.Invoke(); return; }
            IsPaused = paused;
            AudioListener.pause = paused;
            ApplyFreeze();
            StateChanged?.Invoke();
        }

        /// <summary>Opens/closes the review log. The scene behind it is paused while it's open.</summary>
        public void SetReviewOpen(bool open)
        {
            if (open == IsReviewOpen) return;
            IsReviewOpen = open;
            ApplyFreeze();
            StateChanged?.Invoke();
        }

        private bool _timeFrozen;

        // Pause and the review log both stop the world; time comes back only when neither is on.
        private void ApplyFreeze()
        {
            bool frozen = IsPaused || IsReviewOpen;
            if (frozen == _timeFrozen) return;
            _timeFrozen = frozen;
            if (frozen)
            {
                _timeScaleBeforeReview = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _timeScaleBeforeReview;
            }
        }

        // ───────────────────────── Update ─────────────────────────

        private void Update()
        {
            if (!IsOpen) return;

            bool frozen = IsPaused || IsReviewOpen;

            if (_isTyping && !frozen) TickTyping();
            else if (!_isTyping && IsAuto && !frozen) TickAuto();
            UpdateNextIndicator(frozen);

            if (frozen) { _pressed = false; return; }

            bool advancePressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || TappedBox();
            if (!advancePressed) return;

            Lalim.Core.Sfx.Play(Lalim.Core.SfxCue.ButtonTap);   // the dialogue box is a button too
            if (_isTyping) FinishTyping();
            else DisplayNextSentence();
        }

        private void TickTyping()
        {
            int before = Mathf.FloorToInt(_typedChars);
            _typedChars += Time.unscaledDeltaTime * _charsPerSecond * Speed;
            int shown = Mathf.Min(Mathf.FloorToInt(_typedChars), _currentSentence.Length);
            if (_bodyText != null) _bodyText.maxVisibleCharacters = shown;
            KeepTypingInView(shown);

            if (_typeSound != null && shown / 3 != before / 3) GameAudio.PlayOneShot(_audioSource, _typeSound, 0.5f);

            if (shown >= _currentSentence.Length) FinishTyping();
        }

        private void FinishTyping()
        {
            _isTyping = false;
            _autoTimer = 0f;
            if (_bodyText != null) _bodyText.maxVisibleCharacters = _currentSentence.Length;
        }

        // A tap = press and release without dragging (a drag scrolls a long line instead), not on a button.
        private bool TappedBox()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _pressed = !IsPointerOverUIButton();
                _pressPosition = Input.mousePosition;
                return false;
            }
            if (!Input.GetMouseButtonUp(0) || !_pressed) return false;
            _pressed = false;
            float slop = Mathf.Max(12f, Screen.height * 0.02f);
            return ((Vector2)Input.mousePosition - _pressPosition).sqrMagnitude <= slop * slop;
        }

        // While a long line types, scroll so the line being typed stays in view.
        private void KeepTypingInView(int shown)
        {
            if (_bodyScroll == null || _bodyText == null || shown <= 0) return;
            _bodyText.ForceMeshUpdate();
            TMP_TextInfo info = _bodyText.textInfo;
            if (info.characterCount == 0) return;
            TMP_CharacterInfo last = info.characterInfo[Mathf.Min(shown, info.characterCount) - 1];
            RectTransform content = _bodyScroll.content;
            float contentHeight = content.rect.height, viewHeight = _bodyFrame.rect.height;
            if (contentHeight <= viewHeight) return;
            // Local y of the typed line's bottom, measured down from the top of the text.
            float depth = content.rect.yMax - last.descender;
            float needed = Mathf.Clamp(depth - viewHeight, 0f, contentHeight - viewHeight);
            if (needed > content.anchoredPosition.y)
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, needed);
        }

        // The ▼ fades in once a line has finished typing (tap for the next) and floats gently.
        private void UpdateNextIndicator(bool frozen)
        {
            if (_nextIndicator == null) return;
            bool show = !_isTyping;
            if (!show)
            {
                _nextIndicatorAlpha = 0f;
                if (_nextIndicator.gameObject.activeSelf) _nextIndicator.gameObject.SetActive(false);
                return;
            }
            if (!_nextIndicator.gameObject.activeSelf) _nextIndicator.gameObject.SetActive(true);
            if (!frozen) _nextIndicatorAlpha = Mathf.MoveTowards(_nextIndicatorAlpha, 1f, Time.unscaledDeltaTime / _nextIndicatorFadeSeconds);
            Color c = _nextIndicatorColor;
            c.a *= _nextIndicatorAlpha;
            _nextIndicatorImage.color = c;
            if (!frozen) _nextIndicator.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.unscaledTime * _nextIndicatorBobSpeed) * _nextIndicatorBob);
        }

        private void TickAuto()
        {
            _autoTimer += Time.unscaledDeltaTime;
            float delay = (_autoDelayBase + _currentSentence.Length * _autoDelayPerChar) / Speed;
            if (_autoTimer >= delay) DisplayNextSentence();
        }

        public Sprite PortraitFor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            foreach (SpeakerPortrait p in _speakerPortraits)
                if (p.sprite != null && string.Equals(p.speaker, speaker, StringComparison.OrdinalIgnoreCase)) return p.sprite;
            return null;
        }

        public float PortraitScaleFor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return 1f;
            foreach (SpeakerPortrait p in _speakerPortraits)
                if (string.Equals(p.speaker, speaker, StringComparison.OrdinalIgnoreCase)) return p.scale > 0f ? p.scale : 1f;
            return 1f;
        }

        private bool _portraitBaseCached;
        private Vector2 _portraitBasePos;

        private bool _bodyDefaultsCached;
        private Vector2 _bodyAnchorMin;
        private Vector2 _bodyOffsetMin;

        // The portrait sits left of the name strip, so the body text moves over to line up with the name.
        private void ApplyPortrait(Sprite portrait, float scale = 1f)
        {
            bool show = portrait != null && _npcPortraitImage != null;
            if (_npcPortraitImage != null)
            {
                _npcPortraitImage.gameObject.SetActive(show);
                if (show) _npcPortraitImage.sprite = portrait;
                // A bigger bust (e.g. Bakunawa) grows up from the box and out to the left, so its
                // right edge (and the text beside it) stays where it was.
                RectTransform rt = _npcPortraitImage.rectTransform;
                if (!_portraitBaseCached) { _portraitBasePos = rt.anchoredPosition; _portraitBaseCached = true; }
                scale = Mathf.Max(0.1f, scale);
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = _portraitBasePos - new Vector2(rt.rect.width * (scale - 1f) * (1f - rt.pivot.x), 0f);
            }
            if (_bodyText == null) return;
            RectTransform body = _bodyFrame != null ? _bodyFrame : _bodyText.rectTransform;   // the scroll viewport moves, not the text
            if (!_bodyDefaultsCached)
            {
                _bodyAnchorMin = body.anchorMin;
                _bodyOffsetMin = body.offsetMin;
                _bodyDefaultsCached = true;
            }
            body.anchorMin = show ? new Vector2(Mathf.Max(_bodyAnchorMin.x, _bodyLeftWithPortrait), _bodyAnchorMin.y) : _bodyAnchorMin;
            body.offsetMin = show ? new Vector2(_bodyInsetWithPortrait, _bodyOffsetMin.y) : _bodyOffsetMin;
        }

        private void ApplyLayout(bool isPlayer)
        {
            if (_boxImage != null)
            {
                Sprite sprite = isPlayer ? _playerBoxSprite : _npcBoxSprite;
                if (sprite != null) _boxImage.sprite = sprite;
            }
            if (_nameBox != null)
            {
                _nameBox.anchorMin = isPlayer ? _playerNameMin : _npcNameMin;
                _nameBox.anchorMax = isPlayer ? _playerNameMax : _npcNameMax;
            }
            if (_nameText != null)
            {
                _nameText.alignment = isPlayer ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            }
        }

        private static bool IsPointerOverUIButton()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits)
            {
                if (h.gameObject.GetComponentInParent<Selectable>() != null) return true;
            }
            return false;
        }

        private void EndDialogue()
        {
            SetReviewOpen(false);
            SetPaused(false);
            _isTyping = false;
            _dialoguePanel.SetActive(false);

            Mechanics.PlayerController.Instance?.SetMovementEnabled(true);
            Mechanics.InputManager.Instance?.SetInputEnabled(true);
            Mechanics.DialogueCameraPanner.Instance?.EndDialogueSequence();

            EventBus.Publish(new DialogueEndedEvent());
            StateChanged?.Invoke();

            // Clear before invoking: the callback may start the next dialogue.
            Action callback = _onDialogueComplete;
            _onDialogueComplete = null;
            callback?.Invoke();
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject panel, Image box, Sprite npc, Sprite player, RectTransform nameBox,
                                TMP_Text nameText, TMP_Text bodyText, GameObject pausedBadge)
        {
            _dialoguePanel = panel;
            _boxImage = box;
            _npcBoxSprite = npc;
            _playerBoxSprite = player;
            _nameBox = nameBox;
            _nameText = nameText;
            _bodyText = bodyText;
            _pausedBadge = pausedBadge;
        }

        public void EditorSetPortraits(Image portraitImage, List<SpeakerPortrait> portraits)
        {
            _npcPortraitImage = portraitImage;
            _speakerPortraits = portraits;
        }
#endif
    }
}
