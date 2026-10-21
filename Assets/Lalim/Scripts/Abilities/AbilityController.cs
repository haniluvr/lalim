using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Sits on the player. Routes input to the six abilities and enforces the rules between them:
    ///
    ///  - Shift / Surge button: tap (&lt; 0.25s) = Surge (up to 3 in a row as a combo); hold 0.6s =
    ///    Riptide (or cancels an active Riptide); releasing between 0.25s and 0.6s does nothing,
    ///    preventing accidental Riptides. While Riptide runs, taps do nothing.
    ///    A tap made mid-dash is buffered briefly so combo taps aren't lost.
    ///  - F toggles Tidebond (press again to dismount), Q Spirit Sense, R Seagrass Mend, G Bubble Barrier.
    ///  - A 1st-person challenge starting cancels Riptide and dismounts Tidebond; Barrier keeps running.
    ///  - Death / respawn force-ends every active ability (cooldowns keep running).
    ///
    /// Touch buttons call RequestActivate() directly rather than setting InputManager flags, so a
    /// press can't be lost to Update ordering between the EventSystem and this component.
    /// </summary>
    public class AbilityController : MonoBehaviour
    {
        public static AbilityController Instance { get; private set; }

        [SerializeField] private AbilityConfig _config;
        public AbilityConfig Config => _config;

        private readonly Dictionary<AbilityId, AbilityBase> _abilities = new Dictionary<AbilityId, AbilityBase>();

        // Tap/hold state for the Surge/Riptide input.
        private bool _surgeInputHeld;
        private float _holdTime;
        private bool _holdFired;
        private float _surgeBufferTimer;
        private const float SurgeBufferSeconds = 0.3f;

        private float TapThreshold => _config != null ? _config.tapHoldThreshold : 0.25f;
        private float HoldThreshold => _config != null ? _config.holdActivateThreshold : 0.6f;

        /// <summary>
        /// 0 while not charging; ramps 0→1 between the tap threshold (0.25s) and the hold threshold (0.6s).
        /// Drives the charge ring on the Surge/Riptide button.
        /// </summary>
        public float HoldChargeNormalized
        {
            get
            {
                if (!_surgeInputHeld || _holdFired || _holdTime <= TapThreshold) return 0f;
                return Mathf.Clamp01((_holdTime - TapThreshold) / Mathf.Max(0.01f, HoldThreshold - TapThreshold));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            RegisterAbilities();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void RegisterAbilities()
        {
            _abilities.Clear();
            foreach (AbilityBase ability in GetComponentsInChildren<AbilityBase>(true))
            {
                if (_abilities.ContainsKey(ability.Id))
                {
                    Debug.LogWarning($"[AbilityController] Duplicate ability {ability.Id} on {ability.name}; ignoring.");
                    continue;
                }
                _abilities.Add(ability.Id, ability);
            }
        }

        /// <summary>Returns the ability component for 'id', or null if the player doesn't have it.</summary>
        public AbilityBase Get(AbilityId id)
        {
            if (_abilities.TryGetValue(id, out AbilityBase ability) && ability != null) return ability;

            // Spirit Sense may live on a different object than the player in older scenes.
            if (id == AbilityId.SpiritSense && SpiritSense.Instance != null)
            {
                _abilities[id] = SpiritSense.Instance;
                return SpiritSense.Instance;
            }
            return null;
        }

        public T Get<T>(AbilityId id) where T : AbilityBase => Get(id) as T;

        // ─────────────────────────────────────────────
        // Input
        // ─────────────────────────────────────────────

        private void Update()
        {
            InputManager input = InputManager.Instance;
            if (input == null) return;

            UpdateSurgeRiptideInput(input.SurgeRiptideKeyHeld);
            UpdateSurgeBuffer();

            if (input.SpiritSensePressed) RequestActivate(AbilityId.SpiritSense);
            if (input.TidebondPressed) RequestActivate(AbilityId.Tidebond);
            if (input.SeagrassMendPressed) RequestActivate(AbilityId.SeagrassMend);
            if (input.BubbleBarrierPressed) RequestActivate(AbilityId.BubbleBarrier);
        }

        /// <summary>
        /// Tap/hold resolution works from the held LEVEL rather than one-frame down/up flags,
        /// so it can't miss an edge regardless of script execution order.
        /// </summary>
        private void UpdateSurgeRiptideInput(bool heldNow)
        {
            if (heldNow && !_surgeInputHeld)
            {
                _surgeInputHeld = true;
                _holdTime = 0f;
                _holdFired = false;
                return;
            }

            if (heldNow)
            {
                _holdTime += Time.deltaTime;
                if (!_holdFired && _holdTime >= HoldThreshold)
                {
                    _holdFired = true;
                    AbilityBase riptide = Get(AbilityId.Riptide);
                    if (riptide != null)
                    {
                        if (riptide.IsActive) riptide.Cancel();
                        else riptide.TryActivate();
                    }
                }
                return;
            }

            if (_surgeInputHeld)
            {
                _surgeInputHeld = false;
                if (!_holdFired && _holdTime < TapThreshold) TapSurge();
                _holdTime = 0f;
                _holdFired = false;
            }
        }

        private void TapSurge()
        {
            AbilityBase surge = Get(AbilityId.Surge);
            if (surge == null) return;

            // Mid-dash in a combo: hold the tap and fire it the moment the dash ends.
            if (PlayerController.Instance != null && PlayerController.Instance.IsDashing && surge.State == AbilityState.Ready)
            {
                _surgeBufferTimer = SurgeBufferSeconds;
                return;
            }
            surge.TryActivate();
        }

        private void UpdateSurgeBuffer()
        {
            if (_surgeBufferTimer <= 0f) return;
            _surgeBufferTimer -= Time.deltaTime;
            if (PlayerController.Instance != null && PlayerController.Instance.IsDashing) return;
            _surgeBufferTimer = 0f;
            Get(AbilityId.Surge)?.TryActivate();
        }

        /// <summary>
        /// Press handler shared by keyboard and touch buttons. Toggle-style abilities (Tidebond)
        /// end early when pressed while active; everything else just tries to activate.
        /// Surge/Riptide use the hold gesture instead — see InputManager.TriggerSurgeRiptideDown/Up.
        /// </summary>
        public void RequestActivate(AbilityId id)
        {
            AbilityBase ability = Get(id);
            if (ability == null) return;

            if (id == AbilityId.Tidebond && ability.IsActive)
            {
                ability.Cancel();
                return;
            }

            ability.TryActivate();
        }

        /// <summary>Ends every active ability. Used on death/respawn. Cooldowns keep running.</summary>
        public void ForceEndAll()
        {
            foreach (AbilityBase ability in _abilities.Values)
            {
                if (ability != null) ability.ForceEnd();
            }
            _surgeInputHeld = false;
            _holdFired = false;
            _holdTime = 0f;
            _surgeBufferTimer = 0f;
            if (PlayerController.Instance != null) PlayerController.Instance.CancelDash();
        }

        /// <summary>Dev-tool: clears every cooldown.</summary>
        public void ResetAllCooldowns()
        {
            foreach (AbilityBase ability in _abilities.Values)
            {
                if (ability != null) ability.ResetCooldown();
            }
        }

        public IEnumerable<AbilityBase> All => _abilities.Values;

        // ─────────────────────────────────────────────
        // Inter-ability rules
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // A 3rd-person challenge is free swimming (e.g. the Bakunawa chase across the map): a ride
            // in progress carries on. Only a 1st-person minigame, which frames the diver in place,
            // ends the movement skills. Bubble Barrier deliberately keeps running either way.
            if (evt.Perspective == ChallengePerspective.ThirdPerson) return;
            Get(AbilityId.Riptide)?.ForceEnd();
            Get(AbilityId.Tidebond)?.ForceEnd();
        }

        private void OnPlayerDied(PlayerDiedEvent evt) => ForceEndAll();

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Returning to a menu / results screen shouldn't leave a hold gesture half-charged.
            if (evt.NewState != GameState.Exploring && evt.NewState != GameState.InChallenge)
            {
                _surgeInputHeld = false;
                _holdFired = false;
                _holdTime = 0f;
            }
        }
    }
}
