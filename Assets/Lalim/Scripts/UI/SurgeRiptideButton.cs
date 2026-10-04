using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.Abilities;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// The single Surge button: tap = Surge, hold 0.6s = Riptide (hold again to cancel it).
    /// Pointer down/up feed InputManager's held state exactly like the Shift key, so
    /// AbilityController resolves tap vs hold the same way for keyboard and touch.
    ///
    /// The icon never changes. Surge's cooldown shows on the overlay + countdown, and the charge
    /// ring fills while a hold is building up. Riptide's remaining time is shown on the thin line
    /// under the HP bar (RiptideTimerUI), not on the button.
    /// </summary>
    public class SurgeRiptideButton : AbilityButtonUI
    {
        [Header("Riptide (hold)")]
        [SerializeField] private Image _chargeRing;

        private bool _pointerHeld;

        protected override void Awake()
        {
            _ability = AbilityId.Surge;
            base.Awake();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ReleasePointer();
        }

        protected override void Update()
        {
            base.Update();

            AbilityController controller = AbilityController.Instance;
            if (_chargeRing != null) _chargeRing.fillAmount = controller != null ? controller.HoldChargeNormalized : 0f;
        }

        // Holding while Riptide is on cooldown shakes the button, same as a blocked tap.
        protected override bool IsFeedbackFor(AbilityId id) => id == AbilityId.Surge || id == AbilityId.Riptide;

        public override void OnPointerDown(PointerEventData eventData)
        {
            _pointerHeld = true;
            if (InputManager.Instance != null) InputManager.Instance.TriggerSurgeRiptideDown();
        }

        public override void OnPointerUp(PointerEventData eventData) => ReleasePointer();

        private void ReleasePointer()
        {
            if (!_pointerHeld) return;
            _pointerHeld = false;
            if (InputManager.Instance != null) InputManager.Instance.TriggerSurgeRiptideUp();
        }
    }
}
