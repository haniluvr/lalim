using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// A collectible coral fragment that the player can pick up.
    /// Implements IInteractable for the PlayerController interaction system.
    ///
    /// Setup:
    ///   1. Place an empty GameObject where you want the fragment to appear
    ///   2. Add a Collider and a visual mesh (child object recommended)
    ///   3. Attach this script
    ///
    /// The player picks up fragments, then plants them at CoralSlot locations.
    /// Fragments are consumed on planting. The parent CoralReplantingChallenge
    /// tracks the inventory of collected-but-not-yet-planted fragments.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CoralFragment : MonoBehaviour, IInteractable
    {
        [Header("Fragment Configuration")]
        [SerializeField] private string _fragmentName = "Coral Fragment";

        [Header("Visual — Idle Animation")]
        [SerializeField] private float _bobSpeed = 1.2f;
        [SerializeField] private float _bobHeight = 0.06f;
        [SerializeField] private float _rotateSpeed = 25f;

        [Header("Audio")]
        [SerializeField] private AudioClip _collectSound;

        [Header("VFX (Optional)")]
        [SerializeField] private ParticleSystem _collectVFX;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public bool IsCollected { get; private set; }

        private Vector3 _startPosition;
        private CoralReplantingChallenge _challenge;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            _startPosition = transform.position;

            // Ensure collider is solid for raycasting
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            // Find parent challenge
            _challenge = GetComponentInParent<CoralReplantingChallenge>();
        }

        private void Update()
        {
            if (IsCollected) return;

            // Idle animation: gentle bob and rotate
            float yOffset = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.position = _startPosition + Vector3.up * yOffset;
            transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime);
        }

        // ─────────────────────────────────────────────
        // IInteractable
        // ─────────────────────────────────────────────

        public void Interact()
        {
            if (IsCollected) return;

            Collect();
        }

        public string GetInteractionPrompt()
        {
            if (IsCollected) return "";
            return $"Pick up {_fragmentName}";
        }

        // ─────────────────────────────────────────────
        // Collection
        // ─────────────────────────────────────────────

        private void Collect()
        {
            IsCollected = true;

            // Audio
            if (_collectSound != null)
            {
                GameAudio.PlayAt(_collectSound, transform.position);
            }

            // VFX
            if (_collectVFX != null)
            {
                // Detach VFX so it plays after the fragment is deactivated
                _collectVFX.transform.SetParent(null);
                _collectVFX.Play();
                Destroy(_collectVFX.gameObject, _collectVFX.main.duration + 1f);
            }

            // Report player action to reset hesitation timer
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportPlayerAction();
            }

            // Notify parent challenge
            if (_challenge != null)
            {
                _challenge.OnFragmentCollected(this);
            }

            Debug.Log($"[CoralFragment] Collected: {_fragmentName}");

            // Hide the fragment
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Reset this fragment to uncollected state (for challenge retry).
        /// </summary>
        public void ResetFragment()
        {
            IsCollected = false;
            transform.position = _startPosition;
            gameObject.SetActive(true);

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsCollected
                ? new Color(0.5f, 0.5f, 0.5f, 0.3f)
                : new Color(1f, 0.6f, 0.8f, 0.6f);

            Gizmos.DrawSphere(transform.position, 0.25f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"{_fragmentName}\n{(IsCollected ? "✓ COLLECTED" : "○ AVAILABLE")}"
            );
        }
#endif
    }
}
