using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Support skill (placeholder name — rename in AbilityConfig/HUD only): heals 30 HP over 3s,
    /// free (no orbs), 40s cooldown that starts when the heal finishes. Starts every level empty
    /// (0% charge) and fills up. Usable everywhere, including while moving or mounted. Disabled at full HP.
    ///
    /// The dugong plays a nuzzle + seagrass VFX, but the heal never depends on where the dugong
    /// is, so it can't fail because of pathfinding. It is unavailable while the dugong itself is
    /// exhausted (DugongHealth at 0, until it recovers to half).
    /// </summary>
    public class SeagrassMendAbility : AbilityBase
    {
        public override AbilityId Id => AbilityId.SeagrassMend;
        public override int CostOrbs => 0;
        protected override float DurationSeconds => Config != null ? Config.seagrassMendHealDuration : GameConstants.Abilities.SEAGRASS_MEND_HEAL_DURATION;
        protected override float CooldownSeconds => Config != null ? Config.seagrassMendCooldown : GameConstants.Abilities.SEAGRASS_MEND_COOLDOWN;
        protected override float StartingCharge01 => Config != null ? Config.seagrassMendStartCharge : 0f;

        [SerializeField] private ParticleSystem _healVfx;
        [SerializeField] private AudioClip _healSfx;

        private float _healPerSecond;
        private float _healRemaining;

        /// <summary>Total heal for the current difficulty (DDA-scaled: 40 / 30 / 25 HP).</summary>
        public float HealAmount => Config != null
            ? Config.GetSeagrassMendHeal(CurrentDifficulty)
            : GameConstants.Abilities.SEAGRASS_MEND_HEAL_AMOUNT;

        protected override AbilityBlockReason CheckContext()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsAtFullHealth()) return AbilityBlockReason.AtFullHealth;
            // It's the dugong's skill: unavailable while the dugong is exhausted (see DugongHealth).
            if (DugongHealth.Instance != null && DugongHealth.Instance.IsExhausted) return AbilityBlockReason.NotAllowedHere;
            return AbilityBlockReason.None;
        }

        protected override void OnActivate()
        {
            _healRemaining = HealAmount;
            _healPerSecond = _healRemaining / Mathf.Max(DurationSeconds, 0.01f);

            if (_healVfx != null) _healVfx.Play();
            if (_healSfx != null) GameAudio.PlayAt(_healSfx, transform.position);
            if (DugongCompanion.Instance != null) DugongCompanion.Instance.PlayHeal();
        }

        protected override void OnTick(float deltaTime)
        {
            float amount = Mathf.Min(_healRemaining, _healPerSecond * deltaTime);
            _healRemaining -= amount;
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(amount);
        }

        protected override void OnEnd(bool cancelled)
        {
            // Frame-rate rounding can leave a sliver unhealed; pay it out if the heal ran its course.
            if (!cancelled && _healRemaining > 0f && PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.Heal(_healRemaining);
            }
            _healRemaining = 0f;
            if (_healVfx != null) _healVfx.Stop();
        }
    }
}
