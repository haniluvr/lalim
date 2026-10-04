using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// Player HP. Only refilled by SeagrassMendAbility or a checkpoint respawn —
    /// never regenerates passively. Saved with the game (PlayerVitals), so resuming a save
    /// continues at the same HP; a respawn still refills it.
    /// </summary>
    [RequireComponent(typeof(ToxicWaterTracker))]
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [SerializeField] private AbilityConfig _config;

        public float Max => _config != null ? _config.maxHealth : GameConstants.Abilities.PLAYER_MAX_HP;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>Registered by BubbleBarrierAbility; asked first whenever damage comes in.</summary>
        public BubbleBarrierAbility Barrier { get; set; }

        /// <summary>Dev-tool: ignore all damage.</summary>
        public bool Invulnerable { get; set; }
        public bool IsInvulnerable => Invulnerable || IsDead;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            if (_config == null)
            {
                AbilityController controller = GetComponent<AbilityController>();
                if (controller != null) _config = controller.Config;
            }
            Current = Max;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Applies damage, respecting Bubble Barrier: toxic damage is fully blocked while
        /// the barrier is active; a contact hit consumes the barrier's one-hit charge instead
        /// of harming the player, and only harms the player once that charge is gone.
        /// </summary>
        public void TakeDamage(float amount, DamageKind kind)
        {
            if (IsInvulnerable || amount <= 0f) return;
            if (Barrier != null && Barrier.TryBlock(kind)) return;

            Current = Mathf.Max(0f, Current - amount);
            EventBus.Publish(new PlayerHealthChangedEvent { Current = Current, Max = Max, Delta = -amount });

            if (Current <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            float before = Current;
            Current = Mathf.Min(Max, Current + amount);
            float actual = Current - before;
            if (actual > 0f)
            {
                EventBus.Publish(new PlayerHealthChangedEvent { Current = Current, Max = Max, Delta = actual });
            }
        }

        public bool IsAtFullHealth() => Current >= Max - 0.01f;

        private void Die()
        {
            IsDead = true;
            string sectionId = CheckpointManager.Instance != null && CheckpointManager.Instance.CurrentCheckpoint != null
                ? CheckpointManager.Instance.CurrentCheckpoint.SectionId
                : "";

            // CheckpointManager listens for this and runs the respawn sequence.
            EventBus.Publish(new PlayerDiedEvent { Position = transform.position, SectionId = sectionId });

            if (CheckpointManager.Instance == null)
            {
                // No checkpoint system in this scene (e.g. a test scene): revive in place.
                if (AbilityController.Instance != null) AbilityController.Instance.ForceEndAll();
                ResetToFull();
                if (PlayerStamina.Instance != null) PlayerStamina.Instance.RefillToFull();
                EventBus.Publish(new PlayerRespawnedEvent { CheckpointId = "" });
            }
        }

        /// <summary>Resuming a save: HP back to where it was when the game was saved.</summary>
        public void RestoreCurrent(float hp)
        {
            Current = Mathf.Clamp(hp, 1f, Max);
            IsDead = false;
            EventBus.Publish(new PlayerHealthChangedEvent { Current = Current, Max = Max, Delta = 0f });
        }

        // After every Start() has run (abilities set their starting charge there), put the saved
        // vitals back if this scene is being resumed from a save.
        private System.Collections.IEnumerator Start()
        {
            yield return null;
            PlayerVitals.TryRestore();
        }

        /// <summary>Called by CheckpointManager after a respawn completes.</summary>
        public void ResetToFull()
        {
            Current = Max;
            IsDead = false;
            EventBus.Publish(new PlayerHealthChangedEvent { Current = Current, Max = Max, Delta = 0f });
        }
    }
}
