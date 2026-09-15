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

            // 1. Disable gravity & make kinematic so PhysX doesn't fight hover/carry
            Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // 2. Ensure collider exists and is solid for raycasting
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            if (cols.Length == 0)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.5f, 0.5f, 0.5f);
                cols = new Collider[] { box };
            }
            foreach (var col in cols)
            {
                col.isTrigger = false;
            }

            // 3. Auto-assign Interactable layer to root and all children
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = interactableLayer;
                }
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

            if (ToolEquipSystem.Instance != null && !ToolEquipSystem.Instance.IsToolEquipped("04_wastesegregationbag"))
            {
                Debug.LogWarning("[DebrisItem] Waste Segregation Bag must be equipped to pick up debris! Press [3] or [I].");
                return;
            }

            PickUp();
        }

        public string GetInteractionPrompt()
        {
            if (ToolEquipSystem.Instance != null && !ToolEquipSystem.Instance.IsToolEquipped("04_wastesegregationbag"))
            {
                return $"⚠ [3] Equip Waste Bag to collect {_debrisName}";
            }

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

            // Disable physics colliders while carried
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols) col.enabled = false;

            // Freeze Rigidbody so physics engine doesn't drop it out of camera
            Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Find camera transform (with fallback to CameraController or Player)
            Transform carryParent = Camera.main != null ? Camera.main.transform : null;
            if (carryParent == null)
            {
                CameraController cam = FindObjectOfType<CameraController>();
                if (cam != null) carryParent = cam.transform;
                else
                {
                    Camera anyCam = FindObjectOfType<Camera>();
                    if (anyCam != null) carryParent = anyCam.transform;
                    else
                    {
                        PlayerController p = FindObjectOfType<PlayerController>();
                        if (p != null) carryParent = p.transform;
                    }
                }
            }

            if (carryParent != null)
            {
                _carryParent = carryParent;
                transform.SetParent(_carryParent);
                transform.localPosition = new Vector3(0.2f, -0.25f, 0.8f); // In view
                transform.localRotation = Quaternion.identity;
            }

            // Notify challenge manager of player action
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportPlayerAction();
            }

            Debug.Log($"[DebrisItem] Picked up: {_debrisName} ({_category}) attached to {carryParent?.name}");
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

            // Re-enable colliders
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols) col.enabled = true;

            Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rbs)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
            }

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
