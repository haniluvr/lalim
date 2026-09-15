using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// A substrate anchor point where a coral fragment can be planted.
    /// Implements IInteractable for the PlayerController interaction system.
    ///
    /// Setup:
    ///   1. Place an empty GameObject at each planting spot on the reef
    ///   2. Add a Collider (box or sphere, ~0.5m)
    ///   3. Attach this script
    ///   4. Optionally assign a visual mesh for the empty slot and the planted coral
    ///
    /// The player must have the Coral Replanting Kit equipped (ToolEquipSystem)
    /// and be carrying a CoralFragment to interact.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CoralSlot : MonoBehaviour, IInteractable
    {
        [Header("Slot Configuration")]
        [Tooltip("Display name for this planting spot")]
        [SerializeField] private string _slotName = "Coral Slot";

        [Tooltip("Item ID of the required tool (Coral Replanting Kit)")]
        [SerializeField] private string _requiredToolId = "05_coralreplantingkit";

        [Header("Planting Animation")]
        [Tooltip("Time in seconds the planting process takes")]
        [SerializeField] private float _plantingDuration = 3.0f;

        [Header("Visuals")]
        [Tooltip("GameObject shown when the slot is empty (e.g., bare substrate mesh)")]
        [SerializeField] private GameObject _emptySlotVisual;

        [Tooltip("GameObject shown after coral is planted (e.g., coral fragment mesh)")]
        [SerializeField] private GameObject _plantedCoralVisual;

        [Tooltip("Renderer to tint for feedback (can be on the empty slot visual)")]
        [SerializeField] private Renderer _slotRenderer;

        [SerializeField] private Color _availableColor = new Color(0.6f, 0.4f, 0.2f, 0.6f);
        [SerializeField] private Color _plantingColor = new Color(1f, 0.85f, 0.3f, 0.8f);
        [SerializeField] private Color _plantedColor = new Color(0.3f, 0.9f, 0.5f, 0.8f);
        [SerializeField] private Color _noToolColor = new Color(0.8f, 0.3f, 0.3f, 0.5f);

        [Header("Particle Effect (Optional)")]
        [SerializeField] private ParticleSystem _plantedVFX;

        [Header("Audio")]
        [SerializeField] private AudioClip _plantingLoopSound;
        [SerializeField] private AudioClip _plantedSound;
        [SerializeField] private AudioClip _noToolSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public bool IsPlanted { get; private set; }
        public bool IsPlanting { get; private set; }

        private float _plantingProgress;
        private AudioSource _audioSource;
        private Material _slotMaterial;
        private CoralReplantingChallenge _challenge;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Ensure collider is solid for raycasting
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
            if (_slotRenderer != null)
            {
                _slotMaterial = _slotRenderer.material;
                _slotMaterial.color = _availableColor;
            }

            // Initial visual state
            if (_emptySlotVisual != null) _emptySlotVisual.SetActive(true);
            if (_plantedCoralVisual != null) _plantedCoralVisual.SetActive(false);

            // Find parent challenge
            _challenge = GetComponentInParent<CoralReplantingChallenge>();
        }

        private void Update()
        {
            if (!IsPlanting) return;

            // Progress the planting hold
            _plantingProgress += Time.deltaTime;

            // Check if player released the interact button (cancel)
            if (InputManager.Instance != null && !InputManager.Instance.InteractHeld)
            {
                CancelPlanting();
                return;
            }

            // Check completion
            if (_plantingProgress >= _plantingDuration)
            {
                CompletePlanting();
            }
        }

        // ─────────────────────────────────────────────
        // IInteractable
        // ─────────────────────────────────────────────

        public void Interact()
        {
            if (IsPlanted || IsPlanting) return;

            // Check if player has the required tool equipped
            if (ToolEquipSystem.Instance == null ||
                !ToolEquipSystem.Instance.IsToolEquipped(_requiredToolId))
            {
                Debug.Log($"[CoralSlot] Player needs {_requiredToolId} equipped to plant");

                if (_noToolSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_noToolSound);
                }

                // Brief flash of no-tool color
                if (_slotMaterial != null)
                {
                    _slotMaterial.color = _noToolColor;
                    Invoke(nameof(ResetColor), 0.5f);
                }
                return;
            }

            // Check if the parent challenge has fragments available
            if (_challenge != null && !_challenge.HasFragmentsAvailable())
            {
                Debug.Log("[CoralSlot] No coral fragments available to plant");
                return;
            }

            BeginPlanting();
        }

        public string GetInteractionPrompt()
        {
            if (IsPlanted) return $"{_slotName} — Coral planted ✓";

            if (ToolEquipSystem.Instance == null ||
                !ToolEquipSystem.Instance.IsToolEquipped(_requiredToolId))
            {
                return "Equip Coral Replanting Kit to plant coral";
            }

            if (_challenge != null && !_challenge.HasFragmentsAvailable())
            {
                return "Find more coral fragments to plant";
            }

            return $"Hold to plant coral at {_slotName}";
        }

        // ─────────────────────────────────────────────
        // Planting Process
        // ─────────────────────────────────────────────

        private void BeginPlanting()
        {
            IsPlanting = true;
            _plantingProgress = 0f;

            SetColor(_plantingColor);

            // Play looping planting sound
            if (_plantingLoopSound != null && _audioSource != null)
            {
                _audioSource.clip = _plantingLoopSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }

            // Report player action to reset hesitation timer
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportPlayerAction();
            }

            Debug.Log($"[CoralSlot] Planting started at {_slotName}");
        }

        private void CompletePlanting()
        {
            IsPlanting = false;
            IsPlanted = true;

            // Stop loop sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }

            // Play planted sound
            if (_plantedSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_plantedSound);
            }

            // Swap visuals
            if (_emptySlotVisual != null) _emptySlotVisual.SetActive(false);
            if (_plantedCoralVisual != null) _plantedCoralVisual.SetActive(true);

            // Visual feedback
            SetColor(_plantedColor);

            // Play VFX
            if (_plantedVFX != null)
            {
                _plantedVFX.Play();
            }

            // Report task completed to ChallengeManager
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportTaskCompleted();
            }

            // Notify parent challenge
            if (_challenge != null)
            {
                _challenge.OnCoralPlanted(this);
            }

            // Disable collider — no more interaction needed
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Debug.Log($"[CoralSlot] Coral planted at {_slotName}");
        }

        private void CancelPlanting()
        {
            IsPlanting = false;
            _plantingProgress = 0f;

            // Stop loop sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }

            SetColor(_availableColor);

            Debug.Log($"[CoralSlot] Planting cancelled at {_slotName}");
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Reset this slot to empty state (for challenge retry).
        /// </summary>
        public void ResetSlot()
        {
            IsPlanted = false;
            IsPlanting = false;
            _plantingProgress = 0f;

            if (_emptySlotVisual != null) _emptySlotVisual.SetActive(true);
            if (_plantedCoralVisual != null) _plantedCoralVisual.SetActive(false);

            SetColor(_availableColor);

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }

        /// <summary>
        /// Current planting progress from 0 to 1. Used by HUD for progress ring.
        /// </summary>
        public float PlantingProgressNormalized =>
            _plantingDuration > 0f ? Mathf.Clamp01(_plantingProgress / _plantingDuration) : 0f;

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private void SetColor(Color color)
        {
            if (_slotMaterial != null)
            {
                _slotMaterial.color = color;
            }
        }

        private void ResetColor()
        {
            SetColor(_availableColor);
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsPlanted
                ? new Color(0.3f, 0.9f, 0.5f, 0.5f)
                : new Color(0.6f, 0.4f, 0.2f, 0.5f);

            Gizmos.DrawSphere(transform.position, 0.35f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.8f,
                $"{_slotName}\n{(IsPlanted ? "✓ PLANTED" : "○ EMPTY")}"
            );
        }
#endif
    }
}
