using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lalim.UI
{
    /// <summary>
    /// Short spoken lines that don't stop play: a frosted pill near the top of the screen with the
    /// speaker's name and one line ("Kataw Fisher: Let the pawikan go."), shown for a few seconds.
    /// Lines queue; a new line from the same speaker replaces a waiting one. For minigame
    /// comments where a full dialogue would interrupt the throw. Built at runtime.
    /// </summary>
    public class CalloutUI : MonoBehaviour
    {
        private static CalloutUI _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        private struct Pending { public string Speaker; public string Text; public float Seconds; }

        private readonly Queue<Pending> _queue = new Queue<Pending>();
        private CanvasGroup _group;
        private RectTransform _pill;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _line;
        private float _showUntil;
        private bool _showing;

        /// <summary>Shows a line (queued behind any line already showing).</summary>
        public static void Say(string speaker, string text, float seconds = 3.6f)
        {
            if (string.IsNullOrEmpty(text)) return;
            Ensure();
            _instance._queue.Enqueue(new Pending { Speaker = DialogueManager.ResolveTokens(speaker), Text = DialogueManager.ResolveTokens(text), Seconds = seconds });
            if (_instance._queue.Count > 2) _instance._queue.Dequeue();   // don't fall behind
        }

        public static void Clear()
        {
            if (_instance == null) return;
            _instance._queue.Clear();
            _instance._showUntil = 0f;
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("[Callout]", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _instance = go.AddComponent<CalloutUI>();
            _instance.Build();
        }

        private void Build()
        {
            _pill = (RectTransform)new GameObject("Pill", typeof(RectTransform)).transform;
            _pill.SetParent(transform, false);
            _pill.anchorMin = _pill.anchorMax = new Vector2(0.5f, 1f);
            _pill.pivot = new Vector2(0.5f, 1f);
            _pill.anchoredPosition = new Vector2(0f, -232f);
            _pill.sizeDelta = new Vector2(900f, 96f);
            var bg = _pill.gameObject.AddComponent<Image>();
            bg.sprite = GlassSprites.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.16f, 0.18f, 0.78f);
            bg.raycastTarget = false;
            _group = _pill.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;

            TMP_FontAsset title = HUDController.Instance != null ? HUDController.Instance.TitleFont : null;
            TMP_FontAsset body = HUDController.Instance != null ? HUDController.Instance.BodyFont : null;
            _name = Text("Name", title, 24f, new Color(1f, 0.82f, 0.5f), new Vector2(28f, -12f), new Vector2(-28f, -40f));
            _line = Text("Line", body, 28f, new Color(0.94f, 0.96f, 0.93f), new Vector2(28f, -40f), new Vector2(-28f, -88f));
        }

        private TextMeshProUGUI Text(string name, TMP_FontAsset font, float size, Color color, Vector2 topLeft, Vector2 bottomRight)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(_pill, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(topLeft.x, bottomRight.y);
            rt.offsetMax = new Vector2(bottomRight.x, topLeft.y);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _showUntil && _queue.Count > 0)
            {
                Pending p = _queue.Dequeue();
                _name.text = p.Speaker;
                _line.text = p.Text;
                _line.ForceMeshUpdate();
                float h = Mathf.Max(96f, 56f + _line.preferredHeight);
                _pill.sizeDelta = new Vector2(900f, h);
                _showUntil = now + p.Seconds;
                _showing = true;
            }
            else if (now >= _showUntil) _showing = false;

            bool hidden = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
            float target = _showing && !hidden ? 1f : 0f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime * 5f);
        }
    }
}
