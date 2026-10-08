using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Lalim.Core;
using Lalim.Inventory;

namespace Lalim.UI
{
    /// <summary>
    /// The equipped relic's HUD button (one relic at a time; RelicLoadout). It sits between the
    /// inventory button and Spirit Sense and matches the skill bubbles: the bubble background set on
    /// the HUD with the relic's icon art (ItemData.icon) inside. Without art it falls back to a
    /// generated glass bubble and a drawn line icon (RelicIcons). Tapping it uses the relic (RelicLoadout.Use);
    /// a cooldown the relic's owner starts shows as a dark sweep. Pressing and releasing it is
    /// also published (RelicHoldEvent) for relics used by holding: the net aims while held. Nothing shows until a relic with
    /// an action is equipped. Follows Right Handed Mode itself (it is built after HudHandedness
    /// collects the authored layout). Key 1 presses it (desktop testing).
    /// </summary>
    public class RelicHudButtons : MonoBehaviour
    {
        // In the 1920×1080 HUD, from the bottom-right corner: up-left of the inventory button,
        // down-left of Spirit Sense, clear of the interact button.
        private static readonly Vector2 SlotPosition = new Vector2(-592f, 258f);
        private const float ButtonSize = 116f;

        private static readonly Color Cream = new Color(0.92f, 0.93f, 0.88f, 1f);

        private Sprite _background;
        private RectTransform _button;
        private Image _cooldown;
        private string _itemId;
        private float _punch;

        public void Init(Sprite background)
        {
            _background = background;
            Rebuild();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<RelicEquipChangedEvent>(OnEquipChanged);
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);
            EventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RelicEquipChangedEvent>(OnEquipChanged);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
            EventBus.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);
        }

        private void Start() => Rebuild();   // the save (and so the loadout) is restored by now

        private void OnEquipChanged(RelicEquipChangedEvent evt) => Rebuild();
        private void OnInventoryChanged(InventoryChangedEvent evt) => Rebuild();
        private void OnSettingsChanged(SettingsChangedEvent evt) => Rebuild();

        private void Rebuild()
        {
            if (_held && _itemId != null) { _held = false; RelicLoadout.SetHeld(_itemId, false); }
            if (_button != null)
            {
                _button.gameObject.SetActive(false);   // Destroy waits for the end of the frame
                Destroy(_button.gameObject);
                _button = null;
            }
            _itemId = null;

            var equipped = RelicLoadout.Equipped;
            if (equipped.Count == 0 || !RelicLoadout.HasButton(equipped[0])) return;
            _itemId = equipped[0];
            Build();
        }

        private void Build()
        {
            bool mirrored = SettingsManager.Active != null && !SettingsManager.Active.rightHandedMode;
            var go = new GameObject("Relic_" + _itemId, typeof(RectTransform));
            _button = (RectTransform)go.transform;
            _button.SetParent(transform, false);
            _button.anchorMin = _button.anchorMax = new Vector2(mirrored ? 0f : 1f, 0f);
            _button.pivot = new Vector2(0.5f, 0.5f);
            _button.anchoredPosition = new Vector2(mirrored ? -SlotPosition.x : SlotPosition.x, SlotPosition.y);
            _button.sizeDelta = new Vector2(ButtonSize, ButtonSize);

            Image bubble = go.AddComponent<Image>();
            bubble.sprite = _background != null ? _background : BubbleSprite();
            bubble.color = new Color(1f, 1f, 1f, 0.686f);   // the skill buttons' opacity

            // The relic's own icon art; else a drawn line icon; else the model's silhouette.
            ItemData item = InventoryManager.Instance != null ? InventoryManager.Instance.GetItemData(_itemId) : null;
            Sprite drawn = RelicIcons.For(_itemId);
            if (item != null && item.icon != null)
            {
                var art = Child("Icon", _button, ButtonSize * 0.74f).gameObject.AddComponent<Image>();
                art.sprite = item.icon;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            else if (drawn != null)
            {
                var icon = Child("Icon", _button, ButtonSize * 0.68f).gameObject.AddComponent<Image>();
                icon.sprite = drawn;
                icon.color = Cream;
                icon.raycastTarget = false;
            }
            else
            {
                Texture silhouette = ItemPortraits.GetSilhouette(item);
                var pic = Child("Icon", _button, ButtonSize * 0.8f).gameObject.AddComponent<RawImage>();
                pic.texture = silhouette;
                pic.color = Cream;
                pic.raycastTarget = false;
                pic.enabled = silhouette != null;
            }

            _cooldown = Child("Cooldown", _button, ButtonSize * 0.96f).gameObject.AddComponent<Image>();
            _cooldown.sprite = GlassSprites.Circle;
            _cooldown.color = new Color(0.02f, 0.08f, 0.1f, 0.6f);
            _cooldown.type = Image.Type.Filled;
            _cooldown.fillMethod = Image.FillMethod.Radial360;
            _cooldown.fillOrigin = (int)Image.Origin360.Top;
            _cooldown.fillClockwise = false;
            _cooldown.fillAmount = 0f;
            _cooldown.raycastTarget = false;

            go.AddComponent<Button>().onClick.AddListener(Press);
            var hold = go.AddComponent<HoldRelay>();
            hold.Changed = SetHeld;
        }

        /// <summary>Reports the button going down and up (a Button only reports the click).</summary>
        private class HoldRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public System.Action<bool> Changed;
            public void OnPointerDown(PointerEventData e) => Changed?.Invoke(true);
            public void OnPointerUp(PointerEventData e) => Changed?.Invoke(false);
            private void OnDisable() => Changed?.Invoke(false);
        }

        private bool _held;

        private void SetHeld(bool down)
        {
            if (_itemId == null || down == _held) return;
            if (down && RelicLoadout.CooldownRemaining(_itemId) > 0f) return;
            _held = down;
            RelicLoadout.SetHeld(_itemId, down);
        }

        private static RectTransform Child(string name, RectTransform parent, float size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        private void Press()
        {
            if (_itemId == null || RelicLoadout.CooldownRemaining(_itemId) > 0f) return;
            _punch = 1f;
            RelicLoadout.Use(_itemId);
        }

        private void Update()
        {
            if (_button == null) return;
            _cooldown.fillAmount = RelicLoadout.Cooldown01(_itemId);
            _punch = Mathf.MoveTowards(_punch, 0f, PauseClock.DeltaTime * 5f);
            _button.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(_punch * Mathf.PI));

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (_held && kb.digit1Key.wasReleasedThisFrame) SetHeld(false);
            if (Time.timeScale <= 0f) return;
            if (HUDController.Instance != null && !HUDController.Instance.IsShowing) return;   // e.g. 1–3 drive the water gauges
            if (kb.digit1Key.wasPressedThisFrame) { SetHeld(true); Press(); }
        }

        // ───────────────────────── Glass bubble ─────────────────────────

        private static Sprite _bubble;

        /// <summary>Shader-style smoothstep: 0 below <paramref name="edge0"/>, 1 above <paramref name="edge1"/>.</summary>
        private static float Smooth(float edge0, float edge1, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

        /// <summary>
        /// A frosted glass sphere like the skill buttons: teal-grey body darker in the middle, a pale
        /// pearly rim, a soft highlight at the top left and a faint inner ring. Generated once.
        /// </summary>
        private static Sprite BubbleSprite()
        {
            if (_bubble != null) return _bubble;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "RelicBubble" };
            var px = new Color[size * size];
            var centre = new Color(0.33f, 0.52f, 0.55f);
            var body = new Color(0.58f, 0.68f, 0.71f);
            var rim = new Color(0.86f, 0.91f, 0.93f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float edgeAlpha = Mathf.Clamp01((1f - r) * size * 0.5f);   // anti-aliased edge
                if (edgeAlpha <= 0f) { px[y * size + x] = Color.clear; continue; }

                Color c = Color.Lerp(centre, body, Smooth(0f, 1f, r));
                c = Color.Lerp(c, rim, Smooth(0.86f, 0.97f, r));                         // pearly rim
                c = Color.Lerp(c, new Color(0.42f, 0.49f, 0.53f), Smooth(0.975f, 0.995f, r));   // thin darker edge
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.012f, 2f)) * 0.35f;               // faint inner ring
                c = Color.Lerp(c, rim, ring);
                float hx = u + 0.38f, hy = v - 0.42f;                                              // top-left highlight
                float highlight = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx * 1.3f + hy * hy * 2.2f) / 0.4f) * 0.35f;
                c = Color.Lerp(c, Color.white, highlight);
                c.a = edgeAlpha * Mathf.Lerp(0.78f, 1f, Smooth(0.6f, 1f, r));
                px[y * size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            _bubble = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _bubble;
        }
    }
}
