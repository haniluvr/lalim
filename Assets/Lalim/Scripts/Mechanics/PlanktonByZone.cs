using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// The plankton that follows the camera takes on each zone's water as the diver travels the
    /// level west to east (Level 2: sparse bright motes in Caluya, dense brown silt in Ajuy, slow
    /// falling marine snow in Padre Burgos). Bands start at an x position; it eases between them,
    /// so crossing a trench blends one look into the next.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PlanktonByZone : MonoBehaviour
    {
        [Serializable]
        public class Band
        {
            public string name;
            [Tooltip("This band applies from here eastward (until the next band starts).")]
            public float fromX;
            [Min(0f)] public float rate = 30f;
            public Color colour = new Color(0.75f, 1f, 0.95f, 0.55f);
            public Vector2 size = new Vector2(0.02f, 0.06f);
            [Tooltip("Gravity on the motes: 0 drifts, a few thousandths sinks slowly (marine snow).")]
            [Min(0f)] public float sink;
        }

        [SerializeField] private List<Band> _bands = new List<Band>();
        [SerializeField, Min(0.1f)] private float _blendSeconds = 2.5f;

        private ParticleSystem _ps;
        private float _rate, _sink;
        private Color _colour;
        private Vector2 _size;
        private bool _started;

        private void Awake() => _ps = GetComponent<ParticleSystem>();

        private void Update()
        {
            if (_bands.Count == 0) return;
            Camera cam = Camera.main;
            float x = cam != null ? cam.transform.position.x : transform.position.x;
            Band band = _bands[0];
            foreach (Band b in _bands) if (x >= b.fromX) band = b;

            if (!_started)
            {
                _rate = band.rate; _colour = band.colour; _size = band.size; _sink = band.sink;
                _started = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 3f / _blendSeconds);
                _rate = Mathf.Lerp(_rate, band.rate, k);
                _colour = Color.Lerp(_colour, band.colour, k);
                _size = Vector2.Lerp(_size, band.size, k);
                _sink = Mathf.Lerp(_sink, band.sink, k);
            }

            ParticleSystem.MainModule main = _ps.main;
            main.startColor = _colour;
            main.startSize = new ParticleSystem.MinMaxCurve(_size.x, _size.y);
            main.gravityModifier = _sink;
            main.maxParticles = Mathf.Max(100, Mathf.CeilToInt(_rate * main.startLifetime.constantMax * 1.2f));
            ParticleSystem.EmissionModule emission = _ps.emission;
            emission.rateOverTime = _rate;
        }

#if UNITY_EDITOR
        public void EditorSetBands(List<Band> bands) => _bands = bands;
#endif
    }
}
