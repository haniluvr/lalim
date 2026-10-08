using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Loudness matching: one gain per clip so every sound in a group sits at the same perceived
    /// level (the source files were mastered at very different levels). Each clip's gated RMS was
    /// measured (50 ms windows, near-silence ignored) against a target per group: effects -18,
    /// loops -22, ambience -26 and music -16 dBFS, so ambience sits under the effects and music
    /// stays on its own slider. Gains only turn clips down; a clip too quiet to reach its target
    /// was raised in its file instead. GameAudio applies it wherever a clip plays. A new clip is
    /// unmeasured (gain 1) until it gets an entry here. Lives at Resources/Audio/AudioLevels.
    /// </summary>
    public class AudioLevels : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public AudioClip clip;
            [Tooltip("Measured RMS level of the clip, dBFS.")]
            public float rmsDb;
            [Range(0f, 1f)] public float gain = 1f;
        }

        public List<Entry> entries = new List<Entry>();

        private Dictionary<AudioClip, float> _lookup;

        public float Gain(AudioClip clip)
        {
            if (clip == null) return 1f;
            if (_lookup == null)
            {
                _lookup = new Dictionary<AudioClip, float>();
                foreach (Entry e in entries) if (e != null && e.clip != null) _lookup[e.clip] = e.gain;
            }
            return _lookup.TryGetValue(clip, out float g) ? g : 1f;
        }

        private void OnValidate() => _lookup = null;
    }
}
