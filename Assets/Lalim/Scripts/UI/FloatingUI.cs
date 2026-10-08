using UnityEngine;

namespace Lalim.UI
{
    /// <summary>
    /// Makes a UI element bob up and down smoothly like it's floating on water.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FloatingUI : MonoBehaviour
    {
        [Header("Floating Settings")]
        [Tooltip("How far up and down it floats (in pixels).")]
        [SerializeField] private float _amplitude = 15f;
        
        [Tooltip("How fast it floats.")]
        [SerializeField] private float _speed = 2f;

        private RectTransform _rectTransform;
        private Vector2 _startAnchoredPosition;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _startAnchoredPosition = _rectTransform.anchoredPosition;
        }

        private void Update()
        {
            // Use Mathf.Sin to create a smooth, repeating wave based on time
            float newY = _startAnchoredPosition.y + Mathf.Sin(Lalim.Core.PauseClock.Time * _speed) * _amplitude;
            
            // Apply the new position
            _rectTransform.anchoredPosition = new Vector2(_startAnchoredPosition.x, newY);
        }
    }
}
