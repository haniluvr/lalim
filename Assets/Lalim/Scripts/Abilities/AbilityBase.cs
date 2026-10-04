using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    public enum AbilityState { Ready, Active, Cooldown }

    /// <summary>
    /// Shared skeleton for all six abilities: cooldown/duration timers, stamina spend,
    /// and the CanActivate() gate that AbilityController and the HUD both use to decide
    /// whether a button is usable right now and why (AbilityBlockReason).
    ///
    /// Concrete abilities override OnActivate()/OnTick()/OnEnd() for their specific effect.
    /// All timers use scaled time, so they freeze while the game is paused (timeScale = 0).
    /// </summary>
    public abstract class AbilityBase : MonoBehaviour
    {
        public abstract AbilityId Id { get; }
        public abstract int CostOrbs { get; }
        protected abstract float DurationSeconds { get; } // 0 = instant, no Active state
        protected abstract float CooldownSeconds { get; }

        /// <summary>True for abilities that move or shield the player — blocked in minigames where the player can't swim (Fishing, Frequency, Trench, Ritual).</summary>
        protected virtual bool RequiresFreeMovement => false;

        /// <summary>True for abilities that are blocked during any challenge (Tidebond).</summary>
        protected virtual bool ExplorationOnly => false;

        /// <summary>
        /// How charged the ability is when the level starts: 1 = ready, 0.5 = half-way through its
        /// cooldown, 0 = full cooldown. The player is re-created each level, so this re-applies per level.
        /// </summary>
        protected virtual float StartingCharge01 => 1f;

        /// <summary>
        /// 0..1 recharge progress: 1 when ready or active, rising from 0 while on cooldown.
        /// Charge-style buttons (Seagrass Mend card, Bubble Barrier) fill up with this.
        /// Abilities that charge from something other than time (Bubble Barrier) override it.
        /// </summary>
        public virtual float Charge01 => State == AbilityState.Cooldown ? 1f - CooldownNormalized : 1f;

        /// <summary>Ready AND fully charged — what the HUD treats as "usable now" (ignoring stamina/context).</summary>
        public virtual bool IsCharged => State == AbilityState.Ready;

        [SerializeField] protected AbilityConfig Config;

        public AbilityState State { get; private set; } = AbilityState.Ready;
        public float CooldownRemaining { get; private set; }
        public float DurationRemaining { get; protected set; }
        public float CooldownTotal => CooldownSeconds;
        public float DurationTotal => DurationSeconds;

        public bool IsActive => State == AbilityState.Active;
        public float CooldownNormalized => CooldownSeconds > 0f ? Mathf.Clamp01(CooldownRemaining / CooldownSeconds) : 0f;
        public float DurationNormalized => DurationSeconds > 0f ? Mathf.Clamp01(DurationRemaining / DurationSeconds) : 0f;

        protected virtual void Awake()
        {
            if (Config == null)
            {
                AbilityController controller = GetComponentInParent<AbilityController>();
                if (controller != null) Config = controller.Config;
            }
        }

        protected virtual void Start()
        {
            float charge = Mathf.Clamp01(StartingCharge01);
            if (charge < 1f && State == AbilityState.Ready && CooldownSeconds > 0f)
            {
                State = AbilityState.Cooldown;
                CooldownRemaining = CooldownSeconds * (1f - charge);
            }
        }

        protected virtual void Update()
        {
            float dt = GameplayClock.DeltaTime;   // durations and cooldowns hold still during dialogue
            if (State == AbilityState.Active)
            {
                if (dt <= 0f) return;
                DurationRemaining -= dt;
                OnTick(dt);

                if (State == AbilityState.Active && DurationRemaining <= 0f)
                {
                    EndInternal(cancelled: false);
                }
            }
            else if (State == AbilityState.Cooldown)
            {
                CooldownRemaining -= dt;
                if (CooldownRemaining <= 0f)
                {
                    CooldownRemaining = 0f;
                    State = AbilityState.Ready;
                    OnCooldownFinished();
                }
            }
        }

        /// <summary>Checks every gating condition and reports why activation would fail, if it would.</summary>
        public bool CanActivate(out AbilityBlockReason reason)
        {
            if (State == AbilityState.Active) { reason = AbilityBlockReason.AlreadyActive; return false; }
            if (State == AbilityState.Cooldown) { reason = AbilityBlockReason.OnCooldown; return false; }

            reason = CheckGameContext();
            if (reason != AbilityBlockReason.None) return false;

            reason = CheckContext();
            if (reason != AbilityBlockReason.None) return false;

            if (CostOrbs > 0 && PlayerStamina.Instance != null && !PlayerStamina.Instance.HasFullOrbs(CostOrbs))
            {
                reason = AbilityBlockReason.NotEnoughStamina;
                return false;
            }

            reason = AbilityBlockReason.None;
            return true;
        }

        /// <summary>
        /// Checks rules that don't depend on stamina or cooldown. The HUD uses this to decide
        /// whether to show the lock icon (not allowed here) versus the cooldown/stamina states.
        /// </summary>
        public AbilityBlockReason GetContextBlockReason()
        {
            AbilityBlockReason reason = CheckGameContext();
            return reason != AbilityBlockReason.None ? reason : CheckContext();
        }

        private AbilityBlockReason CheckGameContext()
        {
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return AbilityBlockReason.NotAllowedHere;

            GameState state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Exploring;
            if (state != GameState.Exploring && state != GameState.InChallenge) return AbilityBlockReason.NotAllowedHere;

            // A 3rd-person challenge (e.g. trash collection) is free swimming: every ability works.
            // A 1st-person minigame frames the player in place, so movement/exploration skills wait.
            ChallengeManager challenge = ChallengeManager.Instance;
            if (challenge != null && challenge.IsFirstPersonChallengeActive)
            {
                if (ExplorationOnly || RequiresFreeMovement) return AbilityBlockReason.NotAllowedHere;
            }

            return AbilityBlockReason.None;
        }

        /// <summary>Override for ability-specific rules (mounted / not mounted, full HP, ...).</summary>
        protected virtual AbilityBlockReason CheckContext() => AbilityBlockReason.None;

        public bool TryActivate()
        {
            if (!CanActivate(out AbilityBlockReason reason))
            {
                EventBus.Publish(new AbilityBlockedEvent { Ability = Id, Reason = reason });
                if (reason == AbilityBlockReason.NotEnoughStamina && PlayerStamina.Instance != null)
                {
                    EventBus.Publish(new StaminaInsufficientEvent { Ability = Id, Needed = CostOrbs, Have = PlayerStamina.Instance.FullOrbs });
                }
                return false;
            }

            if (CostOrbs > 0 && PlayerStamina.Instance != null && !PlayerStamina.Instance.TrySpend(CostOrbs, Id))
            {
                EventBus.Publish(new AbilityBlockedEvent { Ability = Id, Reason = AbilityBlockReason.NotEnoughStamina });
                return false;
            }

            if (DurationSeconds > 0f)
            {
                State = AbilityState.Active;
                DurationRemaining = DurationSeconds;
            }

            OnActivate();
            EventBus.Publish(new AbilityActivatedEvent { Ability = Id });

            if (DurationSeconds <= 0f && CooldownAfterInstantUse())
            {
                StartCooldown();
            }

            return true;
        }

        /// <summary>Player-requested early end (Riptide hold-again, Tidebond dismount). Cooldown starts now.</summary>
        public void Cancel()
        {
            if (State != AbilityState.Active) return;
            EndInternal(cancelled: true);
        }

        /// <summary>System-requested end (death, respawn, challenge start). Cooldown starts now.</summary>
        public void ForceEnd()
        {
            if (State != AbilityState.Active) return;
            EndInternal(cancelled: true);
        }

        /// <summary>Cuts the remaining cooldown by a fraction (0.5 = halve it). Used by the lost-player safety net.</summary>
        public void ReduceCooldown(float fraction)
        {
            if (State != AbilityState.Cooldown) return;
            CooldownRemaining *= Mathf.Clamp01(1f - fraction);
        }

        /// <summary>Dev-tool: clears the cooldown immediately (charge-based abilities also fill up).</summary>
        public virtual void ResetCooldown()
        {
            if (State != AbilityState.Cooldown) return;
            CooldownRemaining = 0f;
            State = AbilityState.Ready;
            OnCooldownFinished();
        }

        /// <summary>For saving: cooldown left, counting a running skill as about to start its full cooldown.</summary>
        public float CooldownForSave =>
            State == AbilityState.Active ? CooldownSeconds : State == AbilityState.Cooldown ? CooldownRemaining : 0f;

        /// <summary>For saving: extra charge for charge-based skills (Bubble Barrier); -1 when unused.</summary>
        public virtual float ChargeForSave => -1f;

        /// <summary>Resuming a save: cooldown (and charge) exactly as they were.</summary>
        public void RestoreFromSave(float cooldownRemaining, float charge)
        {
            if (State == AbilityState.Active) return;
            DurationRemaining = 0f;
            CooldownRemaining = Mathf.Clamp(cooldownRemaining, 0f, Mathf.Max(0f, CooldownSeconds));
            State = CooldownRemaining > 0f ? AbilityState.Cooldown : AbilityState.Ready;
            if (charge >= 0f) RestoreCharge(charge);
        }

        protected virtual void RestoreCharge(float charge) { }

        /// <summary>Starts the cooldown now (e.g. a combo window ran out). Only valid while Ready.</summary>
        protected void BeginCooldown()
        {
            if (State == AbilityState.Ready) StartCooldown();
        }

        /// <summary>
        /// Instant abilities only: return false to stay Ready after this use (e.g. mid-combo).
        /// Call BeginCooldown() later to end it.
        /// </summary>
        protected virtual bool CooldownAfterInstantUse() => true;

        protected virtual void OnCooldownFinished() { }

        private void EndInternal(bool cancelled)
        {
            OnEnd(cancelled);
            EventBus.Publish(new AbilityEndedEvent { Ability = Id, Cancelled = cancelled });
            StartCooldown();
        }

        private void StartCooldown()
        {
            DurationRemaining = 0f;
            CooldownRemaining = CooldownSeconds;
            State = CooldownSeconds > 0f ? AbilityState.Cooldown : AbilityState.Ready;
        }

        protected virtual void OnActivate() { }
        protected virtual void OnTick(float deltaTime) { }
        protected virtual void OnEnd(bool cancelled) { }

        protected static float CurrentDifficulty => AbilityDifficulty.Current;
    }

    /// <summary>
    /// Reads the live DDA difficulty directly instead of caching DifficultyChangedEvent,
    /// so components that spawn after the last publish (e.g. on scene load) are never stale.
    /// </summary>
    public static class AbilityDifficulty
    {
        public static float Current => Lalim.DDA.DDAManager.Instance != null
            ? Lalim.DDA.DDAManager.Instance.DifficultyLevel
            : GameConstants.DDA.DEFAULT_DIFFICULTY;
    }
}
