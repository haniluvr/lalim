using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lalim.Core
{
    /// <summary>
    /// Marks decorative atmosphere (god rays, caustics, floating particles…) so the
    /// Settings ▸ Quality ▸ Ambient Effect row can switch it off on weaker phones.
    ///
    ///   C = post-processing and all ambient effects off
    ///   B = post-processing + effects with Min Tier B (e.g. caustics)
    ///   A = everything (e.g. god rays too)
    ///
    /// Add it by hand to anything decorative, or let TagKnownObjects find the usual names.
    /// It only hides renderers / lights / decals — gameplay objects are never touched.
    /// </summary>
    public class AmbientEffect : MonoBehaviour
    {
        [Tooltip("Lowest Ambient Effect tier that shows this: 1 = B, 2 = A.")]
        [Range(1, 2)] [SerializeField] private int _minTier = 2;

        /// <summary>Scene object names tagged automatically at load → min tier.</summary>
        private static readonly Dictionary<string, int> KnownNames = new Dictionary<string, int>
        {
            { "Caustics", 1 },
            { "GodRays", 2 },
        };

        private static readonly List<AmbientEffect> _all = new List<AmbientEffect>();
        private static int _tier = 2;

        private Renderer[] _renderers;
        private Light[] _lights;
        private Behaviour[] _decals;
        private bool[] _rendererWasOn, _lightWasOn, _decalWasOn;

        public int MinTier { get => _minTier; set { _minTier = Mathf.Clamp(value, 1, 2); Refresh(); } }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _lights = GetComponentsInChildren<Light>(true);
            var decals = new List<Behaviour>();
            foreach (Behaviour b in GetComponentsInChildren<Behaviour>(true))
                if (b.GetType().Name == "DecalProjector") decals.Add(b);
            _decals = decals.ToArray();

            _rendererWasOn = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) _rendererWasOn[i] = _renderers[i].enabled;
            _lightWasOn = new bool[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _lightWasOn[i] = _lights[i].enabled;
            _decalWasOn = new bool[_decals.Length];
            for (int i = 0; i < _decals.Length; i++) _decalWasOn[i] = _decals[i].enabled;
        }

        private void OnEnable()
        {
            _all.Add(this);
            Refresh();
        }

        private void OnDisable() => _all.Remove(this);

        public static void ApplyTier(int tier)
        {
            _tier = tier;
            for (int i = _all.Count - 1; i >= 0; i--)
                if (_all[i] != null) _all[i].Refresh();
        }

        /// <summary>Adds the component to known decorative objects in a freshly loaded scene.</summary>
        public static void TagKnownObjects(Scene scene)
        {
            if (!scene.IsValid()) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!KnownNames.TryGetValue(t.name, out int minTier)) continue;
                    if (t.GetComponent<AmbientEffect>() != null) continue;
                    var effect = t.gameObject.AddComponent<AmbientEffect>();
                    effect._minTier = minTier;
                    effect.Refresh();
                }
            }
        }

        private void Refresh()
        {
            if (_renderers == null) return;
            bool show = _tier >= _minTier;
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] != null) _renderers[i].enabled = show && _rendererWasOn[i];
            for (int i = 0; i < _lights.Length; i++) if (_lights[i] != null) _lights[i].enabled = show && _lightWasOn[i];
            for (int i = 0; i < _decals.Length; i++) if (_decals[i] != null) _decals[i].enabled = show && _decalWasOn[i];
        }
    }
}
