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
    /// diver and goes into the inventory. It's announced with the zone's "Quest Complete" toast
    /// (no toast of its own). The deity's closing lines follow on in the same conversation — the
    /// dialogue never closes in between — and the relic is received as they begin.
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
        [Tooltip("Carry straight on into the statue's closing lines in the same conversation (its closing step then skips).")]
        [SerializeField] private bool _continueIntoClosing = true;
        [Tooltip("Optional bark played first in the same conversation (e.g. \"report\"), so the diver talks to the deity once, not twice.")]
        [SerializeField] private string _leadInKey;

        private RelicPresentation _presentation;
        private int _leadIn;         // lines before the gift's own (the lead-in bark)
        private int _lineCount;      // the lead-in plus the gift's lines (the closing may follow in the same talk)
        private bool _received;
        private bool _revealed;
        private bool _talkDone;

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

            List<DialogueLine> leadIn = string.IsNullOrEmpty(_leadInKey) ? null : statue.GetBark(_leadInKey);
            _leadIn = leadIn != null ? leadIn.Count : 0;
            _lineCount = _leadIn + lines.Count;
            _received = false;
            _revealed = false;
            _talkDone = false;
            var talk = new List<DialogueLine>();
            if (leadIn != null) talk.AddRange(leadIn);
            talk.AddRange(lines);
            bool withClosing = _continueIntoClosing && statue.ClosingLines != null && statue.ClosingLines.Count > 0;
            if (withClosing) talk.AddRange(statue.ClosingLines);
            ObjectiveTracker.Instance?.SetObjective(statue.transform);
            statue.ArmTalk(talk, () => { if (withClosing) statue.MarkClosingSaid(); OnTalkStarted(); }, OnTalkDone);
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
            int at = Mathf.Min(_leadIn + _revealAtLine, _lineCount - 1);
            if (index >= at && _presentation == null && !_received) Reveal();
            // The gift is over and the closing begins: the relic sweeps into the diver meanwhile.
            if (index >= _lineCount && !_received) Receive();
        }

        /// <summary>A skill (Moontide) is a blessing, not a thing: no floating model during the handover.</summary>
        private bool IsSkill
        {
            get
            {
                ItemData item = InventoryManager.Instance != null && !string.IsNullOrEmpty(_itemId) ? InventoryManager.Instance.GetItemData(_itemId) : null;
                return item != null && item.isSkill;
            }
        }

        private void Reveal()
        {
            if (_revealed) return;
            _revealed = true;
            // The gift's sound marks the handover itself: as the relic appears (or, for a skill
            // with nothing to show, on the same line), not when the toast announces it later.
            Core.Sfx.Play(Core.SfxCue.Reward);
            Transform diver = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            // Moontide: the current it reveals winds round the diver and the dugong as it's given.
            if (_itemId == Core.GameConstants.Relics.MOONTIDE && diver != null)
            {
                Transform dugong = Abilities.DugongCompanion.Instance != null ? Abilities.DugongCompanion.Instance.transform : null;
                MoontideGiftSwirl.Play(diver, dugong, 8f);
            }
            if (_relicPrefab == null || IsSkill) return;   // a template step with no relic chosen yet, or a skill
            _presentation = RelicPresentation.Spawn(_relicPrefab, Statue != null ? Statue.transform : null, diver, _look);
        }

        // The relic sweeps into the diver and goes into the inventory.
        private void Receive()
        {
            _received = true;
            if (_presentation == null) Reveal();
            if (_presentation == null) { Grant(); if (_talkDone) Complete(); return; }   // no relic model
            _presentation.Receive(() =>
            {
                _presentation = null;
                Grant();
                if (_talkDone) Complete();
            });
        }

        private void OnTalkDone()
        {
            if (DialogueManager.Instance != null) DialogueManager.Instance.LineStarted -= OnLine;
            _talkDone = true;
            if (!_received) Receive();          // no closing followed: receive it now, then continue
            else if (_presentation == null) Complete();   // already in the inventory
        }

        /// <summary>The reward's name, for the zone's Quest Complete toast. A skill isn't named:
        /// "You received a new skill".</summary>
        public string RewardName => IsSkill ? "new skill" : _toastBody;

        private void Grant()
        {
            if (string.IsNullOrEmpty(_itemId)) return;   // a template step with no relic chosen yet
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

        public void EditorSetLeadIn(string barkKey) => _leadInKey = barkKey;
#endif
    }
}
