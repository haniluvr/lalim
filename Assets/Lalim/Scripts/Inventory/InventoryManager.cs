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
        private static InventoryManager _instance;
        public static InventoryManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<InventoryManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[InventoryManager]");
                        _instance = go.AddComponent<InventoryManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Database")]
        [SerializeField] private ItemDatabase _itemDatabase;
        public ItemDatabase Database => _itemDatabase;

        [Header("Testing / Starter Items")]
        [Tooltip("Items automatically granted when testing a scene without a save file.")]
        [SerializeField] private List<string> _starterItemIds = new List<string>
        {
            "07_watersamplekit",
            "05_coralreplantingkit",
            "04_wastesegregationbag"
        };

        // ─────────────────────────────────────────────
        // Inventory State
        // ─────────────────────────────────────────────

        private Dictionary<ItemType, List<string>> _inventory = new Dictionary<ItemType, List<string>>
        {
            { ItemType.Relic, new List<string>() },
            { ItemType.Tool, new List<string>() },
            { ItemType.QuestItem, new List<string>() }
        };

        private HashSet<string> _allItemLookup = new HashSet<string>();

        public int RelicCount => _inventory[ItemType.Relic].Count;
        public int ToolCount => _inventory[ItemType.Tool].Count;
        public int QuestItemCount => _inventory[ItemType.QuestItem].Count;
        public int TotalItemCount => _allItemLookup.Count;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            var inv = Instance;
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

            // Auto-fallback to find ItemDatabase if unassigned
            if (_itemDatabase == null)
            {
                _itemDatabase = Resources.Load<ItemDatabase>("ItemDatabase");
#if UNITY_EDITOR
                if (_itemDatabase == null)
                {
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemDatabase");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        _itemDatabase = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
                    }
                }
#endif
            }
        }

        private void Start()
        {
            // Restore from save if available
            if (SaveManager.Instance != null && SaveManager.Instance.HasSaveFile)
            {
                RestoreFromSave(SaveManager.Instance.CurrentSave);
            }
            else
            {
                // Auto-grant starter items for rapid testing
                if (_starterItemIds != null)
                {
                    foreach (string id in _starterItemIds)
                    {
                        if (!string.IsNullOrEmpty(id) && !HasItem(id))
                        {
                            AddItem(id);
                        }
                    }
                }
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
            ItemType storageType = GetStorageType(itemData.itemType);
            _inventory[storageType].Add(itemId);
            _allItemLookup.Add(itemId);

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
                TotalTools = ToolCount,
                TotalQuestItems = QuestItemCount
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
            ItemType storageType = GetStorageType(itemData.itemType);
            _inventory[storageType].Remove(itemId);
            _allItemLookup.Remove(itemId);

            EventBus.Publish(new ItemUsedEvent
            {
                ItemId = itemId,
                Type = itemData.itemType
            });

            EventBus.Publish(new InventoryChangedEvent
            {
                TotalRelics = RelicCount,
                TotalTools = ToolCount,
                TotalQuestItems = QuestItemCount
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
            return _allItemLookup.Contains(itemId);
        }

        /// <summary>
        /// Get all collected item IDs of a specific type.
        /// </summary>
        public List<string> GetCollectedItemIds(ItemType type)
        {
            ItemType storageType = GetStorageType(type);
            return new List<string>(_inventory[storageType]);
        }

        /// <summary>
        /// Get ItemData for all collected items of a type.
        /// </summary>
        public List<ItemData> GetCollectedItems(ItemType type)
        {
            List<ItemData> items = new List<ItemData>();
            List<string> ids = GetCollectedItemIds(type);

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
        /// Retrieve ItemData for an item from the database.
        /// </summary>
        public ItemData GetItemData(string itemId)
        {
            return _itemDatabase != null ? _itemDatabase.GetItem(itemId) : null;
        }

        /// <summary>
        /// Get all items collected by the player across all categories.
        /// </summary>
        public List<ItemData> GetAllCollectedItems()
        {
            List<ItemData> items = new List<ItemData>();
            foreach (string id in _allItemLookup)
            {
                ItemData data = GetItemData(id);
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
            save.collectedRelicIds = new List<string>(_inventory[ItemType.Relic]);
            save.collectedToolIds = new List<string>(_inventory[ItemType.Tool]);
            save.collectedQuestItemIds = new List<string>(_inventory[ItemType.QuestItem]);
        }

        private void RestoreFromSave(SaveData save)
        {
            _inventory[ItemType.Relic] = new List<string>(save.collectedRelicIds ?? new List<string>());
            _inventory[ItemType.Tool] = new List<string>(save.collectedToolIds ?? new List<string>());
            _inventory[ItemType.QuestItem] = new List<string>(save.collectedQuestItemIds ?? new List<string>());

            _allItemLookup = new HashSet<string>();
            foreach (var kvp in _inventory)
            {
                foreach (var id in kvp.Value) _allItemLookup.Add(id);
            }

            Debug.Log($"[Inventory] Restored: {RelicCount} relics, {ToolCount} tools, {QuestItemCount} quest items");
        }

        /// <summary>
        /// Map any ItemType to its storage bucket key.
        /// ConsumableKit and CompanionItem share the Tool bucket.
        /// </summary>
        private ItemType GetStorageType(ItemType type)
        {
            switch (type)
            {
                case ItemType.Relic:       return ItemType.Relic;
                case ItemType.QuestItem:   return ItemType.QuestItem;
                default:                   return ItemType.Tool;  // Tool, ConsumableKit, CompanionItem
            }
        }
    }
}
