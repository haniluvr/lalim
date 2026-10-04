using System.Collections.Generic;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Volume routing for the four settings channels (Music, Voice, SFX, Ambient), plus the
    /// Audio / Quality settings that shape sound:
    ///
    ///  - 3D Spatial Audio: on = sounds come from where they happen in the world;
    ///    off = everything plays flat (2D) at the listener.
    ///  - Headphone Optimization: on = sharp left/right placement; off = softer panning,
    ///    which sounds better through a phone's small speakers.
    ///  - SFX Quality (C/B/A): how many one-shot sounds may overlap (6 / 12 / 24).
    ///
    /// Use GameAudio.PlayAt / GameAudio.PlayOneShot for one-shots. For looping or long
    /// sources call GameAudio.Bind(source, baseVolume, channel), or add an AudioChannelSource.
    /// </summary>
    public static class GameAudio
    {
        private struct Bound
        {
            public AudioSource Source;
            public float BaseVolume;
            public AudioChannel Channel;
        }

        private struct Original
        {
            public float SpatialBlend;
            public float Spread;
        }

        private static readonly int[] MaxOneShotsPerTier = { 6, 12, 24 };
        private const float SpeakerSpread = 110f;

        private static readonly List<Bound> _bound = new List<Bound>();
        private static readonly List<AudioSource> _oneShots = new List<AudioSource>();
        private static readonly Dictionary<AudioSource, Original> _originals = new Dictionary<AudioSource, Original>();

        public static float Gain(AudioChannel channel) => SettingsManager.Active.Gain(channel);

        private static int MaxOneShots => MaxOneShotsPerTier[Mathf.Clamp(SettingsManager.Active.sfxQuality, 0, MaxOneShotsPerTier.Length - 1)];

        /// <summary>Fire-and-forget sound at a world position (replaces AudioSource.PlayClipAtPoint).</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, AudioChannel channel = AudioChannel.Sfx)
        {
            if (clip == null) return;
            float v = volume * Gain(channel);
            if (v <= 0f || !HasOneShotRoom()) return;

            GameSettings s = SettingsManager.Active;
            var go = new GameObject("OneShot " + clip.name);
            go.transform.position = s.spatialAudio ? position : ListenerPosition(position);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = v;
            src.spatialBlend = s.spatialAudio ? 1f : 0f;
            src.spread = s.headphoneOptimization ? 0f : SpeakerSpread;
            src.Play();
            _oneShots.Add(src);
            Object.Destroy(go, clip.length / Mathf.Max(0.01f, Mathf.Abs(src.pitch)) + 0.1f);
        }

        /// <summary>One-shot on an existing source (replaces source.PlayOneShot).</summary>
        public static void PlayOneShot(AudioSource source, AudioClip clip, float volume = 1f, AudioChannel channel = AudioChannel.Sfx)
        {
            if (source == null || clip == null) return;
            float v = volume * Gain(channel);
            if (v <= 0f || !HasOneShotRoom()) return;
            ApplySpatial(source);
            source.PlayOneShot(clip, v);
        }

        /// <summary>Tie a source's volume to a channel; call again to change its base volume.</summary>
        public static void Bind(AudioSource source, float baseVolume, AudioChannel channel)
        {
            if (source == null) return;
            Unbind(source);
            _bound.Add(new Bound { Source = source, BaseVolume = baseVolume, Channel = channel });
            source.volume = baseVolume * Gain(channel);
            ApplySpatial(source);
        }

        public static void Unbind(AudioSource source)
        {
            for (int i = _bound.Count - 1; i >= 0; i--)
                if (_bound[i].Source == null || _bound[i].Source == source) _bound.RemoveAt(i);
        }

        /// <summary>Called by SettingsManager whenever settings are applied.</summary>
        public static void RefreshBoundSources()
        {
            for (int i = _bound.Count - 1; i >= 0; i--)
            {
                Bound b = _bound[i];
                if (b.Source == null) { _bound.RemoveAt(i); continue; }
                b.Source.volume = b.BaseVolume * Gain(b.Channel);
                ApplySpatial(b.Source);
            }
        }

        // ─────────────────────────────────────────────

        /// <summary>Remembers the source's authored 3D settings, then applies Spatial / Headphone settings on top.</summary>
        private static void ApplySpatial(AudioSource source)
        {
            if (!_originals.TryGetValue(source, out Original o))
            {
                o = new Original { SpatialBlend = source.spatialBlend, Spread = source.spread };
                _originals[source] = o;
                if (_originals.Count > 256) PruneOriginals();
            }
            GameSettings s = SettingsManager.Active;
            source.spatialBlend = s.spatialAudio ? o.SpatialBlend : 0f;
            source.spread = s.headphoneOptimization ? o.Spread : Mathf.Max(o.Spread, SpeakerSpread);
        }

        private static bool HasOneShotRoom()
        {
            int playing = 0;
            for (int i = _oneShots.Count - 1; i >= 0; i--)
            {
                if (_oneShots[i] == null || !_oneShots[i].isPlaying) { _oneShots.RemoveAt(i); continue; }
                playing++;
            }
            return playing < MaxOneShots;
        }

        private static Vector3 ListenerPosition(Vector3 fallback)
        {
            var listener = Object.FindFirstObjectByType<AudioListener>();
            return listener != null ? listener.transform.position : fallback;
        }

        private static void PruneOriginals()
        {
            var dead = new List<AudioSource>();
            foreach (var kv in _originals) if (kv.Key == null) dead.Add(kv.Key);
            foreach (var d in dead) _originals.Remove(d);
        }
    }
}
