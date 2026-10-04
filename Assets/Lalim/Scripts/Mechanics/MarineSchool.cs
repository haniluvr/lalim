using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A school of fish roaming around this spot: the school drifts between random points within
    /// <see cref="_roamRadius"/>, each fish keeps its place in the school (cohesion, separation,
    /// alignment), wiggles as it swims, stays above the seabed, steers around rocks, and scatters
    /// when the diver swims through. Fish are spawned from <see cref="_models"/> at start; they have
    /// no colliders, so they never block the player.
    /// </summary>
    public class MarineSchool : MonoBehaviour
    {
        [SerializeField] private GameObject[] _models;
        [SerializeField, Range(1, 30)] private int _count = 8;
        [Tooltip("Fish length in metres.")]
        [SerializeField, Min(0.05f)] private float _length = 0.45f;
        [Tooltip("Turn the model so its head points forward (+Z), in degrees.")]
        [SerializeField] private float _modelYaw;

        [Header("Movement")]
        [SerializeField, Min(1f)] private float _roamRadius = 10f;
        [SerializeField, Min(0.1f)] private float _speed = 1.6f;
        [Tooltip("How spread out the school is (metres).")]
        [SerializeField, Min(0.2f)] private float _spread = 1.8f;
        [SerializeField, Min(0.2f)] private float _minHeight = 1.5f;
        [SerializeField, Min(0.5f)] private float _maxHeight = 6f;
        [SerializeField, Min(0f)] private float _fleeDistance = 3.5f;

        private class Fish
        {
            public Transform body;
            public Vector3 velocity;
            public Vector3 slot;
            public float phase;
            public float pace;
            public Vector3 avoid;
        }

        private readonly List<Fish> _fish = new List<Fish>();
        private Vector3 _home, _centre, _target, _centreVelocity;
        private float _retarget;
        private int _frame;

        private void Start()
        {
            _home = _centre = transform.position;
            PickTarget();
            if (_models == null || _models.Length == 0) return;
            for (int i = 0; i < _count; i++)
            {
                GameObject model = _models[i % _models.Length];
                if (model == null) continue;
                var fish = new Fish
                {
                    slot = Vector3.Scale(Random.insideUnitSphere, new Vector3(_spread, _spread * 0.45f, _spread)),
                    phase = Random.value * 10f,
                    pace = Random.Range(0.85f, 1.15f),
                };
                fish.body = SwimBody.Spawn(model, transform, _length * Random.Range(0.85f, 1.15f), _modelYaw);
                fish.body.position = _centre + fish.slot;
                fish.velocity = Random.onUnitSphere * 0.2f;
                _fish.Add(fish);
            }
        }

        private void PickTarget()
        {
            Vector2 r = Random.insideUnitCircle * _roamRadius;
            Vector3 p = _home + new Vector3(r.x, 0f, r.y);
            float floor = SwimBody.SeabedY(p, _home.y + _maxHeight + 4f);
            p.y = Mathf.Clamp(_home.y + Random.Range(-1.5f, 1.5f), floor + _minHeight + 0.5f, floor + _maxHeight);
            _target = p;
            _retarget = Random.Range(5f, 9f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _fish.Count == 0) return;
            _frame++;

            // The school's centre drifts toward its target, then picks another.
            _retarget -= dt;
            if (_retarget <= 0f || (_target - _centre).sqrMagnitude < 1f) PickTarget();
            Vector3 toTarget = _target - _centre;
            _centreVelocity = Vector3.Lerp(_centreVelocity, toTarget.normalized * _speed * 0.7f, dt * 0.8f);
            _centre += _centreVelocity * dt;

            Vector3 diver = PlayerController.Instance != null ? PlayerController.Instance.transform.position : new Vector3(0f, -1e6f, 0f);
            for (int i = 0; i < _fish.Count; i++)
            {
                Fish f = _fish[i];
                if (f.body == null) continue;
                Vector3 pos = f.body.position;

                Vector3 desired = (_centre + f.slot - pos) * 1.2f + _centreVelocity;
                // Keep a little room from neighbours.
                for (int j = 0; j < _fish.Count; j++)
                {
                    if (j == i || _fish[j].body == null) continue;
                    Vector3 away = pos - _fish[j].body.position;
                    float d = away.magnitude;
                    if (d < _length * 1.2f && d > 0.001f) desired += away / d * (_length * 1.2f - d) * 4f;
                }
                // Scatter from the diver.
                Vector3 fromDiver = pos - diver;
                float dd = fromDiver.magnitude;
                if (_fleeDistance > 0f && dd < _fleeDistance && dd > 0.001f) desired += fromDiver / dd * (_fleeDistance - dd) * 3f;

                // Rocks and seabed (checked a few fish per frame).
                if ((i + _frame) % 4 == 0) f.avoid = SwimBody.Avoidance(pos, f.velocity, _length * 4f + 1f, _minHeight);
                desired += f.avoid;

                Vector3 v = Vector3.ClampMagnitude(desired, _speed * 1.8f * f.pace);
                f.velocity = Vector3.Lerp(f.velocity, v, dt * 2.5f);
                f.body.position = pos + f.velocity * dt;
                SwimBody.Face(f.body, f.velocity, f.phase, dt, 10f);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject[] models, int count, float length, float modelYaw, float roamRadius, float speed)
        {
            _models = models;
            _count = count;
            _length = length;
            _modelYaw = modelYaw;
            _roamRadius = roamRadius;
            _speed = speed;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(Application.isPlaying ? _home : transform.position, _roamRadius);
        }
#endif
    }

    /// <summary>Shared bits for swimming animals: spawning a model, facing, wiggle and avoidance.</summary>
    public static class SwimBody
    {
        /// <summary>Instantiates <paramref name="model"/> under a pivot that faces +Z, scaled to <paramref name="length"/> metres, without colliders.</summary>
        public static Transform Spawn(GameObject model, Transform parent, float length, float modelYaw)
        {
            var pivot = new GameObject(model.name).transform;
            pivot.SetParent(parent, false);
            GameObject go = Object.Instantiate(model, pivot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, modelYaw, 0f);
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (TryBounds(go, out Bounds b))
            {
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                go.transform.localScale *= length / Mathf.Max(0.001f, longest);
                // Centre the body on the pivot, so it turns about its middle.
                if (TryBounds(go, out b)) go.transform.position += pivot.position - b.center;
            }
            return pivot;
        }

        private static bool TryBounds(GameObject go, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Turns toward the swim direction (level-ish) with a tail-wiggle sway.</summary>
        public static void Face(Transform body, Vector3 velocity, float phase, float dt, float wiggleDegrees)
        {
            if (velocity.sqrMagnitude < 0.0004f) return;
            Vector3 dir = velocity.normalized;
            dir.y = Mathf.Clamp(dir.y, -0.5f, 0.5f);
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            float speed = velocity.magnitude;
            float wiggle = Mathf.Sin(Time.time * (6f + speed * 4f) + phase) * wiggleDegrees * Mathf.Clamp01(speed);
            body.rotation = Quaternion.Slerp(body.rotation, look * Quaternion.Euler(0f, wiggle, 0f), dt * 4f);
        }

        /// <summary>The seabed (highest solid surface) below a point, or the point minus 20 m.</summary>
        public static float SeabedY(Vector3 at, float fromY)
        {
            var origin = new Vector3(at.x, fromY, at.z);
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : at.y - 20f;
        }

        /// <summary>A push away from rocks ahead and up off the seabed when too low.</summary>
        public static Vector3 Avoidance(Vector3 pos, Vector3 velocity, float lookAhead, float minHeight)
        {
            Vector3 push = Vector3.zero;
            if (velocity.sqrMagnitude > 0.0001f &&
                Physics.SphereCast(pos, 0.3f, velocity.normalized, out RaycastHit hit, lookAhead, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<PlayerController>() == null)
            {
                push += (hit.normal + Vector3.up * 0.3f) * (lookAhead - hit.distance) * 1.5f;
            }
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit ground, minHeight, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                push += Vector3.up * (minHeight - ground.distance) * 2f;
            }
            return push;
        }
    }
}
