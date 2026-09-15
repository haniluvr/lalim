using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Parameter being measured at this sample point.
    /// Determines the UI label and visual feedback color.
    /// </summary>
    public enum SampleParameter
    {
        pH,
        Salinity,
        Temperature
    }

    /// <summary>
    /// A world-space location where the player can collect a water sample.
    /// Implements IInteractable so the PlayerController picks it up via raycast.
    ///
    /// Setup:
    ///   1. Place an empty GameObject where you want the sample point
    ///   2. Add a Collider (sphere recommended, radius ~1m)
    ///   3. Attach this script
    ///   4. Set the SampleParameter in the Inspector
    ///   5. Optionally assign visual/audio feedback references
    ///
    /// The player must have the Water Sample Kit equipped (ToolEquipSystem)
    /// to interact. On success, reports to the parent WaterSamplingChallenge.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SamplePoint : MonoBehaviour, IInteractable
    {
        [Header("Sample Configuration")]
        [Tooltip("Which parameter this point measures")]
        [SerializeField] private SampleParameter _parameter = SampleParameter.pH;

        [Tooltip("Display name shown in the interaction prompt")]
        [SerializeField] private string _pointName = "Sample Point";

        [Tooltip("Item ID of the required tool (Water Sample Kit)")]
        [SerializeField] private string _requiredToolId = "07_watersamplekit";

        [Header("Sampling Animation")]
        [Tooltip("Time in seconds the sampling process takes (hold duration)")]
        [SerializeField] private float _samplingDuration = 2.5f;

        [Header("Visual Feedback")]
        [Tooltip("Renderer to tint when the point is available/sampled")]
        [SerializeField] private Renderer _markerRenderer;

        [SerializeField] private Color _availableColor = new Color(0.2f, 0.7f, 1f, 0.6f);
        [SerializeField] private Color _samplingColor = new Color(1f, 0.9f, 0.2f, 0.8f);
        [SerializeField] private Color _completedColor = new Color(0.2f, 1f, 0.4f, 0.6f);

        [Header("Particle Effect (Optional)")]
        [Tooltip("Particle system to play when sampling completes")]
        [SerializeField] private ParticleSystem _completeVFX;

        [Header("Audio")]
        [SerializeField] private AudioClip _samplingLoopSound;
        [SerializeField] private AudioClip _sampleCompleteSound;
        [SerializeField] private AudioClip _noToolSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public SampleParameter Parameter => _parameter;
        public bool IsSampled { get; private set; }
        public bool IsSampling { get; private set; }

        private float _samplingProgress;
        private AudioSource _audioSource;
        private Material _markerMaterial;
        private WaterSamplingChallenge _challenge;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Ensure collider is solid (for raycasting, not trigger)
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            // Cache audio source
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1f;
            }

            // Cache material
            if (_markerRenderer != null)
            {
                _markerMaterial = _markerRenderer.material;
                _markerMaterial.color = _availableColor;
            }

            // Find parent challenge
            _challenge = GetComponentInParent<WaterSamplingChallenge>();
        }

        private void Update()
        {
            if (!IsSampling) return;

            // Progress the sampling hold
            _samplingProgress += Time.deltaTime;

            // Check if player released the interact button (cancel)
            if (InputManager.Instance != null && !InputManager.Instance.InteractHeld)
            {
                CancelSampling();
                return;
            }

            // Check completion
            if (_samplingProgress >= _samplingDuration)
            {
                CompleteSampling();
            }
        }

        // ─────────────────────────────────────────────
        // IInteractable
        // ─────────────────────────────────────────────

        public void Interact()
        {
            if (IsSampled || IsSampling) return;

            // Check if player has the required tool equipped
            if (ToolEquipSystem.Instance == null ||
                !ToolEquipSystem.Instance.IsToolEquipped(_requiredToolId))
            {
                Debug.Log($"[SamplePoint] Player needs {_requiredToolId} equipped to sample");

                if (_noToolSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_noToolSound);
                }
                return;
            }

            BeginSampling();
        }

        public string GetInteractionPrompt()
        {
            if (IsSampled) return $"{_pointName} — Already sampled";

            if (ToolEquipSystem.Instance == null ||
                !ToolEquipSystem.Instance.IsToolEquipped(_requiredToolId))
            {
                return $"Equip Water Sample Kit to test {_parameter}";
            }

            return $"Hold to sample {_parameter} at {_pointName}";
        }

        // ─────────────────────────────────────────────
        // Sampling Process
        // ─────────────────────────────────────────────

        private void BeginSampling()
        {
            IsSampling = true;
            _samplingProgress = 0f;

            SetColor(_samplingColor);

            // Play looping sample sound
            if (_samplingLoopSound != null && _audioSource != null)
            {
                _audioSource.clip = _samplingLoopSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }

            // Report player action to reset hesitation timer
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportPlayerAction();
            }

            Debug.Log($"[SamplePoint] Sampling started at {_pointName} ({_parameter})");
        }

        private void CompleteSampling()
        {
            IsSampling = false;
            IsSampled = true;

            // Stop loop sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }

            // Play complete sound
            if (_sampleCompleteSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_sampleCompleteSound);
            }

            // Visual feedback
            SetColor(_completedColor);

            // Play VFX
            if (_completeVFX != null)
            {
                _completeVFX.Play();
            }

            // Report task completed to ChallengeManager
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportTaskCompleted();
            }

            // Notify parent challenge
            if (_challenge != null)
            {
                _challenge.OnSampleCollected(this);
            }

            Debug.Log($"[SamplePoint] Sample collected: {_parameter} at {_pointName}");
        }

        private void CancelSampling()
        {
            IsSampling = false;
            _samplingProgress = 0f;

            // Stop loop sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }

            SetColor(_availableColor);

            Debug.Log($"[SamplePoint] Sampling cancelled at {_pointName}");
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Reset this point to un-sampled state (for challenge retry).
        /// </summary>
        public void ResetPoint()
        {
            IsSampled = false;
            IsSampling = false;
            _samplingProgress = 0f;
            SetColor(_availableColor);
        }

        /// <summary>
        /// Current sampling progress from 0 to 1. Used by HUD for progress ring.
        /// </summary>
        public float SamplingProgressNormalized =>
            _samplingDuration > 0f ? Mathf.Clamp01(_samplingProgress / _samplingDuration) : 0f;

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private void SetColor(Color color)
        {
            if (_markerMaterial != null)
            {
                _markerMaterial.color = color;
            }
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor;
            switch (_parameter)
            {
                case SampleParameter.pH:          gizmoColor = new Color(0.9f, 0.3f, 0.3f, 0.5f); break;
                case SampleParameter.Salinity:    gizmoColor = new Color(0.3f, 0.5f, 0.9f, 0.5f); break;
                case SampleParameter.Temperature: gizmoColor = new Color(0.9f, 0.6f, 0.1f, 0.5f); break;
                default:                          gizmoColor = Color.white; break;
            }

            if (IsSampled) gizmoColor = new Color(0.2f, 1f, 0.4f, 0.5f);

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.0f,
                $"{_pointName}\n({_parameter})\n{(IsSampled ? "✓ SAMPLED" : "○ PENDING")}"
            );
        }
#endif
    }
}
