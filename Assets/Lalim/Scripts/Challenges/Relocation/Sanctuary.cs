using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Challenges.MoonRitual;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// A sanctuary for Minigame 3: a place where one kind of school belongs. Each belongs to a moon
    /// phase: its ring of beacons, a faint haze in its water and a soft light glow in that phase's
    /// colour, and it loops that phase's melody from its middle (3D, louder close by). A school of
    /// the same colour and song belongs here; three sanctuaries take residents only (decoys).
    /// Hidden until Bakunawa wakes. A school that comes home joins the residents, and the place
    /// keeps a glowing moon carving of its phase above it.
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

        [Header("Moon phase")]
        [Tooltip("0 New Moon … 7 Waning Crescent: its colour and its song (-1 = none, plain cyan).")]
        [SerializeField, Range(-1, 7)] private int _moonPhase = -1;
        [Tooltip("The phase's melody, looping from the middle of the sanctuary.")]
        [SerializeField] private AudioClip _song;
        [SerializeField, Range(0f, 1f)] private float _songVolume = 0.8f;
        [SerializeField, Min(10f)] private float _songRange = 60f;
        [Tooltip("An unlit see-through material (URP Unlit, transparent) for the haze and the moon carving; textured and tinted at runtime.")]
        [SerializeField] private Material _glowMaterial;

        private static readonly List<Sanctuary> _all = new List<Sanctuary>();
        public static IReadOnlyList<Sanctuary> All => _all;

        public string DisplayName => _displayName;
        public string Habitat => _habitat;
        public float Radius => _radius;
        public Vector3 Centre => transform.position;
        public bool IsOn { get; private set; }
        public int MoonPhase => _moonPhase;
        public Color GlowColor => _moonPhase >= 0 ? MoonPhases.ColorOf(_moonPhase) : BeaconColor;
        public AudioClip Song => _song;
        /// <summary>The song's source (the release sings along with it, in step).</summary>
        public AudioSource SongSource => _songSource;

        private Transform _visuals;
        private AudioSource _songSource;
        private Light _glow;
        private Transform _carving;
        private float _flash;
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
            if (!on)
            {
                if (_visuals != null) _visuals.gameObject.SetActive(false);
                if (ProximityMusic.Instance != null) ProximityMusic.Instance.SetHush(this, 0f);
                return;
            }
            if (_visuals != null) { _visuals.gameObject.SetActive(true); return; }
            _visuals = new GameObject("[Sanctuary]").transform;
            _visuals.SetParent(transform, false);
            BuildBeacons();
            BuildHaze();
            BuildSong();
            foreach (Resident r in _residents) if (r != null && r.model != null) AddSchool(r.model, r.count, r.length, r.modelYaw, Centre);
        }

        /// <summary>A school released here swims from <paramref name="from"/> into the sanctuary and stays.</summary>
        public void AddSchool(GameObject model, int count, float length, float modelYaw, Vector3 from)
        {
            if (_visuals == null) Show(true);
            for (int i = 0; i < Mathf.Max(1, count); i++) AddSwimmer(model, length, modelYaw, from + UnityEngine.Random.insideUnitSphere * 0.6f);
        }

        /// <summary>A school released here: each fish swims on from where it is now into the sanctuary.</summary>
        public void AddSchoolAt(GameObject model, float length, float modelYaw, IList<Vector3> positions)
        {
            if (_visuals == null) Show(true);
            foreach (Vector3 p in positions) AddSwimmer(model, length, modelYaw, p);
        }

        private void AddSwimmer(GameObject model, float length, float modelYaw, Vector3 at)
        {
            Transform t = SwimBody.Spawn(model, _visuals, length, modelYaw);
            t.position = at;
            _swimmers.Add(new Swimmer { T = t, Phase = UnityEngine.Random.value * 10f, Speed = UnityEngine.Random.Range(0.8f, 1.4f) * Mathf.Clamp(length * 2.5f, 0.6f, 2.5f), Height = UnityEngine.Random.Range(0.25f, 0.7f) });
        }

        /// <summary>A new see-through material showing this place's moon phase in its colour (the release's moon symbol); null if it has none.</summary>
        public Material MoonGlyphMaterial()
        {
            if (_moonPhase < 0 || _glowMaterial == null) return null;
            Material m = HazeMaterial(MoonPhases.Glyph(_moonPhase));
            Color c = GlowColor * 1.6f;
            c.a = 0.95f;
            m.SetColor("_BaseColor", c);
            return m;
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
                Tint(b, GlowColor);
            }
            // One light to carry the glow a long way through the water.
            _glow = new GameObject("BeaconGlow").AddComponent<Light>();
            _glow.transform.SetParent(_visuals, false);
            _glow.transform.position = Centre + Vector3.up * 2f;
            _glow.type = LightType.Point;
            _glow.color = GlowColor;
            _glow.range = _radius * 2.6f;
            _glow.intensity = 3.5f;
            _glow.shadows = LightShadows.None;
        }

        private static MaterialPropertyBlock _mpb;

        /// <summary>The beacons take the phase's colour (the shared glow material stays as it is).</summary>
        private static void Tint(GameObject beacon, Color c)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (Renderer r in beacon.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_EmissionColor", c * 2.2f);
                r.SetPropertyBlock(_mpb);
            }
        }

        private Material _hazeMaterial;

        private Material HazeMaterial(Texture2D tex)
        {
            if (_glowMaterial == null) return null;
            var m = new Material(_glowMaterial) { name = "SanctuaryGlow_" + name };
            m.SetTexture("_BaseMap", tex);
            return m;
        }

        /// <summary>A faint haze of the phase's colour drifting in the sanctuary's water.</summary>
        private void BuildHaze()
        {
            _hazeMaterial = HazeMaterial(MoonPhases.Disc);
            if (_hazeMaterial == null) return;
            Color c = GlowColor;
            _hazeMaterial.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.22f));   // the colour lives on the material
            var go = new GameObject("Haze");
            go.transform.SetParent(_visuals, false);
            go.transform.position = Centre;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = _radius * 0.75f;
            // Fade in and out by size (the plain unlit glow ignores particle colours).
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _hazeMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
        }

        /// <summary>
        /// The phase's melody, sung once from the middle as the diver comes near (heard from about
        /// 60 m, louder close by). It sings again only after they've gone well away and come back,
        /// and not at all once the homecoming is over (<see cref="SilenceAll"/>).
        /// </summary>
        private void BuildSong()
        {
            if (_song == null) return;
            var go = new GameObject("Song");
            go.transform.SetParent(_visuals, false);
            go.transform.position = Centre;
            _songSource = go.AddComponent<AudioSource>();
            _songSource.clip = _song;
            _songSource.loop = false;
            _songSource.playOnAwake = false;
            _songSource.spatialBlend = 1f;
            _songSource.rolloffMode = AudioRolloffMode.Linear;   // fully silent past the range
            _songSource.minDistance = Mathf.Max(4f, _radius * 0.6f);
            _songSource.maxDistance = _songRange;
            _songSource.dopplerLevel = 0f;
            GameAudio.Bind(_songSource, _songVolume, AudioChannel.Music);
        }

        private bool _sung;
        private static bool _silenced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _silenced = false;

        /// <summary>The homecoming is over: no place sings again (and any song still going fades out).</summary>
        public static void SilenceAll()
        {
            _silenced = true;
            foreach (Sanctuary s in _all) if (s._songSource != null && s._songSource.isPlaying) s.StartCoroutine(s.FadeSong());
        }

        private System.Collections.IEnumerator FadeSong()
        {
            AudioSource src = _songSource;
            float from = src.volume;
            for (float t = 0f; t < 1.5f && src != null; t += Time.deltaTime) { src.volume = from * (1f - t / 1.5f); yield return null; }
            if (src != null) src.Stop();
        }

        // Sung once as the diver comes within earshot; again only after they've gone well away.
        private void UpdateSong()
        {
            if (_songSource == null || _silenced) return;
            PlayerController pc = PlayerController.Instance;
            if (pc == null) return;
            float d = Vector3.Distance(pc.transform.position, Centre);
            if (!_sung && d <= _songRange * 0.85f)
            {
                _sung = true;
                _songSource.time = 0f;
                _songSource.Play();
            }
            else if (_sung && d > _songRange + 15f && !_songSource.isPlaying) _sung = false;
        }

        /// <summary>A school of its colour came home: the place flares, and keeps a moon carving of its phase.</summary>
        public void Celebrate()
        {
            _flash = 1f;
            if (_carving != null || _moonPhase < 0 || _glowMaterial == null) return;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "MoonCarving";
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(_visuals, false);
            q.transform.position = Centre + Vector3.up * Mathf.Min(4f, _radius * 0.4f);
            q.transform.localScale = Vector3.one * 2.4f;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = HazeMaterial(MoonPhases.Glyph(_moonPhase));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Color c = GlowColor * 1.6f;
            c.a = 0.95f;
            r.sharedMaterial.SetColor("_BaseColor", c);
            _carving = q.transform;
        }

        /// <summary>Nothing here matches the bag: the glow dips for a moment.</summary>
        public void Dim() => _flash = -0.6f;

        /// <summary>Its school is near but it isn't its turn: a small flicker of recognition.</summary>
        public void Stir() => _flash = Mathf.Max(_flash, 0.35f);

        private void OnDestroy()
        {
            if (_songSource != null) GameAudio.Unbind(_songSource);
            if (ProximityMusic.Instance != null) ProximityMusic.Instance.SetHush(this, 0f);
        }

        // The light breathes, flares on a homecoming, dips on a mismatch; the carving turns slowly to
        // face the camera; the level music ducks under the song near the place.
        private void UpdateGlow(float dt)
        {
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 0.8f);
            if (_glow != null) _glow.intensity = Mathf.Max(0.5f, 3.5f + 0.4f * Mathf.Sin(Time.time * 1.3f) + _flash * 4f);
            Camera cam = Camera.main;
            if (_carving != null && cam != null)
            {
                Vector3 look = _carving.position - cam.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) _carving.rotation = Quaternion.LookRotation(look);
                _carving.position = new Vector3(_carving.position.x, Centre.y + Mathf.Min(4f, _radius * 0.4f) + Mathf.Sin(Time.time * 0.8f) * 0.3f, _carving.position.z);
            }
            if (_songSource != null && ProximityMusic.Instance != null)
            {
                PlayerController pc = PlayerController.Instance;
                float d = pc != null ? Vector3.Distance(pc.transform.position, Centre) : float.MaxValue;
                float near = 1f - Mathf.Clamp01((d - _songSource.minDistance) / Mathf.Max(1f, _songRange - _songSource.minDistance));
                // The level music ducks only while the place is actually singing.
                ProximityMusic.Instance.SetHush(this, _songSource.isPlaying ? near * 0.7f : 0f);
            }
        }

        private class Swimmer
        {
            public Transform T;
            public float Phase, Speed, Height;
            public Vector3 Velocity;
        }

        private void Update()
        {
            if (!IsOn) return;
            float dt = Time.deltaTime;
            UpdateGlow(dt);
            UpdateSong();
            if (_swimmers.Count == 0) return;
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
            Color c = GlowColor;
            Gizmos.color = new Color(c.r, c.g, c.b, 0.5f);
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

        public void EditorSetMoon(int phase, AudioClip song, Material glowMaterial)
        {
            _moonPhase = phase;
            _song = song;
            _glowMaterial = glowMaterial;
        }
#endif
    }
}
