using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Catching
{
    /// <summary>
    /// A clearing in Ajuy's roots where the net is thrown: a circle the fish stay inside, the
    /// Kataw who teaches here (and watches over it during the test), and a ledge/vantage point
    /// above it. Placed in the scene; SelectiveCatchingStep fills it with fish.
    /// </summary>
    public class RootPool : MonoBehaviour
    {
        [SerializeField] private string _poolName = "Root Pool";
        [SerializeField, Min(3f)] private float _radius = 11f;
        [Tooltip("The Kataw who teaches this pool's lesson and comments on throws here.")]
        [SerializeField] private StatueDialogueRouter _teacher;

        public string PoolName => _poolName;
        public float Radius => _radius;
        public Vector3 Centre => transform.position;
        public StatueDialogueRouter Teacher => _teacher;
        public string TeacherName => _teacher != null ? _teacher.DeityName : "Kataw";

        /// <summary>A point in the pool: <paramref name="ring"/> 0 = centre, 1 = edge, at <paramref name="degrees"/>.</summary>
        public Vector3 PointAt(float ring, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return Centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _radius * ring;
        }

        public bool Contains(Vector3 p, float margin = 0f)
        {
            Vector3 d = p - Centre;
            d.y = 0f;
            return d.magnitude <= _radius + margin;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.6f);
            const int seg = 40;
            for (int i = 0; i < seg; i++)
            {
                Gizmos.DrawLine(PointAt(1f, i * 360f / seg), PointAt(1f, (i + 1) * 360f / seg));
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(string poolName, float radius, StatueDialogueRouter teacher)
        {
            _poolName = poolName;
            _radius = radius;
            _teacher = teacher;
        }
#endif
    }
}
