using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Sits on the player. Tracks every ToxicWaterVolume currently overlapped and applies
    /// damage from only the strongest one each frame. Publishes ToxicExposureChangedEvent
    /// whenever the effective severity changes (drives stamina regen penalty + HUD vignette).
    /// </summary>
    public class ToxicWaterTracker : MonoBehaviour
    {
        private readonly List<ToxicWaterVolume> _active = new List<ToxicWaterVolume>();
        private ToxicSeverity _lastPublishedSeverity = ToxicSeverity.None;
        private bool _lastPublishedSlowsRegen;

        public ToxicSeverity CurrentSeverity => _lastPublishedSeverity;

        private void OnEnable() => EventBus.Subscribe<PlayerRespawnedEvent>(OnRespawned);
        private void OnDisable() => EventBus.Unsubscribe<PlayerRespawnedEvent>(OnRespawned);

        // Teleporting out of a volume doesn't always produce OnTriggerExit, so start clean on respawn.
        private void OnRespawned(PlayerRespawnedEvent evt)
        {
            _active.Clear();
            PublishIfChanged();
        }

        public void Enter(ToxicWaterVolume volume)
        {
            if (!_active.Contains(volume)) _active.Add(volume);
            PublishIfChanged();
        }

        public void Exit(ToxicWaterVolume volume)
        {
            _active.Remove(volume);
            PublishIfChanged();
        }

        private void Update()
        {
            if (_active.Count == 0) return;

            // Catches volumes that were disabled/destroyed while the player was inside (e.g. a zone got cleansed).
            PublishIfChanged();
            if (PlayerHealth.Instance == null) return;

            // Paused during dialogue. Minigames keep draining: the water is still dirty.
            if (LevelToxicity.IsSuppressed || GameplayClock.Frozen) return;

            ToxicWaterVolume strongest = GetStrongest();
            if (strongest == null) return;

            float damage = strongest.GetDps(AbilityDifficulty.Current) * Time.deltaTime;
            // ...but it never kills mid-minigame (the player can't swim away or heal there): it stops at 1 HP.
            if (Lalim.Mechanics.ChallengeManager.Instance != null && Lalim.Mechanics.ChallengeManager.Instance.IsFirstPersonChallengeActive)
                damage = Mathf.Min(damage, Mathf.Max(0f, PlayerHealth.Instance.Current - 1f));
            if (damage > 0f) PlayerHealth.Instance.TakeDamage(damage, DamageKind.Toxic);
        }

        /// <summary>Overlapping zones never stack: only the single strongest severity applies.</summary>
        public static ToxicSeverity Strongest(IEnumerable<ToxicSeverity> severities)
        {
            ToxicSeverity strongest = ToxicSeverity.None;
            foreach (ToxicSeverity s in severities) if (s > strongest) strongest = s;
            return strongest;
        }

        private ToxicWaterVolume GetStrongest()
        {
            ToxicWaterVolume strongest = null;
            foreach (var v in _active)
            {
                if (v == null || !v.isActiveAndEnabled) continue;
                if (strongest == null || v.Severity > strongest.Severity) strongest = v;
            }
            return strongest;
        }

        private void PublishIfChanged()
        {
            _active.RemoveAll(v => v == null || !v.isActiveAndEnabled);
            ToxicWaterVolume strongest = GetStrongest();
            ToxicSeverity severity = strongest != null ? strongest.Severity : ToxicSeverity.None;
            bool slowsRegen = false;
            foreach (var v in _active) if (v != null && v.isActiveAndEnabled && v.SlowsStaminaRegen) slowsRegen = true;
            if (severity == _lastPublishedSeverity && slowsRegen == _lastPublishedSlowsRegen) return;

            _lastPublishedSeverity = severity;
            _lastPublishedSlowsRegen = slowsRegen;
            EventBus.Publish(new ToxicExposureChangedEvent { Severity = severity, SlowsStaminaRegen = slowsRegen });
        }
    }
}
