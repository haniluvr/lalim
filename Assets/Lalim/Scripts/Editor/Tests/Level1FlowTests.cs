using NUnit.Framework;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.Tests
{
    public class LevelToxicityTests
    {
        [TearDown]
        public void TearDown()
        {
            LevelToxicity.ReleaseAll();
            LevelToxicity.ClearLevelDps();
        }

        [Test]
        public void Level1IsPinnedToBaseRateWhateverTheDifficulty()
        {
            Assert.AreEqual(0.5f, LevelToxicity.ComputeLevelDps(0.5f, 0f, 0.6f, 1.6f, pinToBase: true), 1e-5f);
            Assert.AreEqual(0.5f, LevelToxicity.ComputeLevelDps(0.5f, 1f, 0.6f, 1.6f, pinToBase: true), 1e-5f);
        }

        [Test]
        public void LaterLevelsScaleThroughNormal()
        {
            Assert.AreEqual(0.3f, LevelToxicity.ComputeLevelDps(0.5f, 0f, 0.6f, 1.6f, false), 1e-5f);
            Assert.AreEqual(0.5f, LevelToxicity.ComputeLevelDps(0.5f, 0.5f, 0.6f, 1.6f, false), 1e-5f);
            Assert.AreEqual(0.8f, LevelToxicity.ComputeLevelDps(0.5f, 1f, 0.6f, 1.6f, false), 1e-5f);
        }

        [Test]
        public void DrainIsPausedWhileAnySourceSuppressesIt()
        {
            object dialogue = "dialogue", minigame = "minigame";
            LevelToxicity.Suppress(dialogue);
            LevelToxicity.Suppress(minigame);
            LevelToxicity.Release(dialogue);
            Assert.IsTrue(LevelToxicity.IsSuppressed);
            LevelToxicity.Release(minigame);
            Assert.IsFalse(LevelToxicity.IsSuppressed);
        }

        [Test]
        public void LevelRateIsUnsetUntilTheDirectorSetsIt()
        {
            Assert.Less(LevelToxicity.CurrentDps, 0f);
            LevelToxicity.SetLevelDps(0.5f);
            Assert.AreEqual(0.5f, LevelToxicity.CurrentDps, 1e-5f);
        }
    }

    public class ProximityMusicTests
    {
        private GameObject _go;
        private ProximityMusic _music;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("music");
            _music = _go.AddComponent<ProximityMusic>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void LouderWhenCloserAndNeverSilent()
        {
            float far = _music.VolumeForDistance(500f);
            float mid = _music.VolumeForDistance(60f);
            float near = _music.VolumeForDistance(2f);
            Assert.Greater(far, 0f, "music must never go silent");
            Assert.Less(far, mid);
            Assert.Less(mid, near);
            Assert.AreEqual(1f, near, 1e-4f);
        }
    }

    public class Level1DialogueTests
    {
        [Test]
        public void EveryStatueHasQuestClosingAndIdleLines()
        {
            Assert.IsNotEmpty(Level1DialogueScripts.AmansinayaQuest());
            Assert.IsNotEmpty(Level1DialogueScripts.AmansinayaClosing());
            Assert.IsNotEmpty(Level1DialogueScripts.AmansinayaIdle());
            Assert.IsNotEmpty(Level1DialogueScripts.HaikQuest());
            Assert.IsNotEmpty(Level1DialogueScripts.HaikClosing());
            Assert.IsNotEmpty(Level1DialogueScripts.NegunoQuest());
            Assert.IsNotEmpty(Level1DialogueScripts.NegunoClosing());
        }

        [Test]
        public void LinesFitTheLongDialogueBox()
        {
            foreach (var lines in new[]
            {
                Level1DialogueScripts.AmansinayaQuest(), Level1DialogueScripts.AmansinayaClosing(),
                Level1DialogueScripts.HaikQuest(), Level1DialogueScripts.HaikClosing(),
                Level1DialogueScripts.NegunoQuest(), Level1DialogueScripts.NegunoClosing()
            })
            {
                foreach (DialogueLine line in lines)
                {
                    Assert.LessOrEqual(line.text.Length, 170, $"Too long for the dialogue box: {line.text}");
                }
            }
        }

        [Test]
        public void HaikTeachesTheHealthyRangesHardModeHides()
        {
            string all = string.Join(" ", Level1DialogueScripts.HaikQuest().ConvertAll(l => l.text));
            StringAssert.Contains("7.9", all);
            StringAssert.Contains("33", all);
            StringAssert.Contains("29", all);
        }

        [Test]
        public void PlayerTokenFallsBackToPlayerWithoutASave()
        {
            Assert.AreEqual("Take care, Player.", DialogueManager.ResolveTokens("Take care, {player}."));
        }
    }

    public class SaveDataTests
    {
        [Test]
        public void ChallengeIdsAreAddedOnce()
        {
            var save = new SaveData();
            save.AddChallengeId("luzon_z1_talk");
            save.AddChallengeId("luzon_z1_talk");
            Assert.AreEqual(1, save.completedChallengeIds.Count);
            Assert.IsTrue(save.HasChallengeId("luzon_z1_talk"));
        }

        [Test]
        public void ResumeFieldsSurviveJsonRoundTrip()
        {
            var save = new SaveData { lastSceneName = "02_Luzon", lastCheckpointId = "cp_z2_statue", levelToxicDps = 0.5f };
            save.collectedTrashIds.Add("Prop_Trash_Debris.013");
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
            Assert.AreEqual("02_Luzon", back.lastSceneName);
            Assert.AreEqual("cp_z2_statue", back.lastCheckpointId);
            Assert.AreEqual(0.5f, back.levelToxicDps, 1e-5f);
            Assert.Contains("Prop_Trash_Debris.013", back.collectedTrashIds);
        }
    }
}
