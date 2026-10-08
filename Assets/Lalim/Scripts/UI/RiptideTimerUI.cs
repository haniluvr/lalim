using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// The swim stamina bar: the thin line under the HP bar, always shown. It's the bar Surge and
    /// Riptide share (SurgeAbility.Swim), split into 6 segments by thin lines; it starts full,
    /// is charged by surges (⅓ / ½ / 1 segment for a 1 / 2 / 3-surge combo) and drains while
    /// Riptide runs — it never refills on its own. When Riptide is refused for lack of a whole
    /// segment, the bar flashes.
    /// (Named for what it used to be — Riptide's timer — so the HUD prefab keeps its reference.)
    /// </summary>
    public class RiptideTimerUI : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 6f;
        [Tooltip("The line is made at least this tall so the segment lines read.")]
        [SerializeField] private float _minHeight = 6f;
        [SerializeField] private float _dividerWidth = 2f;
        [SerializeField] private Color _dividerColor = new Color(0f, 0.04f, 0.08f, 0.9f);

        private readonly List<RectTransform> _dividers = new List<RectTransform>();
        private float _flashTimer;
        private float _shown;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.ignoreParentGroups = true;   // stays up in minigames with the HP bar (HUDController.VitalsShowing)
            if (_fill != null) _fill.color = HudPalette.Riptide;

            var rt = (RectTransform)transform;
            if (rt.sizeDelta.y < _minHeight) rt.sizeDelta = new Vector2(rt.sizeDelta.x, _minHeight);
        }

        private void OnEnable() => EventBus.Subscribe<AbilityBlockedEvent>(OnBlocked);
        private void OnDisable() => EventBus.Unsubscribe<AbilityBlockedEvent>(OnBlocked);

        private void OnBlocked(AbilityBlockedEvent evt)
        {
            if ((evt.Ability == AbilityId.Surge || evt.Ability == AbilityId.Riptide) && evt.Reason == AbilityBlockReason.NotEnoughStamina)
                _flashTimer = 0.6f;
        }

        private void Update()
        {
            SurgeAbility surge = AbilityController.Instance != null ? AbilityController.Instance.Get(AbilityId.Surge) as SurgeAbility : null;
            SwimStaminaPool swim = surge != null ? surge.Swim : null;

            // Always on screen in play, minigames included (hidden in dialogue and menus).
            bool vitals = HUDController.Instance == null || HUDController.Instance.VitalsShowing;
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, swim != null && vitals ? 1f : 0f, _fadeSpeed * Time.unscaledDeltaTime);
            if (swim == null) return;

            if (_dividers.Count != swim.Segments - 1) BuildDividers(swim.Segments);

            // Smooth, so a surge's charge rises visibly rather than snapping.
            _shown = Mathf.MoveTowards(_shown, swim.Normalized, 2f * Lalim.Core.PauseClock.DeltaTime);
            if (Mathf.Abs(_shown - swim.Normalized) > 0.5f) _shown = swim.Normalized;   // respawn / load
            if (_fill != null)
            {
                _fill.fillAmount = _shown;
                Color c = HudPalette.Riptide;
                if (_flashTimer > 0f)
                {
                    _flashTimer -= Lalim.Core.PauseClock.DeltaTime;
                    if (Mathf.PingPong(Lalim.Core.PauseClock.Time * 8f, 1f) > 0.5f) c = HudPalette.Warning;
                }
                else if (swim.RiptideRunning)
                {
                    c = Color.Lerp(HudPalette.Riptide, Color.white, (Mathf.Sin(Lalim.Core.PauseClock.Time * 6f) + 1f) * 0.15f);
                }
                _fill.color = c;
            }
        }

        // Thin lines splitting the bar into equal segments, drawn over the fill.
        private void BuildDividers(int segments)
        {
            foreach (RectTransform d in _dividers) if (d != null) Destroy(d.gameObject);
            _dividers.Clear();
            for (int i = 1; i < segments; i++)
            {
                var go = new GameObject($"Divider{i}", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = new Vector2((float)i / segments, 0f);
                rt.anchorMax = new Vector2((float)i / segments, 1f);
                rt.sizeDelta = new Vector2(_dividerWidth, 0f);
                rt.anchoredPosition = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.color = _dividerColor;
                img.raycastTarget = false;
                _dividers.Add(rt);
            }
        }
    }
}
