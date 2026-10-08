using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// One zone of a level: its statue, its toxic water, its music and its ordered quest steps.
    /// Every zone follows the same loop: toxic → talk to the statue → minigames (each rewarding
    /// the tool the next one needs) → cleanse → open the next barrier.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneQuest : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Keep GameConstants zone IDs (luzon_zone_a/b/c) — RegionProgressTracker unlocks the next region from them.")]
        [SerializeField] private string _zoneId = GameConstants.Zones.LUZON_BLEACHED_REEF;
        [SerializeField] private string _displayName = "Amansinaya's Shallows";
        [SerializeField] private Region _region = Region.Luzon;

        [Header("References")]
        [SerializeField] private StatueDialogueRouter _statue;
        [Tooltip("Covers the zone's swimmable water. Also used to know which zone the player is in.")]
        [SerializeField] private ToxicWaterVolume _toxicVolume;

        [Header("Music")]
        [SerializeField] private AudioClip _bgmPolluted;
        [SerializeField] private AudioClip _bgmCleansed;
        [Tooltip("Ambience loop for this zone (flat stereo, Ambient volume). Plays under the music while the diver is here.")]
        [SerializeField] private AudioClip _ambience;

        [Header("Steps (run in order)")]
        [SerializeField] private List<QuestStep> _steps = new List<QuestStep>();

        public string ZoneId => _zoneId;
        public string DisplayName => _displayName;
        public Region Region => _region;
        public StatueDialogueRouter Statue => _statue;
        public ToxicWaterVolume ToxicVolume => _toxicVolume;
        public IReadOnlyList<QuestStep> Steps => _steps;

        public bool IsCleansed =>
            RegionProgressTracker.Instance != null && RegionProgressTracker.Instance.IsZoneCompleted(_zoneId);

        public AudioClip CurrentTrack => IsCleansed && _bgmCleansed != null ? _bgmCleansed : _bgmPolluted;
        public AudioClip Ambience => _ambience;

        // Cached at Awake: the toxic volume is disabled when the zone is cleansed, and a disabled
        // collider reports empty bounds.
        private Bounds _bounds;
        private bool _hasBounds;

        private void Awake() => CacheBounds();

        private void CacheBounds()
        {
            if (_hasBounds || _toxicVolume == null) return;
            Collider c = _toxicVolume.GetComponent<Collider>();
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) return;
            _bounds = c.bounds;
            _hasBounds = true;
        }

        public bool Contains(Vector3 worldPosition)
        {
            CacheBounds();
            return _hasBounds && _bounds.Contains(worldPosition);
        }

#if UNITY_EDITOR
        public void EditorSetup(string zoneId, string displayName, Region region, StatueDialogueRouter statue,
                                ToxicWaterVolume toxic, List<QuestStep> steps)
        {
            _zoneId = zoneId;
            _displayName = displayName;
            _region = region;
            _statue = statue;
            _toxicVolume = toxic;
            _steps = steps;
        }
#endif
    }
}
