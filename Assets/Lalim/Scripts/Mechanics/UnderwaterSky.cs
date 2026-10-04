using UnityEngine;
using UnityEngine.Rendering;

namespace Lalim.Mechanics
{
    /// <summary>
    /// An underwater "sky" in every direction, even past the edge of the map: a dome that follows
    /// the main camera, drawn behind everything with the Lalim/Underwater Sky shader. Its horizon
    /// is the live fog colour (clean blue, polluted murk, the well's black), so distant geometry
    /// fades straight into it instead of into a flat background colour. The camera's background
    /// colour follows the fog too, as a fallback.
    /// </summary>
    public class UnderwaterSky : MonoBehaviour
    {
        [Tooltip("Material using the Lalim/Underwater Sky shader.")]
        [SerializeField] private Material _material;
        [Tooltip("Dome radius as a share of the camera's far clip distance.")]
        [SerializeField, Range(0.1f, 0.95f)] private float _radiusShare = 0.9f;

        private Transform _dome;
        private Camera _camera;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || _material == null) return;
            if (_dome == null) Build();
            if (cam != _camera)
            {
                _camera = cam;
                _dome.SetParent(cam.transform, false);
                _dome.localPosition = Vector3.zero;
            }
            // The dome stays level and centred on the camera, just inside its far plane.
            _dome.rotation = Quaternion.identity;
            _dome.localScale = Vector3.one * (cam.farClipPlane * _radiusShare * 2f);
            cam.backgroundColor = RenderSettings.fogColor;
        }

        private void Build()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "[UnderwaterSkyDome]";
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _dome = go.transform;
        }

        private void OnDestroy()
        {
            if (_dome != null) Destroy(_dome.gameObject);
        }

#if UNITY_EDITOR
        public void EditorSetup(Material material) => _material = material;
#endif
    }
}
