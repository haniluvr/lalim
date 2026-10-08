using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// The clips behind the shared cues (<see cref="Sfx"/>), with a volume each. Lives at
    /// Resources/Audio/SfxLibrary so any level can play them without wiring.
    /// </summary>
    [CreateAssetMenu(menuName = "Lalim/Audio/Sfx Library", fileName = "SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SfxCue cue;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        private Entry Find(SfxCue cue)
        {
            foreach (Entry e in entries) if (e != null && e.cue == cue) return e;
            return null;
        }

        public AudioClip Clip(SfxCue cue) => Find(cue)?.clip;
        public float Volume(SfxCue cue) => Find(cue)?.volume ?? 1f;
    }
}
