using NUnit.Framework;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.Tests
{
    public class ToastTests
    {
        [Test]
        public void QuestCompleteNamesEveryReward()
        {
            Assert.AreEqual("You received a Radiant Conch", CleanseZoneStep.ReceivedLine(new[] { "Radiant Conch" }));
            Assert.AreEqual("You received a Radiant Conch and a Water Sampling Kit",
                CleanseZoneStep.ReceivedLine(new[] { "Radiant Conch", "Water Sampling Kit" }));
            Assert.AreEqual("You received a Compass, an Oar and a Net",
                CleanseZoneStep.ReceivedLine(new[] { "Compass", "Oar", "Net" }));
        }

        [Test]
        public void ToastsPlayActThenQuestCompleteThenAchievements()
        {
            Assert.Less((int)ToastKind.Act, (int)ToastKind.QuestComplete);
            Assert.Less((int)ToastKind.QuestComplete, (int)ToastKind.Reward);
            Assert.Less((int)ToastKind.Reward, (int)ToastKind.Achievement);
        }
    }
}
