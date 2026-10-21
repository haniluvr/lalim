using UnityEngine;

namespace Lalim.UI
{
    /// <summary>Subtle floating bob for a UI element (menu buttons).</summary>
    public class UIFloat : MonoBehaviour
    {
        [SerializeField] float _amplitude = 4f;
        [SerializeField] float _speed = 0.8f;
        [SerializeField] float _phase;

        RectTransform _rt;
        Vector2 _base;

        void OnEnable()
        {
            _rt = transform as RectTransform;
            if (_rt != null) _base = _rt.anchoredPosition;
        }

        void OnDisable()
        {
            if (_rt != null) _rt.anchoredPosition = _base;
        }

        void Update()
        {
            if (_rt == null) return;
            float t = Time.unscaledTime * _speed + _phase;
            _rt.anchoredPosition = _base + new Vector2(Mathf.Sin(t * 0.7f) * _amplitude * 0.5f, Mathf.Sin(t) * _amplitude);
        }
    }
}
