using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Sorting bin that accepts a specific debris category.
    /// When a player carrying the correct debris type enters the trigger,
    /// the debris is sorted and the challenge progresses.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SortingBin : MonoBehaviour
    {
        [Header("Bin Configuration")]
        [SerializeField] private DebrisCategory _acceptedCategory;
        [SerializeField] private string _binLabel = "Bin";

        [Header("Visual Feedback")]
        [SerializeField] private Renderer _binRenderer;
        [SerializeField] private Color _defaultColor = Color.white;
        [SerializeField] private Color _highlightColor = Color.yellow;
        [SerializeField] private Color _correctColor = Color.green;
        [SerializeField] private Color _wrongColor = Color.red;

        [Header("Audio")]
        [SerializeField] private AudioClip _correctSound;
        [SerializeField] private AudioClip _wrongSound;
        private AudioSource _audioSource;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public DebrisCategory AcceptedCategory => _acceptedCategory;
        public int SortedCount { get; private set; }

        private WasteSegregationChallenge _challenge;
        private Material _binMaterial;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Ensure trigger collider
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            // Cache audio source
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1f; // 3D sound
            }

            // Cache material for color changes
            if (_binRenderer != null)
            {
                _binMaterial = _binRenderer.material;
                _binMaterial.color = _defaultColor;
            }

            // Find the parent challenge controller
            _challenge = GetComponentInParent<WasteSegregationChallenge>();
        }

        // ─────────────────────────────────────────────
        // Trigger Detection
        // ─────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            // Only respond to the player
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;

            // Check if player is carrying debris
            DebrisItem carriedDebris = FindCarriedDebris();
            if (carriedDebris == null)
            {
                // Player entered bin without carrying anything — highlight bin
                SetColor(_highlightColor);
                return;
            }

            // Check if correct category
            if (carriedDebris.Category == _acceptedCategory)
            {
                OnCorrectSort(carriedDebris);
            }
            else
            {
                OnWrongSort(carriedDebris);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;

            // Reset bin color
            SetColor(_defaultColor);
        }

        // ─────────────────────────────────────────────
        // Sort Logic
        // ─────────────────────────────────────────────

        private void OnCorrectSort(DebrisItem debris)
        {
            SortedCount++;

            // Visual feedback
            SetColor(_correctColor);
            Invoke(nameof(ResetColor), 0.5f);

            // Audio feedback
            if (_correctSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_correctSound);
            }

            // Sort the debris (destroys it)
            debris.Sort();

            // Report to challenge manager
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportTaskCompleted();
            }

            // Notify parent challenge
            if (_challenge != null)
            {
                _challenge.OnDebrisSorted(debris.Category, true);
            }

            Debug.Log($"[SortingBin] Correct! {debris.Category} → {_binLabel} (total: {SortedCount})");
        }

        private void OnWrongSort(DebrisItem debris)
        {
            // Visual feedback
            SetColor(_wrongColor);
            Invoke(nameof(ResetColor), 0.5f);

            // Audio feedback
            if (_wrongSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_wrongSound);
            }

            // Drop the debris back
            debris.Drop();

            // Report error to challenge manager
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportError();
            }

            // Notify parent challenge
            if (_challenge != null)
            {
                _challenge.OnDebrisSorted(debris.Category, false);
            }

            Debug.Log($"[SortingBin] Wrong! {debris.Category} doesn't go in {_binLabel} ({_acceptedCategory})");
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        /// <summary>
        /// Find the DebrisItem currently carried by the player (parented to camera).
        /// </summary>
        private DebrisItem FindCarriedDebris()
        {
            // Look through all active debris items for the carried one
            DebrisItem[] allDebris = FindObjectsOfType<DebrisItem>();
            foreach (var debris in allDebris)
            {
                if (debris.IsCarried) return debris;
            }
            return null;
        }

        private void SetColor(Color color)
        {
            if (_binMaterial != null)
            {
                _binMaterial.color = color;
            }
        }

        private void ResetColor()
        {
            SetColor(_defaultColor);
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            switch (_acceptedCategory)
            {
                case DebrisCategory.Bottle: Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.4f); break;
                case DebrisCategory.Metal:   Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.4f); break;
                case DebrisCategory.Food: Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.4f); break;
            }

            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.5f,
                $"{_binLabel}\n({_acceptedCategory})\nSorted: {SortedCount}"
            );
        }
#endif
    }
}
