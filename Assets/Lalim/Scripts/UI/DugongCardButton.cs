using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// Seagrass Mend (support skill) shown as the dugong's portrait card.
    ///
    ///  - The dugong portrait itself always stays in full colour.
    ///  - Recharging: the card's background fills up from the bottom in the desaturated seashell
    ///    colour as the cooldown recovers. It can't be used until it's completely filled.
    ///  - Ready: the background turns gold (HudPalette.ReadyGold), with a short pop.
    ///  - Healing: the frame glows while the 3s heal runs.
    ///  - The green bar under the portrait is the dugong's own HP (DugongHealth).
    /// </summary>
    public class DugongCardButton : AbilityButtonUI
    {
        [Header("Dugong Card")]
        [Tooltip("Background behind the portrait. Filled/Vertical/Bottom — fill = how recharged the skill is.")]
        [SerializeField] private Image _chargeFill;
        [Tooltip("Thin glowing line that rides the recharge level. Hidden when fully charged.")]
        [SerializeField] private RectTransform _fillEdge;
        [SerializeField] private Image _frame;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _statusLabel;

        [Header("Dugong HP Bar")]
        [FormerlySerializedAs("_staminaFill")]
        [SerializeField] private Image _dugongHpFill;
        [FormerlySerializedAs("_staminaColor")]
        [SerializeField] private Color _hpColor = new Color(0.62f, 0.9f, 0.2f, 1f);
        [FormerlySerializedAs("_staminaLowColor")]
        [SerializeField] private Color _hpLowColor = new Color(0.95f, 0.45f, 0.25f, 1f);

        [Header("Frame colors")]
        [SerializeField] private Color _frameIdle = new Color(0.85f, 0.95f, 1f, 0.55f);
        [SerializeField] private Color _frameReady = new Color(0.9f, 1f, 1f, 0.95f);
        [SerializeField] private Color _frameHealing = new Color(0.55f, 1f, 0.6f, 1f);

        private RectTransform _cardRect;
        private bool _wasReady = true;
        private float _popTimer;

        // The card shows its own status text ("23s") instead of a centred number.
        protected override bool ShowCountdown => false;

        protected override void Awake()
        {
            _ability = AbilityId.SeagrassMend;
            base.Awake();
            _cardRect = transform as RectTransform;
            // The recharge fill is the whole point of the card — never let it start hidden.
            if (_chargeFill != null && !_chargeFill.gameObject.activeSelf) _chargeFill.gameObject.SetActive(true);
        }

        protected override void Update()
        {
            base.Update();

            AbilityBase mend = Target;
            float charge = mend != null ? mend.Charge01 : 1f;
            bool healing = mend != null && mend.IsActive;
            bool ready = mend != null && mend.State == AbilityState.Ready;

            if (_chargeFill != null)
            {
                _chargeFill.fillAmount = charge;
                // Fills in the muted seashell colour, turns gold once it's ready.
                _chargeFill.color = ready || healing ? HudPalette.ReadyGold : HudPalette.ShellCharging;
            }
            if (_fillEdge != null)
            {
                bool charging = charge < 0.999f;
                if (_fillEdge.gameObject.activeSelf != charging) _fillEdge.gameObject.SetActive(charging);
                _fillEdge.anchorMin = new Vector2(0f, charge);
                _fillEdge.anchorMax = new Vector2(1f, charge);
            }

            if (ready && !_wasReady) _popTimer = 0.35f;
            _wasReady = ready;

            UpdateFrame(ready, healing);
            UpdateStatus(mend, healing);
            UpdatePop();
            UpdateDugongHp();
        }

        private void UpdateFrame(bool ready, bool healing)
        {
            if (_frame == null) return;
            if (healing)
            {
                float pulse = (Mathf.Sin(Lalim.Core.PauseClock.Time * 8f) + 1f) * 0.5f;
                _frame.color = Color.Lerp(_frameReady, _frameHealing, 0.5f + pulse * 0.5f);
            }
            else
            {
                _frame.color = ready ? _frameReady : _frameIdle;
            }
        }

        private void UpdateStatus(AbilityBase mend, bool healing)
        {
            if (_statusLabel == null) return;
            if (mend == null) { _statusLabel.text = ""; return; }

            if (DugongHealth.Instance != null && DugongHealth.Instance.IsExhausted) _statusLabel.text = "RESTING";
            else if (healing) _statusLabel.text = "HEALING";
            else if (mend.State == AbilityState.Cooldown) _statusLabel.text = $"{Mathf.CeilToInt(mend.CooldownRemaining)}s";
            else _statusLabel.text = "READY";
        }

        private void UpdatePop()
        {
            if (_cardRect == null) return;
            if (_popTimer > 0f)
            {
                _popTimer -= Lalim.Core.PauseClock.DeltaTime;
                float t = 1f - Mathf.Clamp01(_popTimer / 0.35f);
                _cardRect.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
            }
            else if (_cardRect.localScale != Vector3.one)
            {
                _cardRect.localScale = Vector3.one;
            }
        }

        private void UpdateDugongHp()
        {
            if (_dugongHpFill == null) return;
            float value = DugongHealth.Instance != null ? DugongHealth.Instance.Normalized : 1f;
            _dugongHpFill.fillAmount = value;
            _dugongHpFill.color = value < 0.25f ? Color.Lerp(_hpLowColor, _hpColor, value / 0.25f) : _hpColor;
        }
    }
}
