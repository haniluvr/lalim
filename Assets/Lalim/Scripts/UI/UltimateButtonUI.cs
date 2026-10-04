using UnityEngine;
using UnityEngine.UI;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// Bubble Barrier (ultimate) button. Works like the Seagrass Mend card: it charges instead of
    /// costing stamina. A disc BEHIND the (semi-transparent) icon fills from the bottom in the
    /// desaturated seashell colour, and turns the full seashell colour once it's ready
    /// (HudPalette — same as the stamina shells). The icon itself never changes.
    /// It charges from activity (swimming + using other skills), starts each level half-charged
    /// (AbilityConfig.bubbleBarrierStartCharge) and refills from 0 after every use. While the
    /// bubble is up the fill stays empty (it can't charge during use).
    /// </summary>
    public class UltimateButtonUI : AbilityButtonUI
    {
        [Header("Charge")]
        [Tooltip("Disc behind the icon. Filled/Vertical/Bottom.")]
        [SerializeField] private Image _chargeFill;

        private RectTransform _rt;
        private bool _wasReady;
        private float _popTimer;

        protected override bool ShowCountdown => false;

        protected override void Awake()
        {
            _ability = Lalim.Core.AbilityId.BubbleBarrier;
            base.Awake();
            _rt = transform as RectTransform;
            if (_cooldownOverlay != null) _cooldownOverlay.gameObject.SetActive(false);
        }

        protected override void Update()
        {
            base.Update();

            AbilityBase barrier = Target;
            if (barrier == null) return;

            // Ready = fully charged from activity (the barrier has no cooldown).
            bool ready = barrier.IsCharged;

            if (_chargeFill != null)
            {
                _chargeFill.fillAmount = barrier.IsActive ? 0f : barrier.Charge01;
                // Same colours as the seashell stamina orbs.
                _chargeFill.color = ready ? HudPalette.ShellReady : HudPalette.ShellCharging;
            }

            // Small pop the moment it becomes ready (same as the Mend card).
            if (ready && !_wasReady) _popTimer = 0.35f;
            _wasReady = ready;
            if (_rt == null) return;
            if (_popTimer > 0f)
            {
                _popTimer -= Time.unscaledDeltaTime;
                float t = 1f - Mathf.Clamp01(_popTimer / 0.35f);
                _rt.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
            }
            else if (_rt.localScale != Vector3.one)
            {
                _rt.localScale = Vector3.one;
            }
        }
    }
}
