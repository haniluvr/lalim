using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// The four dialogue buttons, top-left, in order: Review, Auto, Speed, Pause.
    /// Visible only while a dialogue is open (the game HUD is hidden then). All state lives in
    /// DialogueManager; this only forwards taps and reflects state:
    ///  - Auto: a spinner turns around the icon while auto-advance is on;
    ///  - Speed: the current speed (1, 1.2, 1.5, 2) in the circle;
    ///  - Pause: the icon toggles to Play while paused.
    /// Icons keep their own colours (no tint for active states). The review overlay sits above
    /// this row, so it covers the buttons while open; it closes with its own X.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueHUD : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _reviewButton;
        [SerializeField] private Button _autoButton;
        [SerializeField] private Button _speedButton;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Image _autoIcon;
        [SerializeField] private Image _reviewIcon;
        [SerializeField] private Image _pauseIcon;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private ReviewOverlayUI _review;

        [Header("Pause / Play")]
        [SerializeField] private Sprite _pauseSprite;
        [SerializeField] private Sprite _playSprite;

        [Header("Auto spinner")]
        [Tooltip("Ring arc around the Auto icon; created at runtime when empty.")]
        [SerializeField] private Image _autoSpinner;
        [SerializeField] private float _spinDegreesPerSecond = 220f;

        private DialogueManager _dm;
        private static Sprite s_arcSprite;

        private void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _reviewButton?.onClick.AddListener(() => _dm?.SetReviewOpen(!_dm.IsReviewOpen));
            _autoButton?.onClick.AddListener(() => _dm?.ToggleAuto());
            _speedButton?.onClick.AddListener(() => _dm?.CycleSpeed());
            _pauseButton?.onClick.AddListener(() => _dm?.TogglePause());
            if (_pauseSprite == null && _pauseIcon != null) _pauseSprite = _pauseIcon.sprite;
            if (_autoSpinner == null && _autoButton != null) _autoSpinner = CreateSpinner(_autoButton.transform);
            SetVisible(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
            if (_dm != null) _dm.StateChanged -= Refresh;
        }

        private void Start()
        {
            _dm = DialogueManager.Instance;
            if (_dm != null) _dm.StateChanged += Refresh;
            Refresh();
        }

        private void Update()
        {
            // Spins on unscaled time: the scene may be paused while the review log is open.
            if (_autoSpinner != null && _autoSpinner.enabled)
            {
                _autoSpinner.rectTransform.Rotate(0f, 0f, -_spinDegreesPerSecond * Time.unscaledDeltaTime);
            }
        }

        private void OnDialogueStarted(DialogueStartedEvent evt) { SetVisible(true); Refresh(); }
        private void OnDialogueEnded(DialogueEndedEvent evt) { SetVisible(false); Refresh(); }

        private void SetVisible(bool visible)
        {
            if (_group == null) return;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
        }

        private void Refresh()
        {
            if (_dm == null) _dm = DialogueManager.Instance;
            if (_dm == null) return;

            if (_autoSpinner != null) _autoSpinner.enabled = _dm.IsAuto;
            if (_pauseIcon != null)
            {
                Sprite sprite = _dm.IsPaused && _playSprite != null ? _playSprite : _pauseSprite;
                if (sprite != null) _pauseIcon.sprite = sprite;
            }
            if (_speedLabel != null) _speedLabel.text = FormatSpeed(_dm.Speed);
            if (_review != null) _review.SetOpen(_dm.IsReviewOpen, _dm.History);
        }

        /// <summary>"1", "1.2", "1.5", "2".</summary>
        public static string FormatSpeed(float speed) => speed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        private static Image CreateSpinner(Transform button)
        {
            var go = new GameObject("AutoSpinner", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(button, false);
            go.transform.SetAsFirstSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-14f, -14f);
            rt.offsetMax = new Vector2(14f, 14f);
            var img = go.GetComponent<Image>();
            img.sprite = ArcSprite();
            img.raycastTarget = false;
            img.color = Color.white;
            img.enabled = false;
            return img;
        }

        /// <summary>A 270° anti-aliased ring arc, generated once.</summary>
        private static Sprite ArcSprite()
        {
            if (s_arcSprite != null) return s_arcSprite;
            const int size = 128;
            const float outer = 62f, inner = 54f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            Vector2 c = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                    float d = p.magnitude;
                    float edge = Mathf.Clamp01(Mathf.Min(outer - d, d - inner) + 0.5f);
                    float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
                    // Fade the arc's tail so it reads as motion.
                    float arc = angle <= 270f ? Mathf.Lerp(0.15f, 1f, angle / 270f) : 0f;
                    byte a = (byte)(255f * edge * arc);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            s_arcSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return s_arcSprite;
        }

#if UNITY_EDITOR
        public void EditorSetPauseSprites(Sprite pause, Sprite play)
        {
            _pauseSprite = pause;
            _playSprite = play;
        }
#endif
    }
}
