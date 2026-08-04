using UnityEngine;
using UnityEngine.EventSystems;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// On-screen virtual joystick for player movement.
    /// Draggable thumb within a circular boundary, outputs normalized Vector2 direction.
    /// Attach to a UI Image that acts as the joystick background.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Components")]
        [SerializeField] private RectTransform _background;  // The outer circle
        [SerializeField] private RectTransform _handle;       // The draggable thumb

        [Header("Settings")]
        [SerializeField] private float _deadZone = 0.1f;     // Normalized (0-1)
        [SerializeField] private float _handleRange = 1.0f;   // Max distance as fraction of background radius

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private Vector2 _inputDirection;
        private Canvas _parentCanvas;
        private Camera _uiCamera;

        public Vector2 Direction => _inputDirection;

        private void Start()
        {
            _parentCanvas = GetComponentInParent<Canvas>();

            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _uiCamera = _parentCanvas.worldCamera;
            }

            // Ensure handle starts centered
            if (_handle != null)
            {
                _handle.anchoredPosition = Vector2.zero;
            }
        }

        // ─────────────────────────────────────────────
        // Pointer Events
        // ─────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_background == null || _handle == null) return;

            // Convert screen position to local position within the background
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background,
                eventData.position,
                _uiCamera,
                out localPoint
            );

            // Normalize to -1..1 range based on background size
            Vector2 backgroundSize = _background.sizeDelta;
            Vector2 normalized = new Vector2(
                localPoint.x / (backgroundSize.x * 0.5f),
                localPoint.y / (backgroundSize.y * 0.5f)
            );

            // Clamp to unit circle
            _inputDirection = normalized.magnitude > 1f ? normalized.normalized : normalized;

            // Apply dead zone
            if (_inputDirection.magnitude < _deadZone)
            {
                _inputDirection = Vector2.zero;
            }

            // Position handle visually
            Vector2 handlePos = _inputDirection * (backgroundSize.x * 0.5f) * _handleRange;
            _handle.anchoredPosition = handlePos;

            // Send to InputManager
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetMoveInput(_inputDirection);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _inputDirection = Vector2.zero;

            if (_handle != null)
            {
                _handle.anchoredPosition = Vector2.zero;
            }

            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetMoveInput(Vector2.zero);
            }
        }
    }
}
