using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Review log: a dark overlay over the whole screen (not a modal box) listing the lines shown
    /// so far in the current conversation (earlier conversations aren't kept). Every line — NPC and player alike — starts from the left, under the
    /// speaker's name (the player's name keeps its own colour). Newest line at the bottom,
    /// scrolled into view on open.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReviewOverlayUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _content;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Color _npcNameColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color _playerNameColor = new Color(1f, 0.85f, 0.55f);
        [SerializeField] private Color _textColor = new Color(0.95f, 0.97f, 1f);
        [SerializeField, Range(0.3f, 1f)] private float _maxLineWidth = 0.72f;

        private int _builtCount;
        private int _builtConversation = -1;

        private void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _closeButton?.onClick.AddListener(() => DialogueManager.Instance?.SetReviewOpen(false));
            Apply(false);
        }

        public void SetOpen(bool open, IReadOnlyList<DialogueManager.HistoryEntry> history)
        {
            if (open) Rebuild(history);
            Apply(open);
        }

        private void Apply(bool open)
        {
            if (_group == null) return;
            _group.alpha = open ? 1f : 0f;
            _group.interactable = open;
            _group.blocksRaycasts = open;
        }

        private void Rebuild(IReadOnlyList<DialogueManager.HistoryEntry> history)
        {
            if (_content == null || history == null) return;
            // Append only new lines (the log only grows within a scene).
            int conversation = DialogueManager.Instance != null ? DialogueManager.Instance.ConversationId : 0;
            if (history.Count < _builtCount || conversation != _builtConversation)
            {
                foreach (Transform c in _content) Destroy(c.gameObject);
                _builtCount = 0;
                _builtConversation = conversation;
            }
            for (int i = _builtCount; i < history.Count; i++) AddEntry(history[i]);
            _builtCount = history.Count;

            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        private void AddEntry(DialogueManager.HistoryEntry e)
        {
            float width = _content.rect.width > 1f ? _content.rect.width : 1600f;
            float lineWidth = width * _maxLineWidth;

            var row = new GameObject(e.IsPlayer ? "PlayerLine" : "NpcLine", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_content, false);
            var layout = row.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 4f;
            layout.padding = new RectOffset(0, 0, 6, 10);

            TMP_Text name = MakeText(row.transform, e.Speaker, 34f, FontStyles.Bold, e.IsPlayer ? _playerNameColor : _npcNameColor);
            name.GetComponent<LayoutElement>().preferredWidth = lineWidth;
            TMP_Text body = MakeText(row.transform, e.Text, 32f, FontStyles.Normal, _textColor);
            body.GetComponent<LayoutElement>().preferredWidth = lineWidth;
        }

        private TMP_Text MakeText(Transform parent, string text, float size, FontStyles style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) tmp.font = _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.TopLeft;   // everyone's lines start from the left
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
