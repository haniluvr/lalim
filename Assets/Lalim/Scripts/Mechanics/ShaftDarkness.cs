using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Light fades as the diver goes down a deep shaft: from <see cref="_topY"/> to
    /// <see cref="_blackY"/> the fog closes in and turns black, the ambient light and the sun go
    /// out, and the shaft's own lights dim, until it's pitch black. Only inside the shaft (a
    /// cylinder of <see cref="_radius"/> around this object); swimming back up brings the light back.
    ///
    /// Runs in LateUpdate on top of whatever else sets fog and lights this frame (e.g. the
    /// PollutionDirector), and puts everything back once the diver is out.
    /// </summary>
    public class ShaftDarkness : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _radius = 18f;
        [Tooltip("Height where the darkening starts.")]
        [SerializeField] private float _topY = -40f;
        [Tooltip("Height where it is pitch black.")]
        [SerializeField] private float _blackY = -105f;
        [Tooltip("How far you can still see at the bottom (fog end, metres).")]
        [SerializeField, Min(0.5f)] private float _blackFogEnd = 5f;
        [SerializeField, Min(0.1f)] private float _blendPerSecond = 1.5f;
        [Tooltip("Lights that go out with depth. Empty = the sun plus every light inside the shaft.")]
        [SerializeField] private List<Light> _lights = new List<Light>();

        private float _k;
        private bool _applied;

        private Color _baseFog, _lastFog;
        private float _baseFogStart, _baseFogEnd, _lastFogStart, _lastFogEnd;
        private SphericalHarmonicsL2 _baseProbe, _lastProbe;
        private readonly Dictionary<Light, float> _baseLight = new Dictionary<Light, float>();
        private readonly Dictionary<Light, float> _lastLight = new Dictionary<Light, float>();

        /// <summary>0 = normal light, 1 = pitch black.</summary>
        public float Darkness => _k;

        private void Start()
        {
            if (_lights.Count == 0) GatherLights();
        }

        private void GatherLights()
        {
            Vector3 c = transform.position;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { _lights.Add(l); continue; }
                Vector3 p = l.transform.position;
                if (new Vector2(p.x - c.x, p.z - c.z).magnitude <= _radius + 4f && p.y <= _topY + 10f && p.y >= _blackY - 30f) _lights.Add(l);
            }
        }

        private void LateUpdate()
        {
            _k = Mathf.MoveTowards(_k, TargetDarkness(), Time.deltaTime * _blendPerSecond);
            if (_k <= 0f)
            {
                if (_applied) Restore();
                return;
            }
            Apply(_k);
        }

        private float TargetDarkness()
        {
            Transform who = PlayerController.Instance != null ? PlayerController.Instance.transform : (Camera.main != null ? Camera.main.transform : null);
            if (who == null) return 0f;
            Vector3 p = who.position, c = transform.position;
            if (new Vector2(p.x - c.x, p.z - c.z).magnitude > _radius) return 0f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_topY, _blackY, p.y));
        }

        private void Apply(float k)
        {
            // Anything changed by someone else since our last write becomes the new "lit" value.
            if (!_applied || RenderSettings.fogColor != _lastFog) _baseFog = RenderSettings.fogColor;
            if (!_applied || !Mathf.Approximately(RenderSettings.fogStartDistance, _lastFogStart)) _baseFogStart = RenderSettings.fogStartDistance;
            if (!_applied || !Mathf.Approximately(RenderSettings.fogEndDistance, _lastFogEnd)) _baseFogEnd = RenderSettings.fogEndDistance;
            if (!_applied || RenderSettings.ambientProbe != _lastProbe) _baseProbe = RenderSettings.ambientProbe;

            _lastFog = Color.Lerp(_baseFog, Color.black, k);
            _lastFogEnd = Mathf.Lerp(_baseFogEnd, Mathf.Min(_baseFogEnd, _blackFogEnd), k);
            _lastFogStart = Mathf.Min(_baseFogStart, _lastFogEnd * 0.5f) * (1f - k);
            _lastProbe = _baseProbe * (1f - k);
            RenderSettings.fogColor = _lastFog;
            RenderSettings.fogEndDistance = _lastFogEnd;
            RenderSettings.fogStartDistance = _lastFogStart;
            RenderSettings.ambientProbe = _lastProbe;

            foreach (Light l in _lights)
            {
                if (l == null) continue;
                if (!_applied || !_lastLight.TryGetValue(l, out float last) || !Mathf.Approximately(l.intensity, last)) _baseLight[l] = l.intensity;
                float v = _baseLight[l] * (1f - k);
                l.intensity = v;
                _lastLight[l] = v;
            }
            _applied = true;
        }

        private void Restore()
        {
            if (RenderSettings.fogColor == _lastFog) RenderSettings.fogColor = _baseFog;
            if (Mathf.Approximately(RenderSettings.fogEndDistance, _lastFogEnd)) RenderSettings.fogEndDistance = _baseFogEnd;
            if (Mathf.Approximately(RenderSettings.fogStartDistance, _lastFogStart)) RenderSettings.fogStartDistance = _baseFogStart;
            if (RenderSettings.ambientProbe == _lastProbe) RenderSettings.ambientProbe = _baseProbe;
            foreach (Light l in _lights)
            {
                if (l != null && _lastLight.TryGetValue(l, out float last) && Mathf.Approximately(l.intensity, last)) l.intensity = _baseLight[l];
            }
            _lastLight.Clear();
            _applied = false;
        }

        private void OnDisable()
        {
            if (_applied) Restore();
            _k = 0f;
        }

#if UNITY_EDITOR
        public void EditorSetup(float radius, float topY, float blackY)
        {
            _radius = radius;
            _topY = topY;
            _blackY = blackY;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0f, 0f, 0.6f);
            Vector3 c = transform.position;
            Gizmos.DrawWireCube(new Vector3(c.x, (_topY + _blackY) * 0.5f, c.z), new Vector3(_radius * 2f, _topY - _blackY, _radius * 2f));
        }
#endif
    }
}
