using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// The zone is clean: HP drain stops, the restoration plays, the level-wide pollution recedes
    /// (PollutionDirector), and the "QUEST COMPLETE" toast shows with the zone's rewards ("You
    /// received a Radiant Conch and a Water Sampling Kit"). Publishes ZoneCompletedEvent once.
    /// </summary>
    public class CleanseZoneStep : QuestStep
    {
        [SerializeField] private string _toastTitle = "Quest Complete";
        [Tooltip("Shown only if the zone gives no reward.")]
        [SerializeField, TextArea(1, 2)] private string _toastBody = "Amansinaya's shallows are clean again";

        protected override void OnBegin()
        {
            if (Zone == null) { Complete(); return; }

            ApplyCleanState();
            Sfx.Play(SfxCue.ZoneCleansed);   // the zone's one big moment; the toast after it stays silent
            EventBus.Publish(new ZoneCompletedEvent { ZoneId = Zone.ZoneId, Region = Zone.Region, ChallengeType = ChallengeType.WasteSegregation });
            List<string> rewards = ZoneRewards();
            ActToastUI.Post(_toastTitle.ToUpperInvariant(), rewards.Count > 0 ? ReceivedLine(rewards) : _toastBody, ToastKind.QuestComplete);
            Complete();
        }

        /// <summary>The relic and end-of-quest tools this zone gives, in step order.</summary>
        private List<string> ZoneRewards()
        {
            var names = new List<string>();
            foreach (QuestStep step in Zone.GetComponentsInChildren<QuestStep>(true))
            {
                var relic = step as RelicRewardStep;
                if (relic != null && !string.IsNullOrEmpty(relic.RewardName)) names.Add(relic.RewardName.Trim());
                var tool = step as GrantItemStep;
                if (tool != null && tool.AnnounceWithQuestComplete && !string.IsNullOrEmpty(tool.RewardName)) names.Add(tool.RewardName.Trim());
            }
            return names;
        }

        /// <summary>"You received a Radiant Conch", "... a Compass and an Oar", "... the Moontide skill", "... a, b and c".</summary>
        public static string ReceivedLine(IList<string> names)
        {
            var parts = new List<string>();
            foreach (string n in names)
            {
                // A name that brings its own article ("the Moontide skill") keeps it.
                string lower = n.ToLowerInvariant();
                bool hasArticle = lower.StartsWith("the ") || lower.StartsWith("a ") || lower.StartsWith("an ");
                parts.Add(hasArticle ? n : ("AEIOUaeiou".IndexOf(n[0]) >= 0 ? "an " : "a ") + n);
            }
            string list = parts.Count == 1 ? parts[0]
                : string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + " and " + parts[parts.Count - 1];
            return "You received " + list;
        }

        public override void RestoreCompleted() => ApplyCleanState();

        private void ApplyCleanState()
        {
            if (Zone == null) return;
            if (Zone.ToxicVolume != null) Zone.ToxicVolume.gameObject.SetActive(false);
            Zone.Statue?.AllowIdleTalk(true);
        }

#if UNITY_EDITOR
        public void EditorSetupToast(string title, string body)
        {
            _toastTitle = title;
            _toastBody = body;
        }
#endif
    }
}
