using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Inventory;

namespace Lalim.UI
{
    /// <summary>
    /// The inventory: a frosted-glass window (same look as the minigame panels) listing the
    /// player's Relics and Tools as cards. Tapping a card shows it on the right; an equippable
    /// relic has an Equip / Unequip button there, which adds or removes its HUD button
    /// (RelicLoadout). The world pauses while it is open.
    ///
    /// Opened by the HUD's inventory button or the I key. Built at runtime, so it needs no art.
    /// </summary>
    public class InventoryModalUI : MonoBehaviour
    {
        public static InventoryModalUI Instance { get; private set; }

        private static readonly Color Dim = new Color(0.01f, 0.05f, 0.08f, 0.6f);
        private static readonly Color GlassFill = new Color(0.06f, 0.22f, 0.32f, 0.72f);
        private static readonly Color GlassEdge = new Color(0.78f, 0.96f, 1f, 0.55f);
        private static readonly Color CardFill = new Color(0.04f, 0.16f, 0.24f, 0.75f);
        private static readonly Color CardEdge = new Color(0.78f, 0.96f, 1f, 0.3f);
        private static readonly Color Cream = new Color(1f, 0.93f, 0.8f);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.65f);

        private TMP_FontAsset _titleFont, _bodyFont;
        private Canvas _canvas;
        private RectTransform _grid;
        private readonly List<GameObject> _cards = new List<GameObject>();
        private readonly Dictionary<string, Image> _cardRims = new Dictionary<string, Image>();
        private readonly Dictionary<string, GameObject> _cardBadges = new Dictionary<string, GameObject>();

        private RawImage _detailPortrait;
        private TMP_Text _detailName, _detailKind, _detailBody, _detailHint;
        private Button _equipButton;
        private Image _equipFill;
        private TMP_Text _equipLabel;
        private GameObject _detailRoot, _emptyNote;

        private string _selected;
        private float _timeScaleBefore = 1f;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        // ───────────────────────── Open / close ─────────────────────────

        /// <summary>Opens or closes the inventory, building it the first time.</summary>
        public static void Toggle(TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
        {
            if (Instance == null) Create(titleFont, bodyFont);
            if (Instance.IsOpen) Instance.Close();
            else Instance.Open();
        }

        private static void Create(TMP_FontAsset titleFont, TMP_FontAsset bodyFont)
        {
            var go = new GameObject("[Inventory]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;   // above the HUD and minigame screens, below the zone banner (80) and pause (100)
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) >= 1.7f ? 1f : 0f;
            var ui = go.AddComponent<InventoryModalUI>();
            ui._canvas = canvas;
            ui._titleFont = titleFont;
            ui._bodyFont = bodyFont;
            ui.Build();
            canvas.enabled = false;
            Instance = ui;
        }

        public void Open()
        {
            if (IsOpen) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Paused) return;
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            PauseClock.SetPaused(true);
            _canvas.enabled = true;
            Refresh();
        }

        public void Close() => Hide(restoreTime: true);

        private void Hide(bool restoreTime)
        {
            if (!IsOpen) return;
            _canvas.enabled = false;
            PauseClock.SetPaused(false);
            if (restoreTime) Time.timeScale = _timeScaleBefore;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) { PauseClock.SetPaused(false); Time.timeScale = _timeScaleBefore; }
        }

        // The pause menu takes over the time scale: step aside without restoring it.
        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.NewState == GameState.Paused) Hide(restoreTime: false);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            if (IsOpen) Refresh();
        }

        // ───────────────────────── Build ─────────────────────────

        private void Build()
        {
            // Full-screen dim: blocks the game behind and closes the window when tapped.
            RectTransform dim = Rect("Dim", transform, Vector2.zero, Vector2.zero);
            Stretch(dim);
            Image dimImg = Simple(dim.gameObject, null, Dim);
            dimImg.raycastTarget = true;
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            RectTransform panel = Rect("Panel", transform, new Vector2(0f, 10f), new Vector2(1560f, 860f));
            Sliced(panel.gameObject, GlassSprites.Rounded, GlassFill, 1f);
            RectTransform sheen = Rect("Sheen", panel, Vector2.zero, Vector2.zero);
            Stretch(sheen);
            Sliced(sheen.gameObject, GlassSprites.RoundedSheen, new Color(1f, 1f, 1f, 0.9f), 1f).raycastTarget = false;
            RectTransform rim = Rect("Rim", panel, Vector2.zero, Vector2.zero);
            Stretch(rim);
            Sliced(rim.gameObject, GlassSprites.RoundedOutline, GlassEdge, 1f).raycastTarget = false;

            Text(panel, "INVENTORY", _titleFont, 60f, Cream, new Vector2(-370f, 360f), new Vector2(700f, 80f), TextAlignmentOptions.Left);

            // Close button (top right)
            RectTransform close = Rect("Close", panel, new Vector2(715f, 365f), new Vector2(84f, 84f));
            Simple(close.gameObject, GlassSprites.Circle, new Color(0.04f, 0.16f, 0.24f, 0.85f)).raycastTarget = true;
            RectTransform closeRim = Rect("Rim", close, Vector2.zero, Vector2.zero);
            Stretch(closeRim);
            Simple(closeRim.gameObject, GlassSprites.CircleOutline, GlassEdge);
            Text(close, "×", _titleFont, 52f, Color.white, Vector2.zero, new Vector2(84f, 84f), TextAlignmentOptions.Center);
            close.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            // Left: the item grid (rebuilt on each refresh), scrolling when there are many items
            RectTransform view = Rect("GridView", panel, new Vector2(-330f, -30f), new Vector2(GridWidth, GridViewHeight));
            view.gameObject.AddComponent<RectMask2D>();
            Image viewHit = Simple(view.gameObject, null, new Color(0f, 0f, 0f, 0f));
            viewHit.raycastTarget = true;   // drag anywhere in the grid to scroll
            _grid = Rect("Grid", view, Vector2.zero, new Vector2(GridWidth, GridViewHeight));
            _grid.anchorMin = _grid.anchorMax = _grid.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = _grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            _emptyNote = Text(view, "Nothing here yet.", _bodyFont, 32f, Muted, Vector2.zero, new Vector2(700f, 60f), TextAlignmentOptions.Center).gameObject;

            // Right: the selected item
            RectTransform detail = Rect("Detail", panel, new Vector2(440f, -40f), new Vector2(560f, 700f));
            Sliced(detail.gameObject, GlassSprites.Rounded, CardFill, 1f).raycastTarget = false;
            _detailRoot = detail.gameObject;
            RectTransform portrait = Rect("Portrait", detail, new Vector2(0f, 170f), new Vector2(300f, 300f));
            _detailPortrait = portrait.gameObject.AddComponent<RawImage>();
            _detailPortrait.raycastTarget = false;
            _detailName = Text(detail, "", _titleFont, 44f, Cream, new Vector2(0f, -15f), new Vector2(520f, 60f), TextAlignmentOptions.Center);
            _detailKind = Text(detail, "", _bodyFont, 24f, HudPalette.ShellReady, new Vector2(0f, -60f), new Vector2(520f, 36f), TextAlignmentOptions.Center);
            _detailBody = Text(detail, "", _bodyFont, 25f, new Color(1f, 1f, 1f, 0.88f), new Vector2(0f, -150f), new Vector2(500f, 140f), TextAlignmentOptions.Top);
            _detailBody.textWrappingMode = TextWrappingModes.Normal;
            _detailHint = Text(detail, "", _bodyFont, 22f, Muted, new Vector2(0f, -235f), new Vector2(500f, 60f), TextAlignmentOptions.Top);
            _detailHint.textWrappingMode = TextWrappingModes.Normal;

            RectTransform equip = Rect("Equip", detail, new Vector2(0f, -300f), new Vector2(320f, 84f));
            _equipFill = Sliced(equip.gameObject, GlassSprites.Rounded, HudPalette.ShellReady, 1f);
            _equipButton = equip.gameObject.AddComponent<Button>();
            _equipButton.onClick.AddListener(OnEquipPressed);
            _equipLabel = Text(equip, "EQUIP", _titleFont, 34f, new Color(0.02f, 0.12f, 0.16f), Vector2.zero, new Vector2(320f, 84f), TextAlignmentOptions.Center);
        }

        // ───────────────────────── Content ─────────────────────────

        private void Refresh()
        {
            foreach (GameObject c in _cards) if (c != null) Destroy(c);
            _cards.Clear();
            _cardRims.Clear();
            _cardBadges.Clear();

            // Everything that isn't a relic is shown as a tool; the manager's per-type lists overlap,
            // so collect by id.
            List<ItemData> relics = Items(ItemType.Relic);
            var tools = new List<ItemData>();
            var seen = new HashSet<string>();
            foreach (ItemData r in relics) seen.Add(r.itemId);
            foreach (ItemType type in new[] { ItemType.Tool, ItemType.ConsumableKit, ItemType.QuestItem, ItemType.CompanionItem })
            {
                foreach (ItemData item in Items(type)) if (item != null && seen.Add(item.itemId)) tools.Add(item);
            }

            float y = -10f;
            y = Section("RELICS", relics, y);
            y = Section("TOOLS", tools, y - 20f);
            _grid.sizeDelta = new Vector2(_grid.sizeDelta.x, Mathf.Max(GridViewHeight, -y));
            _grid.anchoredPosition = Vector2.zero;

            bool any = relics.Count + tools.Count > 0;
            _emptyNote.SetActive(!any);
            _detailRoot.SetActive(any);
            if (!any) return;

            if (_selected == null || !InventoryHas(_selected)) _selected = relics.Count > 0 ? relics[0].itemId : tools[0].itemId;
            Select(_selected);
        }

        /// <summary>Collected items of a type, without skills (they're owned, but not things to carry).</summary>
        private static List<ItemData> Items(ItemType type)
        {
            var items = InventoryManager.Instance != null ? InventoryManager.Instance.GetCollectedItems(type) : new List<ItemData>();
            items.RemoveAll(i => i != null && i.isSkill);
            return items;
        }

        private static bool InventoryHas(string id) => InventoryManager.Instance != null && InventoryManager.Instance.HasItem(id);

        private const float CardW = 150f, CardH = 184f, Gap = 16f;
        private const int PerRow = 5;
        private const float GridWidth = 820f, GridViewHeight = 700f;

        /// <summary>A heading and rows of cards, top-anchored in the scrolling grid; returns the y below it.</summary>
        private float Section(string title, List<ItemData> items, float y)
        {
            if (items.Count == 0) return y;
            GameObject heading = Text(_grid, title, _titleFont, 30f, HudPalette.ShellReady, new Vector2(-GridWidth * 0.5f + 200f, y - 22f), new Vector2(400f, 44f), TextAlignmentOptions.Left).gameObject;
            TopAnchor((RectTransform)heading.transform);
            _cards.Add(heading);
            y -= 52f + CardH * 0.5f;
            for (int i = 0; i < items.Count; i++)
            {
                int col = i % PerRow, row = i / PerRow;
                float x = -(PerRow - 1) * (CardW + Gap) * 0.5f + col * (CardW + Gap);
                _cards.Add(Card(items[i], new Vector2(x, y - row * (CardH + Gap))));
            }
            int rows = (items.Count + PerRow - 1) / PerRow;
            return y - (rows - 1) * (CardH + Gap) - CardH * 0.5f - 20f;
        }

        /// <summary>Grid children are placed from the top of the (scrolling) content.</summary>
        private static void TopAnchor(RectTransform rt) => rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);

        private GameObject Card(ItemData item, Vector2 pos)
        {
            RectTransform card = Rect(item.itemId, _grid, pos, new Vector2(CardW, CardH));
            TopAnchor(card);
            Sliced(card.gameObject, GlassSprites.Rounded, CardFill, 0.6f);
            RectTransform rim = Rect("Rim", card, Vector2.zero, Vector2.zero);
            Stretch(rim);
            _cardRims[item.itemId] = Sliced(rim.gameObject, GlassSprites.RoundedOutline, CardEdge, 0.6f);
            _cardRims[item.itemId].raycastTarget = false;

            RectTransform pic = Rect("Portrait", card, new Vector2(0f, 20f), new Vector2(118f, 118f));
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = ItemPortraits.Get(item);
            raw.raycastTarget = false;
            raw.enabled = raw.texture != null;

            TMP_Text name = Text(card, item.displayName, _bodyFont, 19f, Color.white, new Vector2(0f, -64f), new Vector2(CardW - 14f, 48f), TextAlignmentOptions.Center);
            name.textWrappingMode = TextWrappingModes.Normal;

            RectTransform badge = Rect("Equipped", card, new Vector2(46f, 70f), new Vector2(30f, 30f));
            Simple(badge.gameObject, GlassSprites.Check, HudPalette.ReadyGold);
            badge.gameObject.SetActive(RelicLoadout.IsEquipped(item.itemId));
            _cardBadges[item.itemId] = badge.gameObject;

            string id = item.itemId;
            card.gameObject.AddComponent<Button>().onClick.AddListener(() => Select(id));
            return card.gameObject;
        }

        private void Select(string itemId)
        {
            _selected = itemId;
            foreach (var kv in _cardRims) kv.Value.color = kv.Key == itemId ? HudPalette.ReadyGold : CardEdge;

            ItemData item = InventoryManager.Instance != null ? InventoryManager.Instance.GetItemData(itemId) : null;
            if (item == null) return;
            _detailPortrait.texture = ItemPortraits.Get(item);
            _detailPortrait.enabled = _detailPortrait.texture != null;
            _detailName.text = item.displayName;
            _detailKind.text = KindLabel(item);
            _detailBody.text = item.description;

            bool equippable = RelicLoadout.IsEquippable(itemId);
            _equipButton.gameObject.SetActive(equippable);
            _detailHint.text = equippable ? RelicLoadout.UseHint(itemId) : "";
            if (equippable)
            {
                bool on = RelicLoadout.IsEquipped(itemId);
                // One relic at a time: say what equipping this one puts away.
                var current = RelicLoadout.Equipped;
                if (!on && current.Count > 0)
                {
                    ItemData other = InventoryManager.Instance.GetItemData(current[0]);
                    if (other != null) _detailHint.text += $"\nEquipping it puts away the {other.displayName}.";
                }
                _equipLabel.text = on ? "UNEQUIP" : "EQUIP";
                _equipFill.color = on ? new Color(0.62f, 0.7f, 0.74f, 0.9f) : HudPalette.ShellReady;
            }
            foreach (var kv in _cardBadges) kv.Value.SetActive(RelicLoadout.IsEquipped(kv.Key));
        }

        private static string KindLabel(ItemData item)
        {
            string kind = item.itemType == ItemType.Relic ? "Relic" : item.itemType == ItemType.Tool ? "Tool" : "Item";
            return $"{kind} · {item.regionOrigin}";
        }

        private void OnEquipPressed()
        {
            if (_selected == null) return;
            RelicLoadout.SetEquipped(_selected, !RelicLoadout.IsEquipped(_selected));            Select(_selected);
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

        private static TMP_Text Text(Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 pos, Vector2 box, TextAlignmentOptions align)
        {
            RectTransform rt = Rect("Text", parent, pos, box);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
