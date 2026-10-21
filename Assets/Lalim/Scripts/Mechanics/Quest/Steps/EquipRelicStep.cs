using UnityEngine;
using Lalim.Core;
using Lalim.Inventory;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Waits until the player equips a relic from the inventory (e.g. the Radiant Conch after
    /// Manunubo explains it), so the next talk can give the quest that needs it. Done at once if
    /// it's already equipped. Grants the relic first if a test save skipped the level that gives it.
    /// </summary>
    public class EquipRelicStep : QuestStep
    {
        [SerializeField] private string _itemId = GameConstants.Relics.RADIANT_CONCH;

        protected override void OnBegin()
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null && !inv.HasItem(_itemId))
            {
                Debug.LogWarning($"[EquipRelicStep] The save has no {_itemId}; granting it for this run.");
                inv.AddItem(_itemId);
            }
            ObjectiveTracker.Instance?.Clear();
            if (RelicLoadout.IsEquipped(_itemId)) Complete();
        }

        private void Update()
        {
            if (IsRunning && RelicLoadout.IsEquipped(_itemId)) Complete();
        }

        private void DebugForceComplete()
        {
            RelicLoadout.SetEquipped(_itemId, true);
            Complete();
        }

#if UNITY_EDITOR
        public void EditorSetItem(string itemId) => _itemId = itemId;
#endif
    }
}
