using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.DDA;

namespace Lalim.Tests
{
    public class StaminaPoolTests
    {
        private const float Delay = 1f;
        private const float SecondsPerOrb = 5f;

        [Test]
        public void StartsFull()
        {
            var pool = new StaminaPool(4);
            Assert.AreEqual(4, pool.FullOrbs);
        }

        [Test]
        public void NewLevelStartsWithTwoOfFourOrbsAndRegensFromThere()
        {
            var pool = new StaminaPool(4, GameConstants.Abilities.STARTING_STAMINA_ORBS);
            Assert.AreEqual(2, pool.FullOrbs);
            pool.Tick(SecondsPerOrb, SecondsPerOrb, 1f);
            Assert.AreEqual(3, pool.FullOrbs);
        }

        [Test]
        public void StartingOrbsAreClampedToMax()
        {
            Assert.AreEqual(4, new StaminaPool(4, 9f).FullOrbs);
            Assert.AreEqual(0, new StaminaPool(4, -1f).FullOrbs);
        }

        [Test]
        public void SpendingFromPartialOrbKeepsProgress()
        {
            var pool = new StaminaPool(4);
            pool.TrySpend(2, 0f);                    // 2.0
            pool.Tick(3f, SecondsPerOrb, 1f);        // 2.6
            Assert.AreEqual(2.6f, pool.Current, 1e-4f);

            Assert.IsTrue(pool.TrySpend(1, Delay));
            Assert.AreEqual(1.6f, pool.Current, 1e-4f);
            Assert.AreEqual(1, pool.FullOrbs);
        }

        [Test]
        public void PartialOrbCannotBeSpent()
        {
            var pool = new StaminaPool(4);
            pool.TrySpend(4, 0f);
            pool.Tick(4.9f, SecondsPerOrb, 1f);      // 0.98 orbs
            Assert.IsFalse(pool.TrySpend(1, Delay));
            Assert.AreEqual(0.98f, pool.Current, 1e-4f);
        }

        [Test]
        public void RegenWaitsForDelayAfterSpend()
        {
            var pool = new StaminaPool(4);
            pool.TrySpend(1, Delay);
            pool.Tick(0.9f, SecondsPerOrb, 1f);
            Assert.AreEqual(3f, pool.Current, 1e-4f, "no regen during the 1s delay");

            pool.Tick(0.6f, SecondsPerOrb, 1f);      // 0.1s delay left, 0.5s of regen
            Assert.AreEqual(3.1f, pool.Current, 1e-4f);
        }

        [Test]
        public void OneOrbTakesFiveSeconds()
        {
            var pool = new StaminaPool(4);
            pool.TrySpend(1, 0f);
            pool.Tick(5f, SecondsPerOrb, 1f);
            Assert.AreEqual(4, pool.FullOrbs);
        }

        [Test]
        public void MultipliersStack()
        {
            // Mounted (1.5×) in unshielded toxic water (0.5×) = 0.75×.
            var pool = new StaminaPool(4);
            pool.TrySpend(4, 0f);
            pool.Tick(4f, SecondsPerOrb, 1.5f * 0.5f);
            Assert.AreEqual(0.6f, pool.Current, 1e-4f);
        }

        [Test]
        public void NeverExceedsMax()
        {
            var pool = new StaminaPool(4);
            pool.TrySpend(1, 0f);
            pool.Tick(100f, SecondsPerOrb, 1.5f);
            Assert.AreEqual(4f, pool.Current, 1e-4f);
        }

        [Test]
        public void BasicSkillsCostNoStamina()
        {
            // Surge and Riptide are basic skills: cooldown only.
            var config = ScriptableObject.CreateInstance<AbilityConfig>();
            Assert.AreEqual(0, config.surgeCostOrbs);
            Assert.AreEqual(0, config.riptideCostOrbs);
            Object.DestroyImmediate(config);

            var shipped = AssetDatabase.LoadAssetAtPath<AbilityConfig>("Assets/Lalim/Data/AbilityConfig.asset");
            Assert.IsNotNull(shipped);
            Assert.AreEqual(0, shipped.surgeCostOrbs);
            Assert.AreEqual(0, shipped.riptideCostOrbs);
        }

        [Test]
        public void ChargeSkillsAreFreeAndStartAtTheRightCharge()
        {
            // Bubble Barrier and Seagrass Mend charge over time instead of costing stamina.
            var config = ScriptableObject.CreateInstance<AbilityConfig>();
            Assert.AreEqual(0, config.bubbleBarrierCostOrbs);
            Assert.AreEqual(0.5f, config.bubbleBarrierStartCharge, 1e-4f, "barrier starts each level half-charged");
            Assert.AreEqual(0f, config.seagrassMendStartCharge, 1e-4f, "mend starts each level empty");
            Object.DestroyImmediate(config);

            var shipped = AssetDatabase.LoadAssetAtPath<AbilityConfig>("Assets/Lalim/Data/AbilityConfig.asset");
            Assert.AreEqual(0, shipped.bubbleBarrierCostOrbs);
            Assert.AreEqual(0.5f, shipped.bubbleBarrierStartCharge, 1e-4f);
            Assert.AreEqual(0f, shipped.seagrassMendStartCharge, 1e-4f);
        }
    }

    /// <summary>
    /// The skill economy as a whole (Normal difficulty, shipped config). These pin the calibration
    /// so a later tweak can't silently break how the pieces fit together.
    /// </summary>
    public class SkillEconomyTests
    {
        private AbilityConfig Shipped => AssetDatabase.LoadAssetAtPath<AbilityConfig>("Assets/Lalim/Data/AbilityConfig.asset");

        /// <summary>Stamina spent per second if Spirit Sense and Tidebond are used as often as their cooldowns allow.</summary>
        private static float MaxStaminaDemandPerSecond(AbilityConfig c)
        {
            float senseCycle = c.spiritSenseDurationNormal + c.spiritSenseCooldown;
            float tidebondCycle = c.tidebondFatigueSeconds + c.tidebondCooldown;
            return c.spiritSenseCostOrbs / senseCycle + c.tidebondCostOrbs / tidebondCycle;
        }

        [Test]
        public void OnlySpiritSenseAndTidebondCostStamina()
        {
            var c = Shipped;
            Assert.AreEqual(0, c.surgeCostOrbs);
            Assert.AreEqual(0, c.riptideCostOrbs);
            Assert.AreEqual(0, c.bubbleBarrierCostOrbs);
            Assert.Greater(c.spiritSenseCostOrbs, 0);
            Assert.Greater(c.tidebondCostOrbs, 0);
        }

        [Test]
        public void UsingBothStaminaSkillsIsSustainableInOpenWater()
        {
            var c = Shipped;
            float supply = 1f / c.GetOrbRegenSeconds(0.5f);
            float demand = MaxStaminaDemandPerSecond(c);
            // With Tidebond's 7s cooldown the two together cost about exactly the refill (break-even).
            Assert.LessOrEqual(demand, supply * 1.02f, "refill should (about) keep up when both are used as often as possible");
            Assert.Greater(demand, supply * 0.6f, "...but only just, so stamina still matters");
        }

        [Test]
        public void ToxicWaterForcesAChoice()
        {
            var c = Shipped;
            float toxicSupply = c.regenMultiplierToxic / c.GetOrbRegenSeconds(0.5f);
            Assert.Greater(MaxStaminaDemandPerSecond(c), toxicSupply, "in toxic water you can't keep both up — use Barrier or choose");
        }

        [Test]
        public void EveryStaminaSkillIsAffordableFromAFullPool()
        {
            var c = Shipped;
            Assert.LessOrEqual(c.spiritSenseCostOrbs + c.tidebondCostOrbs, c.maxStaminaOrbs, "Sense + Tidebond back to back from full");
        }

        [Test]
        public void RidingRegenBonusIsSmall()
        {
            // Refill never stops, so a 45s ride regains ~3 shells anyway; the riding BONUS on top
            // must stay under one shell so riding isn't a stamina farm.
            var c = Shipped;
            float bonusShellsPerRide = c.tidebondFatigueSeconds * (c.regenMultiplierMounted - 1f) / c.GetOrbRegenSeconds(0.5f);
            Assert.Less(bonusShellsPerRide, 1f);
        }

        [Test]
        public void BarrierChargesFromActivityNotTime()
        {
            var c = Shipped;
            Assert.AreEqual(0f, c.bubbleBarrierCooldown, 1e-4f, "no cooldown");
            float secondsOfSwimmingFromEmpty = 1f / c.bubbleBarrierChargePerSecondMoving;
            Assert.That(secondsOfSwimmingFromEmpty, Is.InRange(60f, 120f), "about a minute and a half of movement");
            Assert.AreEqual(0f, c.GetBarrierChargeForSkill(AbilityId.BubbleBarrier), 1e-4f, "using the barrier never charges it");
            foreach (AbilityId id in new[] { AbilityId.Surge, AbilityId.Riptide, AbilityId.SpiritSense, AbilityId.Tidebond, AbilityId.SeagrassMend })
            {
                Assert.Greater(c.GetBarrierChargeForSkill(id), 0f, id + " should add barrier charge");
            }
        }

        [Test]
        public void SurgeSpamCannotOutpaceSwimmingForBarrierCharge()
        {
            // A 3-surge combo every (window + cooldown) seconds shouldn't charge the barrier
            // faster than twice plain swimming — skills help, they don't replace moving.
            var c = Shipped;
            float comboCycle = c.surgeComboWindow + c.surgeCooldown;
            float surgeChargePerSecond = c.surgeComboCharges * c.bubbleBarrierChargeOnSurge / comboCycle;
            Assert.Less(surgeChargePerSecond, c.bubbleBarrierChargePerSecondMoving * 2f);
        }
    }

    public class SwimStaminaTests
    {
        private const float RiptidePerSegment = 10f;

        [Test]
        public void StartsFullWithSixSegments()
        {
            var pool = new SwimStaminaPool(GameConstants.Abilities.SWIM_STAMINA_SEGMENTS);
            Assert.AreEqual(6, pool.Segments);
            Assert.AreEqual(6, pool.FullSegments);
            Assert.AreEqual(1f, pool.Normalized, 1e-4f);
        }

        [TestCase(1, 1f / 3f)]
        [TestCase(2, 1f / 2f)]
        [TestCase(3, 1f)]
        public void SurgeCombosChargeAThirdAHalfAndAWholeSegment(int surges, float expected)
        {
            var pool = new SwimStaminaPool(6);
            pool.SetCurrent(0f);
            for (int i = 1; i <= surges; i++) pool.AddSurgeCharge(i);
            Assert.AreEqual(expected, pool.Current, 1e-4f);
        }

        [Test]
        public void ChargeIsCappedAtFull()
        {
            var pool = new SwimStaminaPool(6);
            pool.SetCurrent(5.9f);
            pool.AddSurgeCharge(3);
            Assert.AreEqual(6f, pool.Current, 1e-4f);
        }

        [Test]
        public void NeverRefillsOnItsOwn()
        {
            var pool = new SwimStaminaPool(6);
            pool.SetCurrent(2.5f);
            for (int i = 0; i < 600; i++) pool.Tick(0.1f, RiptidePerSegment);   // a minute
            Assert.AreEqual(2.5f, pool.Current, 1e-4f);
        }

        [Test]
        public void RiptideBurnsOnlyWholeSegmentsAndKeepsTheRest()
        {
            // 4 full segments and a third of the 5th: 40 s of Riptide, the third stays.
            var pool = new SwimStaminaPool(6);
            pool.SetCurrent(4f + 1f / 3f);
            Assert.AreEqual(40f, pool.RiptideSecondsAvailable(RiptidePerSegment), 1e-3f);
            Assert.IsTrue(pool.BeginRiptide());
            for (int i = 0; i < 450; i++) pool.Tick(0.1f, RiptidePerSegment);   // 45 s
            Assert.AreEqual(1f / 3f, pool.Current, 1e-3f, "the partial segment is never burnt");
            pool.EndRiptide();
            Assert.AreEqual(0, pool.FullSegments);
        }

        [Test]
        public void RiptideDrainsOneSegmentPerTenSeconds()
        {
            var pool = new SwimStaminaPool(6);
            pool.BeginRiptide();
            for (int i = 0; i < 100; i++) pool.Tick(0.1f, RiptidePerSegment);
            Assert.AreEqual(5f, pool.Current, 1e-3f);
        }

        [Test]
        public void RiptideNeedsAWholeSegment()
        {
            var pool = new SwimStaminaPool(6);
            pool.SetCurrent(0.9f);
            Assert.AreEqual(0f, pool.RiptideSecondsAvailable(RiptidePerSegment));
            Assert.IsFalse(pool.BeginRiptide());
        }

        [Test]
        public void SurgesDontChargeDuringRiptide()
        {
            var pool = new SwimStaminaPool(6);
            pool.SetCurrent(3f);
            pool.BeginRiptide();
            pool.AddSurgeCharge(1);
            Assert.AreEqual(3f, pool.Current, 1e-4f);
        }

        [Test]
        public void ShippedConfigMatchesTheDesign()
        {
            var c = AssetDatabase.LoadAssetAtPath<AbilityConfig>("Assets/Lalim/Data/AbilityConfig.asset");
            Assert.IsNotNull(c);
            Assert.AreEqual(6, c.swimStaminaSegments);
            Assert.AreEqual(10f, c.riptideSecondsPerSegment, 1e-4f);
        }
    }

    public class AbilityConfigBlendTests
    {
        private AbilityConfig _config;

        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<AbilityConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);

        [TestCase(0f, 12f)]
        [TestCase(0.5f, 15f)]
        [TestCase(1f, 18f)]
        [TestCase(0.25f, 13.5f)]
        [TestCase(0.75f, 16.5f)]
        public void OrbRegenAnchors(float difficulty, float expected)
        {
            Assert.AreEqual(expected, _config.GetOrbRegenSeconds(difficulty), 1e-4f);
        }

        [Test]
        public void OtherAnchorsMatchDesignTable()
        {
            Assert.AreEqual(0.7f, _config.GetToxicDamageMultiplier(0f), 1e-4f);
            Assert.AreEqual(1f, _config.GetToxicDamageMultiplier(0.5f), 1e-4f);
            Assert.AreEqual(1.25f, _config.GetToxicDamageMultiplier(1f), 1e-4f);

            Assert.AreEqual(40f, _config.GetSeagrassMendHeal(0f), 1e-4f);
            Assert.AreEqual(30f, _config.GetSeagrassMendHeal(0.5f), 1e-4f);
            Assert.AreEqual(25f, _config.GetSeagrassMendHeal(1f), 1e-4f);

            Assert.AreEqual(20f, _config.GetSpiritSenseDuration(0f), 1e-4f);
            Assert.AreEqual(15f, _config.GetSpiritSenseDuration(0.5f), 1e-4f);
            Assert.AreEqual(12f, _config.GetSpiritSenseDuration(1f), 1e-4f);

            Assert.AreEqual(1.5f, _config.GetSpiritGlowIntensity(0f), 1e-4f);
            Assert.AreEqual(1f, _config.GetSpiritGlowIntensity(0.5f), 1e-4f);
            Assert.AreEqual(0.6f, _config.GetSpiritGlowIntensity(1f), 1e-4f);
        }

        [Test]
        public void OutOfRangeDifficultyIsClamped()
        {
            Assert.AreEqual(12f, _config.GetOrbRegenSeconds(-1f), 1e-4f);
            Assert.AreEqual(18f, _config.GetOrbRegenSeconds(2f), 1e-4f);
        }
    }

    public class DDAConfigTests
    {
        private DDAConfig _config;

        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<DDAConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_config);

        [TestCase(0f, 0)]
        [TestCase(0.5f, 1)]
        [TestCase(0.99f, 2)]
        [TestCase(1f, 2)]
        public void ComplexityTierIsClamped(float difficulty, int expected)
        {
            Assert.AreEqual(expected, _config.GetComplexityTier(difficulty));
        }

        [Test]
        public void TimerAndTaskBlendThroughNormal()
        {
            Assert.AreEqual(1.3f, _config.GetTimerMultiplier(0f), 1e-4f);
            Assert.AreEqual(1f, _config.GetTimerMultiplier(0.5f), 1e-4f);
            Assert.AreEqual(0.8f, _config.GetTimerMultiplier(1f), 1e-4f);

            Assert.AreEqual(0.8f, _config.GetTaskMultiplier(0f), 1e-4f);
            Assert.AreEqual(1f, _config.GetTaskMultiplier(0.5f), 1e-4f);
            Assert.AreEqual(1.4f, _config.GetTaskMultiplier(1f), 1e-4f);
        }

        [Test]
        public void HintAnchors()
        {
            Assert.AreEqual(5, _config.GetHintBudget(0f));
            Assert.AreEqual(3, _config.GetHintBudget(0.5f));
            Assert.AreEqual(1, _config.GetHintBudget(1f));
        }

        [Test]
        public void SignalScoresPushInTheIntendedDirection()
        {
            Assert.Less(_config.failedChallengeScore, _config.strugglingThreshold, "failed minigame should step down");
            Assert.Less(_config.deathScore, _config.strugglingThreshold, "death should step down");
            Assert.Less(_config.nearDeathSectionScore, _config.strugglingThreshold, "near-deaths should step down");
            Assert.Greater(_config.cleanSectionScore, _config.tooEasyThreshold, "clean section should step up");
            Assert.AreEqual(2f, _config.deathStepScale, "death counts as a double step");
        }

        [Test]
        public void ShippedAssetUsesNarrowedRanges()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DDAConfig>("Assets/Lalim/Data/DDAConfig.asset");
            Assert.IsNotNull(asset);
            Assert.AreEqual(0.8f, asset.minTaskMultiplier, 1e-4f);
            Assert.AreEqual(1.4f, asset.maxTaskMultiplier, 1e-4f);
            Assert.AreEqual(1.3f, asset.minTimerMultiplier, 1e-4f);
            Assert.AreEqual(0.8f, asset.maxTimerMultiplier, 1e-4f);
        }
    }

    public class ToxicDamageTests
    {
        [Test]
        public void OverlappingZonesUseOnlyTheStrongest()
        {
            Assert.AreEqual(ToxicSeverity.Heavy, ToxicWaterTracker.Strongest(new[] { ToxicSeverity.Light, ToxicSeverity.Heavy, ToxicSeverity.Light }));
            Assert.AreEqual(ToxicSeverity.Light, ToxicWaterTracker.Strongest(new[] { ToxicSeverity.Light, ToxicSeverity.Light }));
            Assert.AreEqual(ToxicSeverity.None, ToxicWaterTracker.Strongest(new ToxicSeverity[0]));
        }

        [TestCase(ToxicSeverity.Light, 0.5f, 3f)]
        [TestCase(ToxicSeverity.Heavy, 0.5f, 6f)]
        [TestCase(ToxicSeverity.Heavy, 0f, 4.2f)]
        [TestCase(ToxicSeverity.Heavy, 1f, 7.5f)]
        public void DpsMatchesDesignAndDda(ToxicSeverity severity, float difficulty, float expected)
        {
            var config = ScriptableObject.CreateInstance<AbilityConfig>();
            var go = new GameObject("toxic", typeof(BoxCollider));
            var volume = go.AddComponent<ToxicWaterVolume>();
            var so = new SerializedObject(volume);
            so.FindProperty("_severity").enumValueIndex = (int)severity;
            so.FindProperty("_config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(expected, volume.GetDps(difficulty), 1e-4f);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void HeavyCrossingWithRiptideAndOneHealIsSurvivable()
        {
            // Design rule: 150m of heavy toxic water with Riptide (8.75 m/s), unshielded, one heal.
            var config = ScriptableObject.CreateInstance<AbilityConfig>();
            float seconds = 150f / (GameConstants.Player.MOVE_SPEED * config.riptideSpeedMultiplier);
            float damage = seconds * config.toxicHeavyDpsNormal;
            float endHp = config.maxHealth - damage + config.GetSeagrassMendHeal(0.5f);

            Assert.AreEqual(17.1f, seconds, 0.1f);
            Assert.Greater(endHp, 20f);
            Assert.Less(endHp, 30f);
            Object.DestroyImmediate(config);
        }
    }
}
