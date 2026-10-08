using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Inventory
{
    /// <summary>
    /// Which relic the player has equipped from the inventory: one at a time — equipping another
    /// swaps it in. An equipped relic with an action (the Radiant Conch, the Woven Net Fragment)
    /// gets a HUD button; the Compass is passive and only shows its bar while a minigame needs it.
    /// Kept in the save.
    ///
    /// Tapping a HUD button publishes RelicUsedEvent; the minigame that owns the relic reacts and
    /// may start a cooldown here, which the button shows.
    /// </summary>
    public static class RelicLoadout
    {
        private static readonly Dictionary<string, float> _readyAt = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> _cooldownLength = new Dictionary<string, float>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _readyAt.Clear();
            _cooldownLength.Clear();
        }

        /// <summary>Relics that can be equipped at all.</summary>
        public static bool IsEquippable(string itemId) =>
            itemId == GameConstants.Relics.RADIANT_CONCH ||
            itemId == GameConstants.Relics.WOVEN_NET_FRAGMENT ||
            itemId == GameConstants.Relics.COMPASS;

        /// <summary>Equipped relics that get a HUD button (the Compass is passive).</summary>
        public static bool HasButton(string itemId) =>
            itemId == GameConstants.Relics.RADIANT_CONCH ||
            itemId == GameConstants.Relics.WOVEN_NET_FRAGMENT;

        /// <summary>What an equipped relic does, for the inventory's detail panel.</summary>
        public static string UseHint(string itemId)
        {
            switch (itemId)
            {
                case GameConstants.Relics.RADIANT_CONCH: return "Tap its button to call scattered fish to you.";
                case GameConstants.Relics.WOVEN_NET_FRAGMENT: return "Hold its button to aim, release to throw the net.";
                case GameConstants.Relics.COMPASS: return "Passive: points the way to the right sanctuary.";
                default: return "";
            }
        }

        private static List<string> Saved
        {
            get
            {
                SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
                if (save == null) return null;
                if (save.equippedRelicIds == null) save.equippedRelicIds = new List<string>();
                return save.equippedRelicIds;
            }
        }

        /// <summary>The equipped relic the player still owns (a list for old saves that had several; the first counts).</summary>
        public static List<string> Equipped
        {
            get
            {
                var result = new List<string>();
                List<string> saved = Saved;
                if (saved == null) return result;
                foreach (string id in saved)
                {
                    if (InventoryManager.Instance == null || InventoryManager.Instance.HasItem(id)) { result.Add(id); break; }
                }
                return result;
            }
        }

        public static bool IsEquipped(string itemId)
        {
            List<string> saved = Saved;
            return saved != null && saved.Contains(itemId);
        }

        public static void SetEquipped(string itemId, bool equipped)
        {
            if (!IsEquippable(itemId)) return;
            List<string> saved = Saved;
            if (saved == null || saved.Contains(itemId) == equipped) return;
            if (equipped)
            {
                // One relic at a time: the one already equipped goes back to the inventory.
                foreach (string other in saved.ToArray())
                {
                    saved.Remove(other);
                    EventBus.Publish(new RelicEquipChangedEvent { ItemId = other, Equipped = false });
                }
                saved.Add(itemId);
            }
            else saved.Remove(itemId);
            EventBus.Publish(new RelicEquipChangedEvent { ItemId = itemId, Equipped = equipped });
        }

        /// <summary>Called by a HUD button (or a debug key): asks whoever owns the relic to use it.</summary>
        public static void Use(string itemId)
        {
            if (!IsEquipped(itemId) || CooldownRemaining(itemId) > 0f) return;
            EventBus.Publish(new RelicUsedEvent { ItemId = itemId });
        }

        /// <summary>The HUD button (or its key) went down / came up: for relics used by holding.</summary>
        public static void SetHeld(string itemId, bool down)
        {
            if (!IsEquipped(itemId)) return;
            EventBus.Publish(new RelicHoldEvent { ItemId = itemId, Down = down });
        }

        // ───────────────────────── Cooldowns (game time) ─────────────────────────

        public static void StartCooldown(string itemId, float seconds)
        {
            _readyAt[itemId] = Time.time + seconds;
            _cooldownLength[itemId] = Mathf.Max(0.01f, seconds);
        }

        public static float CooldownRemaining(string itemId) =>
            _readyAt.TryGetValue(itemId, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;

        /// <summary>1 = just used, 0 = ready.</summary>
        public static float Cooldown01(string itemId) =>
            _cooldownLength.TryGetValue(itemId, out float len) ? Mathf.Clamp01(CooldownRemaining(itemId) / len) : 0f;
    }
}
