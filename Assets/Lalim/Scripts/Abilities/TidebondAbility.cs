using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Resonance skill: calls the dugong and rides it at 2.25× speed (11.25 m/s) for up to 45s
    /// of dugong stamina ("fatigue"). Press again to dismount early. Exploration only.
    /// Cooldown (75s) starts on dismount; if the ride ran out, the dugong rests for that long.
    ///
    /// The player's CharacterController keeps doing all the movement — the dugong only snaps
    /// under the player — so collisions and UnderwaterBounds behave exactly as when swimming.
    /// </summary>
    public class TidebondAbility : AbilityBase
    {
        public override AbilityId Id => AbilityId.Tidebond;
        public override int CostOrbs => Config != null ? Config.tidebondCostOrbs : GameConstants.Abilities.TIDEBOND_COST_ORBS;
        protected override float DurationSeconds => Config != null ? Config.tidebondFatigueSeconds : GameConstants.Abilities.TIDEBOND_FATIGUE_SECONDS;
        protected override float CooldownSeconds => Config != null ? Config.tidebondCooldown : GameConstants.Abilities.TIDEBOND_COOLDOWN;
        protected override bool RequiresFreeMovement => true;
        protected override bool ExplorationOnly => true;

        [SerializeField] private ParticleSystem _rideVfx;
        [SerializeField] private AudioClip _callSfx;

        /// <summary>True once the dugong has arrived and the player is riding.</summary>
        public bool IsRiding { get; private set; }

        /// <summary>1 = fresh, 0 = exhausted. Only meaningful while the ability is active.</summary>
        public float FatigueRemaining01 => IsActive ? DurationNormalized : 0f;

        protected override AbilityBlockReason CheckContext()
        {
            if (DugongCompanion.Instance == null) return AbilityBlockReason.NotAllowedHere;
            // Too hurt to carry the player until it recovers (see DugongHealth).
            if (DugongHealth.Instance != null && DugongHealth.Instance.IsExhausted) return AbilityBlockReason.NotAllowedHere;
            if (PlayerController.Instance != null && !PlayerController.Instance.IsMovementEnabled) return AbilityBlockReason.NotAllowedHere;
            return AbilityBlockReason.None;
        }

        protected override void OnActivate()
        {
            // Riptide and Tidebond must never overlap; end Riptide so its cooldown starts now.
            if (AbilityController.Instance != null) AbilityController.Instance.Get(AbilityId.Riptide)?.Cancel();

            IsRiding = false;
            if (_callSfx != null) GameAudio.PlayAt(_callSfx, transform.position);

            float teleportDistance = Config != null ? Config.dugongRecallTeleportDistance : 25f;
            DugongCompanion.Instance.BeginMount(transform, teleportDistance, OnDugongArrived);
        }

        private void OnDugongArrived()
        {
            if (!IsActive) return;

            IsRiding = true;
            float multiplier = Config != null ? Config.tidebondSpeedMultiplier : GameConstants.Abilities.TIDEBOND_SPEED_MULTIPLIER;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.SetSpeedMultiplier(this, multiplier);
                PlayerController.Instance.SetMounted(true);
            }
            if (_rideVfx != null) _rideVfx.Play();
        }

        protected override void OnEnd(bool cancelled)
        {
            IsRiding = false;
            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.ClearSpeedMultiplier(this);
                PlayerController.Instance.SetMounted(false);
            }
            if (_rideVfx != null) _rideVfx.Stop();

            if (DugongCompanion.Instance != null)
            {
                // Ran out of fatigue → the dugong visibly rests for the cooldown. Early dismount → it just follows.
                if (cancelled) DugongCompanion.Instance.Dismount();
                else DugongCompanion.Instance.Rest(CooldownSeconds);
            }
        }

        private void OnDisable() => ForceEnd();
    }
}
