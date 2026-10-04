using UnityEngine;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Makes an area marker (the soft glow ring on the ground + its light) breathe slowly, so it
    /// reads as a gentle hint rather than a bright disc.
    /// </summary>
    public class AreaMarkerPulse : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float _period = 3.5f;
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.12f;
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.5f;
        [SerializeField, Min(0f)] private float _minLight = 0.2f;
        [SerializeField, Min(0f)] private float _maxLight = 1f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private Color[] _colors;
        private Light _light;
        private MaterialPropertyBlock _block;
        private float _phase;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                Material m = _renderers[i].sharedMaterial;
                _colors[i] = m != null && m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.white;
            }
            _light = GetComponentInChildren<Light>(true);
            _block = new MaterialPropertyBlock();
            // Neighbouring areas shouldn't blink in lockstep.
            _phase = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.61f, 1f) * _period;
        }

        private void Update()
        {
            float k = 0.5f - 0.5f * Mathf.Cos((Time.time + _phase) / _period * Mathf.PI * 2f);
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color c = _colors[i];
                c.a = Mathf.Lerp(_minAlpha, _maxAlpha, k);
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColor, c);
                _renderers[i].SetPropertyBlock(_block);
            }
            if (_light != null) _light.intensity = Mathf.Lerp(_minLight, _maxLight, k);
        }
    }
}
