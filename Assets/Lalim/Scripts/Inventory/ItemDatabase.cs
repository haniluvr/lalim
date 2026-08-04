using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Inventory
{
    /// <summary>
    /// Central registry of all ItemData assets. Provides lookup by itemId.
    /// Populate the items list in the Inspector with all game items.
    /// Create via Assets > Create > Lalim > Item Database.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Lalim/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [Header("All Items")]
        [SerializeField] private List<ItemData> _items = new List<ItemData>();

        // Runtime lookup cache
        private Dictionary<string, ItemData> _itemLookup;
        private bool _isInitialized;

        // ─────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────

        /// <summary>
        /// Build the lookup dictionary. Called automatically on first access.
        /// </summary>
        private void Initialize()
        {
            if (_isInitialized) return;

            _itemLookup = new Dictionary<string, ItemData>();

            foreach (var item in _items)
            {
                if (item == null) continue;

                if (!item.IsValid())
                {
                    Debug.LogWarning($"[ItemDatabase] Invalid item skipped: {item.name}");
                    continue;
                }

                if (_itemLookup.ContainsKey(item.itemId))
                {
                    Debug.LogWarning($"[ItemDatabase] Duplicate itemId: {item.itemId}");
                    continue;
                }

                _itemLookup[item.itemId] = item;
            }

            _isInitialized = true;
            Debug.Log($"[ItemDatabase] Initialized with {_itemLookup.Count} items");
        }

        // ─────────────────────────────────────────────
        // Lookup
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get an item by its unique ID.
        /// </summary>
        public ItemData GetItem(string itemId)
        {
            Initialize();

            if (_itemLookup.TryGetValue(itemId, out ItemData item))
            {
                return item;
            }

            Debug.LogWarning($"[ItemDatabase] Item not found: {itemId}");
            return null;
        }

        /// <summary>
        /// Check if an item exists in the database.
        /// </summary>
        public bool HasItem(string itemId)
        {
            Initialize();
            return _itemLookup.ContainsKey(itemId);
        }

        /// <summary>
        /// Get all items of a specific type.
        /// </summary>
        public List<ItemData> GetItemsByType(Core.ItemType type)
        {
            Initialize();

            List<ItemData> result = new List<ItemData>();
            foreach (var item in _itemLookup.Values)
            {
                if (item.itemType == type)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all items from a specific region.
        /// </summary>
        public List<ItemData> GetItemsByRegion(Core.Region region)
        {
            Initialize();

            List<ItemData> result = new List<ItemData>();
            foreach (var item in _itemLookup.Values)
            {
                if (item.regionOrigin == region)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Get all key items (required for progression).
        /// </summary>
        public List<ItemData> GetKeyItems()
        {
            Initialize();

            List<ItemData> result = new List<ItemData>();
            foreach (var item in _itemLookup.Values)
            {
                if (item.isKeyItem)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Get the total number of registered items.
        /// </summary>
        public int Count
        {
            get
            {
                Initialize();
                return _itemLookup.Count;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor: force re-initialization (useful after adding new items).
        /// </summary>
        public void EditorRefresh()
        {
            _isInitialized = false;
            Initialize();
        }

        private void OnValidate()
        {
            _isInitialized = false; // Force re-init when list changes in Inspector
        }
#endif
    }
}
