using System.Collections.Generic;
using NUnit.Framework;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Tests
{
    public class LoadingScreenTests
    {
        [Test]
        public void FactsCoverEveryCategoryAndFitTheCard()
        {
            List<LoadingScreen.Fact> facts = LoadingFacts.Default();
            var categories = new HashSet<string>();
            foreach (LoadingScreen.Fact f in facts)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(f.text));
                Assert.LessOrEqual(f.text.Length, 170, $"Too long for the fact card: {f.text}");
                categories.Add(f.category);
            }
            foreach (string c in new[] { LoadingFacts.Deity, LoadingFacts.Relic, LoadingFacts.Tool, LoadingFacts.Mechanic, LoadingFacts.Ocean, LoadingFacts.Tip })
                Assert.IsTrue(categories.Contains(c), $"No {c} facts");
        }

        [Test]
        public void LoadingStagesFillTheBarInOrder()
        {
            float last = -1f;
            foreach (LoadingStages.Stage stage in System.Enum.GetValues(typeof(LoadingStages.Stage)))
            {
                for (float t = 0f; t <= 1f; t += 0.25f)
                {
                    float p = LoadingStages.Progress(stage, t);
                    Assert.GreaterOrEqual(p, last, $"{stage} at {t}");
                    last = p;
                }
            }
            Assert.AreEqual(0f, LoadingStages.Progress(LoadingStages.Stage.FreeMemory, 0f));
            Assert.AreEqual(1f, LoadingStages.Progress(LoadingStages.Stage.WarmShaders, 1f));
        }
    }
}
