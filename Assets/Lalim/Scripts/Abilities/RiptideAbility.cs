using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Basic skill (hold): 1.75× swim speed (8.75 m/s), paid from the swim bar that surges
    /// charge (no shells). It burns only WHOLE segments, 10s each: 4 full segments and a third of
    /// the 5th = 40s, and the leftover third becomes the bar's first segment. The bar drains as
    /// it runs; holding the button again cancels it and keeps what's left. Either way it ends
    /// on Surge's cooldown (the same ring on the shared button), however long it ran.
    /// Blocked while mounted — Tidebond is faster and the two must never stack.
    /// </summary>
    public class RiptideAbility : AbilityBase
    {
        public override AbilityId Id => AbilityId.Riptide;
        public override int CostOrbs => Config != null ? Config.riptideCostOrbs : GameConstants.Abilities.RIPTIDE_COST_ORBS;
        protected override float DurationSeconds => _duration;
        protected override float CooldownSeconds => 0f;   // Surge's cooldown runs instead (shared button)
        protected override bool RequiresFreeMovement => true;

        [SerializeField] private ParticleSystem _streamVfx;
        [SerializeField] private AudioClip _startSfx;

        private float _duration;

        private SurgeAbility Surge => AbilityController.Instance != null ? AbilityController.Instance.Get(AbilityId.Surge) as SurgeAbility : null;

        protected override AbilityBlockReason CheckContext()
        {
            if (PlayerController.Instance != null && PlayerController.Instance.IsMounted) return AbilityBlockReason.Mounted;

            // Shared button: after a Surge combo, Riptide waits for the same cooldown.
            SurgeAbility surge = Surge;
            if (surge != null && surge.State == AbilityState.Cooldown) return AbilityBlockReason.OnCooldown;

            // How long it would run right now: whole segments only.
            _duration = surge != null ? surge.Swim.RiptideSecondsAvailable(surge.RiptideSecondsPerSegment) : 0f;
            if (_duration <= 0f) return AbilityBlockReason.NotEnoughStamina;
            return AbilityBlockReason.None;
        }

        protected override void OnActivate()
        {
            Surge?.Swim.BeginRiptide();

            float multiplier = Config != null ? Config.riptideSpeedMultiplier : GameConstants.Abilities.RIPTIDE_SPEED_MULTIPLIER;
            if (PlayerController.Instance != null) PlayerController.Instance.SetSpeedMultiplier(this, multiplier);

            if (_streamVfx != null) _streamVfx.Play();
            if (_startSfx != null) GameAudio.PlayAt(_startSfx, transform.position);
        }

        protected override void OnEnd(bool cancelled)
        {
            if (PlayerController.Instance != null) PlayerController.Instance.ClearSpeedMultiplier(this);
            if (_streamVfx != null) _streamVfx.Stop();

            SurgeAbility surge = Surge;
            if (surge != null)
            {
                surge.Swim.EndRiptide();
                surge.StartSharedCooldown();
            }
        }

        private void OnDisable()
        {
            // Never leave a speed multiplier registered if this component goes away mid-effect.
            ForceEnd();
        }
    }
}
