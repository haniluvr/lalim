using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// The dugong's own health — the green bar under the dugong card. Separate from the player's
    /// HP and from Tidebond's ride time.
    ///
    ///  - Resilient: 300 HP and it only takes ~35% of toxic-water damage (AbilityConfig).
    ///  - Hurt whenever the dugong itself is in toxic water — including while you ride it.
    ///    Bubble Barrier protects it when it's inside the bubble (mounted or close by).
    ///  - Recovers slowly once it's been out of toxic water for a few seconds.
    ///  - At 0 HP it's exhausted: you're dismounted, and Tidebond + Seagrass Mend are unavailable
    ///    until it recovers to half.
    ///  - Restored to full when the player respawns.
    /// </summary>
    [DisallowMultipleComponent]
    public class DugongHealth : MonoBehaviour
    {
        public static DugongHealth Instance { get; private set; }

        [SerializeField] private AbilityConfig _config;

        public float Max => _config != null ? _config.dugongMaxHealth : 300f;
        public float Current { get; private set; }
        public float Normalized => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;

        /// <summary>True after collapsing to 0 HP, until it recovers to dugongRecoverFraction.</summary>
        public bool IsExhausted { get; private set; }

        public ToxicSeverity Exposure { get; private set; }

        private float DamageTaken => _config != null ? _config.dugongDamageTaken : 0.35f;
        private float RegenPerSecond => _config != null ? _config.dugongRegenPerSecond : 4f;
        private float RegenDelay => _config != null ? _config.dugongRegenDelay : 4f;
        private float RecoverFraction => _config != null ? _config.dugongRecoverFraction : 0.5f;

        private float _regenDelayRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_config == null && AbilityController.Instance != null) _config = AbilityController.Instance.Config;
            Current = Max;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable() => EventBus.Subscribe<PlayerRespawnedEvent>(OnPlayerRespawned);
        private void OnDisable() => EventBus.Unsubscribe<PlayerRespawnedEvent>(OnPlayerRespawned);

        private void Start()
        {
            // The player (and its AbilityController) may have initialised after this.
            if (_config == null && AbilityController.Instance != null)
            {
                _config = AbilityController.Instance.Config;
                Current = Max;
            }
        }

        private void Update()
        {
            if (GameplayClock.Frozen) return;   // no damage or recovery during dialogue
            ToxicWaterVolume strongest = StrongestVolumeAt(transform.position);
            Exposure = strongest != null ? strongest.Severity : ToxicSeverity.None;

            if (strongest != null && !IsShieldedByBarrier())
            {
                float dps = strongest.GetDps(AbilityDifficulty.Current) * DamageTaken;
                TakeDamage(dps * Time.deltaTime);
                _regenDelayRemaining = RegenDelay;
            }
            else if (_regenDelayRemaining > 0f)
            {
                _regenDelayRemaining -= Time.deltaTime;
            }
            else if (Current < Max)
            {
                Current = Mathf.Min(Max, Current + RegenPerSecond * Time.deltaTime);
            }

            if (IsExhausted && Normalized >= RecoverFraction) IsExhausted = false;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || Current <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            if (Current <= 0f) Exhaust();
        }

        /// <summary>Resuming a save: the dugong's health back to where it was.</summary>
        public void RestoreCurrent(float hp, bool exhausted)
        {
            Current = Mathf.Clamp(hp, 0f, Max);
            IsExhausted = exhausted || Current <= 0f;
            _regenDelayRemaining = 0f;
        }

        public void RestoreToFull()
        {
            Current = Max;
            IsExhausted = false;
            _regenDelayRemaining = 0f;
        }

        private void Exhaust()
        {
            IsExhausted = true;
            // Too hurt to carry the player.
            if (AbilityController.Instance != null) AbilityController.Instance.Get(AbilityId.Tidebond)?.ForceEnd();
        }

        private bool IsShieldedByBarrier()
        {
            BubbleBarrierAbility barrier = PlayerHealth.Instance != null ? PlayerHealth.Instance.Barrier : null;
            if (barrier == null || !barrier.IsActive) return false;
            if (PlayerController.Instance == null) return false;
            if (PlayerController.Instance.IsMounted) return true;
            float r = barrier.CurrentRadius;
            return (transform.position - PlayerController.Instance.transform.position).sqrMagnitude <= r * r;
        }

        private static ToxicWaterVolume StrongestVolumeAt(Vector3 position)
        {
            ToxicWaterVolume strongest = null;
            var volumes = ToxicWaterVolume.Active;
            for (int i = 0; i < volumes.Count; i++)
            {
                ToxicWaterVolume v = volumes[i];
                if (v == null || !v.Contains(position)) continue;
                if (strongest == null || v.Severity > strongest.Severity) strongest = v;
            }
            return strongest;
        }

        private void OnPlayerRespawned(PlayerRespawnedEvent evt) => RestoreToFull();
    }
}
