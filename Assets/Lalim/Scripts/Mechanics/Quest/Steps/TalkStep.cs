using UnityEngine;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Swim to the zone's statue and talk (chat icon). Completes when the quest dialogue ends —
    /// the next step (e.g. trash collection) starts right after.
    /// </summary>
    public class TalkStep : QuestStep
    {
        // Finding the statue is travel, not a quest: its text is there, but the box starts closed.
        protected override bool DefaultBoxOpen => false;

        [Tooltip("Defaults to the zone's statue.")]
        [SerializeField] private StatueDialogueRouter _statue;

        private StatueDialogueRouter Statue => _statue != null ? _statue : (Zone != null ? Zone.Statue : null);

        protected override void OnBegin()
        {
            StatueDialogueRouter statue = Statue;
            if (statue == null) { Debug.LogWarning($"[TalkStep] {StepId}: no statue."); Complete(); return; }

            ObjectiveTracker.Instance?.SetObjective(statue.transform);
            statue.ArmQuest(Complete);
        }

        public override void RestoreCompleted()
        {
            Statue?.MarkQuestDone(allowIdleTalk: Zone != null && Zone.IsCleansed);
        }

        private void DebugForceComplete() { Statue?.MarkQuestDone(false); Complete(); }
    }
}
