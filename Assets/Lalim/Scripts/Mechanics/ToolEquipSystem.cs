using UnityEngine;
using Lalim.Core;
using Lalim.Inventory;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Manages the player's currently equipped tool.
    /// Only one tool can be equipped at a time. The equipped tool changes
    /// how interactions work (e.g., Water Sample Kit → tap location to sample,
    /// Coral Replanting Kit → drag-and-place mode).
    /// 
    /// Challenge scripts can query the equipped tool to validate interactions.
    /// </summary>
    public class ToolEquipSystem : MonoBehaviour
    {
        private static ToolEquipSystem _instance;
        public static ToolEquipSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ToolEquipSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[ToolEquipSystem]");
                        _instance = go.AddComponent<ToolEquipSystem>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        /// <summary>The currently equipped item, or null if nothing is equipped.</summary>
        public ItemData EquippedItem { get; private set; }

        /// <summary>The item ID of the currently equipped tool, or empty string.</summary>
        public string EquippedItemId => EquippedItem != null ? EquippedItem.itemId : "";

        /// <summary>Whether any tool is currently equipped.</summary>
        public bool HasEquippedTool => EquippedItem != null;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            var tools = Instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─────────────────────────────────────────────
        // Equip / Unequip
        // ─────────────────────────────────────────────

        /// <summary>
        /// Equip a tool by item ID. The item must be in the player's inventory.
        /// Only tools, consumable kits, and relics with active abilities can be equipped.
        /// </summary>
        public bool EquipTool(string itemId)
        {
            // Verify item is in inventory
            if (InventoryManager.Instance == null || !InventoryManager.Instance.HasItem(itemId))
            {
                Debug.LogWarning($"[ToolEquip] Cannot equip — item not in inventory: {itemId}");
                return false;
            }

            // Look up item data from the database
            // We need to get ItemData from the InventoryManager or ItemDatabase
            // For now, we'll work with the item ID and let challenge scripts validate
            ItemData itemData = GetItemDataById(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[ToolEquip] Item data not found: {itemId}");
                return false;
            }

            // Validate item type — only certain types can be equipped
            if (itemData.itemType != ItemType.Tool &&
                itemData.itemType != ItemType.ConsumableKit &&
                itemData.itemType != ItemType.Relic)
            {
                Debug.LogWarning($"[ToolEquip] Item type {itemData.itemType} cannot be equipped");
                return false;
            }

            // Unequip current tool first
            if (HasEquippedTool)
            {
                UnequipTool();
            }

            EquippedItem = itemData;

            Debug.Log($"[ToolEquip] Equipped: {itemData.displayName} ({itemData.itemType})");

            EventBus.Publish(new ToolEquippedEvent
            {
                ItemId = itemId,
                Type = itemData.itemType
            });

            return true;
        }

        /// <summary>
        /// Unequip the currently equipped tool.
        /// </summary>
        public void UnequipTool()
        {
            if (!HasEquippedTool) return;

            string previousId = EquippedItemId;
            string previousName = EquippedItem.displayName;

            EquippedItem = null;

            Debug.Log($"[ToolEquip] Unequipped: {previousName}");

            EventBus.Publish(new ToolUnequippedEvent
            {
                ItemId = previousId
            });
        }

        /// <summary>
        /// Toggle equip status: if already equipped, unequips it; otherwise equips it.
        /// </summary>
        public void ToggleEquip(string itemId)
        {
            if (IsToolEquipped(itemId))
            {
                UnequipTool();
            }
            else
            {
                EquipTool(itemId);
            }
        }

        // ─────────────────────────────────────────────
        // Query
        // ─────────────────────────────────────────────

        /// <summary>
        /// Check if a specific tool is currently equipped.
        /// </summary>
        public bool IsToolEquipped(string itemId)
        {
            return HasEquippedTool && EquippedItemId == itemId;
        }

        /// <summary>
        /// Check if the equipped tool is of a specific type.
        /// </summary>
        public bool IsEquippedType(ItemType type)
        {
            return HasEquippedTool && EquippedItem.itemType == type;
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Retrieve ItemData by ID. Checks inventory directly, then database.
        /// </summary>
        private ItemData GetItemDataById(string itemId)
        {
            if (InventoryManager.Instance != null)
            {
                ItemData direct = InventoryManager.Instance.GetItemData(itemId);
                if (direct != null) return direct;

                // Check tools & consumable kits
                var tools = InventoryManager.Instance.GetCollectedItems(ItemType.Tool);
                foreach (var item in tools)
                {
                    if (item != null && item.itemId == itemId) return item;
                }

                // Check relics
                var relics = InventoryManager.Instance.GetCollectedItems(ItemType.Relic);
                foreach (var item in relics)
                {
                    if (item != null && item.itemId == itemId) return item;
                }
            }

            return null;
        }
    }
}
