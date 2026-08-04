using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Manages 3D spatialized audio navigation with escalating hints.
    /// When the player hasn't made progress for a while, hints escalate:
    ///   1. Subtle ambient directional cue
    ///   2. Louder, distinct audio ping
    ///   3. Voice narration hint
    /// 
    /// Escalation thresholds are adjusted by DDA difficulty.
    /// </summary>
    public class AudioNavigationManager : MonoBehaviour
    {
        public static AudioNavigationManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _ambientCueSource;
        [SerializeField] private AudioSource _pingSource;
        [SerializeField] private AudioSource _voiceSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip _subtleAmbientClip;
        [SerializeField] private AudioClip _distinctPingClip;
        [SerializeField] private AudioClip[] _voiceHintClips; // Pool of voice hints

        [Header("Escalation Timing (seconds)")]
        [SerializeField] private float _tier1Delay = GameConstants.Audio.ESCALATION_TIER1_DELAY;
        [SerializeField] private float _tier2Delay = GameConstants.Audio.ESCALATION_TIER2_DELAY;
        [SerializeField] private float _tier3Delay = GameConstants.Audio.ESCALATION_TIER3_DELAY;

        [Header("Target")]
        [SerializeField] private Transform _navigationTarget;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public HintEscalationLevel CurrentEscalation { get; private set; } = HintEscalationLevel.None;

        private float _timeSinceProgress;
        private bool _isActive;
        private float _baseTier1Delay;
        private float _baseTier2Delay;
        private float _baseTier3Delay;

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

            _baseTier1Delay = _tier1Delay;
            _baseTier2Delay = _tier2Delay;
            _baseTier3Delay = _tier3Delay;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Subscribe<ItemCollectedEvent>(OnProgress);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeProgress);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<DifficultyChangedEvent>(OnDifficultyChanged);
            EventBus.Unsubscribe<ItemCollectedEvent>(OnProgress);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeProgress);
        }

        private void Update()
        {
            if (!_isActive || _navigationTarget == null) return;

            _timeSinceProgress += Time.deltaTime;

            // Check escalation thresholds
            HintEscalationLevel targetLevel = HintEscalationLevel.None;

            if (_timeSinceProgress >= _tier3Delay)
            {
                targetLevel = HintEscalationLevel.VoiceHint;
            }
            else if (_timeSinceProgress >= _tier2Delay)
            {
                targetLevel = HintEscalationLevel.DistinctPing;
            }
            else if (_timeSinceProgress >= _tier1Delay)
            {
                targetLevel = HintEscalationLevel.SubtleAmbient;
            }

            // Escalate if needed
            if (targetLevel > CurrentEscalation)
            {
                EscalateTo(targetLevel);
            }

            // Update spatial positioning of audio sources
            UpdateAudioPositions();
        }

        // ─────────────────────────────────────────────
        // Escalation
        // ─────────────────────────────────────────────

        private void EscalateTo(HintEscalationLevel level)
        {
            CurrentEscalation = level;

            Debug.Log($"[AudioNav] Hint escalated to: {level}");

            EventBus.Publish(new AudioHintEscalatedEvent
            {
                Level = level,
                TargetPosition = _navigationTarget != null ? _navigationTarget.position : Vector3.zero
            });

            switch (level)
            {
                case HintEscalationLevel.SubtleAmbient:
                    PlaySubtleHint();
                    break;

                case HintEscalationLevel.DistinctPing:
                    PlayDistinctPing();
                    break;

                case HintEscalationLevel.VoiceHint:
                    PlayVoiceHint();
                    break;
            }
        }

        private void PlaySubtleHint()
        {
            if (_ambientCueSource == null || _subtleAmbientClip == null) return;

            _ambientCueSource.clip = _subtleAmbientClip;
            _ambientCueSource.volume = 0.3f;
            _ambientCueSource.loop = true;
            _ambientCueSource.Play();
        }

        private void PlayDistinctPing()
        {
            if (_pingSource == null || _distinctPingClip == null) return;

            _pingSource.clip = _distinctPingClip;
            _pingSource.volume = 0.7f;
            _pingSource.loop = false;
            _pingSource.Play();

            // Also keep ambient playing
        }

        private void PlayVoiceHint()
        {
            if (_voiceSource == null || _voiceHintClips == null || _voiceHintClips.Length == 0) return;

            // Pick a random voice hint
            AudioClip clip = _voiceHintClips[Random.Range(0, _voiceHintClips.Length)];
            _voiceSource.clip = clip;
            _voiceSource.volume = 1.0f;
            _voiceSource.spatialBlend = 0.5f; // Mix of 2D and 3D
            _voiceSource.Play();
        }

        // ─────────────────────────────────────────────
        // Audio Positioning
        // ─────────────────────────────────────────────

        private void UpdateAudioPositions()
        {
            if (_navigationTarget == null) return;

            Vector3 targetPos = _navigationTarget.position;

            if (_ambientCueSource != null)
                _ambientCueSource.transform.position = targetPos;

            if (_pingSource != null)
                _pingSource.transform.position = targetPos;
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Set the navigation target. Audio cues will point toward this position.
        /// </summary>
        public void SetNavigationTarget(Transform target)
        {
            _navigationTarget = target;
            ResetProgress(); // New target = reset escalation
        }

        /// <summary>
        /// Clear the navigation target and stop all audio cues.
        /// </summary>
        public void ClearTarget()
        {
            _navigationTarget = null;
            StopAllHints();
            ResetProgress();
        }

        /// <summary>
        /// Call when the player makes meaningful progress (resets escalation timer).
        /// </summary>
        public void RecordProgress()
        {
            ResetProgress();
        }

        private void ResetProgress()
        {
            _timeSinceProgress = 0f;
            CurrentEscalation = HintEscalationLevel.None;
            StopAllHints();
        }

        private void StopAllHints()
        {
            if (_ambientCueSource != null && _ambientCueSource.isPlaying)
                _ambientCueSource.Stop();

            if (_pingSource != null && _pingSource.isPlaying)
                _pingSource.Stop();

            if (_voiceSource != null && _voiceSource.isPlaying)
                _voiceSource.Stop();
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            _isActive = evt.NewState == GameState.Exploring;

            if (!_isActive)
            {
                StopAllHints();
            }
        }

        private void OnDifficultyChanged(DifficultyChangedEvent evt)
        {
            // At lower difficulty, hints come sooner (shorter delays)
            // At higher difficulty, hints come later (player should explore more)
            float delayMultiplier = Mathf.Lerp(0.5f, 1.5f, evt.DifficultyLevel);

            _tier1Delay = _baseTier1Delay * delayMultiplier;
            _tier2Delay = _baseTier2Delay * delayMultiplier;
            _tier3Delay = _baseTier3Delay * delayMultiplier;
        }

        private void OnProgress(ItemCollectedEvent evt)
        {
            RecordProgress();
        }

        private void OnChallengeProgress(ChallengeCompletedEvent evt)
        {
            RecordProgress();
        }
    }
}
