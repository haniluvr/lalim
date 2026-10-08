using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// The zone's ambience bed: a flat stereo loop under the music, on the Ambient volume. When the
    /// diver crosses into another zone the two beds crossfade over a few seconds; between zones (a
    /// trench) the last one carries on. Lives in the level scene, so it goes when the level does.
    /// The level director asks for the zone's clip (ZoneQuest's Ambience) as the diver moves.
    /// </summary>
    public class ZoneAmbience : MonoBehaviour
    {
        private const float FadeSeconds = 3f;
        private const float BaseVolume = 0.7f;

        private static ZoneAmbience _instance;

        private AudioSource _a, _b;       // _a is the current bed, _b the one fading out
        private float _mix = 1f;          // 0 → 1 as _a fades in over _b
        private float _out;               // 1 normally, 0 under a loading screen (fades in as a scene appears)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>Crossfade to this loop (nothing happens if it's already playing).</summary>
        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (_instance == null)
            {
                var go = new GameObject("[ZoneAmbience]");
                _instance = go.AddComponent<ZoneAmbience>();
            }
            _instance.Crossfade(clip);
        }

        private void Awake()
        {
            _a = NewSource();
            _b = NewSource();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }

        private AudioSource NewSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;    // ambience is never positional
            src.volume = 0f;
            return src;
        }

        private void Crossfade(AudioClip clip)
        {
            if (_a.clip == clip) return;
            // The current bed becomes the one fading out; the new one starts somewhere random so
            // revisiting a zone doesn't restart the loop from the top.
            AudioSource old = _a;
            _a = _b;
            _b = old;
            _a.clip = clip;
            _a.time = Random.value * Mathf.Max(0f, clip.length - 0.1f);
            _a.Play();
            _mix = _b.isPlaying ? 0f : 1f;
        }

        private void Update()
        {
            _mix = Mathf.MoveTowards(_mix, 1f, Time.unscaledDeltaTime / FadeSeconds);
            // The loading screen has no ambience: fade out while it comes in over the scene.
            bool loading = SceneLoader.Instance != null && SceneLoader.Instance.IsLoading;
            _out = Mathf.MoveTowards(_out, loading ? 0f : 1f, Time.unscaledDeltaTime / 0.8f);
            float gain = BaseVolume * GameAudio.Gain(AudioChannel.Ambient) * _out;
            _a.volume = gain * GameAudio.ClipGain(_a.clip) * Mathf.Sin(_mix * Mathf.PI * 0.5f);            // equal-power crossfade
            _b.volume = gain * GameAudio.ClipGain(_b.clip) * Mathf.Cos(_mix * Mathf.PI * 0.5f);
            if (_mix >= 1f && _b.isPlaying) _b.Stop();
        }
    }
}
