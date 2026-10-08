using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// One HUD ability button, bound to an AbilityId. Reads the ability's state every frame
    /// (cheaper than per-frame events) and shows:
    ///  - a dark radial overlay that shrinks 1 → 0 as the cooldown recovers,
    ///  - a countdown number (seconds left) in the middle — only while on cooldown, never for a
    ///    skill's active duration,
    ///  - a seashell + N stamina cost label under the button (dim when unaffordable, flashes red on a failed press),
    ///  - dimming + a lock icon when the ability isn't allowed right now.
    ///
    /// Uses IPointerDownHandler so presses register instantly and work alongside the joystick (multi-touch).
    /// </summary>
    public class AbilityButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] protected AbilityId _ability;

        [Header("Visuals")]
        [SerializeField] protected Image _icon;
        [Tooltip("Filled/Radial360 image. Fill = remaining cooldown fraction.")]
        [SerializeField] protected Image _cooldownOverlay;
        [Tooltip("Seconds left while recharging, shown in the middle of the button.")]
        [SerializeField] protected TMP_Text _countdownText;
        [SerializeField] protected GameObject _lockIcon;
        [SerializeField] protected CanvasGroup _canvasGroup;

        [Header("Stamina cost (seashell + N under the button)")]
        [SerializeField] protected GameObject _costRoot;
        [FormerlySerializedAs("_costStar")]
        [SerializeField] protected Image _costIcon;
        [SerializeField] protected TMP_Text _costText;

        [Header("Colors")]
        [SerializeField] protected Color _costAffordable = HudPalette.ShellReady;
        [SerializeField] protected Color _costUnaffordable = new Color(0.88f, 0.9f, 0.93f, 0.95f);
        [SerializeField] protected Color _costFlash = new Color(1f, 0.25f, 0.25f, 1f);
        [SerializeField] protected float _dimmedAlpha = 0.45f;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private float _shakeTimer;
        private float _flashTimer;

        public AbilityId Ability => _ability;
        protected AbilityBase Target => AbilityController.Instance != null ? AbilityController.Instance.Get(_ability) : null;

        /// <summary>Charge-style buttons (Mend card, Barrier) show their fill instead of a number.</summary>
        protected virtual bool ShowCountdown => true;

        protected virtual void Awake()
        {
            _rect = transform as RectTransform;
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        protected virtual void Start()
        {
            if (_rect != null) _restPosition = _rect.anchoredPosition;
        }

        protected virtual void OnEnable()
        {
            EventBus.Subscribe<AbilityBlockedEvent>(OnAbilityBlocked);
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<AbilityBlockedEvent>(OnAbilityBlocked);
            if (_rect != null && _shakeTimer > 0f) _rect.anchoredPosition = _restPosition;
            _shakeTimer = 0f;
        }

        protected virtual void Update()
        {
            AbilityBase ability = Target;
            if (ability == null)
            {
                _canvasGroup.alpha = _dimmedAlpha;
                return;
            }

            AbilityBlockReason context = ability.GetContextBlockReason();
            bool locked = context == AbilityBlockReason.NotAllowedHere
                       || context == AbilityBlockReason.Mounted
                       || context == AbilityBlockReason.NotMounted;
            bool dimmed = locked || context == AbilityBlockReason.AtFullHealth;

            _canvasGroup.alpha = dimmed && !ability.IsActive ? _dimmedAlpha : 1f;
            if (_lockIcon != null) _lockIcon.SetActive(locked && !ability.IsActive);

            if (_cooldownOverlay != null) _cooldownOverlay.fillAmount = ability.CooldownNormalized;

            UpdateCountdown(ability);
            UpdateCost(ability.CostOrbs);
            UpdateShake();
        }

        protected virtual void UpdateCountdown(AbilityBase ability)
        {
            if (_countdownText == null) return;
            bool show = ShowCountdown && ability.State == AbilityState.Cooldown && !(_lockIcon != null && _lockIcon.activeSelf);
            if (_countdownText.gameObject.activeSelf != show) _countdownText.gameObject.SetActive(show);
            if (show) _countdownText.text = Mathf.CeilToInt(ability.CooldownRemaining).ToString();
        }

        // ─────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (AbilityController.Instance != null) AbilityController.Instance.RequestActivate(_ability);
        }

        public virtual void OnPointerUp(PointerEventData eventData) { }

        // ─────────────────────────────────────────────
        // Feedback
        // ─────────────────────────────────────────────

        private void OnAbilityBlocked(AbilityBlockedEvent evt)
        {
            if (!IsFeedbackFor(evt.Ability)) return;

            // Rest position is taken when a shake starts, so layout changes (e.g. left-handed
            // HUD mirroring) are respected.
            if (_shakeTimer <= 0f && _rect != null) _restPosition = _rect.anchoredPosition;

            if (evt.Reason == AbilityBlockReason.NotEnoughStamina)
            {
                _shakeTimer = 0.35f;
                _flashTimer = 0.6f;
            }
            else if (evt.Reason == AbilityBlockReason.OnCooldown || evt.Reason == AbilityBlockReason.NotAllowedHere
                  || evt.Reason == AbilityBlockReason.Busy || evt.Reason == AbilityBlockReason.NotCharged
                  || evt.Reason == AbilityBlockReason.Mounted || evt.Reason == AbilityBlockReason.AtFullHealth)
            {
                _shakeTimer = 0.2f;
            }
        }

        protected virtual bool IsFeedbackFor(AbilityId id) => id == _ability;

        private void UpdateShake()
        {
            if (_rect == null || _shakeTimer <= 0f) return;
            _shakeTimer -= Lalim.Core.PauseClock.DeltaTime;
            float offset = _shakeTimer > 0f ? Mathf.Sin(Lalim.Core.PauseClock.Time * 60f) * 8f * (_shakeTimer / 0.35f) : 0f;
            _rect.anchoredPosition = _restPosition + new Vector2(offset, 0f);
        }

        private void UpdateCost(int cost)
        {
            if (_costRoot == null) return;
            bool show = cost > 0;
            if (_costRoot.activeSelf != show) _costRoot.SetActive(show);
            if (!show) return;

            int have = PlayerStamina.Instance != null ? PlayerStamina.Instance.FullOrbs : cost;
            bool flashing = _flashTimer > 0f;
            if (flashing) _flashTimer -= Lalim.Core.PauseClock.DeltaTime;
            bool flashOn = flashing && Mathf.PingPong(Lalim.Core.PauseClock.Time * 8f, 1f) > 0.5f;

            Color color = flashOn ? _costFlash : (have >= cost ? _costAffordable : _costUnaffordable);
            if (_costText != null)
            {
                _costText.text = cost.ToString();
                _costText.color = color;
            }
            if (_costIcon != null) _costIcon.color = color;
        }
    }
}
