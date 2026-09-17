using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Moon Tide — active ability allowing the diver to harness lunar tidal shifts.
    /// Cycles through TideLevel states (Low -> Mid -> High) with cooldown management,
    /// triggering environmental tidal surges and notifying gameplay listeners via EventBus.
    /// </summary>
    public class MoonTideAbility : MonoBehaviour
    {
        public static MoonTideAbility Instance { get; private set; }

        [Header("Tide Ability Settings")]
        [SerializeField] private float _cooldownDuration = 12.0f;
        [SerializeField] private AudioClip _tidalSurgeSound;
        [SerializeField] private ParticleSystem _tideSurgeVFX;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public TideLevel CurrentTide { get; private set; } = TideLevel.Mid;
        public bool IsOnCooldown { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float CooldownTotal => _cooldownDuration;
        public float CooldownNormalized => _cooldownDuration > 0f ? Mathf.Clamp01(CooldownRemaining / _cooldownDuration) : 0f;

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
        }

        private void Update()
        {
            if (IsOnCooldown)
            {
                CooldownRemaining -= Time.deltaTime;
                if (CooldownRemaining <= 0f)
                {
                    IsOnCooldown = false;
                    CooldownRemaining = 0f;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Trigger the Moon Tide ability, cycling the tidal level and triggering tidal effects.
        /// </summary>
        public void TriggerMoonTide()
        {
            if (IsOnCooldown)
            {
                Debug.Log($"[MoonTide] On cooldown: {CooldownRemaining:F1}s remaining");
                return;
            }

            TideLevel prev = CurrentTide;
            switch (CurrentTide)
            {
                case TideLevel.Low:
                    CurrentTide = TideLevel.Mid;
                    break;
                case TideLevel.Mid:
                    CurrentTide = TideLevel.High;
                    break;
                case TideLevel.High:
                default:
                    CurrentTide = TideLevel.Low;
                    break;
            }

            IsOnCooldown = true;
            CooldownRemaining = _cooldownDuration;

            // Audio & VFX
            if (_tidalSurgeSound != null)
            {
                AudioSource.PlayClipAtPoint(_tidalSurgeSound, transform.position);
            }

            if (_tideSurgeVFX != null)
            {
                ParticleSystem pulse = Instantiate(_tideSurgeVFX, transform.position, Quaternion.identity);
                Destroy(pulse.gameObject, pulse.main.duration + 1f);
            }

            Debug.Log($"[MoonTide] Tide shifted: {prev} -> {CurrentTide}!");

            EventBus.Publish(new TideChangedEvent
            {
                PreviousTide = prev,
                NewTide = CurrentTide
            });
        }
    }
}
