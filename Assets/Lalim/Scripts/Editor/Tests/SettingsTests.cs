using NUnit.Framework;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Tests
{
    public class GameSettingsTests
    {
        [Test]
        public void MutedChannelIsSilentButKeepsItsLevel()
        {
            var s = new GameSettings { sfx = 6 };
            s.SetMuted(AudioChannel.Sfx, true);
            Assert.AreEqual(0f, s.Gain(AudioChannel.Sfx));
            Assert.AreEqual(6, s.GetVolume(AudioChannel.Sfx));
            s.SetMuted(AudioChannel.Sfx, false);
            Assert.AreEqual(0.36f, s.Gain(AudioChannel.Sfx), 1e-4f);
        }

        [Test]
        public void GainIsZeroAtZeroAndOneAtMax()
        {
            var s = new GameSettings();
            s.SetVolume(AudioChannel.Music, 0);
            s.SetVolume(AudioChannel.Voice, GameSettings.VolumeSteps);
            Assert.AreEqual(0f, s.Gain(AudioChannel.Music));
            Assert.AreEqual(1f, s.Gain(AudioChannel.Voice));
        }

        [Test]
        public void SanitizeClampsOutOfRangeValues()
        {
            var s = new GameSettings { music = 99, sfx = -3, graphicsPreset = 12, cameraSensitivity = 0 };
            s.Sanitize();
            Assert.AreEqual(GameSettings.VolumeSteps, s.music);
            Assert.AreEqual(0, s.sfx);
            Assert.AreEqual(GameSettings.GraphicsPresetNames.Length - 1, s.graphicsPreset);
            Assert.AreEqual(1, s.cameraSensitivity);
        }

        [Test]
        public void LookMultiplierIsOneAtDefaultAndMonotonic()
        {
            var s = new GameSettings();
            Assert.AreEqual(1f, s.LookMultiplier, 1e-4f);
            float last = 0f;
            for (int i = 1; i <= GameSettings.SensitivitySteps; i++)
            {
                s.cameraSensitivity = i;
                Assert.Greater(s.LookMultiplier, last);
                last = s.LookMultiplier;
            }
        }

        [Test]
        public void CloneIsIndependentAndSameAsDetectsChanges()
        {
            var a = new GameSettings();
            var b = a.Clone();
            Assert.IsTrue(a.SameAs(b));
            b.autoSwim = !b.autoSwim;
            Assert.IsFalse(a.SameAs(b));
            Assert.AreNotEqual(a.autoSwim, b.autoSwim);
        }

        [Test]
        public void JsonRoundTripKeepsEverything()
        {
            var a = new GameSettings { music = 3, ambientMuted = true, graphicsPreset = 3, highFrameRate = false, invertCameraY = true,
                                       haptics = false, spatialAudio = false, gyroscope = true, rightHandedMode = false, textureQuality = 0, sssQuality = true };
            var b = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(a));
            Assert.IsTrue(a.SameAs(b));
        }

        [Test]
        public void EveryPresetMapsToARealQualityLevel()
        {
            for (int p = 0; p < GameSettings.CustomizePreset; p++)
            {
                int level = SettingsManager.QualityLevelForPreset(p);
                Assert.That(level, Is.InRange(0, QualitySettings.names.Length - 1));
            }
            Assert.Less(SettingsManager.QualityLevelForPreset(0), SettingsManager.QualityLevelForPreset(3));
        }

        [Test]
        public void PresetsSetEveryRowAndUltraBeatsSmooth()
        {
            var s = new GameSettings();
            s.ApplyPreset(0);
            int smooth = 0;
            foreach (QualityRow row in System.Enum.GetValues(typeof(QualityRow))) smooth += s.GetTier(row);
            s.ApplyPreset(3);
            int ultra = 0;
            foreach (QualityRow row in System.Enum.GetValues(typeof(QualityRow))) ultra += s.GetTier(row);
            Assert.Greater(ultra, smooth);
            Assert.AreEqual(3, s.graphicsPreset);
            Assert.IsTrue(s.sssQuality);
        }

        [Test]
        public void ChangingOneRowSwitchesToCustomizeAndKeepsTheRest()
        {
            var s = new GameSettings();
            s.ApplyPreset(2);
            int mapping = s.mappingQuality;
            s.SetTierCustom(QualityRow.Texture, 0);
            Assert.AreEqual(GameSettings.CustomizePreset, s.graphicsPreset);
            Assert.AreEqual(0, s.textureQuality);
            Assert.AreEqual(mapping, s.mappingQuality);

            s.ApplyPreset(GameSettings.CustomizePreset); // choosing Customize itself changes nothing
            Assert.AreEqual(0, s.textureQuality);
        }

        [Test]
        public void CustomizeFollowsTheClosestQualityLevel()
        {
            var s = new GameSettings();
            foreach (QualityRow row in System.Enum.GetValues(typeof(QualityRow))) s.SetTierCustom(row, 0);
            Assert.AreEqual(0, s.EffectivePreset);
            foreach (QualityRow row in System.Enum.GetValues(typeof(QualityRow))) s.SetTierCustom(row, 2);
            Assert.AreEqual(3, s.EffectivePreset);
        }
    }
}
