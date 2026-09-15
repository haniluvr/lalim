using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Controls the environmental transformation of a zone from "Broken/Polluted" to "Restored/Healed".
    /// 
    /// Setup:
    ///   1. Group your zone's broken environment elements under one parent (or list).
    ///   2. Group your zone's restored/healed elements under another parent (or list).
    ///   3. Set the ZoneId (e.g., "luzon_zone_a", "luzon_zone_b", "luzon_zone_c").
    /// 
    /// Behavior:
    ///   - On Start: checks RegionProgressTracker to see if the zone was already completed (from save).
    ///     If completed, immediately shows the restored state. Otherwise, shows the broken state.
    ///   - On ZoneCompletedEvent: triggers a dramatic transition (plays VFX, fanfare sound, 
    ///     swaps broken objects for restored objects).
    /// </summary>
    public class ZoneRestorationController : MonoBehaviour
    {
        [Header("Zone Identification")]
        [Tooltip("Must match the ZoneId in the ZoneTrigger (e.g., luzon_zone_a, luzon_zone_b, luzon_zone_c)")]
        [SerializeField] private string _zoneId;

        [Header("Container Parents (Recommended)")]
        [Tooltip("Parent GameObject containing all broken/polluted assets (active before completion)")]
        [SerializeField] private GameObject _brokenStateContainer;

        [Tooltip("Parent GameObject containing all restored/healed assets (active after completion)")]
        [SerializeField] private GameObject _restoredStateContainer;

        [Header("Individual Objects (Optional fine-grained control)")]
        [Tooltip("Extra specific objects to disable when restored")]
        [SerializeField] private List<GameObject> _additionalBrokenObjects = new List<GameObject>();

        [Tooltip("Extra specific objects to enable when restored")]
        [SerializeField] private List<GameObject> _additionalRestoredObjects = new List<GameObject>();

        [Header("Transition Feedback")]
        [Tooltip("Optional particle effect that bursts across the zone upon completion (e.g., cleansing wave)")]
        [SerializeField] private ParticleSystem _restorationVFX;

        [Tooltip("Sound played when the area heals/restores")]
        [SerializeField] private AudioClip _restorationFanfare;

        [Tooltip("Delay in seconds after challenge completion before swapping visuals (allows VFX/sound to start)")]
        [SerializeField] private float _transitionDelay = 0.5f;

        [Header("Audio")]
        private AudioSource _audioSource;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public bool IsRestored { get; private set; }

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1.0f; // 3D sound
            }
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
            // Check if this zone is already completed in save data
            bool alreadyCompleted = false;
            if (RegionProgressTracker.Instance != null)
            {
                alreadyCompleted = RegionProgressTracker.Instance.IsZoneCompleted(_zoneId);
            }

            ApplyStateImmediate(alreadyCompleted);
        }

        // ─────────────────────────────────────────────
        // Event Handling
        // ─────────────────────────────────────────────

        private void OnZoneCompleted(ZoneCompletedEvent evt)
        {
            if (evt.ZoneId != _zoneId) return;

            if (!IsRestored)
            {
                StartCoroutine(PerformRestorationSequence());
            }
        }

        // ─────────────────────────────────────────────
        // Restoration Sequence
        // ─────────────────────────────────────────────

        private IEnumerator PerformRestorationSequence()
        {
            IsRestored = true;
            Debug.Log($"[ZoneRestoration] Beginning restoration for zone: {_zoneId}");

            // 1. Play restoration particle burst
            if (_restorationVFX != null)
            {
                _restorationVFX.Play();
            }

            // 2. Play audio fanfare
            if (_restorationFanfare != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_restorationFanfare);
            }

            // 3. Optional dramatic pause
            if (_transitionDelay > 0f)
            {
                yield return new WaitForSeconds(_transitionDelay);
            }

            // 4. Swap visual states
            ApplyStateImmediate(true);

            Debug.Log($"[ZoneRestoration] Zone {_zoneId} is now RESTORED!");
        }

        /// <summary>
        /// Instantly swaps the active states of broken vs. restored containers.
        /// </summary>
        public void ApplyStateImmediate(bool isRestored)
        {
            IsRestored = isRestored;

            // Broken container: active when NOT restored
            if (_brokenStateContainer != null)
            {
                _brokenStateContainer.SetActive(!isRestored);
            }

            // Restored container: active when restored
            if (_restoredStateContainer != null)
            {
                _restoredStateContainer.SetActive(isRestored);
            }

            // Individual broken objects
            foreach (var obj in _additionalBrokenObjects)
            {
                if (obj != null) obj.SetActive(!isRestored);
            }

            // Individual restored objects
            foreach (var obj in _additionalRestoredObjects)
            {
                if (obj != null) obj.SetActive(isRestored);
            }
        }

        // ─────────────────────────────────────────────
        // Editor Helper
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Preview Broken State")]
        private void PreviewBroken()
        {
            ApplyStateImmediate(false);
        }

        [ContextMenu("Preview Restored State")]
        private void PreviewRestored()
        {
            ApplyStateImmediate(true);
        }
#endif
    }
}
