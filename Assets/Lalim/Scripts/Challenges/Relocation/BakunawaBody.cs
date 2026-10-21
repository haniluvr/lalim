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
    ///
    /// Solid: a capsule rides on each spine bone, as thick as the mesh is there (measured from the
    /// skinned mesh), so the diver and the camera can't pass inside the body. They sit on the
    /// Ignore Raycast layer: the diver and the camera still collide with them, but the serpent's own
    /// steering, sight and the seabed checks look straight through.
    /// </summary>
    public class BakunawaBody : MonoBehaviour
    {
        [SerializeField] private GameObject _model;
        [Tooltip("Model scale. Its thickness (measured from the mesh) sets how big a gap keeps it out.")]
        [SerializeField, Min(0.01f)] private float _scale = 4f;
        [Tooltip("Rotation of the jaw bone (around Jaw Axis, from its rest pose) with the mouth shut.")]
        [SerializeField] private float _jawClosedDegrees = 0f;
        [Tooltip("Rotation of the jaw bone (around Jaw Axis, from its rest pose) with the mouth fully open.")]
        [SerializeField] private float _jawOpenDegrees = 38f;
        [SerializeField] private Vector3 _jawAxis = Vector3.right;

        [Header("Mouth extras (optional)")]
        [Tooltip("The tongue's root bone: it is pulled back into the throat (shortened) while the mouth is nearly shut.")]
        [SerializeField] private string _tongueBone = "Tongue_00";
        [Tooltip("Open amount (0..1) over which the tongue slides out: in below x, fully out above y.")]
        [SerializeField] private Vector2 _tongueOut = new Vector2(0.27f, 0.62f);
        [SerializeField, Range(0.01f, 1f)] private float _tongueInLength = 0.1f;
        [Tooltip("A blend shape that seals the lips together while the mouth is shut (fades out as it opens).")]
        [SerializeField] private string _sealBlendShape = "MouthSeal";
        [SerializeField, Range(0.01f, 1f)] private float _sealUntil = 0.19f;

        private readonly List<Transform> _bones = new List<Transform>();
        private float[] _segment;            // distance from bone i to bone i+1 (scaled)
        private float[] _radius;             // body radius around bone i (scaled)
        private Quaternion[] _correction;    // rest rotation relative to LookRotation(restDir, up)
        private Transform _jaw;
        private Quaternion _jawRest;
        private Transform _tongue;
        private Vector3 _tongueRest;
        private SkinnedMeshRenderer _sealRenderer;
        private int _sealIndex = -1;
        private float _jaw01, _jawTarget;

        // The head's path: newest point first, spaced about _step apart.
        private readonly List<Vector3> _trail = new List<Vector3>();
        private float _step = 0.6f;
        private Vector3 _poseUp = Vector3.up;   // the head bone's up, carried from frame to frame
        private Transform _instance;

        public float Length { get; private set; }
        /// <summary>How thick the body is (its measured width; before that is known, a guess from the scale).</summary>
        public float Thickness => _bodyWidth > 0f ? _bodyWidth : 1.25f * _scale;
        public bool Ready => _bones.Count > 0;

        /// <summary>The head bone's length (to Spine_01): curls tighter than this can't show on the head.</summary>
        public float HeadBoneLength => _segment != null && _segment.Length > 0 ? _segment[0] : Thickness;

        /// <summary>
        /// Keeps the body in the pose it has now while the head only sways a little (e.g. reared up,
        /// talking): the path stops growing, so the swaying doesn't slowly draw the body up into it.
        /// </summary>
        public bool HoldShape { get; set; }

        /// <summary>
        /// Swimming in place (e.g. while it talks): its head stays steady, and a slow swimming
        /// wave runs down its body, side to side, growing toward the tail. Runs on real time, so
        /// it keeps going while a conversation pauses the game.
        /// </summary>
        public bool SwimInPlace { get; set; }

        [Header("Swimming in place")]
        [SerializeField, Min(1f)] private float _swayWavelength = 28f;
        [SerializeField, Min(0.5f)] private float _swaySeconds = 3.4f;   // one beat of the wave
        [SerializeField, Min(0f)] private float _swayAmplitude = 2.2f;   // side to side, at the tail
        [SerializeField, Min(0f)] private float _swayHeadStill = 7f;     // metres behind the head before it starts
        private float _sway01;

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
            if (!string.IsNullOrEmpty(_tongueBone) && byName.TryGetValue(_tongueBone, out _tongue)) _tongueRest = _tongue.localScale;
            if (!string.IsNullOrEmpty(_sealBlendShape))
                foreach (SkinnedMeshRenderer smr in _instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int k = smr.sharedMesh != null ? smr.sharedMesh.GetBlendShapeIndex(_sealBlendShape) : -1;
                    if (k >= 0) { _sealRenderer = smr; _sealIndex = k; break; }
                }
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
            MeasureCentres();
            MeasureRadii();
            AddColliders();
            _instance.SetParent(transform, true);
            CoilAt(transform.position, transform.forward);
        }

        /// <summary>
        /// The rig's spine bones don't run down the middle of the mesh: toward the front they lie a
        /// few metres off to one side (at the head, about a head-width), and the model rests with its
        /// head turned about 25° to that side. Laid on the path as they are, the head and neck swing
        /// out beside where it swims and it looks twisted. So, from the mesh at rest: each bone's
        /// offset to the middle of the flesh it moves (in its own space), to slide the bone over so
        /// that middle runs along the path; and the head's snout, to turn the head to lead with it.
        /// </summary>
        private void MeasureCentres()
        {
            int n = _bones.Count;
            _centre = new Vector3[n];
            // Every vertex counts toward each bone it follows, by its weight, in that bone's space.
            var pts = new List<Vector3>[n];
            var wts = new List<float>[n];
            for (int i = 0; i < n; i++) { pts[i] = new List<Vector3>(); wts[i] = new List<float>(); }
            var head = new List<Vector3>();
            foreach (SkinnedMeshRenderer smr in _instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = smr.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Vector3[] verts = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Matrix4x4[] bind = mesh.bindposes;
                Transform[] smrBones = smr.bones;
                if (weights.Length != verts.Length) continue;
                var spineIndex = new int[smrBones.Length];
                for (int b = 0; b < smrBones.Length; b++) spineIndex[b] = _bones.IndexOf(smrBones[b]);
                for (int v = 0; v < verts.Length; v++)
                {
                    BoneWeight bw = weights[v];
                    AddWeighted(pts, wts, spineIndex, bind, verts[v], bw.boneIndex0, bw.weight0);
                    AddWeighted(pts, wts, spineIndex, bind, verts[v], bw.boneIndex1, bw.weight1);
                    AddWeighted(pts, wts, spineIndex, bind, verts[v], bw.boneIndex2, bw.weight2);
                    AddWeighted(pts, wts, spineIndex, bind, verts[v], bw.boneIndex3, bw.weight3);
                    if (bw.weight0 >= 0.5f && bw.boneIndex0 >= 0 && bw.boneIndex0 < smrBones.Length && spineIndex[bw.boneIndex0] == 0)
                        head.Add(bind[bw.boneIndex0].MultiplyPoint3x4(verts[v]));
                }
            }
            // The middle of each section, across the bone (its local Y runs along the body): a
            // weighted mean, then again without the far-out fins and horns.
            var raw = new Vector3[n];
            var known = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (pts[i].Count < 12) continue;
                Vector3 m = WeightedMean(pts[i], wts[i], Vector3.zero, float.MaxValue);
                var d = new List<float>(pts[i].Count);
                foreach (Vector3 p in pts[i]) d.Add(new Vector2(p.x - m.x, p.z - m.z).magnitude);
                float cut = Median(d) * 1.4f;
                m = WeightedMean(pts[i], wts[i], m, cut);
                raw[i] = new Vector3(m.x, 0f, m.z);
                known[i] = true;
            }
            // Smoothed along the body, so the sections slide over together rather than kinking.
            for (int i = 0; i < n; i++)
            {
                Vector3 sum = Vector3.zero;
                float w = 0f;
                for (int k = -2; k <= 2; k++)
                {
                    int j = i + k;
                    if (j < 0 || j >= n || !known[j]) continue;
                    float f = k == 0 ? 3f : (k == 1 || k == -1 ? 2f : 1f);
                    sum += raw[j] * f;
                    w += f;
                }
                _centre[i] = w > 0f ? sum / w : Vector3.zero;
            }
            // The head is its own rigid piece: its own middle, unsmoothed.
            if (known[0]) _centre[0] = raw[0];

            // The snout: the head's front (its bone's -Y points ahead, away from the neck), averaged
            // over the frontmost few percent, from the middle of the head.
            if (head.Count < 50) return;
            head.Sort((p, q) => p.y.CompareTo(q.y));
            int take = Mathf.Max(5, head.Count / 50);
            Vector3 tip = Vector3.zero;
            for (int k = 0; k < take; k++) tip += head[k];
            tip /= take;
            Vector3 snout = tip - new Vector3(_centre[0].x, 0f, _centre[0].z);
            snout.y = Mathf.Min(snout.y, -1e-3f);
            snout.Normalize();
            // A head turned further than this from its neck means the measure went wrong: keep the rest relation.
            if (Vector3.Angle(snout, Vector3.down) > 50f) return;
            // Posed, the bone's -Y points along the path: turn the head so the snout does instead.
            _correction[0] = _correction[0] * Quaternion.FromToRotation(snout, Vector3.down);
        }

        private void AddWeighted(List<Vector3>[] pts, List<float>[] wts, int[] spineIndex, Matrix4x4[] bind, Vector3 vertex, int bone, float weight)
        {
            if (weight < 0.15f || bone < 0 || bone >= spineIndex.Length || spineIndex[bone] < 0) return;
            pts[spineIndex[bone]].Add(bind[bone].MultiplyPoint3x4(vertex));
            wts[spineIndex[bone]].Add(weight);
        }

        private static Vector3 WeightedMean(List<Vector3> pts, List<float> wts, Vector3 around, float within)
        {
            Vector3 sum = Vector3.zero;
            float w = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (new Vector2(pts[i].x - around.x, pts[i].z - around.z).magnitude > within) continue;
                sum += pts[i] * wts[i];
                w += wts[i];
            }
            return w > 0f ? sum / w : around;
        }

        private Vector3[] _centre;   // the middle of the flesh each bone moves, in its own space (rest)

        private static float Median(List<float> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>
        /// The body's thickness around each spine bone, from the skinned mesh at rest: each vertex's
        /// distance from the line of the bone it follows most (fins and horns trimmed off by taking
        /// a middle percentile, not the farthest vertex).
        /// </summary>
        private void MeasureRadii()
        {
            int n = _bones.Count;
            _radius = new float[n];
            var samples = new List<float>[n];
            for (int i = 0; i < n; i++) samples[i] = new List<float>();
            foreach (SkinnedMeshRenderer smr in _instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = smr.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Vector3[] verts = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Matrix4x4[] bind = mesh.bindposes;
                Transform[] smrBones = smr.bones;
                if (weights.Length != verts.Length) continue;
                var spineIndex = new int[smrBones.Length];
                for (int b = 0; b < smrBones.Length; b++) spineIndex[b] = _bones.IndexOf(smrBones[b]);
                for (int v = 0; v < verts.Length; v++)
                {
                    int b = weights[v].boneIndex0;
                    if (b < 0 || b >= smrBones.Length || spineIndex[b] < 0) continue;
                    Vector3 local = bind[b].MultiplyPoint3x4(verts[v]);      // in the bone's own space
                    Vector3 c = _centre != null ? _centre[spineIndex[b]] : Vector3.zero;
                    samples[spineIndex[b]].Add(new Vector2(local.x - c.x, local.z - c.z).magnitude);
                }
            }
            float fallback = Thickness * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float scale = _bones[i].lossyScale.x;
                List<float> list = samples[i];
                if (list.Count < 12) { _radius[i] = i > 0 ? _radius[i - 1] * 0.92f : fallback; continue; }
                list.Sort();
                _radius[i] = list[Mathf.Clamp(Mathf.RoundToInt(list.Count * 0.55f), 0, list.Count - 1)] * scale;
            }
            // The coil is laid by the real width of the body (the middle of its length).
            var mid = new List<float>(_radius);
            mid.Sort();
            _bodyWidth = 2f * mid[mid.Count / 2];
        }

        private float _bodyWidth;

        /// <summary>The body's measured width (diameter through its middle), for laying the coil.</summary>
        public float BodyWidth => Thickness;

        /// <summary>The head's measured radius: its colliders keep the diver at least this far from the head bone.</summary>
        public float HeadRadius => _radius != null && _radius.Length > 0 ? _radius[0] : Thickness * 0.5f;

        private void AddColliders()
        {
            int n = _bones.Count;
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            for (int i = 0; i < n; i++)
            {
                float r = Mathf.Max(0.25f, _radius[i]);
                float len = i < n - 1 ? _segment[i] : r * 2f;
                float scale = Mathf.Max(0.0001f, _bones[i].lossyScale.x);
                var go = new GameObject("Hit_" + i) { layer = layer };
                go.transform.SetParent(_bones[i], false);
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 1;                                    // along the bone (local Y → next bone)
                cap.radius = r / scale;
                cap.height = (len + r * 2f) / scale;                  // overlaps the next one, no gaps at bends
                // On the middle of the flesh, not the off-centre bone.
                Vector3 mid = _centre != null ? _centre[i] : Vector3.zero;
                cap.center = new Vector3(mid.x, len * 0.5f / scale, mid.z);
            }
            // Moving colliders want a kinematic body to ride on.
            var rb = _instance.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        /// <summary>
        /// How far <paramref name="p"/> (a body of <paramref name="extra"/> radius) is inside the
        /// serpent, and the way out. Zero when clear.
        /// </summary>
        public float Penetration(Vector3 p, float extra, out Vector3 outward)
        {
            outward = Vector3.zero;
            if (_radius == null) return 0f;
            float deepest = 0f;
            for (int i = 0; i < _bones.Count - 1; i++)
            {
                Vector3 a = _bones[i].position, b = _bones[i + 1].position;
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
                Vector3 c = a + ab * t;
                float r = Mathf.Lerp(_radius[i], _radius[i + 1], t) + extra;
                Vector3 d = p - c;
                float depth = r - d.magnitude;
                if (depth > deepest)
                {
                    deepest = depth;
                    outward = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
                }
            }
            return deepest;
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
            // An Archimedean spiral outward from the head: the head lies on top in the middle. Each
            // turn sits a body-width out from the last, so the loops lie side by side, not through
            // each other.
            float w = BodyWidth;
            float r0 = w * 1.1f, gap = w * 1.08f;
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
            if (_trail.Count == 1) _trail.Add(_trail[0]);
            // The front point is the head itself; behind it, fixed points a step apart along where
            // it swam (measured from the last fixed point, so a slow swim lays them down too, not
            // only a fast one). The oldest fall off past the tail.
            _trail[0] = head;
            Vector3 from = _trail[1];
            float moved = Vector3.Distance(head, from);
            if (!HoldShape && moved >= _step)
            {
                int add = Mathf.Min(64, Mathf.FloorToInt(moved / _step));
                // Oldest first, each in just behind the head, so the newest ends up nearest it.
                for (int k = 1; k <= add; k++) _trail.Insert(1, Vector3.Lerp(from, head, k * _step / moved));
            }
            float need = Length + _step * 4f;
            int keep = Mathf.CeilToInt(need / _step) + 2;
            if (_trail.Count > keep) _trail.RemoveRange(keep, _trail.Count - keep);

            _jaw01 = Mathf.MoveTowards(_jaw01, _jawTarget, Time.deltaTime * 2.5f);
            _sway01 = Mathf.MoveTowards(_sway01, SwimInPlace ? 1f : 0f, Time.unscaledDeltaTime * 0.7f);
            Pose();
        }

        private void Pose()
        {
            int n = _bones.Count;
            var at = new Vector3[n];
            float dist = 0f;
            var along = new float[n];
            for (int i = 0; i < n; i++)
            {
                at[i] = PointAlong(dist);
                along[i] = dist;
                dist += _segment[i];
            }
            if (_sway01 > 0.001f) Sway(at, along);
            // Each bone's "up" is carried down the body from the one before it (only nudged toward
            // world up where the body is level), so a stretch rising straight up doesn't flip its
            // roll and twist the body the way a fixed world-up reference does.
            Vector3 up = _poseUp;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = i < n - 1 ? at[i + 1] - at[i] : at[i] - at[i - 1];
                if (dir.sqrMagnitude < 1e-6f) dir = -transform.forward;
                dir.Normalize();
                Vector3 carried = Vector3.ProjectOnPlane(up, dir);
                Vector3 level = Vector3.ProjectOnPlane(Vector3.up, dir);
                float w = level.sqrMagnitude;   // 1 when the bone lies level, 0 when it points straight up or down
                if (carried.sqrMagnitude < 1e-4f) carried = level.sqrMagnitude > 1e-4f ? level : Up(dir);
                carried.Normalize();
                up = w > 1e-4f ? Vector3.Slerp(carried, level.normalized, (i == 0 ? 0.5f : 0.2f) * w * w) : carried;
                if (i == 0) _poseUp = up;
                Quaternion rot = Quaternion.LookRotation(dir, up) * _correction[i];
                // Slide it sideways (not along the path) so the middle of its flesh is on the path.
                Vector3 off = _centre != null ? rot * Vector3.Scale(_centre[i], _bones[i].lossyScale) : Vector3.zero;
                off -= dir * Vector3.Dot(off, dir);
                _bones[i].SetPositionAndRotation(at[i] - off, rot);
            }
            PoseMouth();
        }

        // A travelling wave from the neck to the tail: each point moves sideways (and a little up
        // and down) across the body's own line, more the further back it is; the head holds still.
        private void Sway(Vector3[] at, float[] along)
        {
            int n = at.Length;
            var side = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = i < n - 1 ? at[i + 1] - at[i] : at[i] - at[i - 1];
                Vector3 s = Vector3.Cross(Vector3.up, dir);
                side[i] = s.sqrMagnitude > 1e-6f ? s.normalized : transform.right;
            }
            float time = Time.unscaledTime * Mathf.PI * 2f / _swaySeconds;
            for (int i = 1; i < n; i++)
            {
                float d = along[i];
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - _swayHeadStill * 0.4f) / _swayHeadStill));
                if (k <= 0f) continue;
                float amp = Mathf.Lerp(0.5f, _swayAmplitude, Mathf.Clamp01(d / Mathf.Max(1f, Length))) * k * _sway01;
                float phase = d * Mathf.PI * 2f / _swayWavelength - time;
                at[i] += side[i] * Mathf.Sin(phase) * amp + Vector3.up * Mathf.Cos(phase * 0.5f) * amp * 0.15f;
            }
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

        // The jaw (shut .. open), the tongue (in while nearly shut) and the lip seal, all from one open amount.
        private void PoseMouth()
        {
            float open = Mathf.SmoothStep(0f, 1f, _jaw01);
            if (_jaw != null)
                _jaw.localRotation = _jawRest * Quaternion.AngleAxis(Mathf.Lerp(_jawClosedDegrees, _jawOpenDegrees, open), _jawAxis);
            if (_tongue != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_tongueOut.x, _tongueOut.y, open));
                Vector3 s = _tongueRest;
                s.y *= Mathf.Lerp(_tongueInLength, 1f, t);
                _tongue.localScale = s;
            }
            if (_sealRenderer != null) _sealRenderer.SetBlendShapeWeight(_sealIndex, 100f * Mathf.Clamp01(1f - open / _sealUntil));
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
