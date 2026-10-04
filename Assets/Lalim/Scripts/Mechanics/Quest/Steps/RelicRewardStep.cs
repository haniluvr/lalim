using System.Collections.Generic;
using UnityEngine;
using Lalim.Inventory;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// After the zone's last minigame: swim back to the deity and talk. The deity thanks the diver
    /// (the statue's "relic_gift" lines) and, at <see cref="_revealAtLine"/>, the relic fades into view,
    /// glowing, and hovers in front of the diver. When the conversation ends it sweeps into the
    /// diver and goes into the inventory. It's announced with the zone's "Quest Complete" toast.
    /// Relics are kept in the save and are needed in Level 2 (GameConstants.Relics).
    /// </summary>
    public class RelicRewardStep : QuestStep
    {
        // Returning to the statue is travel: the box starts closed.
        protected override bool DefaultBoxOpen => false;

        public const string GiftBarkKey = "relic_gift";

        [SerializeField] private string _itemId = Core.GameConstants.Relics.RADIANT_CONCH;
        [SerializeField] private GameObject _relicPrefab;
        [Tooltip("The statue's bark holding the thank-you + handover lines.")]
        [SerializeField] private string _giftKey = GiftBarkKey;
        [Tooltip("0-based line of the gift lines on which the relic fades into view.")]
        [SerializeField, Min(0)] private int _revealAtLine = 2;
        [Tooltip("The relic's name in the Quest Complete toast (\"You received a Radiant Conch\").")]
        [SerializeField, TextArea(1, 2)] private string _toastBody = "Radiant Conch";
        [Tooltip("Defaults to the zone's statue.")]
        [SerializeField] private StatueDialogueRouter _statue;
        [Tooltip("Halo, sparkle and fade-in materials for the relic's glow.")]
        [SerializeField] private RelicPresentation.Look _look;

        private RelicPresentation _presentation;
        private int _lineCount;

        private StatueDialogueRouter Statue => _statue != null ? _statue : (Zone != null ? Zone.Statue : null);

        protected override void OnBegin()
        {
            StatueDialogueRouter statue = Statue;
            List<DialogueLine> lines = statue != null ? statue.GetBark(_giftKey) : null;
            if (statue == null || lines == null || lines.Count == 0)
            {
                Debug.LogWarning($"[RelicRewardStep] {StepId}: no statue or no '{_giftKey}' lines; granting directly.");
                Grant();
                Complete();
                return;
            }

            _lineCount = lines.Count;
            ObjectiveTracker.Instance?.SetObjective(statue.transform);
            statue.ArmTalk(lines, OnTalkStarted, OnTalkDone);
        }

        private void OnTalkStarted()
        {
            ObjectiveTracker.Instance?.Clear();
            if (DialogueManager.Instance == null) return;
            DialogueManager.Instance.LineStarted += OnLine;
            // The first line was shown before this subscription.
            OnLine(0);
        }

        private void OnLine(int index)
        {
            // Lines edited shorter than the reveal index still get the reveal, on the last line.
            int at = Mathf.Min(_revealAtLine, _lineCount - 1);
            if (index >= at && _presentation == null) Reveal();
        }

        private void Reveal()
        {
            Transform diver = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            _presentation = RelicPresentation.Spawn(_relicPrefab, Statue != null ? Statue.transform : null, diver, _look);
        }

        private void OnTalkDone()
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.LineStarted -= OnLine;
            if (_presentation == null) Reveal();
            _presentation.Receive(() =>
            {
                _presentation = null;
                Grant();
                Complete();
            });
        }

        /// <summary>The relic's name, for the zone's Quest Complete toast.</summary>
        public string RewardName => _toastBody;

        private void Grant()
        {
            if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem(_itemId))
                InventoryManager.Instance.AddItem(_itemId);
        }

        // Also covers old saves that were already past this step when it was added.
        public override void RestoreCompleted() => Grant();

        private void OnDisable()
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.LineStarted -= OnLine;
        }

        private void DebugForceComplete()
        {
            Statue?.CancelTalk();
            if (DialogueManager.Instance != null) DialogueManager.Instance.LineStarted -= OnLine;
            if (_presentation != null) Destroy(_presentation.gameObject);
            _presentation = null;
            Grant();
            Complete();
        }

#if UNITY_EDITOR
        public void EditorSetupRelic(string itemId, GameObject prefab, string toastBody, int revealAtLine)
        {
            _itemId = itemId;
            _relicPrefab = prefab;
            _toastBody = toastBody;
            _revealAtLine = revealAtLine;
        }

        public void EditorSetupLook(RelicPresentation.Look look) => _look = look;
#endif
    }
}
