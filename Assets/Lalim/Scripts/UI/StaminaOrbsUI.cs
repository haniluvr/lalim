using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// Stamina as a row of seashells under the HP bar. Like the Seagrass Mend card and the
    /// Bubble Barrier, each shell FILLS from the bottom: the shell currently recharging fills in
    /// the desaturated seashell colour, and a full shell turns the bright seashell colour
    /// (HudPalette) — ready to spend. Spent shells are an empty outline.
    /// When a press fails for lack of stamina, the missing shells flash red.
    ///
    /// Driven by StaminaChangedEvent (throttled to ~10Hz by PlayerStamina), not polled per frame.
    /// </summary>
    public class StaminaOrbsUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _container;
        [FormerlySerializedAs("_starSprite")]
        [SerializeField] private Sprite _shellSprite;
        [SerializeField] private Sprite _dotSprite;
        [FormerlySerializedAs("_starSize")]
        [SerializeField] private float _shellSize = 38f;
        [SerializeField] private float _spacing = 20f;

        private readonly List<Image> _outlines = new List<Image>();
        private readonly List<Image> _fills = new List<Image>();
        private float _current;
        private int _max;
        private int _flashFrom;
        private int _flashTo;
        private float _flashTimer;

        private void Awake()
        {
            if (_container == null) _container = (RectTransform)transform;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<StaminaChangedEvent>(OnStaminaChanged);
            EventBus.Subscribe<StaminaInsufficientEvent>(OnStaminaInsufficient);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StaminaChangedEvent>(OnStaminaChanged);
            EventBus.Unsubscribe<StaminaInsufficientEvent>(OnStaminaInsufficient);
        }

        private void Start()
        {
            int max = PlayerStamina.Instance != null ? PlayerStamina.Instance.MaxOrbs : GameConstants.Abilities.MAX_STAMINA_ORBS;
            _current = PlayerStamina.Instance != null ? PlayerStamina.Instance.Current : max;
            Build(max);
            Refresh();
        }

        private void Update()
        {
            if (_flashTimer <= 0f) return;
            _flashTimer -= Lalim.Core.PauseClock.DeltaTime;
            Refresh();
        }

        private void OnStaminaChanged(StaminaChangedEvent evt)
        {
            if (evt.MaxOrbs != _max) Build(evt.MaxOrbs);
            _current = evt.Current;
            Refresh();
        }

        private void OnStaminaInsufficient(StaminaInsufficientEvent evt)
        {
            _flashFrom = evt.Have;
            _flashTo = Mathf.Min(evt.Needed, _max);
            _flashTimer = 0.6f;
        }

        private void Build(int max)
        {
            _max = max;
            for (int i = _container.childCount - 1; i >= 0; i--) Destroy(_container.GetChild(i).gameObject);
            _outlines.Clear();
            _fills.Clear();

            float total = max * _shellSize + (max - 1) * _spacing;
            float x = -total * 0.5f + _shellSize * 0.5f;

            for (int i = 0; i < max; i++)
            {
                Image outline = CreateImage($"Shell{i}", _container, _shellSprite, new Vector2(x, 0f), new Vector2(_shellSize, _shellSize), HudPalette.ShellEmpty);
                Image fill = CreateImage("Fill", outline.transform, _shellSprite, Vector2.zero, new Vector2(_shellSize, _shellSize), HudPalette.ShellReady);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                _outlines.Add(outline);
                _fills.Add(fill);

                if (i < max - 1)
                {
                    float dotX = x + (_shellSize + _spacing) * 0.5f;
                    CreateImage($"Pearl{i}", _container, _dotSprite, new Vector2(dotX, -4f), new Vector2(5f, 5f), HudPalette.Pearl);
                }
                x += _shellSize + _spacing;
            }
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true; // the sea_shell art isn't square
            img.raycastTarget = false;
            return img;
        }

        private void Refresh()
        {
            int full = Mathf.FloorToInt(_current + 0.0001f);
            bool flashOn = _flashTimer > 0f && Mathf.PingPong(Lalim.Core.PauseClock.Time * 8f, 1f) > 0.5f;

            for (int i = 0; i < _fills.Count; i++)
            {
                bool isFull = i < full;
                bool isFilling = i == full;
                _fills[i].fillAmount = isFull ? 1f : (isFilling ? Mathf.Clamp01(_current - full) : 0f);
                _fills[i].color = isFull ? HudPalette.ShellReady : HudPalette.ShellCharging;

                bool flash = flashOn && i >= _flashFrom && i < _flashTo;
                _outlines[i].color = flash ? HudPalette.Warning : HudPalette.ShellEmpty;
            }
        }
    }
}
