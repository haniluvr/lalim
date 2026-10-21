using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.UI;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// Minigame 3's only screen piece, built at runtime in the minigames' glass style: the Compass
    /// bar (top of the screen, only while the Compass is equipped), a glowing bar with a marker
    /// sliding toward the nearest sanctuary the diver hasn't visited yet, with its name and
    /// distance. Within about 60 m it fades away: from there the place's glow and song take over,
    /// and the schools go home by themselves. No cards or questions on screen.
    /// </summary>
    public class RelocationHud : MonoBehaviour
    {
        private const float BarWidth = 640f;

        private TMP_FontAsset _title, _body;
        private CanvasGroup _compass;
        private RectTransform _marker, _left, _right;
        private TMP_Text _compassLabel;
        private float _compassAlpha;
        private bool _compassOn;

        public static RelocationHud Create(TMP_FontAsset title, TMP_FontAsset body)
        {
            var go = new GameObject("[RelocationHud]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 35;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var hud = go.AddComponent<RelocationHud>();
            hud._title = title;
            hud._body = body;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            // ── Compass bar ──
            RectTransform bar = Rect("Compass", transform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(BarWidth, 74f));
            _compass = bar.gameObject.AddComponent<CanvasGroup>();
            _compass.blocksRaycasts = false;
            _compass.alpha = 0f;
            ZoneBannerUI.Avoid(bar);   // banners ("The tuna are home…") drop below the compass while it shows
            Sliced(bar, GlassSprites.Rounded, new Color(0.06f, 0.22f, 0.32f, 0.55f), 0.6f);
            Sliced(bar, GlassSprites.RoundedOutline, new Color(0.6f, 0.95f, 1f, 0.6f), 0.6f);
            RectTransform track = Rect("Track", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(BarWidth - 70f, 6f));
            Sliced(track, GlassSprites.Rounded, new Color(Sanctuary.BeaconColor.r, Sanctuary.BeaconColor.g, Sanctuary.BeaconColor.b, 0.45f), 0.1f);
            RectTransform centre = Rect("Ahead", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(3f, 22f));
            centre.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);
            _marker = Rect("Marker", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(30f, 30f));
            var m = _marker.gameObject.AddComponent<Image>();
            m.sprite = GlassSprites.Circle;
            m.color = Sanctuary.BeaconColor;
            m.raycastTarget = false;
            _left = Chevron(bar, "Left", new Vector2(-BarWidth * 0.5f + 22f, 14f), 90f);
            _right = Chevron(bar, "Right", new Vector2(BarWidth * 0.5f - 22f, 14f), -90f);
            _compassLabel = Text(Rect("Label", bar, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(BarWidth - 40f, 30f)), "", 22f, _body, new Color(1f, 1f, 1f, 0.9f));

        }

        // ───────────────────────── Compass ─────────────────────────

        /// <summary>Hidden when <paramref name="target"/> is null, and fades away within <paramref name="closeRange"/>.</summary>
        public void SetCompass(bool visible, Sanctuary target, Transform viewer, float closeRange)
        {
            _compassOn = visible && target != null && viewer != null && Vector3.Distance(target.Centre, viewer.position) > closeRange;
            if (!_compassOn) return;
            Vector3 to = target.Centre - viewer.position;
            float dist = to.magnitude;
            Vector3 fwd = viewer.forward; fwd.y = 0f;
            Vector3 flat = to; flat.y = 0f;
            float angle = fwd.sqrMagnitude > 0.001f && flat.sqrMagnitude > 0.001f ? Vector3.SignedAngle(fwd, flat, Vector3.up) : 0f;
            float x = Mathf.Clamp(angle / 90f, -1f, 1f) * (BarWidth * 0.5f - 50f);
            _marker.anchoredPosition = new Vector2(Mathf.Lerp(_marker.anchoredPosition.x, x, Time.unscaledDeltaTime * 8f), 14f);
            _left.gameObject.SetActive(angle < -90f);
            _right.gameObject.SetActive(angle > 90f);
            string depth = to.y < -8f ? " · below" : to.y > 8f ? " · above" : "";
            _compassLabel.text = $"{target.DisplayName} · {dist:0} m{depth}";
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            bool toast = ActToastUI.IsBusy;
            _compassAlpha = Mathf.MoveTowards(_compassAlpha, _compassOn && !toast ? 1f : 0f, dt * 4f);
            _compass.alpha = _compassAlpha * (0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.4f));   // a soft glow pulse
        }

        // ───────────────────────── Helpers ─────────────────────────

        private RectTransform Chevron(RectTransform parent, string name, Vector2 pos, float rot)
        {
            RectTransform c = Rect(name, parent, new Vector2(0.5f, 0.5f), pos, new Vector2(34f, 34f));
            c.localRotation = Quaternion.Euler(0f, 0f, rot);
            var img = c.gameObject.AddComponent<Image>();
            img.sprite = GlassSprites.Chevron;
            img.color = Sanctuary.BeaconColor;
            img.raycastTarget = false;
            c.gameObject.SetActive(false);
            return c;
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

        private static Image Sliced(RectTransform parent, Sprite sprite, Color color, float cornerScale)
        {
            RectTransform rt = Rect("Glass", parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / cornerScale;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text Text(RectTransform rt, string text, float size, TMP_FontAsset font, Color color)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
