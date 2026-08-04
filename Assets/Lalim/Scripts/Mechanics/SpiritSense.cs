using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// SpiritSense — the player's core ability with two modes:
    /// 
    /// 1. Audio Compass (passive, always on):
    ///    - Plays a spatialized audio cue at the current objective's position
    ///    - Audio intensity/panning indicates direction relative to player facing
    /// 
    /// 2. Reveal Mode (active, player-triggered):
    ///    - Pulse effect that reveals hidden objects within a radius
    ///    - Objects tagged "SpiritHidden" become visible
    ///    - Cooldown timer (modifiable by DDA for struggling players)
    /// </summary>
    public class SpiritSense : MonoBehaviour
    {
        public static SpiritSense Instance { get; private set; }

        [Header("Compass Settings")]
        [SerializeField] private AudioClip _compassPingClip;
        [SerializeField] private float _compassUpdateInterval = GameConstants.SpiritSense.COMPASS_UPDATE_INTERVAL;
        [SerializeField] private float _compassMaxVolume = 0.8f;
        [SerializeField] private float _compassMinVolume = 0.1f;

        [Header("Reveal Settings")]
        [SerializeField] private float _revealRadius = GameConstants.SpiritSense.REVEAL_RADIUS;
        [SerializeField] private float _cooldownDuration = GameConstants.SpiritSense.COOLDOWN_SECONDS;
        [SerializeField] private AudioClip _revealSoundEffect;
        [SerializeField] private ParticleSystem _revealPulsePrefab;

        [Header("Target")]
        [SerializeField] private Transform _currentObjective;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public bool IsOnCooldown { get; private set; }
        public float CooldownRemaining { get; private set; }
        public float CooldownTotal => _cooldownDuration;

        private AudioSource _compassAudioSource;
        private float _compassTimer;
        private float _baseCooldown;

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

            // Create a dedicated AudioSource for the compass
            _compassAudioSource = gameObject.AddComponent<AudioSource>();
            _compassAudioSource.spatialBlend = 1.0f; // Full 3D
            _compassAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _compassAudioSource.maxDistance = 50f;
            _compassAudioSource.loop = false;
            _compassAudioSource.playOnAwake = false;

            _baseCooldown = _cooldownDuration;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void Update()
        {
            // Update cooldown
            if (IsOnCooldown)
            {
                CooldownRemaining -= Time.deltaTime;
                if (CooldownRemaining <= 0)
                {
                    IsOnCooldown = false;
                    CooldownRemaining = 0;
                }

                EventBus.Publish(new SpiritSenseCooldownEvent
                {
                    RemainingTime = CooldownRemaining,
                    TotalCooldown = _cooldownDuration
                });
            }

            // Update compass audio
            UpdateCompassAudio();

            // Check for SpiritSense activation input
            if (InputManager.Instance != null && InputManager.Instance.SpiritSensePressed)
            {
                TryActivateReveal();
            }
        }

        // ─────────────────────────────────────────────
        // Audio Compass (Passive)
        // ─────────────────────────────────────────────

        private void UpdateCompassAudio()
        {
            if (_currentObjective == null || _compassPingClip == null) return;

            _compassTimer -= Time.deltaTime;
            if (_compassTimer > 0) return;

            _compassTimer = _compassUpdateInterval;

            // Position the audio source at the objective
            _compassAudioSource.transform.position = _currentObjective.position;

            // Calculate how well the player is facing the objective
            Vector3 toObjective = (_currentObjective.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, toObjective);

            // dot: 1.0 = facing directly at, -1.0 = facing away
            // Map to volume: louder when facing the objective
            float volume = Mathf.Lerp(_compassMinVolume, _compassMaxVolume, (dot + 1f) * 0.5f);
            _compassAudioSource.volume = volume;

            // Play ping
            _compassAudioSource.PlayOneShot(_compassPingClip);
        }

        // ─────────────────────────────────────────────
        // Reveal Mode (Active Ability)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Attempt to activate the reveal pulse.
        /// </summary>
        public void TryActivateReveal()
        {
            if (IsOnCooldown)
            {
                Debug.Log($"[SpiritSense] On cooldown: {CooldownRemaining:F1}s remaining");
                return;
            }

            ActivateReveal();
        }

        private void ActivateReveal()
        {
            Debug.Log("[SpiritSense] Reveal activated!");

            // Start cooldown
            IsOnCooldown = true;
            CooldownRemaining = _cooldownDuration;

            // Play reveal sound
            if (_revealSoundEffect != null)
            {
                AudioSource.PlayClipAtPoint(_revealSoundEffect, transform.position);
            }

            // Spawn pulse VFX
            if (_revealPulsePrefab != null)
            {
                ParticleSystem pulse = Instantiate(_revealPulsePrefab, transform.position, Quaternion.identity);
                Destroy(pulse.gameObject, pulse.main.duration + 1f);
            }

            // Publish activation event
            EventBus.Publish(new SpiritSenseActivatedEvent
            {
                PlayerPosition = transform.position,
                RevealRadius = _revealRadius
            });

            // Find and reveal hidden objects in range
            RevealHiddenObjects();
        }

        private void RevealHiddenObjects()
        {
            // Find all colliders in range
            Collider[] colliders = Physics.OverlapSphere(transform.position, _revealRadius);

            foreach (Collider col in colliders)
            {
                HiddenObject hidden = col.GetComponent<HiddenObject>();
                if (hidden != null && !hidden.IsRevealed)
                {
                    hidden.Reveal();

                    EventBus.Publish(new SpiritSenseRevealEvent
                    {
                        RevealedObject = col.gameObject,
                        ObjectId = col.gameObject.name
                    });
                }
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Set the current navigation objective.
        /// </summary>
        public void SetObjective(Transform objective)
        {
            _currentObjective = objective;
        }

        /// <summary>
        /// Clear the current objective (stops compass audio).
        /// </summary>
        public void ClearObjective()
        {
            _currentObjective = null;
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnDifficultyChanged(DifficultyChangedEvent evt)
        {
            // Easier difficulty = shorter cooldown (more forgiving)
            // At max difficulty, cooldown is 1.5x base. At min, it's 0.6x base.
            float cooldownMultiplier = Mathf.Lerp(0.6f, 1.5f, evt.DifficultyLevel);
            _cooldownDuration = _baseCooldown * cooldownMultiplier;
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            // Disable compass during menus/pause
            enabled = evt.NewState == GameState.Exploring || evt.NewState == GameState.InChallenge;
        }

        // ─────────────────────────────────────────────
        // Gizmos
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _revealRadius);

            if (_currentObjective != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _currentObjective.position);
            }
        }
#endif
    }
}
