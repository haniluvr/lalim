using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Inventory
{
    /// <summary>
    /// MonoBehaviour placed on world objects the player can pick up.
    /// Implements IInteractable for the PlayerController interaction system.
    /// On interaction: adds item to inventory, plays collect effect, disables self.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InteractableItem : MonoBehaviour, IInteractable
    {
        [Header("Item Reference")]
        [SerializeField] private ItemData _itemData;

        [Header("Interaction")]
        [SerializeField] private string _interactionPrompt = "Collect";

        [SerializeField] private float _bobSpeed = 2.0f;
        [SerializeField] private float _bobHeight = 0.15f;
        [SerializeField] private float _rotateSpeed = 45.0f;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _highlightEffect;  // Optional glow/outline
        [SerializeField] private ParticleSystem _collectParticles;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private Vector3 _startPosition;
        private bool _isCollected;

        private void Start()
        {
            _startPosition = transform.position;

            if (_highlightEffect != null)
            {
                _highlightEffect.SetActive(false);
            }

            // Validate item data
            if (_itemData == null)
            {
                Debug.LogWarning($"[InteractableItem] No ItemData assigned on {gameObject.name}");
            }
        }

        private void Update()
        {
            if (_isCollected) return;

            // Idle animation: bob up and down + rotate
            float yOffset = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.position = _startPosition + Vector3.up * yOffset;
            transform.Rotate(Vector3.up, _rotateSpeed * Time.deltaTime);
        }

        // ─────────────────────────────────────────────
        // IInteractable
        // ─────────────────────────────────────────────

        public void Interact()
        {
            if (_isCollected || _itemData == null) return;

            // Try to add to inventory
            if (InventoryManager.Instance != null)
            {
                bool added = InventoryManager.Instance.AddItem(_itemData.itemId);
                if (!added) return; // Already collected (loaded from save)
            }

            Collect();
        }

        public string GetInteractionPrompt()
        {
            if (_itemData != null)
            {
                return $"{_interactionPrompt} {_itemData.displayName}";
            }
            return _interactionPrompt;
        }

        // ─────────────────────────────────────────────
        // Collection
        // ─────────────────────────────────────────────

        private void Collect()
        {
            _isCollected = true;

            // Play collect sound
            if (_itemData.collectSound != null)
            {
                GameAudio.PlayAt(_itemData.collectSound, transform.position);
            }

            // Play particle effect
            if (_collectParticles != null)
            {
                _collectParticles.transform.SetParent(null); // Detach so it doesn't get disabled
                _collectParticles.Play();
                Destroy(_collectParticles.gameObject, _collectParticles.main.duration + 1f);
            }

            // Disable the pickup
            DisablePickup();
        }

        private void DisablePickup()
        {
            // Disable renderers
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }

            // Disable collider
            foreach (var col in GetComponents<Collider>())
            {
                col.enabled = false;
            }

            // Disable highlight
            if (_highlightEffect != null)
            {
                _highlightEffect.SetActive(false);
            }

            // Destroy after a short delay (allows effects to finish)
            Destroy(gameObject, 2.0f);
        }

        // ─────────────────────────────────────────────
        // Highlight (for proximity feedback)
        // ─────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            if (other.CompareTag(GameConstants.Tags.PLAYER) && _highlightEffect != null)
            {
                _highlightEffect.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(GameConstants.Tags.PLAYER) && _highlightEffect != null)
            {
                _highlightEffect.SetActive(false);
            }
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            if (_itemData != null)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 1.5f,
                    $"{_itemData.displayName} ({_itemData.itemType})"
                );
            }
        }
#endif
    }
}
