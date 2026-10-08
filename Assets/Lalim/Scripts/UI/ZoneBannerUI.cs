using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Light, text-only banner for short warnings: a locked barrier, the wrong tool, a living coral.
    /// Deliberately NOT the achievement toast sprite (ActToastUI), and it always gives way to it.
    ///
    /// A frosted-glass card (like the minigame instruction cards) at the top of the screen, sized
    /// to its message. It stays clear of the quest box — measured fully open, even while closed —
    /// by narrowing (the text wraps) or, on narrow screens, sliding right of it.
    ///
    /// Builds its own overlay canvas if none is assigned, so it works in any scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneBannerUI : MonoBehaviour
    {
        public static ZoneBannerUI Instance { get; private set; }

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _text;
        [SerializeField, Min(0f)] private float _fadeSeconds = 0.5f;
        [SerializeField, Min(0.5f)] private float _holdSeconds = 3.5f;
        [SerializeField] private TMP_FontAsset _font;

        private const float TopMargin = 36f, PadX = 40f, PadY = 22f, MinWidth = 360f, MaxWidth = 900f;
        private const float QuestGap = 40f, RightReserve = 180f;   // right: the pause button

        private readonly Queue<string> _queue = new Queue<string>();
        private bool _playing;

        // On-screen cards (minigame instructions) the banner must not cover: it drops just below them.
        private static readonly List<RectTransform> Avoided = new List<RectTransform>();
        private const float AvoidGap = 16f;

        /// <summary>Keep banners below this card while it's on screen (e.g. a minigame's instruction card).</summary>
        public static void Avoid(RectTransform card)
        {
            if (card != null && !Avoided.Contains(card)) Avoided.Add(card);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_group == null || _text == null) BuildDefault();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Shows a banner; tolerates a missing instance by creating one.</summary>
        public static void Post(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (Instance == null) new GameObject("[ZoneBannerUI]").AddComponent<ZoneBannerUI>();
            Instance.Enqueue(message);
        }

        public void Enqueue(string message)
        {
            _queue.Enqueue(message);
            if (!_playing) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _playing = true;
            while (_queue.Count > 0)
            {
                // Toasts come first: wait for dialogue and for any toast to finish.
                while ((DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) || ActToastUI.IsBusy) yield return null;

                _text.text = _queue.Dequeue();
                Layout(_text.text);
                yield return Fade(0f, 1f);
                // A toast arriving mid-banner cuts it short.
                for (float t = 0f; t < _holdSeconds && !ActToastUI.IsBusy; t += Lalim.Core.PauseClock.DeltaTime) yield return null;
                yield return Fade(1f, 0f);
            }
            _playing = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < _fadeSeconds)
            {
                t += Lalim.Core.PauseClock.DeltaTime;
                _group.alpha = Mathf.Lerp(from, to, t / _fadeSeconds);
                yield return null;
            }
            _group.alpha = to;
        }

        // Sizes the card to the message and keeps it clear of the (open) quest box.
        private void Layout(string message)
        {
            var card = _group.transform as RectTransform;
            var canvas = card != null ? card.GetComponentInParent<Canvas>() : null;
            if (card == null || canvas == null) return;
            var canvasRt = (RectTransform)canvas.rootCanvas.transform;
            Camera cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            float width = canvasRt.rect.width;
            float centre = width * 0.5f;

            float left = 24f;
            if (QuestObjectiveUI.Instance != null && QuestObjectiveUI.Instance.TryGetOpenScreenRect(out Rect quest) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, new Vector2(quest.xMax, quest.yMax), cam, out Vector2 edge))
            {
                left = Mathf.Max(left, edge.x + width * canvasRt.pivot.x + QuestGap);
            }
            float right = width - RightReserve;

            float oneLine = _text.GetPreferredValues(message).x;
            float cardWidth = Mathf.Clamp(oneLine + PadX * 2f, MinWidth, MaxWidth);
            float roomCentred = 2f * Mathf.Min(centre - left, right - centre);
            float x = centre;
            if (cardWidth > roomCentred)
            {
                if (roomCentred >= 520f) cardWidth = roomCentred;   // stay centred, wrap the text
                else
                {
                    cardWidth = Mathf.Min(cardWidth, right - left);  // narrow screen: sit right of the quest box
                    x = left + cardWidth * 0.5f;
                }
            }
            float textHeight = _text.GetPreferredValues(message, cardWidth - PadX * 2f, 0f).y;

            card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(cardWidth, Mathf.Max(84f, textHeight + PadY * 2f));
            card.anchoredPosition = new Vector2(x - centre, -TopBelowAvoided(canvasRt, cam));
        }

        // Distance from the top of the screen to place the banner: the usual margin, or just under
        // the lowest on-screen card it has been asked to avoid.
        private static float TopBelowAvoided(RectTransform canvasRt, Camera cam)
        {
            float top = TopMargin;
            var corners = new Vector3[4];
            for (int i = Avoided.Count - 1; i >= 0; i--)
            {
                RectTransform r = Avoided[i];
                if (r == null) { Avoided.RemoveAt(i); continue; }
                if (!r.gameObject.activeInHierarchy) continue;
                CanvasGroup g = r.GetComponentInParent<CanvasGroup>();
                if (g != null && g.alpha < 0.05f) continue;
                Canvas c = r.GetComponentInParent<Canvas>();
                if (c == null) continue;
                Camera rCam = c.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.rootCanvas.worldCamera;
                r.GetWorldCorners(corners);   // 0 = bottom-left
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(rCam, corners[0]);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, cam, out Vector2 local))
                    top = Mathf.Max(top, canvasRt.rect.yMax - local.y + AvoidGap);
            }
            return top;
        }

        private void BuildDefault()
        {
            var canvasGo = new GameObject("ZoneBannerCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Frosted-glass card: tinted fill, a sheen along the top, a light rim.
            var card = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            card.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -TopMargin);
            rt.sizeDelta = new Vector2(MaxWidth, 84f);
            Glass(rt, "Fill", GlassSprites.Rounded, new Color(0.06f, 0.22f, 0.32f, 0.62f));
            Glass(rt, "Sheen", GlassSprites.RoundedSheen, new Color(1f, 1f, 1f, 0.9f));
            Glass(rt, "Rim", GlassSprites.RoundedOutline, new Color(0.78f, 0.96f, 1f, 0.55f));
            _group = card.GetComponent<CanvasGroup>();

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(card.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(PadX, PadY);
            trt.offsetMax = new Vector2(-PadX, -PadY);
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 30f;
            tmp.color = new Color(1f, 1f, 1f, 0.92f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            _text = tmp;
        }

        private static void Glass(RectTransform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / 0.75f;
            img.color = color;
            img.raycastTarget = false;
        }

#if UNITY_EDITOR
        public void EditorSetFont(TMP_FontAsset font) => _font = font;
#endif
    }
}
