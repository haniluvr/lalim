using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Brings the level model's baked-in sea life to life: the fish, rays, turtles and whale sharks
    /// placed in Blender are plain meshes, so on their own they hang still in the water. This finds
    /// them by name and swims each in a slow loop around where it was placed, turning along its
    /// path with a little tail sway and bob. Only animals near the camera move (the rest wait),
    /// so hundreds of them cost little.
    ///
    /// The meshes stand upright with their length along local Z and the head toward +Z.
    /// </summary>
    public class AmbientSwimmers : MonoBehaviour
    {
        [Tooltip("Name fragments of the baked animals to animate (case-insensitive).")]
        [SerializeField] private string[] _nameContains = { "fish_X_", "ray_X_", "turtle_X_", "whaleshark" };
        [Tooltip("Only animals this close to the camera swim; the rest hold until the camera comes near.")]
        [SerializeField, Min(10f)] private float _activeRange = 90f;
        [SerializeField] private bool _headTowardMinusZ = false;

        private class Swimmer
        {
            public Transform T;
            public Vector3 Home;
            public float Radius, Speed, Angle, Dir, Phase, Bob, Wiggle;
        }

        private readonly List<Swimmer> _swimmers = new List<Swimmer>();

        private void Start()
        {
            foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name.ToLowerInvariant();
                bool match = false;
                foreach (string part in _nameContains) if (n.Contains(part.ToLowerInvariant())) { match = true; break; }
                if (!match) continue;
                Transform t = r.transform;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                float length = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.z * t.lossyScale.z : 1f;
                bool big = n.Contains("whaleshark");
                bool slow = big || n.Contains("turtle") || n.Contains("ray_x_");
                var s = new Swimmer
                {
                    T = t,
                    Radius = big ? Random.Range(20f, 30f) : slow ? Random.Range(5f, 9f) : Mathf.Clamp(length * 3f, 1.5f, 5f) * Random.Range(0.8f, 1.3f),
                    Speed = big ? 1.6f : slow ? 0.9f : Mathf.Clamp(length * 1.4f, 0.7f, 2.4f) * Random.Range(0.8f, 1.2f),
                    Dir = Random.value < 0.5f ? 1f : -1f,
                    Phase = Random.value * 10f,
                    Bob = big ? 1.2f : slow ? 0.4f : 0.25f,
                    Wiggle = big ? 4f : slow ? 6f : 14f,
                };
                // Start on the loop where it stands now: the loop's centre sits beside it.
                s.Angle = Random.value * Mathf.PI * 2f;
                s.Home = t.position - new Vector3(Mathf.Cos(s.Angle), 0f, Mathf.Sin(s.Angle)) * s.Radius;
                _swimmers.Add(s);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _swimmers.Count == 0) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            float range2 = _activeRange * _activeRange;
            float now = Time.time;

            foreach (Swimmer s in _swimmers)
            {
                if (s.T == null) continue;
                if ((s.Home - eye).sqrMagnitude > range2) continue;
                s.Angle += s.Dir * s.Speed / s.Radius * dt;
                Vector3 pos = s.Home + new Vector3(Mathf.Cos(s.Angle), 0f, Mathf.Sin(s.Angle)) * s.Radius;
                pos.y = s.Home.y + Mathf.Sin(now * 0.7f + s.Phase) * s.Bob;
                Vector3 tangent = new Vector3(-Mathf.Sin(s.Angle), 0f, Mathf.Cos(s.Angle)) * s.Dir;
                float sway = Mathf.Sin(now * (4f + s.Speed * 3f) + s.Phase) * s.Wiggle;
                Quaternion face = Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0f, (_headTowardMinusZ ? 180f : 0f) + sway, 0f);
                s.T.SetPositionAndRotation(pos, face);
            }
        }
    }
}
