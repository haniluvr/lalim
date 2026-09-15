using System;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Defines and enforces underwater boundaries for the scene.
    /// Creates physical barrier colliders for perimeter walls and water surface ceiling.
    /// Clamps diver position to prevent clipping out of bounds into the void.
    /// Provides soft current pushback when the player swims too close to the edge.
    /// </summary>
    [ExecuteAlways]
    public class UnderwaterBounds : MonoBehaviour
    {
        public static UnderwaterBounds Instance { get; private set; }

        [Header("Bounds Dimensions")]
        [Tooltip("Center of the underwater volume in world coordinates")]
        [SerializeField] private Vector3 _boundsCenter = new Vector3(0f, 7.5f, 0f);

        [Tooltip("Dimensions (Width X, Height Y, Length Z) of the underwater volume")]
        [SerializeField] private Vector3 _boundsSize = new Vector3(96f, 15f, 96f);

        [Header("Collision Barriers")]
        [Tooltip("Automatically generate physical BoxColliders at the perimeter and surface")]
        [SerializeField] private bool _generateColliders = true;

        [Tooltip("Thickness of the invisible barrier colliders (meters)")]
        [SerializeField] private float _barrierThickness = 2.0f;

        [Header("Soft Ocean Current Pushback")]
        [Tooltip("Apply gentle inward current push when approaching outer boundaries")]
        [SerializeField] private bool _enableCurrentPushback = true;

        [Tooltip("Distance from boundary where ocean currents start pushing back")]
        [SerializeField] private float _currentMargin = 4.0f;

        [Tooltip("Maximum inward current force")]
        [SerializeField] private float _currentPushForce = 5.0f;

        [Header("Position Clamping")]
        [Tooltip("Hard clamp diver position inside bounds as a fallback safeguard")]
        [SerializeField] private bool _clampPlayerPosition = true;

        [Header("Water Surface Visuals")]
        [Tooltip("Material for the water surface ceiling (M_WaterSurface)")]
        [SerializeField] private Material _surfaceMaterial;
        [SerializeField] private bool _createSurfaceVisual = true;

        [Header("Scene View Gizmos")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color _boundsColor = new Color(0.1f, 0.7f, 1.0f, 0.4f);
        [SerializeField] private Color _surfaceColor = new Color(0.3f, 0.9f, 1.0f, 0.7f);

        // ─────────────────────────────────────────────
        // Public Accessors
        // ─────────────────────────────────────────────

        public Vector3 Center => _boundsCenter;
        public Vector3 Size => _boundsSize;
        public Vector3 Min => _boundsCenter - _boundsSize * 0.5f;
        public Vector3 Max => _boundsCenter + _boundsSize * 0.5f;
        public float WaterSurfaceY => Max.y;
        public float SeabedY => Min.y;

        // Container for runtime-generated barrier colliders
        private const string BARRIERS_CONTAINER_NAME = "[Generated_Barriers]";
        private const string SURFACE_VISUAL_NAME = "[Water_Surface_Ceiling]";

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
            }

            if (_generateColliders)
            {
                EnsureColliders();
            }

            if (_createSurfaceVisual && _surfaceMaterial != null)
            {
                EnsureSurfaceVisual();
            }
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void OnValidate()
        {
            // Update colliders when modified in Inspector during edit mode
            if (!Application.isPlaying && _generateColliders)
            {
                EnsureColliders();
            }
        }

        // ─────────────────────────────────────────────
        // Barrier Colliders Generation
        // ─────────────────────────────────────────────

        /// <summary>
        /// Creates or updates 5 solid physical BoxColliders:
        /// Ceiling (Water surface), North, South, East, West walls.
        /// </summary>
        public void EnsureColliders()
        {
            Transform container = transform.Find(BARRIERS_CONTAINER_NAME);
            if (container == null)
            {
                GameObject containerGo = new GameObject(BARRIERS_CONTAINER_NAME);
                containerGo.transform.SetParent(transform, false);
                container = containerGo.transform;
            }

            Vector3 min = Min;
            Vector3 max = Max;
            Vector3 center = Center;
            Vector3 size = Size;
            float t = _barrierThickness;
            float halfT = t * 0.5f;

            // Ceiling (Water Surface barrier at Max.y)
            CreateOrUpdateBox(container, "Ceiling_Barrier",
                new Vector3(center.x, max.y + halfT, center.z),
                new Vector3(size.x + t * 2, t, size.z + t * 2));

            // North Wall (+Z)
            CreateOrUpdateBox(container, "North_Wall_Barrier",
                new Vector3(center.x, center.y, max.z + halfT),
                new Vector3(size.x + t * 2, size.y + t * 2, t));

            // South Wall (-Z)
            CreateOrUpdateBox(container, "South_Wall_Barrier",
                new Vector3(center.x, center.y, min.z - halfT),
                new Vector3(size.x + t * 2, size.y + t * 2, t));

            // East Wall (+X)
            CreateOrUpdateBox(container, "East_Wall_Barrier",
                new Vector3(max.x + halfT, center.y, center.z),
                new Vector3(t, size.y + t * 2, size.z + t * 2));

            // West Wall (-X)
            CreateOrUpdateBox(container, "West_Wall_Barrier",
                new Vector3(min.x - halfT, center.y, center.z),
                new Vector3(t, size.y + t * 2, size.z + t * 2));

            // Floor Barrier (-Y) to ensure player never clips below seafloor
            CreateOrUpdateBox(container, "Floor_Barrier",
                new Vector3(center.x, min.y - halfT, center.z),
                new Vector3(size.x + t * 2, t, size.z + t * 2));
        }

        private void CreateOrUpdateBox(Transform parent, string name, Vector3 localPos, Vector3 boxSize)
        {
            Transform child = parent.Find(name);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = child.gameObject;
            }

            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
            }

            col.size = boxSize;
            col.center = Vector3.zero;
            col.isTrigger = false; // Solid physical collider for CharacterController
        }

        // ─────────────────────────────────────────────
        // Water Surface Ceiling Visual
        // ─────────────────────────────────────────────

        private void EnsureSurfaceVisual()
        {
            Transform surfaceTransform = transform.Find(SURFACE_VISUAL_NAME);
            GameObject surfaceGo;
            if (surfaceTransform == null)
            {
                surfaceGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
                surfaceGo.name = SURFACE_VISUAL_NAME;
                surfaceGo.transform.SetParent(transform, false);

                // Remove the default MeshCollider from primitive plane
                Collider col = surfaceGo.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
            else
            {
                surfaceGo = surfaceTransform.gameObject;
            }

            // Position at water surface, rotated so the plane faces downward towards the diver
            surfaceGo.transform.localPosition = new Vector3(_boundsCenter.x, WaterSurfaceY, _boundsCenter.z);
            surfaceGo.transform.localRotation = Quaternion.Euler(180f, 0f, 0f); // Face down
            surfaceGo.transform.localScale = new Vector3(_boundsSize.x * 0.1f, 1f, _boundsSize.z * 0.1f);

            MeshRenderer renderer = surfaceGo.GetComponent<MeshRenderer>();
            if (renderer != null && _surfaceMaterial != null)
            {
                renderer.sharedMaterial = _surfaceMaterial;
            }
        }

        // ─────────────────────────────────────────────
        // Ocean Current & Clamping API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Calculates inward ocean current force if the given position is near boundaries.
        /// </summary>
        public Vector3 GetPushbackForce(Vector3 position)
        {
            if (!_enableCurrentPushback) return Vector3.zero;

            Vector3 push = Vector3.zero;
            Vector3 min = Min;
            Vector3 max = Max;

            // X axis
            float distMinX = position.x - min.x;
            float distMaxX = max.x - position.x;
            if (distMinX < _currentMargin && distMinX >= 0f)
            {
                float t = 1f - (distMinX / _currentMargin);
                push.x += t * _currentPushForce;
            }
            else if (distMaxX < _currentMargin && distMaxX >= 0f)
            {
                float t = 1f - (distMaxX / _currentMargin);
                push.x -= t * _currentPushForce;
            }

            // Z axis
            float distMinZ = position.z - min.z;
            float distMaxZ = max.z - position.z;
            if (distMinZ < _currentMargin && distMinZ >= 0f)
            {
                float t = 1f - (distMinZ / _currentMargin);
                push.z += t * _currentPushForce;
            }
            else if (distMaxZ < _currentMargin && distMaxZ >= 0f)
            {
                float t = 1f - (distMaxZ / _currentMargin);
                push.z -= t * _currentPushForce;
            }

            // Y axis (Surface and Seabed)
            float distMaxY = max.y - position.y;
            float distMinY = position.y - min.y;
            if (distMaxY < _currentMargin && distMaxY >= 0f)
            {
                float t = 1f - (distMaxY / _currentMargin);
                push.y -= t * (_currentPushForce * 0.5f); // Gentle surface surge
            }
            else if (distMinY < _currentMargin && distMinY >= 0f)
            {
                float t = 1f - (distMinY / _currentMargin);
                push.y += t * (_currentPushForce * 0.5f); // Gentle seabed lift
            }

            return push;
        }

        /// <summary>
        /// Clamps a position to stay inside the underwater bounds with padding for player radius.
        /// </summary>
        public Vector3 ClampPosition(Vector3 position, float radius = 0.5f)
        {
            if (!_clampPlayerPosition) return position;

            Vector3 min = Min + Vector3.one * radius;
            Vector3 max = Max - Vector3.one * radius;

            return new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y),
                Mathf.Clamp(position.z, min.z, max.z)
            );
        }

        // ─────────────────────────────────────────────
        // Gizmos
        // ─────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            // Draw bounding wireframe cube
            Gizmos.color = _boundsColor;
            Gizmos.DrawWireCube(_boundsCenter, _boundsSize);

            // Draw water surface plane marker
            Gizmos.color = _surfaceColor;
            Vector3 surfaceCenter = new Vector3(_boundsCenter.x, WaterSurfaceY, _boundsCenter.z);
            Vector3 surfaceSize = new Vector3(_boundsSize.x, 0.05f, _boundsSize.z);
            Gizmos.DrawCube(surfaceCenter, surfaceSize);

            // Draw ocean seabed marker
            Gizmos.color = new Color(0.8f, 0.7f, 0.4f, 0.3f);
            Vector3 seabedCenter = new Vector3(_boundsCenter.x, SeabedY, _boundsCenter.z);
            Gizmos.DrawCube(seabedCenter, surfaceSize);
        }
    }
}
