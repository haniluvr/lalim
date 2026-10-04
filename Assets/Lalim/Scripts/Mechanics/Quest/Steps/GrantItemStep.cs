using UnityEngine;
using Lalim.Inventory;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Gives the player a tool. A reward at the end of a zone is announced with the zone's
    /// "Quest Complete" toast; a tool handed over mid-quest gets its own toast (after the dialogue).
    /// </summary>
    public class GrantItemStep : QuestStep
    {
        [SerializeField] private string _itemId = "07_watersamplekit";
        [Tooltip("On: part of the zone's Quest Complete toast. Off: its own toast right away (a tool needed mid-quest).")]
        [SerializeField] private bool _announceWithQuestComplete = true;
        [SerializeField] private string _toastTitle = "Tool Received";
        [Tooltip("The tool's name (\"You received a Water Sampling Kit\").")]
        [SerializeField, TextArea(1, 2)] private string _toastBody = "Water Sampling Kit";

        public bool AnnounceWithQuestComplete => _announceWithQuestComplete;
        public string RewardName => _toastBody;

        protected override void OnBegin()
        {
            InventoryManager.Instance?.AddItem(_itemId);
            if (!_announceWithQuestComplete)
                ActToastUI.Post(_toastTitle.ToUpperInvariant(), CleanseZoneStep.ReceivedLine(new[] { _toastBody }), ToastKind.Reward);
            Complete();
        }

        public override void RestoreCompleted()
        {
            // The inventory is saved too; this only covers saves made between the step and the inventory write.
            if (InventoryManager.Instance != null && !InventoryManager.Instance.HasItem(_itemId))
            {
                InventoryManager.Instance.AddItem(_itemId);
            }
        }

#if UNITY_EDITOR
        public void EditorSetupItem(string itemId, string title, string body, bool announceWithQuestComplete)
        {
            _itemId = itemId;
            _announceWithQuestComplete = announceWithQuestComplete;
            _toastTitle = title;
            _toastBody = body;
        }
#endif
    }
}
