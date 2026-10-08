using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Keeps an ambient emitter (a zone's murk, silt or marine snow) around the main camera, clamped
    /// inside the zone's bounds, so a large zone only needs particles near the viewer. The particle
    /// system should simulate in world space, so the particles themselves stay put in the water.
    /// </summary>
    public class FollowCameraInBounds : MonoBehaviour
    {
        [Tooltip("The zone's volume (e.g. its ToxicWater box). Empty = follow the camera everywhere.")]
        [SerializeField] private Collider _bounds;

        private Bounds _cached;
        private bool _hasBounds;

        private void Awake()
        {
            // Cached once: the zone's toxic volume is disabled when the zone is cleansed.
            if (_bounds != null && _bounds.enabled && _bounds.gameObject.activeInHierarchy)
            {
                _cached = _bounds.bounds;
                _hasBounds = true;
            }
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            transform.position = _hasBounds ? _cached.ClosestPoint(p) : p;
        }

#if UNITY_EDITOR
        public void EditorSetup(Collider bounds) => _bounds = bounds;
#endif
    }
}
