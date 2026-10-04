using System.Collections.Generic;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>Bowl category for the Aman Sinaya altar (Plastic / Glass / Metal).</summary>
    public enum OfferingCategory { Plastic, Glass, Metal }

    public enum OfferingType
    {
        // Plastic
        FishingLine, PlasticFragment, PlasticBottle, GroceryBag, CupOrCutlery, FoodWrapper,
        // Glass
        GlassBottle, CeramicFragment,
        // Metal
        BeverageCan, FishingSinker
    }

    public static class OfferingClassification
    {
        public static OfferingCategory CategoryOf(OfferingType type)
        {
            switch (type)
            {
                case OfferingType.GlassBottle:
                case OfferingType.CeramicFragment:
                    return OfferingCategory.Glass;
                case OfferingType.BeverageCan:
                case OfferingType.FishingSinker:
                    return OfferingCategory.Metal;
                default:
                    return OfferingCategory.Plastic;
            }
        }

        public static string DisplayName(OfferingType type)
        {
            switch (type)
            {
                case OfferingType.FishingLine: return "Fishing Line";
                case OfferingType.PlasticFragment: return "Plastic Fragment";
                case OfferingType.PlasticBottle: return "Beverage Bottle";
                case OfferingType.GroceryBag: return "Grocery Bag";
                case OfferingType.CupOrCutlery: return "Cup / Cutlery";
                case OfferingType.FoodWrapper: return "Food Wrapper";
                case OfferingType.GlassBottle: return "Glass Bottle";
                case OfferingType.CeramicFragment: return "Ceramic Fragment";
                case OfferingType.BeverageCan: return "Beverage Can";
                case OfferingType.FishingSinker: return "Fishing Sinker";
                default: return type.ToString();
            }
        }
    }

    /// <summary>
    /// One of the 25 debris pieces the player reclaims for Aman Sinaya.
    /// Promoted from a decorative reef-debris mesh: add this component, a solid Collider,
    /// and pick a Type. Category follows automatically.
    ///
    /// World phase: picked up via the existing IInteractable / PlayerController raycast system
    /// (same convention as DebrisItem), consistent with the rest of the codebase.
    /// Altar phase: EnterAltarMode() re-parents it onto the offering tray as a small,
    /// solid-collider token for SegregationAltar's mouse raycast sorting.
    ///
    /// Glow (for SpiritVision): a runtime point light is the primary effect since we can't
    /// assume any particular shader/emission setup on these decorative meshes.
    /// </summary>
    [DisallowMultipleComponent]
    public class OfferingItem : MonoBehaviour, IInteractable
    {
        static readonly HashSet<OfferingItem> s_Active = new HashSet<OfferingItem>();
        public static IReadOnlyCollection<OfferingItem> ActiveItems => s_Active;

        /// <summary>Raised when the player picks this item up in the world (the trash collection step listens).</summary>
        public static event System.Action<OfferingItem> Collected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_Active.Clear(); Collected = null; }

        [Tooltip("Stable ID for saving which trash was already collected. Defaults to the object name.")]
        [SerializeField] private string _saveId;
        public string SaveId => string.IsNullOrEmpty(_saveId) ? gameObject.name : _saveId;

        [Header("Classification")]
        [SerializeField] private OfferingType _type = OfferingType.PlasticBottle;

        [Header("Glow (Spirit Vision)")]
        [SerializeField] private Color _glowColor = new Color(0.15f, 1f, 0.85f);
        [SerializeField, Min(0f)] private float _glowIntensity = 4f;
        [SerializeField, Min(0.5f)] private float _glowRange = 6f;
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 5f;

        private Collider _col;
        private Light _glowLight;
        private float _glowBlend;
        private float _glowTarget;
        private bool _collected;
        private bool _isAltarToken;

        public OfferingType Type => _type;
        public OfferingCategory Category => OfferingClassification.CategoryOf(_type);
        public string DisplayName => OfferingClassification.DisplayName(_type);
        public bool IsCollected => _collected;

        /// <summary>False until the trash collection minigame starts (or for trash that's just set dressing).</summary>
        public bool PickupEnabled { get; private set; } = true;

        public void SetPickupEnabled(bool enabled)
        {
            PickupEnabled = enabled;
            if (_col != null && !_isAltarToken) _col.enabled = enabled;
        }
        public Collider InteractCollider => _col;

        private void Awake()
        {
            _col = GetComponent<Collider>();
            if (_col == null) _col = gameObject.AddComponent<SphereCollider>();
            _col.isTrigger = false;

            GameObject lightGO = new GameObject("SpiritGlow");
            lightGO.transform.SetParent(transform, false);
            _glowLight = lightGO.AddComponent<Light>();
            _glowLight.type = LightType.Point;
            _glowLight.color = _glowColor;
            _glowLight.range = _glowRange;
            _glowLight.intensity = 0f;
            _glowLight.shadows = LightShadows.None;
            _glowLight.enabled = false;
        }

        private void OnEnable() { if (!_collected) s_Active.Add(this); }
        private void OnDisable() { s_Active.Remove(this); }

        private void Update()
        {
            if (Mathf.Approximately(_glowBlend, _glowTarget)) return;
            _glowBlend = Mathf.MoveTowards(_glowBlend, _glowTarget, Time.deltaTime * _fadeSpeed);
            float pulse = 1f + Mathf.Sin(Time.time * 2.2f) * 0.2f;
            _glowLight.enabled = _glowBlend > 0.01f;
            _glowLight.intensity = _glowIntensity * _glowBlend * pulse;
        }

        /// <summary>Called by SpiritVision to turn the bioluminescent aura on/off.</summary>
        public void ToggleGlow(bool active) { _glowTarget = active ? 1f : 0f; }

        // ---------------------------------------------------------- IInteractable
        public void Interact()
        {
            if (_collected || _isAltarToken || !PickupEnabled) return;
            Collect();
        }

        public string GetInteractionPrompt()
        {
            if (_collected || _isAltarToken || !PickupEnabled) return "";
            return "Pickup";
        }

        public void Collect()
        {
            if (_collected) return;
            _collected = true;
            s_Active.Remove(this);
            ToggleGlow(false);

            Collected?.Invoke(this);
            gameObject.SetActive(false);
        }

        /// <summary>Marks the item collected without raising Collected (restoring a save).</summary>
        public void MarkCollectedSilently()
        {
            _collected = true;
            s_Active.Remove(this);
            ToggleGlow(false);
            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------- Altar mode
        /// <summary>Re-enables this (already collected) item as a clickable offering on the tray.</summary>
        public void EnterAltarMode(Transform trayParent, Vector3 localPosition, float displaySize)
        {
            _isAltarToken = true;
            gameObject.SetActive(true);
            transform.SetParent(trayParent, false);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Bounds b = GetRenderBounds();
            float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (largest > 0.0001f) transform.localScale *= displaySize / largest;

            _col.isTrigger = false;
            _col.enabled = true;
            ToggleGlow(false);
        }

        /// <summary>
        /// Readies this (already collected) piece for the sorting shrines: unparented and with no
        /// collider (the shrines move it on screen and pick it by its screen position).
        /// </summary>
        public void EnterSortMode()
        {
            _isAltarToken = true;
            gameObject.SetActive(true);
            transform.SetParent(null, true);
            if (_col != null) _col.enabled = false;
            ToggleGlow(false);
        }

        public void SetHeld(bool held) { if (_col != null) _col.enabled = !held; }

        public Bounds GetRenderBounds()
        {
            Renderer[] rs = GetComponentsInChildren<Renderer>(true);
            bool first = true;
            Bounds result = new Bounds(transform.position, Vector3.zero);
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { result = r.bounds; first = false; }
                else result.Encapsulate(r.bounds);
            }
            return result;
        }
    }
}
