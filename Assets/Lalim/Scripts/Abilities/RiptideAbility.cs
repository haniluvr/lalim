using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Basic skill (hold): 1.75× swim speed (8.75 m/s) for 60s on a 30s cooldown. Costs no stamina.
    /// Holding the button again cancels it.
    /// Blocked while mounted — Tidebond is faster and the two must never stack.
    /// </summary>
    public class RiptideAbility : AbilityBase
    {
        public override AbilityId Id => AbilityId.Riptide;
        public override int CostOrbs => Config != null ? Config.riptideCostOrbs : GameConstants.Abilities.RIPTIDE_COST_ORBS;
        protected override float DurationSeconds => Config != null ? Config.riptideDuration : GameConstants.Abilities.RIPTIDE_DURATION;
        protected override float CooldownSeconds => Config != null ? Config.riptideCooldown : GameConstants.Abilities.RIPTIDE_COOLDOWN;
        protected override bool RequiresFreeMovement => true;

        [SerializeField] private ParticleSystem _streamVfx;
        [SerializeField] private AudioClip _startSfx;

        protected override AbilityBlockReason CheckContext()
        {
            if (PlayerController.Instance != null && PlayerController.Instance.IsMounted) return AbilityBlockReason.Mounted;

            // Shared button: after a Surge combo, Riptide waits for the same cooldown.
            AbilityBase surge = AbilityController.Instance != null ? AbilityController.Instance.Get(AbilityId.Surge) : null;
            if (surge != null && surge.State == AbilityState.Cooldown) return AbilityBlockReason.OnCooldown;
            return AbilityBlockReason.None;
        }

        protected override void OnActivate()
        {
            float multiplier = Config != null ? Config.riptideSpeedMultiplier : GameConstants.Abilities.RIPTIDE_SPEED_MULTIPLIER;
            if (PlayerController.Instance != null) PlayerController.Instance.SetSpeedMultiplier(this, multiplier);

            if (_streamVfx != null) _streamVfx.Play();
            if (_startSfx != null) GameAudio.PlayAt(_startSfx, transform.position);
        }

        protected override void OnEnd(bool cancelled)
        {
            if (PlayerController.Instance != null) PlayerController.Instance.ClearSpeedMultiplier(this);
            if (_streamVfx != null) _streamVfx.Stop();
        }

        private void OnDisable()
        {
            // Never leave a speed multiplier registered if this component goes away mid-effect.
            ForceEnd();
        }
    }
}
