using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// The minigames' hint button: a light bulb with a badge counting the hints left, greyed out
    /// when none remain. Sits flush with the left edge of a minigame's glass card behind a hairline
    /// (<see cref="AttachToCard"/>), or stands on its own as a small glass button
    /// (<see cref="CreateButton"/>). The minigame decides what a hint does and spends it through
    /// ChallengeManager.UseHint (which also plays the hint sound).
    /// </summary>
    public class HintBulb : MonoBehaviour
    {
        /// <summary>Width the bulb takes at the card's left edge.</summary>
        public const float CardZone = 118f;

        private Image _bulb;
        private RectTransform _badge;
        private TMP_Text _badgeText;
        private Button _button;

        /// <summary>
        /// A bulb zone at the left edge of <paramref name="card"/> (a card of the given height), split
        /// off by a hairline. Widen the card by <see cref="CardZone"/> and shift its content right by
        /// half of it to make room.
        /// </summary>
        public static HintBulb AttachToCard(RectTransform card, float cardHeight, Action onHint, TMP_FontAsset font)
        {
            RectTransform zone = Rect("HintButton", card, new Vector2(0f, 0.5f), new Vector2(CardZone * 0.5f, 0f), new Vector2(CardZone, cardHeight));
            HintBulb bulb = Build(zone, onHint, font);
            RectTransform divider = Rect("Divider", card, new Vector2(0f, 0.5f), new Vector2(CardZone, 0f), new Vector2(2f, cardHeight - 36f));
            var div = divider.gameObject.AddComponent<Image>();
            div.color = new Color(0.78f, 0.96f, 1f, 0.28f);
            div.raycastTarget = false;
            return bulb;
        }

        /// <summary>A free-standing glass button with the bulb, centred on <paramref name="pos"/> in <paramref name="parent"/>.</summary>
        public static HintBulb CreateButton(RectTransform parent, Vector2 pos, float size, Action onHint, TMP_FontAsset font)
        {
            RectTransform zone = Rect("HintButton", parent, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            Sliced(zone, GlassSprites.Rounded, new Color(1f, 1f, 1f, 0.14f));
            Sliced(zone, GlassSprites.RoundedOutline, new Color(0.78f, 0.96f, 1f, 0.6f));
            return Build(zone, onHint, font);
        }

        private static HintBulb Build(RectTransform zone, Action onHint, TMP_FontAsset font)
        {
            var hit = zone.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            var bulb = zone.gameObject.AddComponent<HintBulb>();
            bulb._button = zone.gameObject.AddComponent<Button>();
            bulb._button.transition = Selectable.Transition.None;
            bulb._button.onClick.AddListener(() => onHint?.Invoke());

            RectTransform icon = Rect("Bulb", zone, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            bulb._bulb = icon.gameObject.AddComponent<Image>();
            bulb._bulb.sprite = GlassSprites.Bulb;
            bulb._bulb.raycastTarget = false;

            bulb._badge = Rect("Count", zone, new Vector2(0.5f, 0.5f), new Vector2(26f, 24f), new Vector2(32f, 32f));
            var badgeImg = bulb._badge.gameObject.AddComponent<Image>();
            badgeImg.sprite = GlassSprites.Circle;
            badgeImg.color = new Color(0.55f, 1f, 0.75f, 0.95f);
            badgeImg.raycastTarget = false;

            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(bulb._badge, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(32f, 32f);
            bulb._badgeText = go.GetComponent<TextMeshProUGUI>();
            if (font != null) bulb._badgeText.font = font;
            bulb._badgeText.fontSize = 20f;
            bulb._badgeText.color = new Color(0.03f, 0.14f, 0.2f);
            bulb._badgeText.alignment = TextAlignmentOptions.Center;
            bulb._badgeText.textWrappingMode = TextWrappingModes.NoWrap;
            bulb._badgeText.raycastTarget = false;
            return bulb;
        }

        /// <summary>Shows how many hints are left; none left = greyed out and untappable.</summary>
        public void SetHints(int left)
        {
            _badgeText.text = left.ToString();
            _badge.gameObject.SetActive(left > 0);
            _button.interactable = left > 0;
            _bulb.color = left > 0 ? new Color(1f, 0.93f, 0.55f, 1f) : new Color(1f, 1f, 1f, 0.3f);
        }

        /// <summary>Hints left in the running challenge (or the minigame's budget before it starts).</summary>
        public static int Remaining(int budget)
        {
            var cm = Lalim.Mechanics.ChallengeManager.Instance;
            return cm != null && cm.IsChallengeActive ? cm.HintsRemaining : budget;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Sliced(RectTransform parent, Sprite sprite, Color color)
        {
            RectTransform rt = Rect("Glass", parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / 0.85f;
            img.color = color;
            img.raycastTarget = false;
        }
    }
}
