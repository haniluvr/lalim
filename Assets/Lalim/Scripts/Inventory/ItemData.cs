using UnityEngine;
using Lalim.Core;

namespace Lalim.Inventory
{
    /// <summary>
    /// ScriptableObject defining a single item's properties.
    /// Create instances via Assets > Create > Lalim > Item Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItem", menuName = "Lalim/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this item (used in save data)")]
        public string itemId;

        [Tooltip("Display name shown to the player")]
        public string displayName;

        [TextArea(3, 6)]
        [Tooltip("Description of the item's significance")]
        public string description;

        [Header("Classification")]
        public ItemType itemType;

        [Tooltip("Which region this item originates from")]
        public Region regionOrigin;

        [Tooltip("If true, this item is required for region completion")]
        public bool isKeyItem;

        [Tooltip("A skill or blessing rather than an object (Moontide): owned like an item, but not shown " +
                 "in the inventory and handed over without a floating model (the Quest Complete toast says \"You received a new skill\").")]
        public bool isSkill;

        [Header("Visuals")]
        [Tooltip("Icon displayed in inventory UI")]
        public Sprite icon;

        [Tooltip("3D model prefab for world placement (optional)")]
        public GameObject worldModelPrefab;

        [Header("Audio")]
        [Tooltip("Sound played when item is collected")]
        public AudioClip collectSound;

        /// <summary>
        /// Validate that this item has required fields set.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(itemId) && !string.IsNullOrEmpty(displayName);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-generate itemId from asset name if empty
            if (string.IsNullOrEmpty(itemId))
            {
                itemId = name.ToLower().Replace(" ", "_");
            }
        }
#endif
    }
}
