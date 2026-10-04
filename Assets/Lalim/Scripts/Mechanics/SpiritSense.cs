using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Spirit Sense (Q) — unified with the old SpiritVision (V):
    ///
    /// 1. Audio Compass (passive, always on): spatialized ping at the current objective,
    ///    louder when the player faces it.
    ///
    /// 2. Active (1 orb, 15s at Normal, 40s cooldown after it ends):
    ///    - fades in the desaturated "spirit sight" screen effect,
    ///    - sends the reveal pulse (HiddenObjects within 40m),
    ///    - lights up every target relevant to the current task — within 40m while exploring,
    ///      the whole area during a minigame,
    ///    - shows a guiding light for the whole duration that leads the player to the next thing
    ///      to do (the objective, or the nearest relevant target) and circles above it on arrival.
    ///
    /// DDA changes duration and glow strength only; cooldown and cost never change.
    /// </summary>
    public class SpiritSense : AbilityBase
    {
        public static SpiritSense Instance { get; private set; }

        public override AbilityId Id => AbilityId.SpiritSense;
        public override int CostOrbs => Config != null ? Config.spiritSenseCostOrbs : GameConstants.Abilities.SPIRIT_SENSE_COST_ORBS;
        protected override float DurationSeconds => Config != null ? Config.GetSpiritSenseDuration(CurrentDifficulty) : GameConstants.Abilities.SPIRIT_SENSE_DURATION;
        protected override float CooldownSeconds => Config != null ? Config.spiritSenseCooldown : GameConstants.SpiritSense.COOLDOWN_SECONDS;

        [Header("Compass Settings")]
        [SerializeField] private AudioClip _compassPingClip;
        [SerializeField] private float _compassUpdateInterval = GameConstants.SpiritSense.COMPASS_UPDATE_INTERVAL;
        [SerializeField] private float _compassMaxVolume = 0.8f;
        [SerializeField] private float _compassMinVolume = 0.1f;

        [Header("Reveal")]
        [SerializeField] private AudioClip _revealSoundEffect;
        [SerializeField] private ParticleSystem _revealPulsePrefab;

        [Header("Spirit Sight (screen effect)")]
        [Tooltip("Optional global Volume. If empty, one is created at runtime.")]
        [SerializeField] private Volume _volume;
        [SerializeField, Min(0.05f)] private float _transitionSeconds = 0.7f;
        [SerializeField, Range(-100f, 0f)] private float _saturation = -70f;
        [SerializeField, Range(-100f, 100f)] private float _contrast = -15f;
        [SerializeField, Range(0f, 0.6f)] private float _vignette = 0.3f;

        [Header("Guiding Wisp")]
        [SerializeField] private SpiritWisp _wispPrefab;
        [SerializeField] private Color _wispColor = new Color(0.5f, 1f, 0.95f);
        [SerializeField] private float _wispSpawnAhead = 3f;

        [Header("Target")]
        [SerializeField] private Transform _currentObjective;

        [Header("Safety net")]
        [Tooltip("Fraction of the remaining cooldown removed when audio hints escalate to DistinctPing (player seems lost).")]
        [SerializeField, Range(0f, 1f)] private float _lostPlayerCooldownCut = 0.5f;

        private const float RefreshInterval = 0.5f;

        private AudioSource _compassAudioSource;
        private float _compassTimer;
        private float _refreshTimer;
        private float _volumeBlend;
        private VolumeProfile _runtimeProfile;
        private SpiritWisp _wisp;

        public Transform CurrentObjective => _currentObjective;
        private float ExplorationRadius => Config != null ? Config.spiritSenseExplorationRadius : GameConstants.SpiritSense.REVEAL_RADIUS;
        private float GlowIntensity => Config != null ? Config.GetSpiritGlowIntensity(CurrentDifficulty) : 1f;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _compassAudioSource = gameObject.AddComponent<AudioSource>();
            _compassAudioSource.spatialBlend = 1.0f;
            _compassAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _compassAudioSource.maxDistance = 50f;
            _compassAudioSource.loop = false;
            _compassAudioSource.playOnAwake = false;

            EnsureVolume();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_runtimeProfile != null) Destroy(_runtimeProfile);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AudioHintEscalatedEvent>(OnAudioHintEscalated);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AudioHintEscalatedEvent>(OnAudioHintEscalated);
            ForceEnd();
        }

        protected override void Update()
        {
            base.Update();
            UpdateCompassAudio();
            UpdateVolumeBlend();
        }

        // ─────────────────────────────────────────────
        // Audio Compass (Passive)
        // ─────────────────────────────────────────────

        private void UpdateCompassAudio()
        {
            if (_currentObjective == null || _compassPingClip == null) return;

            GameState state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Exploring;
            if (state != GameState.Exploring && state != GameState.InChallenge) return;

            _compassTimer -= Time.deltaTime;
            if (_compassTimer > 0) return;
            _compassTimer = _compassUpdateInterval;

            _compassAudioSource.transform.position = _currentObjective.position;

            Vector3 toObjective = (_currentObjective.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toObjective);
            _compassAudioSource.volume = Mathf.Lerp(_compassMinVolume, _compassMaxVolume, (dot + 1f) * 0.5f);
            GameAudio.PlayOneShot(_compassAudioSource, _compassPingClip);
        }

        // ─────────────────────────────────────────────
        // Active ability
        // ─────────────────────────────────────────────

        protected override void OnActivate()
        {
            if (_revealSoundEffect != null) GameAudio.PlayAt(_revealSoundEffect, transform.position);

            if (_revealPulsePrefab != null)
            {
                ParticleSystem pulse = Instantiate(_revealPulsePrefab, transform.position, Quaternion.identity);
                Destroy(pulse.gameObject, pulse.main.duration + 1f);
            }

            EventBus.Publish(new SpiritSenseActivatedEvent
            {
                PlayerPosition = transform.position,
                RevealRadius = ExplorationRadius
            });

            SpiritSenseRelevance.AttachToKnownTargets();
            RevealHiddenObjects();
            _refreshTimer = 0f;
            RefreshTargets();
        }

        protected override void OnTick(float deltaTime)
        {
            _refreshTimer -= deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                RefreshTargets();
            }
        }

        protected override void OnEnd(bool cancelled)
        {
            foreach (SpiritGlow glow in SpiritGlow.All) if (glow != null) glow.SetGlow(false, 0f);
            if (_wisp != null) { _wisp.Dismiss(); _wisp = null; }
        }

        private SpiritSenseContext BuildContext()
        {
            bool inChallenge = ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive;
            return new SpiritSenseContext
            {
                Origin = transform.position,
                Radius = ExplorationRadius,
                InChallenge = inChallenge,
                Challenge = inChallenge ? ChallengeManager.Instance.ActiveChallengeType : default,
                Objective = _currentObjective,
                CarriedDebris = SpiritSenseRelevance.FindCarriedDebris()
            };
        }

        /// <summary>Re-evaluates every target (relevance can change: a bin only glows while you hold matching debris).</summary>
        private void RefreshTargets()
        {
            SpiritSenseContext ctx = BuildContext();
            float intensity = GlowIntensity;

            foreach (SpiritGlow glow in SpiritGlow.All)
            {
                glow.SetGlow(glow.IsRelevant(ctx), intensity);
            }

            UpdateWisp(ctx);
        }

        /// <summary>
        /// The guiding light is there for the whole duration and always points at the next thing
        /// to do: the current objective if one is set, otherwise the nearest relevant target
        /// anywhere (trash to collect, the right bin, a sample point...). Retargets as things get done.
        /// </summary>
        private void UpdateWisp(SpiritSenseContext ctx)
        {
            Transform target = PickGuideTarget(ctx, _wisp != null ? _wisp.Target : null);

            if (_wisp == null)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : transform;
                Vector3 spawn = transform.position + cam.forward * _wispSpawnAhead + Vector3.up;
                _wisp = _wispPrefab != null
                    ? Instantiate(_wispPrefab, spawn, Quaternion.identity)
                    : SpiritWisp.CreateRuntime(_wispColor);
                _wisp.transform.position = spawn;
                _wisp.Init(transform, target);
            }
            else if (_wisp.Target != target)
            {
                _wisp.Retarget(target);
            }
        }

        /// <summary>
        /// Objective first; otherwise the nearest relevant target. Keeps the current target unless
        /// another is clearly closer (25%), so the light doesn't flicker between two similar ones.
        /// </summary>
        private Transform PickGuideTarget(SpiritSenseContext ctx, Transform current)
        {
            if (_currentObjective != null && _currentObjective.gameObject.activeInHierarchy) return _currentObjective;

            SpiritSenseContext anywhere = ctx;
            anywhere.InChallenge = true; // relevance across the whole area, not just the glow radius

            Transform best = null;
            float bestSqr = float.MaxValue;
            float currentSqr = float.MaxValue;
            foreach (SpiritGlow glow in SpiritGlow.All)
            {
                if (glow == null || !glow.IsRelevant(anywhere)) continue;
                float sqr = (glow.transform.position - transform.position).sqrMagnitude;
                if (glow.transform == current) currentSqr = sqr;
                if (sqr < bestSqr) { bestSqr = sqr; best = glow.transform; }
            }

            if (current != null && currentSqr < float.MaxValue && bestSqr > currentSqr * 0.5625f) return current; // 0.75² hysteresis
            return best;
        }

        // By distance, not physics: a hidden object has its colliders switched off (so it can't be
        // touched or picked up unseen), which an OverlapSphere would never find.
        private void RevealHiddenObjects()
        {
            float sqrRadius = ExplorationRadius * ExplorationRadius;
            foreach (HiddenObject hidden in new System.Collections.Generic.List<HiddenObject>(HiddenObject.Active))
            {
                if (hidden == null || hidden.IsRevealed) continue;
                if ((hidden.transform.position - transform.position).sqrMagnitude > sqrRadius) continue;
                hidden.Reveal();
                EventBus.Publish(new SpiritSenseRevealEvent
                {
                    RevealedObject = hidden.gameObject,
                    ObjectId = hidden.gameObject.name
                });
            }
        }

        // ─────────────────────────────────────────────
        // Screen effect (moved from SpiritVision)
        // ─────────────────────────────────────────────

        private void UpdateVolumeBlend()
        {
            float target = IsActive ? 1f : 0f;
            if (Mathf.Approximately(_volumeBlend, target)) return;
            _volumeBlend = Mathf.MoveTowards(_volumeBlend, target, Time.deltaTime / _transitionSeconds);
            if (_volume != null) _volume.weight = Mathf.SmoothStep(0f, 1f, _volumeBlend);
        }

        private void EnsureVolume()
        {
            if (_volume == null)
            {
                GameObject go = new GameObject("SpiritSenseVolume (runtime)");
                go.transform.SetParent(transform, false);
                _volume = go.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 60f;

                _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                var color = _runtimeProfile.Add<ColorAdjustments>(true);
                color.saturation.Override(_saturation);
                color.contrast.Override(_contrast);
                var vig = _runtimeProfile.Add<Vignette>(true);
                vig.intensity.Override(_vignette);
                vig.smoothness.Override(0.4f);
                _volume.profile = _runtimeProfile;
            }
            _volume.weight = 0f;
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>Legacy entry point (old HUD button). Routes through the normal activation rules.</summary>
        public void TryActivateReveal() => TryActivate();

        public void SetObjective(Transform objective) => _currentObjective = objective;

        public void ClearObjective() => _currentObjective = null;

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        /// <summary>Lost-player nudge: when audio hints escalate to a distinct ping, cut the remaining cooldown.</summary>
        private void OnAudioHintEscalated(AudioHintEscalatedEvent evt)
        {
            if (evt.Level == HintEscalationLevel.DistinctPing) ReduceCooldown(_lostPlayerCooldownCut);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, ExplorationRadius);

            if (_currentObjective != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _currentObjective.position);
            }
        }
#endif
    }
}
