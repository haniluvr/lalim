using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Basic skill (tap): an 8m dash that can be chained 3 times in a row as a combo. Surges cost
    /// nothing — they CHARGE the swim bar Riptide spends (Swim; no shells): 1 surge = ⅓ of a
    /// segment, a 2-surge combo = ½, a 3-surge combo = 1 whole segment. The 7s cooldown only
    /// starts after the 3rd surge of a combo — one or two surges never trigger it, the combo just
    /// resets once its window (1.5s) runs out. Riptide ends on the same cooldown.
    /// Blocked while Riptide is running, mid-dash and while mounted.
    /// </summary>
    public class SurgeAbility : AbilityBase
    {
        public override AbilityId Id => AbilityId.Surge;
        public override int CostOrbs => Config != null ? Config.surgeCostOrbs : GameConstants.Abilities.SURGE_COST_ORBS;
        protected override float DurationSeconds => 0f; // instant — cooldown is handled by the combo below
        protected override float CooldownSeconds => Config != null ? Config.surgeCooldown : GameConstants.Abilities.SURGE_COOLDOWN;

        [SerializeField] private ParticleSystem _trailVfx;
        [SerializeField] private AudioClip _whooshSfx;

        protected override bool RequiresFreeMovement => true;

        private int MaxCharges => Mathf.Max(1, Config != null ? Config.surgeComboCharges : GameConstants.Abilities.SURGE_COMBO_CHARGES);
        private float ComboWindow => Config != null ? Config.surgeComboWindow : GameConstants.Abilities.SURGE_COMBO_WINDOW;

        private int _chargesUsed;
        private float _comboTimer;

        /// <summary>The swim bar surges charge and Riptide spends (starts full each level).</summary>
        public SwimStaminaPool Swim { get; private set; }

        public float RiptideSecondsPerSegment => Config != null ? Config.riptideSecondsPerSegment : GameConstants.Abilities.RIPTIDE_SECONDS_PER_SEGMENT;

        protected override void Awake()
        {
            base.Awake();
            Swim = new SwimStaminaPool(Config != null ? Config.swimStaminaSegments : GameConstants.Abilities.SWIM_STAMINA_SEGMENTS);
        }

        private void OnEnable() => EventBus.Subscribe<PlayerRespawnedEvent>(OnRespawned);
        private void OnDisable() => EventBus.Unsubscribe<PlayerRespawnedEvent>(OnRespawned);
        private void OnRespawned(PlayerRespawnedEvent evt) => Swim.Refill();

        // Saved with the cooldown, as a 0..1 fill of the bar.
        public override float ChargeForSave => Swim.Normalized;
        protected override void RestoreCharge(float charge) => Swim.SetCurrent(charge * Swim.Segments);

        /// <summary>Riptide finished: the shared cooldown ring runs on the button, whatever Riptide burnt.</summary>
        public void StartSharedCooldown()
        {
            _chargesUsed = MaxCharges;   // the combo is over
            BeginCooldown();
        }

        /// <summary>Surges left in the current combo before the cooldown starts.</summary>
        public int ChargesLeft => State == AbilityState.Cooldown ? 0 : MaxCharges - _chargesUsed;
        public int ComboCharges => MaxCharges;

        protected override AbilityBlockReason CheckContext()
        {
            PlayerController player = PlayerController.Instance;
            if (player != null && player.IsMounted) return AbilityBlockReason.Mounted;
            if (player != null && !player.IsMovementEnabled) return AbilityBlockReason.NotAllowedHere;

            // Riptide and Surge share one button: while Riptide runs, a tap does nothing.
            AbilityBase riptide = AbilityController.Instance != null ? AbilityController.Instance.Get(AbilityId.Riptide) : null;
            if (riptide != null && riptide.IsActive) return AbilityBlockReason.Busy;

            // Each surge in the combo has to finish before the next one.
            if (player != null && player.IsDashing) return AbilityBlockReason.Busy;
            return AbilityBlockReason.None;
        }

        protected override bool CooldownAfterInstantUse()
        {
            _chargesUsed++;
            _comboTimer = ComboWindow;
            return _chargesUsed >= MaxCharges;
        }

        protected override void Update()
        {
            base.Update();
            Swim.Tick(GameplayClock.DeltaTime, RiptideSecondsPerSegment);   // Riptide's drain; holds still during dialogue

            // Mid-combo and the player stopped chaining: the combo resets, with no cooldown
            // (only a full 3-surge combo puts Surge on cooldown).
            if (State == AbilityState.Ready && _chargesUsed > 0 && ComboWindow > 0f)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f)
                {
                    _chargesUsed = 0;
                    _comboTimer = 0f;
                }
            }
        }

        protected override void OnCooldownFinished()
        {
            _chargesUsed = 0;
            _comboTimer = 0f;
        }

        protected override void OnActivate()
        {
            // This surge's place in the combo decides its charge (runs before the combo count goes up).
            Swim.AddSurgeCharge(_chargesUsed + 1);
            if (PlayerController.Instance == null) return;

            Vector2 moveInput = InputManager.Instance != null ? InputManager.Instance.MoveInput : Vector2.zero;
            Vector3 dir;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                // Reconstruct the same camera-relative direction PlayerController uses for swimming.
                Transform cam = Camera.main != null ? Camera.main.transform : PlayerController.Instance.transform;
                Vector3 right = cam.right;
                right.y = 0f; right.Normalize();
                dir = (cam.forward * moveInput.y + right * moveInput.x).normalized;
            }
            else
            {
                // Not moving: dash the way the diver is facing (the camera may be looking at their front).
                dir = PlayerController.Instance.transform.forward;
            }

            float distance = Config != null ? Config.surgeDashDistance : GameConstants.Abilities.SURGE_DASH_DISTANCE;
            float duration = Config != null ? Config.surgeDashDuration : GameConstants.Abilities.SURGE_DASH_DURATION;
            float retain = Config != null ? Config.surgeExitVelocityRetain : 0.4f;

            PlayerController.Instance.Dash(dir, distance, duration, retain);

            if (_trailVfx != null) _trailVfx.Play();
            if (_whooshSfx != null) GameAudio.PlayAt(_whooshSfx, transform.position);
        }
    }
}
