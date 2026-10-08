using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.UI;

namespace Lalim.Challenges
{
    /// <summary>
    /// The "Water Sampling Kit" panel for the water readings minigame: a frosted-glass modal with
    /// three arc gauges (pH, Salinity, Temp). Each gauge has a sweeping needle, a green target band
    /// and a STOP button; "TESTING" checks and a COMPLETE button run along the bottom. A STOP
    /// fires the moment it's touched (not on release), and a touch anywhere on a gauge counts too,
    /// so the needle stops where the player meant it to.
    ///
    /// Built at runtime (sprites are generated, so it needs no art); WaterCalibrationMinigame drives it.
    /// </summary>
    public class WaterSensorsUI : MonoBehaviour
    {
        // Glass palette.
        private static readonly Color GlassFill = new Color(0.06f, 0.22f, 0.32f, 0.62f);
        private static readonly Color GlassEdge = new Color(0.78f, 0.96f, 1f, 0.55f);
        private static readonly Color Cream = new Color(1f, 0.93f, 0.8f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.45f);
        private static readonly Color Mint = new Color(0.62f, 1f, 0.7f);
        private static readonly Color LowArc = new Color(0.98f, 0.66f, 0.48f, 0.45f);
        private static readonly Color BandArc = new Color(0.42f, 0.88f, 0.48f, 0.8f);
        private static readonly Color HighArc = new Color(0.62f, 0.74f, 0.9f, 0.42f);
        private static readonly Color NeedleColor = new Color(1f, 0.5f, 0.25f);
        private static readonly Color MissRed = new Color(1f, 0.35f, 0.3f, 0.9f);

        private const float ArcSpan = 150f;      // degrees, symmetric about the top
        private const float ArcRadius = 175f;    // outer radius (canvas units)
        private const float ArcThickness = 48f;

        public event Action<int> StopPressed;
        public event Action CompletePressed;
        public event Action HintPressed;

        private TMP_FontAsset _titleFont, _bodyFont;
        private Sprite _handIcon;
        private TMP_Text _subtitle;
        private readonly Gauge[] _gauges = new Gauge[3];
        private Image[] _checkFills = new Image[3];
        private Image[] _checkMarks = new Image[3];
        private Button _complete;
        private CanvasGroup _completeGroup;
        private TMP_Text _completeLabel;
        private bool _completeReady;
        private HintBulb _hints;

        private class Gauge
        {
            public RectTransform Root, Needle, Button;
            public Image Band, ButtonFill;
            public TMP_Text Status, ButtonLabel;
            public Image ButtonIcon;
            public Button ButtonComp;
            public Vector2 BandRange;   // as 0..1 along the arc
            public bool Locked;
            public Coroutine Flash;
        }

        // ───────────────────────── Build ─────────────────────────

        public static WaterSensorsUI Create(Transform parent, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, Sprite handIcon)
        {
            var go = new GameObject("[SeaSensors]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Wide phones scale by height; narrower screens (tablets, editor) by width so it always fits.
            scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) >= 1.7f ? 1f : 0f;
            go.AddComponent<NoTapSound>();   // the gauges are the game, not a menu
            var ui = go.AddComponent<WaterSensorsUI>();
            ui._titleFont = titleFont;
            ui._bodyFont = bodyFont;
            ui._handIcon = handIcon;
            ui.Build();
            return ui;
        }

        private static readonly string[] Names = { "pH LEVEL", "SALINITY", "TEMP °C" };
        private static readonly string[] ButtonNames = { "STOP pH", "STOP SALINITY", "STOP TEMP" };

        private void Build()
        {
            // Frosted glass panel: tinted fill, a soft highlight across the top, a light rim.
            // Lifted a little so the HP / swim bar / shells under it stay clear.
            RectTransform panel = Rect("Panel", transform, new Vector2(0f, 12f), new Vector2(1600f, 850f));
            Sliced(panel.gameObject, GlassSprites.Rounded, GlassFill, 1f);
            RectTransform sheen = Rect("Sheen", panel, Vector2.zero, Vector2.zero);
            Stretch(sheen);
            Sliced(sheen.gameObject, GlassSprites.RoundedSheen, new Color(1f, 1f, 1f, 0.9f), 1f);
            RectTransform rim = Rect("Rim", panel, Vector2.zero, Vector2.zero);
            Stretch(rim);
            Sliced(rim.gameObject, GlassSprites.RoundedOutline, GlassEdge, 1f);

            Text(panel, "WATER SAMPLING KIT", _titleFont, 66f, Cream, new Vector2(0f, 355f), new Vector2(1200f, 90f), true);
            _subtitle = Text(panel, "", _bodyFont, 28f, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, 300f), new Vector2(1200f, 40f), false);
            RectTransform line = Rect("Divider", panel, new Vector2(0f, 268f), new Vector2(1380f, 2f));
            line.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);

            for (int i = 0; i < 3; i++) _gauges[i] = BuildGauge(panel, i, new Vector2(-500f + 500f * i, 0f));

            // Hint bulb in the panel's top-left corner.
            _hints = HintBulb.CreateButton(panel, new Vector2(-705f, 330f), 96f, () => HintPressed?.Invoke(), _titleFont);

            // Bottom row: TESTING ✓✓✓   [COMPLETE]
            Text(panel, "TESTING", _titleFont, 34f, new Color(0.85f, 0.95f, 1f, 0.9f), new Vector2(-250f, -345f), new Vector2(260f, 60f), true);
            for (int i = 0; i < 3; i++)
            {
                RectTransform c = Rect($"Check{i}", panel, new Vector2(-75f + 90f * i, -345f), new Vector2(68f, 68f));
                Simple(c.gameObject, GlassSprites.CircleOutline, new Color(1f, 1f, 1f, 0.55f));
                RectTransform fill = Rect("Fill", c, Vector2.zero, new Vector2(62f, 62f));
                _checkFills[i] = Simple(fill.gameObject, GlassSprites.Circle, new Color(0.38f, 0.82f, 0.52f, 0.95f));
                _checkFills[i].enabled = false;
                RectTransform mark = Rect("Mark", c, Vector2.zero, new Vector2(40f, 40f));
                _checkMarks[i] = Simple(mark.gameObject, GlassSprites.Check, Color.white);
                _checkMarks[i].enabled = false;
            }
            RectTransform done = Rect("Complete", panel, new Vector2(330f, -345f), new Vector2(300f, 88f));
            Sliced(done.gameObject, GlassSprites.Rounded, new Color(1f, 1f, 1f, 0.14f), 0.9f);
            RectTransform doneRim = Rect("Rim", done, Vector2.zero, Vector2.zero);
            Stretch(doneRim);
            Sliced(doneRim.gameObject, GlassSprites.RoundedOutline, new Color(0.8f, 1f, 0.85f, 0.7f), 0.9f);
            _completeLabel = Text(done, "COMPLETE", _titleFont, 34f, Mint, Vector2.zero, new Vector2(280f, 70f), true);
            _complete = done.gameObject.AddComponent<Button>();
            _complete.transition = Selectable.Transition.None;
            _complete.onClick.AddListener(() => { if (_completeReady) CompletePressed?.Invoke(); });
            _completeGroup = done.gameObject.AddComponent<CanvasGroup>();
            _completeGroup.alpha = 0.35f;
        }

        private Gauge BuildGauge(RectTransform panel, int index, Vector2 at)
        {
            var g = new Gauge();
            g.Root = Rect(Names[index], panel, at, new Vector2(460f, 560f));

            Text(g.Root, Names[index], _titleFont, 40f, Color.white, new Vector2(0f, 200f), new Vector2(440f, 56f), true);

            // Arc: rim, three coloured segments (low / band / high), ticks, needle.
            Vector2 c = new Vector2(0f, -40f);
            Arc(g.Root, "Rim", c, ArcRadius + 4f, GlassSprites.RingWide, 0f, 1f, new Color(1f, 1f, 1f, 0.5f));
            Arc(g.Root, "Low", c, ArcRadius, GlassSprites.Ring, 0f, 1f, LowArc);
            Arc(g.Root, "High", c, ArcRadius, GlassSprites.Ring, 0f, 1f, HighArc);
            g.Band = Arc(g.Root, "Band", c, ArcRadius, GlassSprites.Ring, 0.4f, 0.6f, BandArc);
            foreach (float f in new[] { 0.17f, 0.5f, 0.83f })
            {
                RectTransform tick = Rect("Tick", g.Root, ArcPoint(c, f, ArcRadius - ArcThickness * 0.5f), new Vector2(4f, 22f));
                tick.localRotation = Quaternion.Euler(0f, 0f, -ArcAngle(f));
                tick.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.6f);
            }
            g.Needle = Rect("Needle", g.Root, c, new Vector2(36f, 40f));
            g.Needle.pivot = new Vector2(0.5f, 0.5f);
            Simple(g.Needle.gameObject, GlassSprites.Triangle, NeedleColor);

            g.Status = Text(g.Root, "STOP!", _titleFont, 34f, Gold, new Vector2(0f, -110f), new Vector2(440f, 50f), true);

            // STOP button: a glass pill with the hand icon.
            g.Button = Rect("Stop", g.Root, new Vector2(0f, -200f), new Vector2(400f, 100f));
            g.ButtonFill = Sliced(g.Button.gameObject, GlassSprites.Rounded, new Color(1f, 1f, 1f, 0.16f), 0.85f);
            RectTransform bRim = Rect("Rim", g.Button, Vector2.zero, Vector2.zero);
            Stretch(bRim);
            Sliced(bRim.gameObject, GlassSprites.RoundedOutline, new Color(1f, 1f, 1f, 0.6f), 0.85f);
            RectTransform icon = Rect("Icon", g.Button, new Vector2(-125f, 0f), new Vector2(64f, 64f));
            g.ButtonIcon = Simple(icon.gameObject, _handIcon != null ? _handIcon : GlassSprites.Circle, Color.white);
            g.ButtonIcon.preserveAspect = true;
            g.ButtonLabel = Text(g.Button, ButtonNames[index], _titleFont, 32f, Color.white, new Vector2(35f, 0f), new Vector2(300f, 70f), true);
            g.ButtonComp = g.Button.gameObject.AddComponent<Button>();
            g.ButtonComp.transition = Selectable.Transition.None;
            int captured = index;
            // Stop on touch-down: on release the needle would already have moved on.
            g.Button.gameObject.AddComponent<PressDown>().Pressed = () => StopPressed?.Invoke(captured);
            // The whole gauge is a (bigger) target too.
            var hit = g.Root.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            g.Root.gameObject.AddComponent<PressDown>().Pressed = () => StopPressed?.Invoke(captured);
            return g;
        }

        /// <summary>Calls <see cref="Pressed"/> as soon as a pointer goes down on this UI element.</summary>
        private class PressDown : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
        {
            public Action Pressed;
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) => Pressed?.Invoke();
        }

        // ───────────────────────── Driving ─────────────────────────

        public void SetSubtitle(string text) => _subtitle.text = text;

        public void SetHints(int left) => _hints.SetHints(left);

        /// <summary>Lays out one gauge: its scale ends and its green band (both as values and as 0..1).</summary>
        public void SetupGauge(int i, string minLabel, string maxLabel, Vector2 band, string bandLow, string bandHigh)
        {
            Gauge g = _gauges[i];
            g.BandRange = band;
            Vector2 c = new Vector2(0f, -40f);
            SetArc(g.Band.rectTransform, g.Band, band.x, band.y);
            SetArc((RectTransform)g.Root.Find("Low"), g.Root.Find("Low").GetComponent<Image>(), 0f, band.x);
            SetArc((RectTransform)g.Root.Find("High"), g.Root.Find("High").GetComponent<Image>(), band.y, 1f);

            // Scale ends under the arc ends, band ends above the band.
            Text(g.Root, minLabel, _bodyFont, 28f, new Color(1f, 1f, 1f, 0.85f), ArcPoint(c, 0f, ArcRadius) + new Vector2(0f, -48f), new Vector2(140f, 40f), true);
            Text(g.Root, maxLabel, _bodyFont, 28f, new Color(1f, 1f, 1f, 0.85f), ArcPoint(c, 1f, ArcRadius) + new Vector2(0f, -48f), new Vector2(160f, 40f), true);
            // A narrow band gets one "low–high" label over its middle, so the two never overlap.
            float bandArcLength = (band.y - band.x) * ArcSpan * Mathf.Deg2Rad * (ArcRadius + 26f);
            if (bandArcLength < 120f)
            {
                Text(g.Root, $"{bandLow}–{bandHigh}", _titleFont, 26f, Mint, ArcPoint(c, (band.x + band.y) * 0.5f, ArcRadius + 28f), new Vector2(200f, 36f), true);
            }
            else
            {
                Text(g.Root, bandLow, _titleFont, 26f, Mint, ArcPoint(c, band.x, ArcRadius + 26f), new Vector2(120f, 36f), true);
                Text(g.Root, bandHigh, _titleFont, 26f, Mint, ArcPoint(c, band.y, ArcRadius + 26f), new Vector2(120f, 36f), true);
            }
        }

        /// <summary>Needle position along the arc, 0 (left end) to 1 (right end).</summary>
        public void SetNeedle(int i, float f)
        {
            Gauge g = _gauges[i];
            Vector2 c = new Vector2(0f, -40f);
            // Inside the arc, tip pointing out at it.
            g.Needle.anchoredPosition = ArcPoint(c, f, ArcRadius - ArcThickness - 26f);
            g.Needle.localRotation = Quaternion.Euler(0f, 0f, -ArcAngle(f));
        }

        public void SetPulse(float t)
        {
            foreach (Gauge g in _gauges)
            {
                if (g.Locked) continue;
                g.Status.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 3f));
            }
            if (_completeReady) _complete.transform.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(t * 4f));
        }

        public void Lock(int i, string reading, bool normal)
        {
            Gauge g = _gauges[i];
            g.Locked = true;
            g.Status.text = reading;
            g.Status.color = normal ? Mint : Gold;
            g.Status.alpha = 1f;
            g.ButtonLabel.text = "LOCKED";
            g.ButtonLabel.color = Mint;
            if (_handIcon != null) g.ButtonIcon.sprite = GlassSprites.Check;
            g.ButtonIcon.color = Mint;
            g.ButtonFill.color = new Color(0.4f, 0.9f, 0.55f, 0.22f);
            g.ButtonComp.interactable = false;
            _checkFills[i].enabled = true;
            _checkMarks[i].enabled = true;
        }

        public void Miss(int i, float lockedFor)
        {
            Gauge g = _gauges[i];
            if (g.Flash != null) StopCoroutine(g.Flash);
            g.Flash = StartCoroutine(MissRoutine(g, lockedFor));
        }

        private IEnumerator MissRoutine(Gauge g, float lockedFor)
        {
            g.Status.text = "MISSED!";
            g.Status.color = MissRed;
            Vector2 home = new Vector2(0f, -200f);
            float duration = Mathf.Max(0.35f, lockedFor);
            for (float t = 0f; t < duration; t += Lalim.Core.PauseClock.DeltaTime)
            {
                float shake = t < 0.35f ? Mathf.Sin(t * 70f) * 14f * (1f - t / 0.35f) : 0f;
                g.Button.anchoredPosition = home + new Vector2(shake, 0f);
                g.Band.color = Color.Lerp(MissRed, BandArc, t / duration);
                g.ButtonFill.color = Color.Lerp(new Color(1f, 0.4f, 0.35f, 0.3f), new Color(1f, 1f, 1f, 0.16f), t / duration);
                yield return null;
            }
            g.Button.anchoredPosition = home;
            g.Band.color = BandArc;
            g.ButtonFill.color = new Color(1f, 1f, 1f, 0.16f);
            if (!g.Locked) { g.Status.text = "STOP!"; g.Status.color = Gold; }
            g.Flash = null;
        }

        public void SetCompleteReady()
        {
            _completeReady = true;
            _completeGroup.alpha = 1f;
            _completeLabel.color = Mint;
        }

        // ───────────────────────── Arc maths ─────────────────────────

        /// <summary>Clockwise degrees from straight up, for 0..1 along the arc.</summary>
        private static float ArcAngle(float f) => -ArcSpan * 0.5f + ArcSpan * Mathf.Clamp01(f);

        private static Vector2 ArcPoint(Vector2 centre, float f, float radius)
        {
            float a = ArcAngle(f) * Mathf.Deg2Rad;
            return centre + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * radius;
        }

        private Image Arc(RectTransform parent, string name, Vector2 centre, float radius, Sprite ring, float from, float to, Color color)
        {
            RectTransform rt = Rect(name, parent, centre, new Vector2(radius * 2f, radius * 2f));
            Image img = Simple(rt.gameObject, ring, color);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            SetArc(rt, img, from, to);
            return img;
        }

        private static void SetArc(RectTransform rt, Image img, float from, float to)
        {
            // Radial fill starts at the top and runs clockwise; turn the image so it starts at "from".
            rt.localRotation = Quaternion.Euler(0f, 0f, -ArcAngle(from));
            img.fillAmount = Mathf.Max(0f, (to - from) * ArcSpan / 360f);
        }

        // ───────────────────────── Tiny UI helpers ─────────────────────────

        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static Image Simple(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image Sliced(GameObject go, Sprite sprite, Color color, float cornerScale)
        {
            Image img = Simple(go, sprite, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / cornerScale;
            img.raycastTarget = true;
            return img;
        }

        private static TMP_Text Text(Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 box, bool bold)
        {
            RectTransform rt = Rect("Text", parent, pos, box);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = bold && font == null ? FontStyles.Bold : FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
