using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A lone sea animal (turtle, stingray, …) cruising around its spot: it swims toward a new
    /// point within <see cref="roamRadius"/> every few seconds, glides with a gentle bob, keeps
    /// above the seabed, steers around rocks and veers away from the diver. No colliders: it never
    /// blocks the player.
    ///
    /// Two ways to use it: on the root of a prefab whose child is the model facing +Z, or straight
    /// on a creature placed in a scene in any pose — set <see cref="headAxis"/> (the head direction
    /// in its own space) and it turns that toward where it swims, keeping its back up.
    /// </summary>
    public class WanderingMarineAnimal : MonoBehaviour
    {
        [SerializeField] private float speed = 1.5f;
        [SerializeField] private float rotationSpeed = 1.0f;
        [SerializeField] private float changeDirectionInterval = 6f;
        [SerializeField] private float bobbingAmount = 0.15f;
        [SerializeField] private float bobbingSpeed = 1.2f;

        [Header("Area")]
        [SerializeField, Min(1f)] private float roamRadius = 14f;
        [SerializeField, Min(0.2f)] private float minHeight = 1.5f;
        [SerializeField, Min(0.5f)] private float maxHeight = 7f;
        [SerializeField, Min(0f)] private float fleeDistance = 4f;
        [Tooltip("Side-to-side sway while swimming (degrees).")]
        [SerializeField, Min(0f)] private float wiggle = 4f;

        [Header("Placed creatures")]
        [Tooltip("Head direction in this object's own space; zero = a prefab whose model faces +Z.")]
        [SerializeField] private Vector3 headAxis = Vector3.zero;
        [Tooltip("Only turn about the vertical (e.g. an octopus standing upright).")]
        [SerializeField] private bool yawOnly;

        private Vector3 _home, _target, _velocity, _avoid, _basePos;
        private Transform _body;
        private Vector3 _bodyHome;
        private Quaternion _pose = Quaternion.identity;   // placed creatures: rotation relative to "head forward, back up"
        private bool _posed;
        private float _timer, _phase;
        private int _frame;

        private void Start()
        {
            _home = _basePos = transform.position;
            _phase = Random.Range(0f, 10f);
            _posed = headAxis.sqrMagnitude > 0.0001f;
            if (_posed)
            {
                Vector3 head = transform.TransformDirection(headAxis.normalized);
                Vector3 up = BackUp(head);
                if (yawOnly) { head.y = 0f; up = Vector3.up; if (head.sqrMagnitude < 0.0001f) head = transform.forward; }
                _pose = Quaternion.Inverse(Quaternion.LookRotation(head.normalized, up)) * transform.rotation;
            }
            else if (transform.childCount > 0)
            {
                _body = transform.GetChild(0);
                _bodyHome = _body.localPosition;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>()) Destroy(c);
            PickTarget();
        }

        private void PickTarget()
        {
            Vector2 r = Random.insideUnitCircle * roamRadius;
            Vector3 p = _home + new Vector3(r.x, 0f, r.y);
            float floor = SwimBody.SeabedY(p, _home.y + maxHeight + 4f);
            p.y = Mathf.Clamp(_home.y + Random.Range(-1f, 1f), floor + minHeight + 0.5f, floor + maxHeight);
            _target = p;
            _timer = changeDirectionInterval + Random.Range(-2f, 2f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _frame++;

            _timer -= dt;
            if (_timer <= 0f || (_target - transform.position).sqrMagnitude < 2f) PickTarget();

            Vector3 pos = _basePos;
            Vector3 desired = (_target - pos).normalized * speed;
            if (PlayerController.Instance != null && fleeDistance > 0f)
            {
                Vector3 away = pos - PlayerController.Instance.transform.position;
                float d = away.magnitude;
                if (d < fleeDistance && d > 0.001f) desired += away / d * (fleeDistance - d) * 1.5f;
            }
            if (_frame % 3 == 0) _avoid = SwimBody.Avoidance(pos, _velocity, speed * 3f + 2f, minHeight);
            desired += _avoid;

            _velocity = Vector3.Lerp(_velocity, Vector3.ClampMagnitude(desired, speed * 1.6f), dt * rotationSpeed);
            _basePos = pos + _velocity * dt;
            float bob = Mathf.Sin(Time.time * bobbingSpeed + _phase) * bobbingAmount;

            if (_posed)
            {
                transform.position = _basePos + Vector3.up * bob;
                FacePosed(dt);
            }
            else
            {
                transform.position = _basePos;
                SwimBody.Face(transform, _velocity, _phase, dt * rotationSpeed, wiggle);
                if (_body != null) _body.localPosition = _bodyHome + Vector3.up * bob;
            }
        }

        private void FacePosed(float dt)
        {
            if (_velocity.sqrMagnitude < 0.0004f) return;
            Vector3 dir = _velocity.normalized;
            if (yawOnly) dir.y = 0f;
            else dir.y = Mathf.Clamp(dir.y, -0.4f, 0.4f);
            if (dir.sqrMagnitude < 0.0001f) return;
            float sway = Mathf.Sin(Time.time * (4f + speed * 2f) + _phase) * wiggle * Mathf.Clamp01(_velocity.magnitude);
            Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, sway, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look * _pose, dt * rotationSpeed * 2f);
        }

        // "Up" across the body as placed, so the creature keeps the roll it was posed with.
        private Vector3 BackUp(Vector3 head)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, head);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(transform.forward, head);
            return up.normalized;
        }

#if UNITY_EDITOR
        public void EditorSetup(float swimSpeed, float radius, float lowest, float highest)
        {
            speed = swimSpeed;
            roamRadius = radius;
            minHeight = lowest;
            maxHeight = highest;
        }

        public void EditorSetupPosed(Vector3 head, bool onlyYaw, float bob)
        {
            headAxis = head;
            yawOnly = onlyYaw;
            bobbingAmount = bob;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.7f, 0.5f);
            Gizmos.DrawWireSphere(Application.isPlaying ? _home : transform.position, roamRadius);
        }
#endif
    }
}
