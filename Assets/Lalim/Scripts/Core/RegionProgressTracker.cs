using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Core
{
    /// <summary>
    /// Tracks zone completion state per region.
    /// When all 3 zones in a region are completed, publishes RegionCompletedEvent.
    /// Integrates with SaveManager for persistence.
    /// </summary>
    public class RegionProgressTracker : MonoBehaviour
    {
        public static RegionProgressTracker Instance { get; private set; }

        // Zone completion state: zoneId → completed
        private Dictionary<string, bool> _zoneCompletion = new Dictionary<string, bool>();

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

            InitializeZones();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ZoneCompletedEvent>(OnZoneCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ZoneCompletedEvent>(OnZoneCompleted);
        }

        private void Start()
        {
            // Restore from save if available
            if (SaveManager.Instance != null && SaveManager.Instance.HasCurrentSave)
            {
                RestoreFromSave(SaveManager.Instance.CurrentSave);
            }
        }

        // ─────────────────────────────────────────────
        // Initialization
        // ─────────────────────────────────────────────

        private void InitializeZones()
        {
            // Register all zones as incomplete
            RegisterZonesForRegion(Region.Luzon);
            RegisterZonesForRegion(Region.Visayas);
            RegisterZonesForRegion(Region.Mindanao);
        }

        private void RegisterZonesForRegion(Region region)
        {
            string[] zones = GameConstants.Zones.GetZonesForRegion(region);
            foreach (string zoneId in zones)
            {
                if (!_zoneCompletion.ContainsKey(zoneId))
                {
                    _zoneCompletion[zoneId] = false;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Query
        // ─────────────────────────────────────────────

        /// <summary>
        /// Check if a specific zone has been completed.
        /// </summary>
        public bool IsZoneCompleted(string zoneId)
        {
            return _zoneCompletion.ContainsKey(zoneId) && _zoneCompletion[zoneId];
        }

        /// <summary>
        /// Get the number of completed zones in a region.
        /// </summary>
        public int GetCompletedZoneCount(Region region)
        {
            int count = 0;
            string[] zones = GameConstants.Zones.GetZonesForRegion(region);
            foreach (string zoneId in zones)
            {
                if (IsZoneCompleted(zoneId)) count++;
            }
            return count;
        }

        /// <summary>
        /// Get the completion ratio for a region (0.0 to 1.0).
        /// </summary>
        public float GetCompletionRatio(Region region)
        {
            int total = GameConstants.Zones.ZONES_PER_REGION;
            if (total <= 0) return 0f;
            return (float)GetCompletedZoneCount(region) / total;
        }

        /// <summary>
        /// Check if all zones in a region are completed.
        /// </summary>
        public bool IsRegionFullyCompleted(Region region)
        {
            return GetCompletedZoneCount(region) >= GameConstants.Zones.ZONES_PER_REGION;
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnZoneCompleted(ZoneCompletedEvent evt)
        {
            if (!_zoneCompletion.ContainsKey(evt.ZoneId))
            {
                Debug.LogWarning($"[RegionProgress] Unknown zone ID: {evt.ZoneId}");
                return;
            }

            if (_zoneCompletion[evt.ZoneId])
            {
                Debug.Log($"[RegionProgress] Zone already completed: {evt.ZoneId}");
                return;
            }

            _zoneCompletion[evt.ZoneId] = true;
            int completed = GetCompletedZoneCount(evt.Region);
            int total = GameConstants.Zones.ZONES_PER_REGION;

            Debug.Log($"[RegionProgress] Zone completed: {evt.ZoneId} " +
                      $"({completed}/{total} in {evt.Region})");

            // Check if the entire region is now complete
            if (completed >= total)
            {
                Debug.Log($"[RegionProgress] Region fully completed: {evt.Region}!");

                EventBus.Publish(new RegionCompletedEvent
                {
                    CompletedRegion = evt.Region
                });
            }

            // Update save data
            UpdateSaveData();
        }

        // ─────────────────────────────────────────────
        // Save / Load
        // ─────────────────────────────────────────────

        private void UpdateSaveData()
        {
            if (SaveManager.Instance == null) return;

            var save = SaveManager.Instance.CurrentSave;

            // Convert to serializable list
            if (save.completedZoneIds == null)
            {
                save.completedZoneIds = new List<string>();
            }
            else
            {
                save.completedZoneIds.Clear();
            }

            foreach (var kvp in _zoneCompletion)
            {
                if (kvp.Value)
                {
                    save.completedZoneIds.Add(kvp.Key);
                }
            }
        }

        private void RestoreFromSave(SaveData save)
        {
            if (save.completedZoneIds == null) return;

            foreach (string zoneId in save.completedZoneIds)
            {
                if (_zoneCompletion.ContainsKey(zoneId))
                {
                    _zoneCompletion[zoneId] = true;
                }
            }

            Debug.Log($"[RegionProgress] Restored {save.completedZoneIds.Count} completed zones from save");
        }
    }
}
