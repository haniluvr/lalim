using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Trigger volume representing a toxic water hazard. The player tracks every
    /// volume it's currently overlapping and applies only the single strongest
    /// severity (Heavy beats Light) — overlapping zones never stack damage.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ToxicWaterVolume : MonoBehaviour
    {
        [SerializeField] private ToxicSeverity _severity = ToxicSeverity.Light;
        [SerializeField] private AbilityConfig _config;

        [Tooltip("Level zone volume: use the level-wide drain set by the level director (LevelToxicity) instead of the live difficulty-scaled rate.")]
        [SerializeField] private bool _useLevelRate;

        [Tooltip("Halve stamina regen while inside (local toxic pockets). Off for level-wide zone pollution, which covers the whole level until cleansed.")]
        [SerializeField] private bool _slowsStaminaRegen = true;

        public bool SlowsStaminaRegen => _slowsStaminaRegen;

        public ToxicSeverity Severity => _severity;

        private static readonly System.Collections.Generic.List<ToxicWaterVolume> s_Active = new System.Collections.Generic.List<ToxicWaterVolume>();

        /// <summary>Every enabled volume — used by things without a collider (the dugong) to test exposure.</summary>
        public static System.Collections.Generic.IReadOnlyList<ToxicWaterVolume> Active => s_Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_Active.Clear();

        private Collider _collider;

        private void Awake() => _collider = GetComponent<Collider>();
        private void OnEnable() { if (!s_Active.Contains(this)) s_Active.Add(this); }
        private void OnDisable() => s_Active.Remove(this);

        /// <summary>True if a world-space point is inside this volume.</summary>
        public bool Contains(Vector3 point)
        {
            if (_collider == null || !_collider.enabled) return false;
            if (!_collider.bounds.Contains(point)) return false;
            return (_collider.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;
            ToxicWaterTracker tracker = other.GetComponentInParent<ToxicWaterTracker>();
            if (tracker != null) tracker.Enter(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;
            ToxicWaterTracker tracker = other.GetComponentInParent<ToxicWaterTracker>();
            if (tracker != null) tracker.Exit(this);
        }

        public float GetDps(float difficulty)
        {
            if (_useLevelRate && LevelToxicity.CurrentDps >= 0f) return LevelToxicity.CurrentDps;

            float baseDps;
            if (_severity == ToxicSeverity.Heavy)
                baseDps = _config != null ? _config.toxicHeavyDpsNormal : GameConstants.Abilities.TOXIC_HEAVY_DPS;
            else if (_severity == ToxicSeverity.Light)
                baseDps = _config != null ? _config.toxicLightDpsNormal : GameConstants.Abilities.TOXIC_LIGHT_DPS;
            else
                return 0f;

            float mult = _config != null ? _config.GetToxicDamageMultiplier(difficulty) : 1f;
            return baseDps * mult;
        }
    }
}
