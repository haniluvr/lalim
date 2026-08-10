using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Category of debris for the waste segregation challenge.
    /// Determines which sorting bin accepts this item.
    /// </summary>
    public enum DebrisCategory
    {
        Bottle,    // Plastic bottles, bags, straws
        Metal,      // Metal cans, scrap
        Food     // Mixed waste, organic debris
    }

    /// <summary>
    /// A debris item that can be picked up and carried to a sorting bin.
    /// Implements IInteractable for the PlayerController interaction system.
    /// 
    /// States: Idle → Carried → Sorted (destroyed)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DebrisItem : MonoBehaviour, IInteractable
    {
        [Header("Debris Configuration")]
        [SerializeField] private DebrisCategory _category;
        [SerializeField] private string _debrisName = "Debris";

        [Header("Visual")]
        [SerializeField] private float _bobSpeed = 1.5f;
        [SerializeField] private float _bobHeight = 0.08f;
        [SerializeField] private float _rotateSpeed = 30f;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public DebrisCategory Category => _category;
        public bool IsCarried { get; private set; }
        public bool IsSorted { get; private set; }

        private Vector3 _startPosition;
        private Transform _carryParent;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            _startPosition = transform.position;

            // Ensure collider is set properly
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false; // Solid for raycasting
            }
        }

        private void Update()
        {
            if (IsCarried || IsSorted) return;

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
            if (IsCarried || IsSorted) return;

            PickUp();
        }

        public string GetInteractionPrompt()
        {
            return $"Pick up {_debrisName}";
        }

        // ─────────────────────────────────────────────
        // Pick Up / Drop
        // ─────────────────────────────────────────────

        /// <summary>
        /// Pick up this debris. Attaches to the camera view for carrying.
        /// </summary>
        public void PickUp()
        {
            IsCarried = true;

            // Disable physics collider while carried
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Attach to camera (or player) for carrying
            Transform cameraTransform = Camera.main?.transform;
            if (cameraTransform != null)
            {
                _carryParent = cameraTransform;
                transform.SetParent(_carryParent);
                transform.localPosition = new Vector3(0f, -0.3f, 1.0f); // In front of camera
                transform.localScale = Vector3.one * 0.5f; // Scale down when carried
                transform.localRotation = Quaternion.identity;
            }

            // Notify challenge manager of player action
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportPlayerAction();
            }

            Debug.Log($"[DebrisItem] Picked up: {_debrisName} ({_category})");
        }

        /// <summary>
        /// Drop this debris (returns to original position).
        /// Called when sorting fails or player cancels.
        /// </summary>
        public void Drop()
        {
            if (!IsCarried) return;

            IsCarried = false;

            // Detach from camera
            transform.SetParent(null);
            transform.position = _startPosition;
            transform.localScale = Vector3.one;

            // Re-enable collider
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;

            Debug.Log($"[DebrisItem] Dropped: {_debrisName}");
        }

        /// <summary>
        /// Mark this debris as successfully sorted. Plays effect and destroys.
        /// </summary>
        public void Sort()
        {
            if (IsSorted) return;

            IsSorted = true;
            IsCarried = false;

            Debug.Log($"[DebrisItem] Sorted correctly: {_debrisName} ({_category})");

            // Detach and destroy
            transform.SetParent(null);
            Destroy(gameObject, 0.1f);
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Color by category
            switch (_category)
            {
                case DebrisCategory.Bottle: Gizmos.color = Color.blue; break;
                case DebrisCategory.Metal:   Gizmos.color = Color.gray; break;
                case DebrisCategory.Food: Gizmos.color = Color.green; break;
            }
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"{_debrisName}\n({_category})"
            );
        }
#endif
    }
}
