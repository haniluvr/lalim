using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Inventory
{
    /// <summary>
    /// Manages the player's inventory. Tracks collected Relics and Conservation Tools
    /// separately. Publishes events on changes and integrates with SaveManager.
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Database")]
        [SerializeField] private ItemDatabase _itemDatabase;

        // ─────────────────────────────────────────────
        // Inventory State
        // ─────────────────────────────────────────────

        private List<string> _collectedRelicIds = new List<string>();
        private List<string> _collectedToolIds = new List<string>();

        private HashSet<string> _relicLookup = new HashSet<string>();
        private HashSet<string> _toolLookup = new HashSet<string>();

        public int RelicCount => _collectedRelicIds.Count;
        public int ToolCount => _collectedToolIds.Count;
        public int TotalItemCount => RelicCount + ToolCount;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Restore from save if available
            if (SaveManager.Instance != null && SaveManager.Instance.HasSaveFile)
            {
                RestoreFromSave(SaveManager.Instance.CurrentSave);
            }
        }

        // ─────────────────────────────────────────────
        // Add / Remove
        // ─────────────────────────────────────────────

        /// <summary>
        /// Add an item to the inventory by ID.
        /// Returns false if item already collected or not found in database.
        /// </summary>
        public bool AddItem(string itemId)
        {
            if (HasItem(itemId))
            {
                Debug.LogWarning($"[Inventory] Item already collected: {itemId}");
                return false;
            }

            ItemData itemData = _itemDatabase?.GetItem(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[Inventory] Item not found in database: {itemId}");
                return false;
            }

            // Add to appropriate list
            if (itemData.itemType == ItemType.Relic)
            {
                _collectedRelicIds.Add(itemId);
                _relicLookup.Add(itemId);
            }
            else if (itemData.itemType == ItemType.ConservationTool)
            {
                _collectedToolIds.Add(itemId);
                _toolLookup.Add(itemId);
            }

            Debug.Log($"[Inventory] Collected: {itemData.displayName} ({itemData.itemType})");

            // Publish events
            EventBus.Publish(new ItemCollectedEvent
            {
                ItemId = itemId,
                Type = itemData.itemType,
                RegionOrigin = itemData.regionOrigin.ToString()
            });

            EventBus.Publish(new InventoryChangedEvent
            {
                TotalRelics = RelicCount,
                TotalTools = ToolCount
            });

            // Update save data
            UpdateSaveData();

            return true;
        }

        /// <summary>
        /// Use (consume) an item from the inventory.
        /// Returns false if item not in inventory.
        /// </summary>
        public bool UseItem(string itemId)
        {
            if (!HasItem(itemId))
            {
                Debug.LogWarning($"[Inventory] Cannot use item not in inventory: {itemId}");
                return false;
            }

            ItemData itemData = _itemDatabase?.GetItem(itemId);
            if (itemData == null) return false;

            // Remove from lists
            if (itemData.itemType == ItemType.Relic)
            {
                _collectedRelicIds.Remove(itemId);
                _relicLookup.Remove(itemId);
            }
            else
            {
                _collectedToolIds.Remove(itemId);
                _toolLookup.Remove(itemId);
            }

            EventBus.Publish(new ItemUsedEvent
            {
                ItemId = itemId,
                Type = itemData.itemType
            });

            EventBus.Publish(new InventoryChangedEvent
            {
                TotalRelics = RelicCount,
                TotalTools = ToolCount
            });

            UpdateSaveData();

            return true;
        }

        // ─────────────────────────────────────────────
        // Query
        // ─────────────────────────────────────────────

        /// <summary>
        /// Check if the player has collected a specific item.
        /// </summary>
        public bool HasItem(string itemId)
        {
            return _relicLookup.Contains(itemId) || _toolLookup.Contains(itemId);
        }

        /// <summary>
        /// Get all collected item IDs of a specific type.
        /// </summary>
        public List<string> GetCollectedItemIds(ItemType type)
        {
            return type == ItemType.Relic
                ? new List<string>(_collectedRelicIds)
                : new List<string>(_collectedToolIds);
        }

        /// <summary>
        /// Get ItemData for all collected items of a type.
        /// </summary>
        public List<ItemData> GetCollectedItems(ItemType type)
        {
            List<ItemData> items = new List<ItemData>();
            List<string> ids = type == ItemType.Relic ? _collectedRelicIds : _collectedToolIds;

            foreach (string id in ids)
            {
                ItemData data = _itemDatabase?.GetItem(id);
                if (data != null)
                {
                    items.Add(data);
                }
            }

            return items;
        }

        /// <summary>
        /// Check if all key items for a region have been collected.
        /// </summary>
        public bool HasAllKeyItemsForRegion(Region region)
        {
            if (_itemDatabase == null) return false;

            List<ItemData> keyItems = _itemDatabase.GetKeyItems();
            foreach (var item in keyItems)
            {
                if (item.regionOrigin == region && !HasItem(item.itemId))
                {
                    return false;
                }
            }

            return true;
        }

        // ─────────────────────────────────────────────
        // Save / Load
        // ─────────────────────────────────────────────

        private void UpdateSaveData()
        {
            if (SaveManager.Instance == null) return;

            var save = SaveManager.Instance.CurrentSave;
            save.collectedRelicIds = new List<string>(_collectedRelicIds);
            save.collectedToolIds = new List<string>(_collectedToolIds);
        }

        private void RestoreFromSave(SaveData save)
        {
            _collectedRelicIds = new List<string>(save.collectedRelicIds ?? new List<string>());
            _collectedToolIds = new List<string>(save.collectedToolIds ?? new List<string>());

            _relicLookup = new HashSet<string>(_collectedRelicIds);
            _toolLookup = new HashSet<string>(_collectedToolIds);

            Debug.Log($"[Inventory] Restored: {RelicCount} relics, {ToolCount} tools");
        }
    }
}
