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
    ///  - Review: log of every line shown so far in this scene (never future lines); the scene is
    ///    paused (timeScale 0) while it's open.
    ///  - Auto: advances by itself after each line finishes typing.
    ///  - Speed: 1 → 1.2 → 1.5 → 2× typing (and auto delay). Never skips lines.
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

        public bool IsOpen => _dialoguePanel != null && _dialoguePanel.activeSelf;
        public bool IsAuto { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsReviewOpen { get; private set; }
        public int SpeedIndex => _speedIndex;
        public float Speed => Speeds[_speedIndex];
        public IReadOnlyList<HistoryEntry> History => _history;

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

            IsAuto = PlayerPrefs.GetInt(PrefAuto, 0) == 1;
            _speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefSpeed, 0), 0, Speeds.Length - 1);

            if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
            if (_pausedBadge != null) _pausedBadge.SetActive(false);   // legacy "PAUSED" label: never shown
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
            ApplyPortrait(line.isPlayer ? null : (line.portrait != null ? line.portrait : PortraitFor(speaker)));

            _history.Add(new HistoryEntry { Speaker = speaker, Text = _currentSentence, IsPlayer = line.isPlayer });

            Mechanics.DialogueCameraPanner.Instance?.PanToSpeaker(line.isPlayer);

            if (_bodyText != null)
            {
                _bodyText.text = _currentSentence;
                _bodyText.maxVisibleCharacters = 0;
            }
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
            PlayerPrefs.SetInt(PrefAuto, IsAuto ? 1 : 0);
            StateChanged?.Invoke();
        }

        public void CycleSpeed()
        {
            _speedIndex = (_speedIndex + 1) % Speeds.Length;
            PlayerPrefs.SetInt(PrefSpeed, _speedIndex);
            StateChanged?.Invoke();
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

            if (frozen) return;

            bool advancePressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E)
                                  || (Input.GetMouseButtonDown(0) && !IsPointerOverUIButton());
            if (!advancePressed) return;

            if (_isTyping) FinishTyping();
            else DisplayNextSentence();
        }

        private void TickTyping()
        {
            int before = Mathf.FloorToInt(_typedChars);
            _typedChars += Time.unscaledDeltaTime * _charsPerSecond * Speed;
            int shown = Mathf.Min(Mathf.FloorToInt(_typedChars), _currentSentence.Length);
            if (_bodyText != null) _bodyText.maxVisibleCharacters = shown;

            if (_typeSound != null && shown / 3 != before / 3) GameAudio.PlayOneShot(_audioSource, _typeSound, 0.5f);

            if (shown >= _currentSentence.Length) FinishTyping();
        }

        private void FinishTyping()
        {
            _isTyping = false;
            _autoTimer = 0f;
            if (_bodyText != null) _bodyText.maxVisibleCharacters = _currentSentence.Length;
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

        private bool _bodyDefaultsCached;
        private Vector2 _bodyAnchorMin;
        private Vector2 _bodyOffsetMin;

        // The portrait sits left of the name strip, so the body text moves over to line up with the name.
        private void ApplyPortrait(Sprite portrait)
        {
            bool show = portrait != null && _npcPortraitImage != null;
            if (_npcPortraitImage != null)
            {
                _npcPortraitImage.gameObject.SetActive(show);
                if (show) _npcPortraitImage.sprite = portrait;
            }
            if (_bodyText == null) return;
            RectTransform body = _bodyText.rectTransform;
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
