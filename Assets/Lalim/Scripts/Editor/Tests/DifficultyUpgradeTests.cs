using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lalim.Challenges;
using Lalim.Core;
using Lalim.DDA;

namespace Lalim.Tests
{
    public class DdaScoringTests
    {
        private DDAConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<DDAConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        private static ChallengeCompletedEvent Run(float completion, float firstAction, int errors, int hintsUsed, int hintBudget) =>
            new ChallengeCompletedEvent
            {
                ChallengeId = "test",
                Success = true,
                CompletionTime = completion,
                ExpectedTime = 60f,
                HesitationTime = 0f,
                ErrorCount = errors,
                FirstActionLatency = firstAction,
                ExpectedFirstAction = 6f,
                HintsUsed = hintsUsed,
                HintBudget = hintBudget
            };

        [Test]
        public void QuickPickupScoresHigherThanSlowPickup()
        {
            float quick = DDAManager.ScorePerformance(_config, Run(60f, 2f, 0, 0, 0), out _);
            float slow = DDAManager.ScorePerformance(_config, Run(60f, 15f, 0, 0, 0), out _);
            Assert.Greater(quick, slow);
        }

        [Test]
        public void UsingHintsLowersTheScore()
        {
            float none = DDAManager.ScorePerformance(_config, Run(60f, 6f, 0, 0, 3), out _);
            float all = DDAManager.ScorePerformance(_config, Run(60f, 6f, 0, 3, 3), out _);
            Assert.Greater(none, all);
        }

        [Test]
        public void MinigamesWithoutHintsAreNotRewardedForUnusedHints()
        {
            // Budget 0 = no hint button: the hint metric is left out, not counted as "no hints used".
            float noHintUi = DDAManager.ScorePerformance(_config, Run(60f, 6f, 0, 0, 0), out string breakdown);
            StringAssert.DoesNotContain("hints", breakdown);
            Assert.That(noHintUi, Is.InRange(0f, 1f));
        }

        [Test]
        public void MissingFirstActionIsLeftOut()
        {
            DDAManager.ScorePerformance(_config, Run(60f, -1f, 0, 0, 0), out string breakdown);
            StringAssert.DoesNotContain("pickup", breakdown);
        }

        [Test]
        public void HistoryPullsTowardEarlierMinigames()
        {
            var earlier = new List<float> { 0.2f, 0.2f };
            Assert.AreEqual(0.9f, DDAManager.BlendWithHistory(0.9f, new List<float>(), 0.35f), 1e-5f, "no history: latest stands");
            float blended = DDAManager.BlendWithHistory(0.9f, earlier, 0.35f);
            Assert.AreEqual(Mathf.Lerp(0.9f, 0.2f, 0.35f), blended, 1e-5f);
        }

        [Test]
        public void HintScoreRange()
        {
            Assert.AreEqual(1f, DDAManager.CalculateHintScore(0, 3), 1e-5f);
            Assert.AreEqual(0.2f, DDAManager.CalculateHintScore(3, 3), 1e-5f);
        }
    }

    public class MinigameProfileTests
    {
        [Test]
        public void BlendsThroughNormalAtHalfDifficulty()
        {
            var p = ScriptableObject.CreateInstance<MinigameDifficultyProfile>();
            p.EnsureKnob("count", 12f, 18f, 25f);
            Assert.AreEqual(12f, p.Get("count", 0f), 1e-4f);
            Assert.AreEqual(15f, p.Get("count", 0.25f), 1e-4f);
            Assert.AreEqual(18f, p.Get("count", 0.5f), 1e-4f);
            Assert.AreEqual(25f, p.Get("count", 1f), 1e-4f);
            Object.DestroyImmediate(p);
        }

        [Test]
        public void EnsureKnobKeepsTunedValues()
        {
            var p = ScriptableObject.CreateInstance<MinigameDifficultyProfile>();
            p.EnsureKnob("count", 12f, 18f, 25f);
            p.knobs[0].normal = 20f; // designer tweak
            p.EnsureKnob("count", 1f, 2f, 3f);
            Assert.AreEqual(20f, p.Get("count", 0.5f), 1e-4f);
            Object.DestroyImmediate(p);
        }
    }

    public class WaterReadingTests
    {
        [Test]
        public void HealthyRangesMatchHaiksDialogue()
        {
            Assert.IsTrue(WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.PH, 8.1f));
            Assert.IsFalse(WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.PH, 7.6f));
            Assert.IsTrue(WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.Salinity, 35f));
            Assert.IsFalse(WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.Salinity, 41f));
            Assert.IsTrue(WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.Temperature, 27f));
            Assert.IsFalse(WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.Temperature, 31f));
        }

        [Test]
        public void ReadingsAreFormattedForTheScanner()
        {
            Assert.AreEqual("pH 7.6", WaterCalibrationMinigame.FormatValue(WaterCalibrationMinigame.Param.PH, 7.6f));
            Assert.AreEqual("Salinity 41 ppt", WaterCalibrationMinigame.FormatValue(WaterCalibrationMinigame.Param.Salinity, 41f));
        }
    }
}
