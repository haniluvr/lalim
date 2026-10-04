using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A portal surface shaped like an arch opening: a rectangle with a semicircle on top. The pivot
    /// is at the bottom centre and the shape lies in the local XY plane, so it can be placed by hand
    /// inside an arch. Width and height are set here (not with the transform's scale) so the top
    /// stays round. The video "covers" the shape at its own aspect ratio, centred.
    ///
    /// The mesh is generated (in edit mode too) and never saved as an asset.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ArchPortalSurface : MonoBehaviour
    {
        [Tooltip("Full width; the semicircle on top has half this as its radius.")]
        [SerializeField, Min(0.001f)] private float _width = 0.2f;
        [Tooltip("Full height, bottom edge to the top of the semicircle.")]
        [SerializeField, Min(0.001f)] private float _height = 0.3f;
        [SerializeField, Range(4, 64)] private int _arcSegments = 32;
        [Tooltip("Video width ÷ height (720×1280 → 0.5625).")]
        [SerializeField, Min(0.01f)] private float _videoAspect = 0.5625f;

        private Mesh _mesh;

        public float Width => _width;
        public float Height => _height;

        private void OnEnable() => Rebuild();

        private void OnDestroy()
        {
            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }

#if UNITY_EDITOR
        // MeshFilter can't be changed inside OnValidate itself.
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };

        public void EditorSetup(float width, float height, float videoAspect)
        {
            _width = width;
            _height = height;
            _videoAspect = videoAspect;
            Rebuild();
        }
#endif

        public void Rebuild()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "ArchPortalSurface", hideFlags = HideFlags.DontSave };
            }

            float r = _width * 0.5f;
            float rect = Mathf.Max(0f, _height - r);   // height of the straight sides
            float top = rect + r;

            // Outline (counter-clockwise from bottom-left), then a fan from an interior point.
            int n = _arcSegments + 1;
            var verts = new Vector3[n + 3];
            verts[0] = new Vector3(0f, top * 0.5f, 0f);
            verts[1] = new Vector3(-r, 0f, 0f);
            verts[2] = new Vector3(r, 0f, 0f);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * i / _arcSegments;   // 0 = right side, π = left side
                verts[3 + i] = new Vector3(Mathf.Cos(a) * r, rect + Mathf.Sin(a) * r, 0f);
            }

            var tris = new int[(n + 2) * 3];
            int outline = n + 2, t = 0;
            for (int i = 0; i < outline; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % outline;
                tris[t++] = 0; tris[t++] = b; tris[t++] = a;
            }

            float videoW = Mathf.Max(_width, top * _videoAspect);
            float videoH = videoW / _videoAspect;
            var uvs = new Vector2[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                uvs[i] = new Vector2(0.5f + verts[i].x / videoW, 0.5f + (verts[i].y - top * 0.5f) / videoH);

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            var gate = GetComponent<TeleportArchGate>();
            if (gate != null) gate.SyncColliders();   // trigger, blocker and fog follow the shape
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(0f, _height * 0.5f, 0f), new Vector3(_width, _height, 0f));
        }
    }
}
