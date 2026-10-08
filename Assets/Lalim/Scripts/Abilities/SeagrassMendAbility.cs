using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Support skill (placeholder name — rename in AbilityConfig/HUD only): heals 30 HP over 3s,
    /// and the dugong by the same share of its own health. Free (no orbs). The card empties the
    /// moment it's used; the 40s recharge starts when the heal finishes. Starts every level empty
    /// (0% charge) and fills up. Usable everywhere, including while moving or mounted. Disabled
    /// while both the player and the dugong are at full health.
    ///
    /// Both play the green heal particles (the player's are a copy of the dugong's), but the heal
    /// never depends on where the dugong is, so it can't fail because of pathfinding. It is
    /// unavailable while the dugong itself is exhausted (DugongHealth at 0, until it recovers to half).
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
        private float _dugongHealPerSecond;

        /// <summary>Empty from the moment it's used; refills over the cooldown that follows the heal.</summary>
        public override float Charge01 => IsActive ? 0f : base.Charge01;

        /// <summary>Total heal for the current difficulty (DDA-scaled: 40 / 30 / 25 HP).</summary>
        public float HealAmount => Config != null
            ? Config.GetSeagrassMendHeal(CurrentDifficulty)
            : GameConstants.Abilities.SEAGRASS_MEND_HEAL_AMOUNT;

        protected override AbilityBlockReason CheckContext()
        {
            bool playerFull = PlayerHealth.Instance == null || PlayerHealth.Instance.IsAtFullHealth();
            bool dugongFull = DugongHealth.Instance == null || DugongHealth.Instance.IsAtFullHealth;
            if (playerFull && dugongFull) return AbilityBlockReason.AtFullHealth;
            // It's the dugong's skill: unavailable while the dugong is exhausted (see DugongHealth).
            if (DugongHealth.Instance != null && DugongHealth.Instance.IsExhausted) return AbilityBlockReason.NotAllowedHere;
            return AbilityBlockReason.None;
        }

        protected override void OnActivate()
        {
            _healRemaining = HealAmount;
            _healPerSecond = _healRemaining / Mathf.Max(DurationSeconds, 0.01f);
            // The dugong gets the same share of its (much bigger) health.
            float share = PlayerHealth.Instance != null && PlayerHealth.Instance.Max > 0f ? _healRemaining / PlayerHealth.Instance.Max : 0.3f;
            _dugongHealPerSecond = DugongHealth.Instance != null ? share * DugongHealth.Instance.Max / Mathf.Max(DurationSeconds, 0.01f) : 0f;

            EnsurePlayerVfx();
            if (_healVfx != null) _healVfx.Play();
            if (_healSfx != null) GameAudio.PlayAt(_healSfx, transform.position);
            if (DugongCompanion.Instance != null) DugongCompanion.Instance.PlayHeal();
        }

        protected override void OnTick(float deltaTime)
        {
            float amount = Mathf.Min(_healRemaining, _healPerSecond * deltaTime);
            _healRemaining -= amount;
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Heal(amount);
            if (DugongHealth.Instance != null) DugongHealth.Instance.Heal(_dugongHealPerSecond * deltaTime);
        }

        // The player shares the dugong's green heal particles: a copy, sized for the diver.
        private void EnsurePlayerVfx()
        {
            if (_healVfx != null) return;
            ParticleSystem source = DugongCompanion.Instance != null ? DugongCompanion.Instance.HealVfx : null;
            if (source == null) return;
            ParticleSystem copy = Instantiate(source, transform);
            copy.name = "VFX_Heal (player)";
            copy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            CharacterController cc = GetComponent<CharacterController>();
            copy.transform.localPosition = cc != null ? cc.center : Vector3.up * 0.5f;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            var shape = copy.shape;
            shape.radius = Mathf.Min(shape.radius, cc != null ? Mathf.Max(0.3f, cc.height * 0.6f) : 0.5f);
            var main = copy.main;
            main.duration = Mathf.Max(0.1f, DurationSeconds);   // sparkles for the whole heal
            _healVfx = copy;
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
