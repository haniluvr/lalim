using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Lalim.Core;
using Lalim.EditorTools;
using Lalim.Inventory;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.Tests
{
    public class RelicRewardTests
    {
        private static IEnumerable<(string deity, List<DialogueLine> lines, int reveal, string relic)> Gifts()
        {
            yield return (Level1DialogueScripts.Amansinaya, Level1DialogueScripts.AmansinayaRelicGift(), Level1DialogueScripts.AmansinayaRelicLine, "Conch");
            yield return (Level1DialogueScripts.Haik, Level1DialogueScripts.HaikRelicGift(), Level1DialogueScripts.HaikRelicLine, "compass");
            yield return (Level1DialogueScripts.Neguno, Level1DialogueScripts.NegunoRelicGift(), Level1DialogueScripts.NegunoRelicLine, "net");
        }

        [Test]
        public void EachDeityHandsOverTheirRelicOnTheRevealLine()
        {
            foreach (var g in Gifts())
            {
                Assert.IsNotEmpty(g.lines, g.deity);
                Assert.Less(g.reveal, g.lines.Count, $"{g.deity}: reveal line out of range");
                DialogueLine reveal = g.lines[g.reveal];
                Assert.IsFalse(reveal.isPlayer, $"{g.deity}: the deity hands the relic over");
                StringAssert.Contains(g.relic, reveal.text, $"{g.deity}: reveal line should name the relic");
                foreach (DialogueLine line in g.lines) Assert.LessOrEqual(line.text.Length, 170, line.text);
            }
        }

        [Test]
        public void ThankYouLinesMovedOutOfTheClosings()
        {
            Assert.AreEqual(Level1DialogueScripts.AmansinayaRelicGift()[0].text, "Do you feel it? My shallows taste sweet again.");
            foreach (DialogueLine l in Level1DialogueScripts.AmansinayaClosing()) StringAssert.DoesNotContain("taste sweet", l.text);
            foreach (DialogueLine l in Level1DialogueScripts.HaikClosing()) StringAssert.DoesNotContain("young coral holds", l.text);
            foreach (DialogueLine l in Level1DialogueScripts.NegunoClosing()) StringAssert.DoesNotContain("rarer than pearls", l.text);
        }

        [Test]
        public void BarksCarryTheGiftUnderItsKey()
        {
            foreach (var barks in new[] { Level1DialogueScripts.AmansinayaBarks(), Level1DialogueScripts.HaikBarks(), Level1DialogueScripts.NegunoBarks() })
                Assert.IsTrue(barks.Exists(b => b.key == RelicRewardStep.GiftBarkKey));
        }

        [Test]
        public void RelicItemsAreLuzonKeyRelicsInTheDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(RelicAssetBuilder.ItemDatabasePath);
            Assert.IsNotNull(db);
            CollectionAssert.AreEquivalent(
                new[] { GameConstants.Relics.RADIANT_CONCH, GameConstants.Relics.COMPASS, GameConstants.Relics.WOVEN_NET_FRAGMENT },
                System.Array.ConvertAll(RelicAssetBuilder.All, r => r.ItemId));

            foreach (RelicAssetBuilder.Relic r in RelicAssetBuilder.All)
            {
                ItemData item = db.GetItem(r.ItemId);
                Assert.IsNotNull(item, r.ItemId);
                Assert.AreEqual(ItemType.Relic, item.itemType, r.ItemId);
                Assert.AreEqual(Region.Luzon, item.regionOrigin, r.ItemId);
                Assert.IsTrue(item.isKeyItem, r.ItemId);
                Assert.IsNotNull(item.worldModelPrefab, r.ItemId);
            }
            Assert.AreEqual(3, db.GetItemsByType(ItemType.Relic).Count, "only the three Level 1 relics");
        }

        [Test]
        public void RelicMeshesAreLightEnoughForMobile()
        {
            foreach (RelicAssetBuilder.Relic r in RelicAssetBuilder.All)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RelicAssetBuilder.PrefabPath(r));
                Assert.IsNotNull(prefab, r.Key);
                Mesh mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.LessOrEqual(mesh.triangles.Length / 3, 25000, r.Key);
            }
        }

        [Test]
        public void StatueOffersAPendingTalkUntilCancelled()
        {
            var go = new GameObject("statue");
            try
            {
                var statue = go.AddComponent<StatueDialogueRouter>();
                Assert.AreEqual("", statue.GetInteractionPrompt());
                statue.ArmTalk(Level1DialogueScripts.HaikRelicGift(), null, null);
                Assert.IsTrue(statue.HasPendingTalk);
                Assert.AreEqual("Talk", statue.GetInteractionPrompt());
                statue.CancelTalk();
                Assert.IsFalse(statue.HasPendingTalk);
                Assert.AreEqual("", statue.GetInteractionPrompt());
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ResumeSkipsPastStepsAddedBeforeTheSavesProgress()
        {
            // Old save: zone 1 finished before the relic gift (index 3) existed.
            Assert.AreEqual(6, LevelFlowDirector.ResumeIndex(new[] { true, true, true, false, true, true, false }));
            // Normal save: resume at the first unfinished step.
            Assert.AreEqual(3, LevelFlowDirector.ResumeIndex(new[] { true, true, true, false, false }));
            Assert.AreEqual(0, LevelFlowDirector.ResumeIndex(new[] { false, false }));
        }
    }
}
