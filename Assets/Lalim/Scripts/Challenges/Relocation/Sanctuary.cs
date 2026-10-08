using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// A sanctuary for Minigame 3: a place where one kind of school belongs. Every sanctuary wears
    /// the same signature so it reads anywhere on the map: a ring of cyan beacons on the seabed
    /// (the only cyan glow in the level), and residents of the right kind already swimming inside,
    /// so the player sees who lives there before reading anything. Hidden until Bakunawa wakes.
    /// A school released here joins the residents.
    /// </summary>
    public class Sanctuary : MonoBehaviour
    {
        [Serializable]
        public class Resident
        {
            public GameObject model;
            [Min(1)] public int count = 3;
            [Min(0.05f)] public float length = 0.5f;
            public float modelYaw = 180f;
        }

        public static readonly Color BeaconColor = new Color(0.25f, 0.95f, 1f);

        [SerializeField] private string _displayName = "Sanctuary";
        [Tooltip("School keys (from the Ajuy catch) that belong here; empty = residents only, no relocated school.")]
        [SerializeField] private string[] _accepts = new string[0];
        [Tooltip("A few words on the habitat, shown when the player arrives (\"Seagrass bed, shallow light\").")]
        [SerializeField] private string _habitat = "";
        [SerializeField, Min(3f)] private float _radius = 12f;
        [SerializeField] private List<Resident> _residents = new List<Resident>();
        [SerializeField, Range(3, 9)] private int _beacons = 6;
        [Tooltip("The glowing anemone used for the beacons (a glowing sphere stands in if empty).")]
        [SerializeField] private GameObject _beaconModel;
        [SerializeField] private Material _beaconMaterial;

        private static readonly List<Sanctuary> _all = new List<Sanctuary>();
        public static IReadOnlyList<Sanctuary> All => _all;

        public string DisplayName => _displayName;
        public string Habitat => _habitat;
        public float Radius => _radius;
        public Vector3 Centre => transform.position;
        public bool IsOn { get; private set; }

        private Transform _visuals;
        private readonly List<Swimmer> _swimmers = new List<Swimmer>();

        private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
        private void OnDisable() => _all.Remove(this);

        public bool Accepts(string key) => Array.IndexOf(_accepts, key) >= 0;
        public bool TakesSchools => _accepts.Length > 0;

        /// <summary>Inside the sanctuary: within its radius, and not far above or below it.</summary>
        public bool Contains(Vector3 p)
        {
            Vector3 d = p - Centre;
            float flat = new Vector2(d.x, d.z).magnitude;
            return IsOn && flat <= _radius && Mathf.Abs(d.y) <= _radius * 0.8f;
        }

        public static Sanctuary At(Vector3 p)
        {
            foreach (Sanctuary s in _all) if (s.Contains(p)) return s;
            return null;
        }

        /// <summary>The nearest switched-on sanctuary that takes any of these schools.</summary>
        public static Sanctuary NearestFor(IEnumerable<string> keys, Vector3 from)
        {
            Sanctuary best = null;
            float bestD = float.MaxValue;
            foreach (Sanctuary s in _all)
            {
                if (!s.IsOn) continue;
                bool takes = false;
                foreach (string k in keys) if (s.Accepts(k)) { takes = true; break; }
                if (!takes) continue;
                float d = Vector3.Distance(from, s.Centre);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        /// <summary>Switches the sanctuary on: beacons light, residents swim in.</summary>
        public void Show(bool on)
        {
            if (on == IsOn) return;
            IsOn = on;
            if (!on) { if (_visuals != null) _visuals.gameObject.SetActive(false); return; }
            if (_visuals != null) { _visuals.gameObject.SetActive(true); return; }
            _visuals = new GameObject("[Sanctuary]").transform;
            _visuals.SetParent(transform, false);
            BuildBeacons();
            foreach (Resident r in _residents) if (r != null && r.model != null) AddSchool(r.model, r.count, r.length, r.modelYaw, Centre);
        }

        /// <summary>A school released here swims from <paramref name="from"/> into the sanctuary and stays.</summary>
        public void AddSchool(GameObject model, int count, float length, float modelYaw, Vector3 from)
        {
            if (_visuals == null) Show(true);
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                Transform t = SwimBody.Spawn(model, _visuals, length, modelYaw);
                t.position = from + UnityEngine.Random.insideUnitSphere * 0.6f;
                _swimmers.Add(new Swimmer { T = t, Phase = UnityEngine.Random.value * 10f, Speed = UnityEngine.Random.Range(0.8f, 1.4f) * Mathf.Clamp(length * 2.5f, 0.6f, 2.5f), Height = UnityEngine.Random.Range(0.25f, 0.7f) });
            }
        }

        private void BuildBeacons()
        {
            for (int i = 0; i < _beacons; i++)
            {
                float a = (i + 0.5f) / _beacons * Mathf.PI * 2f;
                Vector3 p = Centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * _radius * 0.85f;
                float floor = SwimBody.SeabedY(p, Centre.y + _radius);
                Vector3 spot = new Vector3(p.x, floor + 0.3f, p.z);
                Quaternion tilt = Quaternion.identity;
                if (floor < Centre.y - _radius * 1.5f)
                {
                    // A sanctuary on a cliff (no floor below): the beacons cling to the wall instead.
                    Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 from = Centre + Vector3.up * Mathf.Sin(a * 2f) * _radius * 0.3f;
                    if (Physics.Raycast(from, outward, out RaycastHit wall, _radius * 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        spot = wall.point + wall.normal * 0.2f;
                        tilt = Quaternion.FromToRotation(Vector3.up, wall.normal);
                    }
                    else spot = new Vector3(p.x, Centre.y - _radius * 0.5f, p.z);   // open water: float at the edge
                }
                GameObject b;
                if (_beaconModel != null)
                {
                    b = Instantiate(_beaconModel, _visuals);
                    foreach (Collider c in b.GetComponentsInChildren<Collider>()) Destroy(c);
                    b.transform.localScale *= 1.4f;
                }
                else
                {
                    b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    b.transform.SetParent(_visuals, false);
                    Destroy(b.GetComponent<Collider>());
                    b.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);
                }
                b.name = "Beacon";
                b.transform.position = spot;
                b.transform.rotation = tilt * Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                if (_beaconMaterial != null) foreach (Renderer r in b.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _beaconMaterial;
            }
            // One light to carry the cyan glow a long way through the water.
            var light = new GameObject("BeaconGlow").AddComponent<Light>();
            light.transform.SetParent(_visuals, false);
            light.transform.position = Centre + Vector3.up * 2f;
            light.type = LightType.Point;
            light.color = BeaconColor;
            light.range = _radius * 2.6f;
            light.intensity = 3.5f;
            light.shadows = LightShadows.None;
        }

        private class Swimmer
        {
            public Transform T;
            public float Phase, Speed, Height;
            public Vector3 Velocity;
        }

        private void Update()
        {
            if (!IsOn || _swimmers.Count == 0) return;
            float dt = Time.deltaTime;
            float floor = SwimBody.SeabedY(Centre, Centre.y + _radius);
            if (floor < Centre.y - _radius) floor = Centre.y - _radius * 0.5f;
            foreach (Swimmer s in _swimmers)
            {
                if (s.T == null) continue;
                // Slow loops around the middle, each at its own radius and height: settled, at home.
                float a = Time.time * s.Speed / (_radius * 0.6f) + s.Phase;
                float r = _radius * (0.35f + 0.35f * Mathf.Sin(s.Phase * 1.7f + Time.time * 0.05f));
                Vector3 want = new Vector3(Centre.x + Mathf.Cos(a) * r, Mathf.Lerp(floor + 1f, Centre.y + _radius * 0.3f, s.Height), Centre.z + Mathf.Sin(a) * r);
                Vector3 to = want - s.T.position;
                s.Velocity = Vector3.Lerp(s.Velocity, Vector3.ClampMagnitude(to, 3f) * 1.2f, dt * 1.5f);
                s.T.position += s.Velocity * dt;
                SwimBody.Face(s.T, s.Velocity, s.Phase, dt, 10f);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.25f, 0.95f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }

#if UNITY_EDITOR
        public void EditorSetup(string displayName, string habitat, string[] accepts, float radius, List<Resident> residents, GameObject beaconModel, Material beaconMaterial)
        {
            _displayName = displayName;
            _habitat = habitat;
            _accepts = accepts;
            _radius = radius;
            _residents = residents;
            _beaconModel = beaconModel;
            _beaconMaterial = beaconMaterial;
        }
#endif
    }
}
