using UnityEngine;
using UnityEngine.EventSystems;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// Handles swipe-to-look input on the right side of the screen.
    /// Outputs delta movement for camera rotation via InputManager.
    /// Ignores touches that originate on UI elements (joystick, buttons).
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Graphic))]
    public class TouchLookHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Settings")]
        [SerializeField] private float _sensitivity = 1.0f;
        [SerializeField] private bool _invertY = false;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private int _activeTouchId = -1;
        private Vector2 _previousTouchPosition;
        private bool _isDragging;

        private void Start()
        {
            UnityEngine.UI.Graphic graphic = GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null && !graphic.raycastTarget)
            {
                graphic.raycastTarget = true;
            }
        }

        // ─────────────────────────────────────────────
        // Pointer Events
        // ─────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isDragging) return; // Only track one touch at a time

            _activeTouchId = eventData.pointerId;
            _previousTouchPosition = eventData.position;
            _isDragging = true;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetTouchActive(true);
                InputManager.Instance.SetTapPosition(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || eventData.pointerId != _activeTouchId) return;

            Vector2 currentPosition = eventData.position;
            Vector2 delta = currentPosition - _previousTouchPosition;

            // Apply sensitivity
            delta *= _sensitivity;

            // Optionally invert Y axis
            if (_invertY)
            {
                delta.y = -delta.y;
            }

            // Send to InputManager
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetLookDelta(delta);
            }

            _previousTouchPosition = currentPosition;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activeTouchId) return;

            _isDragging = false;
            _activeTouchId = -1;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetLookDelta(Vector2.zero);
                InputManager.Instance.SetTouchActive(false);
            }
        }
    }
}
