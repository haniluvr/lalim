using NUnit.Framework;
using Lalim.UI;

namespace Lalim.Tests
{
    public class InteractPromptTests
    {
        [Test]
        public void TapToPromptsBreakAfterTheVerb()
        {
            Assert.AreEqual("Tap to use\nWater Sampling Kit", WorldInteractPrompt.FormatPrompt("Tap to use Water Sampling Kit"));
        }

        [Test]
        public void ShortPromptsStayOnOneLine()
        {
            Assert.AreEqual("Talk", WorldInteractPrompt.FormatPrompt("Talk"));
            Assert.AreEqual("Prepare the site", WorldInteractPrompt.FormatPrompt("Prepare the site"));
        }

        [Test]
        public void NeverMoreThanTwoLines()
        {
            string text = WorldInteractPrompt.FormatPrompt("Gently lift the tangled ghost net off the coral");
            Assert.AreEqual(1, text.Split('\n').Length - 1);
            Assert.AreEqual("a\nb c", WorldInteractPrompt.FormatPrompt("a\nb\nc"));
        }
    }
}
