using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Single source of truth for every ability's timing, cost, and range numbers,
    /// plus the DDA anchor values that scale a subset of them. Designers tune
    /// everything here without touching code — mirrors the DDAConfig pattern.
    ///
    /// Cooldowns, costs, durations, speeds, and ranges are intentionally NOT
    /// DDA-scaled: only orb regen speed, toxic damage, Seagrass Mend heal amount,
    /// Spirit Sense duration, and glow intensity change with difficulty. This keeps
    /// controls feeling identical across difficulty tiers.
    ///
    /// HOW THE ECONOMY FITS TOGETHER (Normal difficulty)
    ///  Each skill has exactly one limiter, so nothing double-gates:
    ///   - Surge / Riptide (basic):   cooldown only — free, always available for movement.
    ///   - Spirit Sense (1 shell):    stamina is the main limit; short 20s cooldown after its 15s.
    ///   - Tidebond (2 shells):       stamina is the main limit; 30s cooldown after the 45s ride,
    ///                                and the dugong's own HP gates it when hurt.
    ///   - Seagrass Mend (support):   time-based charge (40s), gated by dugong HP. No stamina.
    ///   - Bubble Barrier (ultimate): charges from ACTIVITY — swimming (~90s of movement from empty)
    ///                                plus a bonus for every other skill used. No cooldown, no stamina.
    ///  Stamina: 4 shells, 15s each. Using Spirit Sense AND Tidebond as often as their cooldowns
    ///  allow costs ~1 shell / 18s, just under the 1 / 15s refill — sustainable in open water.
    ///  Toxic water halves the refill (1 / 30s), so both can't be kept up there: you choose,
    ///  or raise Bubble Barrier (which restores normal refill). Riding regenerates 1.2× faster.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityConfig", menuName = "Lalim/Ability Config")]
    public class AbilityConfig : ScriptableObject
    {
        [Header("Player Resources")]
        public float maxHealth = GameConstants.Abilities.PLAYER_MAX_HP;
        public int maxStaminaOrbs = GameConstants.Abilities.MAX_STAMINA_ORBS;
        [Tooltip("Full shells the player has when a new game or a new level starts. Checkpoint respawns still refill to full.")]
        public int startingStaminaOrbs = GameConstants.Abilities.STARTING_STAMINA_ORBS;
        [Tooltip("Seconds to fill one stamina orb at Normal difficulty")]
        public float orbRegenSecondsNormal = GameConstants.Abilities.ORB_REGEN_SECONDS;
        public float orbRegenDelaySeconds = GameConstants.Abilities.ORB_REGEN_DELAY_SECONDS;
        [Tooltip("Regen speed multiplier while mounted on the dugong (small: riding shouldn't refund Tidebond's own cost)")]
        public float regenMultiplierMounted = 1.2f;
        [Tooltip("Regen speed multiplier while in toxic water without Bubble Barrier active")]
        public float regenMultiplierToxic = 0.5f;

        [Header("Surge (tap)")]
        public float surgeDashDistance = GameConstants.Abilities.SURGE_DASH_DISTANCE;
        public float surgeDashDuration = GameConstants.Abilities.SURGE_DASH_DURATION;
        public float surgeCooldown = GameConstants.Abilities.SURGE_COOLDOWN;
        public int surgeCostOrbs = GameConstants.Abilities.SURGE_COST_ORBS;
        [Tooltip("Surges allowed in a row (a combo) before the cooldown starts")]
        [Min(1)] public int surgeComboCharges = GameConstants.Abilities.SURGE_COMBO_CHARGES;
        [Tooltip("Seconds after a surge to chain the next one. If it runs out, the combo ends and the cooldown starts. 0 = no time limit.")]
        [Min(0f)] public float surgeComboWindow = GameConstants.Abilities.SURGE_COMBO_WINDOW;
        [Tooltip("Fraction of dash speed retained as glide velocity when the dash ends")]
        [Range(0f, 1f)] public float surgeExitVelocityRetain = 0.4f;
        [Tooltip("Max hold time (seconds) before a press is treated as a hold instead of a tap")]
        public float tapHoldThreshold = 0.25f;
        [Tooltip("Hold time (seconds) required to trigger Riptide")]
        public float holdActivateThreshold = 0.6f;

        [Header("Riptide (hold)")]
        public float riptideDuration = GameConstants.Abilities.RIPTIDE_DURATION;
        public float riptideCooldown = GameConstants.Abilities.RIPTIDE_COOLDOWN;
        public int riptideCostOrbs = GameConstants.Abilities.RIPTIDE_COST_ORBS;
        public float riptideSpeedMultiplier = GameConstants.Abilities.RIPTIDE_SPEED_MULTIPLIER;

        [Header("Spirit Sense (active)")]
        public float spiritSenseDurationNormal = GameConstants.Abilities.SPIRIT_SENSE_DURATION;
        public float spiritSenseCooldown = GameConstants.SpiritSense.COOLDOWN_SECONDS;
        public int spiritSenseCostOrbs = GameConstants.Abilities.SPIRIT_SENSE_COST_ORBS;
        public float spiritSenseExplorationRadius = GameConstants.SpiritSense.REVEAL_RADIUS;
        [Tooltip("Distance beyond the reveal radius at which the guiding wisp spawns")]
        public float spiritSenseWispTriggerDistance = GameConstants.SpiritSense.REVEAL_RADIUS;

        [Header("Tidebond (resonance)")]
        public float tidebondFatigueSeconds = GameConstants.Abilities.TIDEBOND_FATIGUE_SECONDS;
        public float tidebondCooldown = GameConstants.Abilities.TIDEBOND_COOLDOWN;
        public int tidebondCostOrbs = GameConstants.Abilities.TIDEBOND_COST_ORBS;
        public float tidebondSpeedMultiplier = GameConstants.Abilities.TIDEBOND_SPEED_MULTIPLIER;
        [Tooltip("If the dugong is farther than this from the player when called, it teleports to a point just out of camera view instead of swimming over")]
        public float dugongRecallTeleportDistance = 25f;

        [Header("Dugong Health (the green bar under the dugong card)")]
        [Tooltip("The dugong is much tougher than the player (100 HP)")]
        public float dugongMaxHealth = 300f;
        [Tooltip("Fraction of toxic-water damage the dugong actually takes (it's resilient)")]
        [Range(0f, 1f)] public float dugongDamageTaken = 0.35f;
        [Tooltip("HP per second the dugong recovers once it's out of toxic water")]
        public float dugongRegenPerSecond = 4f;
        [Tooltip("Seconds out of toxic water before the dugong starts recovering")]
        public float dugongRegenDelay = 4f;
        [Tooltip("After collapsing to 0 HP, Tidebond and Seagrass Mend come back once the dugong recovers to this fraction")]
        [Range(0f, 1f)] public float dugongRecoverFraction = 0.5f;

        [Header("Seagrass Mend (support)")]
        public float seagrassMendHealDuration = GameConstants.Abilities.SEAGRASS_MEND_HEAL_DURATION;
        public float seagrassMendCooldown = GameConstants.Abilities.SEAGRASS_MEND_COOLDOWN;
        public float seagrassMendHealAmountNormal = GameConstants.Abilities.SEAGRASS_MEND_HEAL_AMOUNT;
        [Tooltip("How charged the skill is when a level starts (0 = empty, fills over the cooldown)")]
        [Range(0f, 1f)] public float seagrassMendStartCharge = 0f;

        [Header("Bubble Barrier (ultimate)")]
        public float bubbleBarrierDuration = GameConstants.Abilities.BUBBLE_BARRIER_DURATION;
        [Tooltip("Unused: the barrier has no cooldown — it charges from activity (see below)")]
        public float bubbleBarrierCooldown = GameConstants.Abilities.BUBBLE_BARRIER_COOLDOWN;
        public int bubbleBarrierCostOrbs = GameConstants.Abilities.BUBBLE_BARRIER_COST_ORBS;
        [Tooltip("How charged the barrier is when a level starts. After each use it refills from 0.")]
        [Range(0f, 1f)] public float bubbleBarrierStartCharge = 0.5f;
        public float bubbleBarrierRadiusSolo = GameConstants.Abilities.BUBBLE_BARRIER_RADIUS_SOLO;
        public float bubbleBarrierRadiusWithDugong = GameConstants.Abilities.BUBBLE_BARRIER_RADIUS_WITH_DUGONG;
        public float bubbleBarrierDugongProximity = GameConstants.Abilities.BUBBLE_BARRIER_DUGONG_PROXIMITY;
        [Tooltip("Number of contact hits the barrier absorbs before it drops (toxic water blocking continues regardless)")]
        public int bubbleBarrierContactCharges = 1;

        [Header("Bubble Barrier charging (activity-based, no cooldown)")]
        [Tooltip("Charge gained per second while the player is swimming. 1/90 = ~90s of movement from empty to full.")]
        public float bubbleBarrierChargePerSecondMoving = 1f / 90f;
        [Tooltip("Speed (m/s) above which the player counts as moving")]
        public float bubbleBarrierMinMoveSpeed = 0.5f;
        [Tooltip("Movement charge multiplier while riding the dugong (the dugong is doing the swimming)")]
        [Range(0f, 1f)] public float bubbleBarrierMountedChargeFactor = 0.5f;
        [Tooltip("Bonus charge for each skill used (the inventory never counts)")]
        public float bubbleBarrierChargeOnSurge = 0.015f;
        public float bubbleBarrierChargeOnRiptide = 0.04f;
        public float bubbleBarrierChargeOnSpiritSense = 0.06f;
        public float bubbleBarrierChargeOnTidebond = 0.06f;
        public float bubbleBarrierChargeOnSeagrassMend = 0.05f;

        /// <summary>Barrier charge granted when 'id' is used (0 for the barrier itself).</summary>
        public float GetBarrierChargeForSkill(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Surge: return bubbleBarrierChargeOnSurge;
                case AbilityId.Riptide: return bubbleBarrierChargeOnRiptide;
                case AbilityId.SpiritSense: return bubbleBarrierChargeOnSpiritSense;
                case AbilityId.Tidebond: return bubbleBarrierChargeOnTidebond;
                case AbilityId.SeagrassMend: return bubbleBarrierChargeOnSeagrassMend;
                default: return 0f;
            }
        }

        [Header("Hazards")]
        public float toxicLightDpsNormal = GameConstants.Abilities.TOXIC_LIGHT_DPS;
        public float toxicHeavyDpsNormal = GameConstants.Abilities.TOXIC_HEAVY_DPS;
        public float contactHazardDamage = GameConstants.Abilities.CONTACT_HAZARD_DAMAGE;
        public float contactHazardReTriggerDelay = 1f;

        [Header("DDA Anchors — Assist (difficulty 0.0)")]
        public float orbRegenSecondsAssist = 12f;
        public float toxicDamageMultiplierAssist = 0.7f;
        public float seagrassMendHealAssist = 40f;
        public float spiritSenseDurationAssist = 20f;
        public float spiritGlowIntensityAssist = 1.5f;

        [Header("DDA Anchors — Challenge (difficulty 1.0)")]
        public float orbRegenSecondsChallenge = 18f;
        public float toxicDamageMultiplierChallenge = 1.25f;
        public float seagrassMendHealChallenge = 25f;
        public float spiritSenseDurationChallenge = 12f;
        public float spiritGlowIntensityChallenge = 0.6f;

        // ─────────────────────────────────────────────
        // Blended lookups (difficulty: 0 = Assist, 0.5 = Normal, 1 = Challenge)
        // Piecewise-linear through the Normal anchor so 0.5 always equals the
        // designer-authored "Normal" value above, not just the midpoint.
        // ─────────────────────────────────────────────

        public float GetOrbRegenSeconds(float difficulty) =>
            BlendThroughNormal(orbRegenSecondsAssist, orbRegenSecondsNormal, orbRegenSecondsChallenge, difficulty);

        public float GetToxicDamageMultiplier(float difficulty) =>
            BlendThroughNormal(toxicDamageMultiplierAssist, 1.0f, toxicDamageMultiplierChallenge, difficulty);

        public float GetSeagrassMendHeal(float difficulty) =>
            BlendThroughNormal(seagrassMendHealAssist, seagrassMendHealAmountNormal, seagrassMendHealChallenge, difficulty);

        public float GetSpiritSenseDuration(float difficulty) =>
            BlendThroughNormal(spiritSenseDurationAssist, spiritSenseDurationNormal, spiritSenseDurationChallenge, difficulty);

        public float GetSpiritGlowIntensity(float difficulty) =>
            BlendThroughNormal(spiritGlowIntensityAssist, 1.0f, spiritGlowIntensityChallenge, difficulty);

        private static float BlendThroughNormal(float assist, float normal, float challenge, float difficulty)
        {
            difficulty = Mathf.Clamp01(difficulty);
            return difficulty <= 0.5f
                ? Mathf.Lerp(assist, normal, difficulty / 0.5f)
                : Mathf.Lerp(normal, challenge, (difficulty - 0.5f) / 0.5f);
        }
    }
}
