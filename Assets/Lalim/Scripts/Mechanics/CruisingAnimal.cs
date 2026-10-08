using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A big animal cruising open water far from the seabed (the whale sharks off the Padre Burgos
    /// wall): a slow, wide loop around where it was placed with a gentle rise and fall, always
    /// facing where it swims. WanderingMarineAnimal keeps near the seabed; this one doesn't care
    /// what's below. The model is a child facing +Z.
    /// </summary>
    public class CruisingAnimal : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float _radius = 30f;
        [SerializeField, Range(0.3f, 1f)] private float _squash = 0.6f;   // loop depth vs width
        [SerializeField, Min(0.1f)] private float _speed = 1.4f;
        [SerializeField, Min(0f)] private float _rise = 4f;
        [SerializeField] private bool _clockwise;

        private Vector3 _centre;
        private float _angle;
        private float _phase;

        private void Start()
        {
            _phase = Random.value * 10f;
            _angle = Random.value * Mathf.PI * 2f;
            _centre = transform.position - Offset(_angle);
        }

        private Vector3 Offset(float a)
        {
            Quaternion yaw = Quaternion.Euler(0f, _phase * 36f, 0f);
            return yaw * new Vector3(Mathf.Cos(a) * _radius, Mathf.Sin(a * 0.5f + _phase) * _rise, Mathf.Sin(a) * _radius * _squash);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _angle += (_clockwise ? -1f : 1f) * _speed / _radius * dt;
            Vector3 next = _centre + Offset(_angle);
            Vector3 dir = next - transform.position;
            transform.position = next;
            if (dir.sqrMagnitude > 1e-6f)
            {
                dir.y *= 0.3f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 1.5f);
            }
        }
    }
}
