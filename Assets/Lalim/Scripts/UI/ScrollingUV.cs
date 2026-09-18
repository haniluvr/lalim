using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Scrolls the UV coordinates of a RawImage to create a flowing texture effect (e.g., flowing water).
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ScrollingUV : MonoBehaviour
    {
        [SerializeField] private Vector2 _scrollSpeed = new Vector2(0.1f, 0f);
        
        private RawImage _rawImage;
        private Rect _uvRect;

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
            _uvRect = _rawImage.uvRect;
        }

        private void Update()
        {
            if (_rawImage != null)
            {
                _uvRect.position += _scrollSpeed * Time.unscaledDeltaTime;
                _rawImage.uvRect = _uvRect;
            }
        }
    }
}
