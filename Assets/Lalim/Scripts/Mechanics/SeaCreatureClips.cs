using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Which of each rigged sea creature's clips to play: its swim and its idle. Lives at
    /// Resources/SeaCreatureClips so creatures spawned at runtime (schools, ghost-net animals) can
    /// look theirs up by model name. Built by Tools ▸ Lalim ▸ Use Rigged Sea Creatures.
    /// </summary>
    public class SeaCreatureClips : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string model;
            public AnimationClip swim;
            public AnimationClip idle;
        }

        public const string ResourcePath = "SeaCreatureClips";

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        private static SeaCreatureClips _instance;
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _instance = null; _loaded = false; }

        /// <summary>The model's idle (or swim, if it has no idle) or swim clip; null if it isn't listed.</summary>
        public static AnimationClip Find(string model, bool idle)
        {
            if (!_loaded) { _instance = Resources.Load<SeaCreatureClips>(ResourcePath); _loaded = true; }
            if (_instance == null || string.IsNullOrEmpty(model)) return null;
            model = model.Replace("(Clone)", "").Trim();
            foreach (Entry e in _instance._entries)
            {
                if (e == null || e.model != model) continue;
                return idle ? (e.idle != null ? e.idle : e.swim) : (e.swim != null ? e.swim : e.idle);
            }
            return null;
        }

#if UNITY_EDITOR
        public List<Entry> EditorEntries => _entries;
#endif
    }
}
