using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Orb-based stamina pool (4 orbs by design). Stored as a single float so that
    /// spending stamina from a partly-filled orb preserves its fill progress instead
    /// of resetting it — e.g. spending 1 orb at 2.6 leaves 1.6, not 1.0.
    ///
    /// Only FULL orbs (Mathf.FloorToInt(_current)) can be spent. Regen pauses for a
    /// short delay after any spend, then refills continuously, sped up while mounted
    /// and slowed while in unshielded toxic water.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        public static PlayerStamina Instance { get; private set; }

        [SerializeField] private AbilityConfig _config;

        public int MaxOrbs => _pool.MaxOrbs;
        public float Current => _pool.Current;
        public int FullOrbs => _pool.FullOrbs;

        private StaminaPool _pool = new StaminaPool(GameConstants.Abilities.MAX_STAMINA_ORBS);
        private bool _mounted;
        private ToxicSeverity _toxicSeverity;
        private bool _barrierBlockingToxic;
        private float _lastPublishedCurrent = -1f;
        private float _publishThrottle;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_config == null)
            {
                AbilityController controller = GetComponent<AbilityController>();
                if (controller != null) _config = controller.Config;
            }
            // Each level scene has its own player, so this is "new game / new level": start part-filled.
            _pool = new StaminaPool(
                _config != null ? _config.maxStaminaOrbs : GameConstants.Abilities.MAX_STAMINA_ORBS,
                _config != null ? _config.startingStaminaOrbs : GameConstants.Abilities.STARTING_STAMINA_ORBS);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DugongMountChangedEvent>(OnMountChanged);
            EventBus.Subscribe<ToxicExposureChangedEvent>(OnToxicExposureChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DugongMountChangedEvent>(OnMountChanged);
            EventBus.Unsubscribe<ToxicExposureChangedEvent>(OnToxicExposureChanged);
        }

        /// <summary>Product of the active regen factors: 1.5× while riding, 0.5× in toxic water without the barrier.</summary>
        public float RegenMultiplier
        {
            get
            {
                float multiplier = 1f;
                if (_mounted) multiplier *= _config != null ? _config.regenMultiplierMounted : 1.5f;
                if (_toxicSeverity != ToxicSeverity.None && _toxicSlowsRegen && !_barrierBlockingToxic)
                    multiplier *= _config != null ? _config.regenMultiplierToxic : 0.5f;
                return multiplier;
            }
        }

        private void Update()
        {
            // DDA scales regen through seconds-per-orb (4s / 5s / 6s).
            float secondsPerOrb = _config != null ? _config.GetOrbRegenSeconds(AbilityDifficulty.Current) : GameConstants.Abilities.ORB_REGEN_SECONDS;
            _pool.Tick(GameplayClock.DeltaTime, secondsPerOrb, RegenMultiplier);   // no regen during dialogue

            // Throttle UI publish to ~10Hz, but always publish immediately when FullOrbs changes.
            _publishThrottle -= Time.deltaTime;
            bool fullOrbsChanged = Mathf.FloorToInt(_lastPublishedCurrent + 0.0001f) != FullOrbs;
            bool changed = !Mathf.Approximately(_lastPublishedCurrent, Current);
            if (fullOrbsChanged || (changed && _publishThrottle <= 0f))
            {
                Publish();
            }
        }

        private void Publish()
        {
            _publishThrottle = 0.1f;
            _lastPublishedCurrent = Current;
            EventBus.Publish(new StaminaChangedEvent { Current = Current, FullOrbs = FullOrbs, MaxOrbs = MaxOrbs });
        }

        /// <summary>Attempts to spend the given number of full orbs. Returns false (and publishes StaminaInsufficientEvent) if not enough are available.</summary>
        public bool TrySpend(int orbs, AbilityId ability)
        {
            float delay = _config != null ? _config.orbRegenDelaySeconds : GameConstants.Abilities.ORB_REGEN_DELAY_SECONDS;
            if (!_pool.TrySpend(orbs, delay, free: InfiniteStamina))
            {
                EventBus.Publish(new StaminaInsufficientEvent { Ability = ability, Needed = orbs, Have = FullOrbs });
                return false;
            }

            Publish();
            return true;
        }

        public bool HasFullOrbs(int orbs) => _pool.CanSpend(orbs);

        /// <summary>Instantly restores stamina to full. Used on respawn/load, and by dev tools.</summary>
        public void RefillToFull()
        {
            _pool.Refill();
            Publish();
        }

        /// <summary>Resuming a save: stamina back to where it was.</summary>
        public void RestoreCurrent(float orbs)
        {
            _pool.SetCurrent(orbs);
            Publish();
        }

        /// <summary>Dev-tool alias for RefillToFull.</summary>
        public void DebugRefill() => RefillToFull();

        /// <summary>Dev-tool: when true, spending never reduces stamina.</summary>
        public bool InfiniteStamina { get; set; }

        private void OnMountChanged(DugongMountChangedEvent evt) => _mounted = evt.Mounted;
        private bool _toxicSlowsRegen;

        private void OnToxicExposureChanged(ToxicExposureChangedEvent evt)
        {
            _toxicSeverity = evt.Severity;
            _toxicSlowsRegen = evt.SlowsStaminaRegen;
        }

        /// <summary>Called by BubbleBarrierAbility so regen doesn't get the toxic penalty while shielded.</summary>
        public void SetBarrierBlockingToxic(bool blocking) => _barrierBlockingToxic = blocking;
    }
}
