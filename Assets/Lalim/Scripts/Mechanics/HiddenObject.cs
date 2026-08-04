using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// MonoBehaviour for objects that are only visible via SpiritSense reveal.
    /// Starts hidden (renderers/colliders disabled) and reveals with a fade-in
    /// effect when the SpiritSense pulse reaches it.
    /// </summary>
    public class HiddenObject : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _fadeInDuration = 1.0f;
        [SerializeField] private bool _stayRevealedPermanently = true;
        [SerializeField] private float _temporaryRevealDuration = 10f; // If not permanent

        [Header("Audio")]
        [SerializeField] private AudioClip _revealSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public bool IsRevealed { get; private set; }

        private Renderer[] _renderers;
        private Collider[] _interactionColliders;
        private MaterialPropertyBlock _propBlock;
        private float _fadeProgress;
        private bool _isFading;
        private float _revealTimer;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _interactionColliders = GetComponentsInChildren<Collider>();
            _propBlock = new MaterialPropertyBlock();

            // Start hidden
            Hide();
        }

        private void Update()
        {
            if (_isFading)
            {
                UpdateFadeIn();
            }
            else if (IsRevealed && !_stayRevealedPermanently)
            {
                // Count down temporary reveal
                _revealTimer -= Time.deltaTime;
                if (_revealTimer <= 0)
                {
                    Hide();
                }
            }
        }

        // ─────────────────────────────────────────────
        // Hide / Reveal
        // ─────────────────────────────────────────────

        /// <summary>
        /// Hide this object (disable renderers and interaction colliders).
        /// </summary>
        public void Hide()
        {
            IsRevealed = false;
            _isFading = false;

            foreach (var renderer in _renderers)
            {
                renderer.enabled = false;
            }

            // Disable interaction colliders but keep trigger collider active
            // so SpiritSense OverlapSphere can still find it
            foreach (var col in _interactionColliders)
            {
                if (!col.isTrigger)
                {
                    col.enabled = false;
                }
            }
        }

        /// <summary>
        /// Reveal this object with a fade-in effect.
        /// </summary>
        public void Reveal()
        {
            if (IsRevealed) return;

            Debug.Log($"[HiddenObject] Revealed: {gameObject.name}");

            IsRevealed = true;
            _fadeProgress = 0f;
            _isFading = true;
            _revealTimer = _temporaryRevealDuration;

            // Enable renderers (start transparent)
            foreach (var renderer in _renderers)
            {
                renderer.enabled = true;
                SetRendererAlpha(renderer, 0f);
            }

            // Enable interaction colliders
            foreach (var col in _interactionColliders)
            {
                col.enabled = true;
            }

            // Play reveal sound
            if (_revealSound != null)
            {
                AudioSource.PlayClipAtPoint(_revealSound, transform.position);
            }
        }

        // ─────────────────────────────────────────────
        // Fade Effect
        // ─────────────────────────────────────────────

        private void UpdateFadeIn()
        {
            _fadeProgress += Time.deltaTime / _fadeInDuration;

            float alpha = Mathf.SmoothStep(0f, 1f, _fadeProgress);

            foreach (var renderer in _renderers)
            {
                SetRendererAlpha(renderer, alpha);
            }

            if (_fadeProgress >= 1f)
            {
                _isFading = false;

                // Reset to full opacity
                foreach (var renderer in _renderers)
                {
                    SetRendererAlpha(renderer, 1f);
                }
            }
        }

        private void SetRendererAlpha(Renderer renderer, float alpha)
        {
            renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat("_Alpha", alpha);

            // Also try standard shader properties
            Color color = _propBlock.GetColor("_BaseColor");
            if (color == default)
            {
                color = Color.white;
            }
            color.a = alpha;
            _propBlock.SetColor("_BaseColor", color);

            renderer.SetPropertyBlock(_propBlock);
        }

        // ─────────────────────────────────────────────
        // Gizmos
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!IsRevealed)
            {
                Gizmos.color = new Color(0.5f, 0f, 1f, 0.4f);
                Gizmos.DrawCube(transform.position, Vector3.one * 0.5f);
                UnityEditor.Handles.Label(transform.position + Vector3.up, "Hidden");
            }
        }
#endif
    }
}
