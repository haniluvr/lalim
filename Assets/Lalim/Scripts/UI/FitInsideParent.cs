using UnityEngine;

namespace Lalim.UI
{
    /// <summary>
    /// Shrinks a fixed-size panel so it always fits inside its parent (with a margin), whatever
    /// the canvas scaler or aspect ratio. Lets the same modal prefab sit in the HUD canvas and
    /// the main-menu canvas, which use different reference resolutions. Never scales above 1.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class FitInsideParent : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 1f)] private float _fill = 0.94f;

        private RectTransform _rect;

        private void OnEnable() => Fit();
        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            var parent = _rect.parent as RectTransform;
            if (parent == null) return;

            Vector2 size = _rect.rect.size;
            Vector2 room = parent.rect.size * _fill;
            if (size.x <= 0f || size.y <= 0f || room.x <= 0f || room.y <= 0f) return;

            float scale = Mathf.Min(1f, room.x / size.x, room.y / size.y);
            if (!Mathf.Approximately(_rect.localScale.x, scale))
                _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
