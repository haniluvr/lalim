using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// Bakunawa's serpent body, driven procedurally: this transform is the head, and the 26 spine
    /// bones of the rigged model (Spine_00 at the head to Spine_25 at the tail) are laid along the
    /// path the head has swum, so the body follows exactly where the head went, like a real
    /// serpent. Asleep, the path is a coil on the abyss floor. The jaw opens for the roar and
    /// strikes.
    ///
    /// Each bone's rotation is its rest rotation carried along with its direction: at rest, bone i
    /// points from its head toward bone i+1 (its local Y axis), and that relation is kept.
    /// </summary>
    public class BakunawaBody : MonoBehaviour
    {
        [SerializeField] private GameObject _model;
        [Tooltip("Model scale: 4 makes it about 90 m long and 5 m thick, so gaps under 6 m keep it out.")]
        [SerializeField, Min(0.1f)] private float _scale = 4f;
        [Tooltip("Rotation of the jaw bone when fully open (around its local X).")]
        [SerializeField] private float _jawOpenDegrees = 38f;
        [SerializeField] private Vector3 _jawAxis = Vector3.right;

        private readonly List<Transform> _bones = new List<Transform>();
        private float[] _segment;            // distance from bone i to bone i+1 (scaled)
        private Quaternion[] _correction;    // rest rotation relative to LookRotation(restDir, up)
        private Transform _jaw;
        private Quaternion _jawRest;
        private float _jaw01, _jawTarget;

        // The head's path: newest point first, spaced about _step apart.
        private readonly List<Vector3> _trail = new List<Vector3>();
        private float _step = 0.6f;
        private Transform _instance;

        public float Length { get; private set; }
        public float Thickness => 1.25f * _scale;
        public bool Ready => _bones.Count > 0;

        /// <summary>0 shut, 1 open (eases there).</summary>
        public void SetJaw(float open01) => _jawTarget = Mathf.Clamp01(open01);

        private void Awake() => Build();

        private void Build()
        {
            if (_model == null || _bones.Count > 0) return;
            _instance = Instantiate(_model, transform.position, Quaternion.identity).transform;
            _instance.name = "BakunawaModel";
            _instance.localScale = Vector3.one * _scale;
            foreach (Collider c in _instance.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (SkinnedMeshRenderer r in _instance.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;

            var byName = new Dictionary<string, Transform>();
            foreach (Transform t in _instance.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
            for (int i = 0; ; i++)
            {
                if (!byName.TryGetValue($"Spine_{i:00}", out Transform b)) break;
                _bones.Add(b);
            }
            if (byName.TryGetValue("Jaw", out _jaw)) _jawRest = _jaw.localRotation;
            if (_bones.Count < 2) { Debug.LogWarning("[BakunawaBody] No Spine_00.. bones found."); return; }

            int n = _bones.Count;
            _segment = new float[n];
            _correction = new Quaternion[n];
            Length = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = RestDir(i);
                _correction[i] = Quaternion.Inverse(Quaternion.LookRotation(dir, Up(dir))) * _bones[i].rotation;
                _segment[i] = i < n - 1 ? Vector3.Distance(_bones[i].position, _bones[i + 1].position) : 0f;
                Length += _segment[i];
            }
            _step = Mathf.Max(0.3f, _segment[0] * 0.5f);
            _instance.SetParent(transform, true);
            CoilAt(transform.position, transform.forward);
        }

        private Vector3 RestDir(int i)
        {
            int n = _bones.Count;
            Vector3 d = i < n - 1 ? _bones[i + 1].position - _bones[i].position : _bones[i].position - _bones[i - 1].position;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        private static Vector3 Up(Vector3 dir) => Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;

        /// <summary>Lays the whole body in a loose coil on the floor, head at <paramref name="head"/>.</summary>
        public void CoilAt(Vector3 head, Vector3 facing)
        {
            _trail.Clear();
            transform.position = head;
            float total = Length + _step * 4f;
            // An Archimedean spiral outward from the head: the head lies on top in the middle.
            float r0 = Thickness * 1.6f, gap = Thickness * 1.25f;
            float angle0 = Mathf.Atan2(facing.z, facing.x);
            float travelled = 0f, a = 0f;
            Vector3 prev = head;
            _trail.Add(head);
            while (travelled < total)
            {
                a += 0.05f;
                float r = r0 + gap * a / (Mathf.PI * 2f);
                Vector3 p = head + new Vector3(Mathf.Cos(angle0 + Mathf.PI + a) * r, -Thickness * 0.35f * Mathf.Clamp01(a / 3f), Mathf.Sin(angle0 + Mathf.PI + a) * r);
                travelled += Vector3.Distance(p, prev);
                prev = p;
                if (Vector3.Distance(p, _trail[_trail.Count - 1]) >= _step) _trail.Add(p);
            }
            Pose();
        }

        private void LateUpdate()
        {
            if (!Ready) return;
            Vector3 head = transform.position;
            if (_trail.Count == 0) _trail.Add(head);
            // New path points as the head moves; the oldest fall off past the tail.
            float moved = Vector3.Distance(head, _trail[0]);
            if (moved >= _step)
            {
                int add = Mathf.Min(64, Mathf.FloorToInt(moved / _step));
                Vector3 from = _trail[0];
                // Oldest first, so the newest (the head itself) ends up at the front.
                for (int k = 1; k <= add; k++) _trail.Insert(0, Vector3.Lerp(from, head, k / (float)add));
            }
            else _trail[0] = head;   // smooth between samples
            float need = Length + _step * 4f;
            int keep = Mathf.CeilToInt(need / _step) + 2;
            if (_trail.Count > keep) _trail.RemoveRange(keep, _trail.Count - keep);

            _jaw01 = Mathf.MoveTowards(_jaw01, _jawTarget, Time.deltaTime * 2.5f);
            Pose();
        }

        private void Pose()
        {
            int n = _bones.Count;
            var at = new Vector3[n];
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                at[i] = PointAlong(dist);
                dist += _segment[i];
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = i < n - 1 ? at[i + 1] - at[i] : at[i] - at[i - 1];
                if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
                dir.Normalize();
                _bones[i].SetPositionAndRotation(at[i], Quaternion.LookRotation(dir, Up(dir)) * _correction[i]);
            }
            if (_jaw != null) _jaw.localRotation = _jawRest * Quaternion.AngleAxis(_jawOpenDegrees * Mathf.SmoothStep(0f, 1f, _jaw01), _jawAxis);
        }

        /// <summary>The point <paramref name="d"/> metres back along the path from the head.</summary>
        private Vector3 PointAlong(float d)
        {
            if (_trail.Count == 0) return transform.position;
            float left = d;
            for (int i = 0; i < _trail.Count - 1; i++)
            {
                float seg = Vector3.Distance(_trail[i], _trail[i + 1]);
                if (left <= seg) return Vector3.Lerp(_trail[i], _trail[i + 1], seg > 1e-5f ? left / seg : 0f);
                left -= seg;
            }
            // Past the end of the path: carry straight on behind.
            Vector3 last = _trail[_trail.Count - 1];
            Vector3 back = _trail.Count > 1 ? (last - _trail[_trail.Count - 2]).normalized : -transform.forward;
            return last + back * left;
        }

        /// <summary>Closest distance from <paramref name="p"/> to the body (for strikes and the body-blocking push).</summary>
        public float DistanceToBody(Vector3 p, out Vector3 closest)
        {
            closest = transform.position;
            float best = float.MaxValue;
            for (int i = 0; i < _bones.Count - 1; i++)
            {
                Vector3 a = _bones[i].position, b = _bones[i + 1].position;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                Vector3 c = a + ab * t;
                float d = Vector3.Distance(p, c);
                if (d < best) { best = d; closest = c; }
            }
            return best;
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject model, float scale)
        {
            _model = model;
            _scale = scale;
        }
#endif
    }
}
