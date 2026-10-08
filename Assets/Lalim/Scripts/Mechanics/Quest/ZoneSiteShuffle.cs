using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Puts a zone's minigame areas in random places each playthrough: three spots are drawn from
    /// a pool of good ones (flat open sand, clear of rocks and coral, with room for the first-person
    /// view — found in the editor by Tools ▸ Lalim ▸ Level 1 ▸ Find Zone 2 Sites), and area A/B/C of
    /// every minigame listed goes to the same spot (zone 2: the water readings, then the coral
    /// replanting where the readings were). The draw is saved, so reloading keeps the places.
    /// LevelFlowDirector applies it before placing the player and starting the steps.
    /// </summary>
    [DisallowMultipleComponent]
    public class ZoneSiteShuffle : MonoBehaviour
    {
        [System.Serializable]
        public struct Site
        {
            public Vector3 ground;   // point on the sand
            public float yaw;        // facing that leaves the view point (behind) in open water
        }

        [Tooltip("Candidate spots (filled by the editor tool).")]
        [SerializeField] private List<Site> _sites = new List<Site>();
        [Tooltip("Area A/B/C of each minigame (same index = same spot).")]
        [SerializeField] private List<AreaStation> _firstStations = new List<AreaStation>();
        [SerializeField] private List<AreaStation> _secondStations = new List<AreaStation>();
        [Tooltip("The areas' checkpoints, A/B/C (moved along).")]
        [SerializeField] private List<Checkpoint> _checkpoints = new List<Checkpoint>();
        [Tooltip("Height of a station's pivot above the sand.")]
        [SerializeField] private float _stationHeight = 1.5f;
        [SerializeField] private float _checkpointHeight = 4f;
        [Tooltip("Picked spots are at least this far apart (metres).")]
        [SerializeField, Min(0f)] private float _minSpacing = 12f;

        public IReadOnlyList<Site> Sites => _sites;

        /// <summary>Called by LevelFlowDirector once the save is loaded, before anything is placed.</summary>
        public static void ApplyAll()
        {
            foreach (ZoneSiteShuffle s in FindObjectsByType<ZoneSiteShuffle>(FindObjectsInactive.Include)) s.Apply();
        }

        public void Apply()
        {
            int count = Mathf.Min(_firstStations.Count, Mathf.Max(_secondStations.Count, _firstStations.Count));
            if (_sites.Count < count || count == 0) return;

            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            int seed = save != null ? save.siteShuffleSeed : 0;
            if (seed == 0)
            {
                seed = Random.Range(1, int.MaxValue);
                if (save != null) save.siteShuffleSeed = seed;
            }

            List<Site> picked = Pick(seed, count);
            for (int i = 0; i < picked.Count; i++)
            {
                Place(i < _firstStations.Count ? _firstStations[i] : null, picked[i]);
                Place(i < _secondStations.Count ? _secondStations[i] : null, picked[i]);
                if (i < _checkpoints.Count && _checkpoints[i] != null)
                    _checkpoints[i].transform.position = picked[i].ground + Vector3.up * _checkpointHeight;
            }
        }

        // A random first spot, then each next one random among those far enough from the others.
        private List<Site> Pick(int seed, int count)
        {
            var rng = new System.Random(seed);
            var pool = new List<Site>(_sites);
            var picked = new List<Site>();
            float spacing = _minSpacing;
            while (picked.Count < count && pool.Count > 0)
            {
                var ok = new List<int>();
                for (int i = 0; i < pool.Count; i++)
                {
                    bool far = true;
                    foreach (Site p in picked) if (Flat(pool[i].ground - p.ground) < spacing) { far = false; break; }
                    if (far) ok.Add(i);
                }
                if (ok.Count == 0) { spacing *= 0.7f; continue; }   // crowded: relax a little
                int take = ok[rng.Next(ok.Count)];
                picked.Add(pool[take]);
                pool.RemoveAt(take);
            }
            return picked;
        }

        private void Place(AreaStation station, Site site)
        {
            if (station == null) return;
            station.transform.SetPositionAndRotation(site.ground + Vector3.up * _stationHeight, Quaternion.Euler(0f, site.yaw, 0f));
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

#if UNITY_EDITOR
        public void EditorSetup(List<Site> sites, List<AreaStation> first, List<AreaStation> second, List<Checkpoint> checkpoints)
        {
            _sites = sites;
            _firstStations = first;
            _secondStations = second;
            _checkpoints = checkpoints;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.8f, 0.8f);
            foreach (Site s in _sites)
            {
                Gizmos.DrawWireSphere(s.ground + Vector3.up * 0.2f, 0.6f);
                Gizmos.DrawLine(s.ground + Vector3.up, s.ground + Vector3.up + Quaternion.Euler(0f, s.yaw, 0f) * Vector3.forward * 1.5f);
            }
        }
#endif
    }
}
