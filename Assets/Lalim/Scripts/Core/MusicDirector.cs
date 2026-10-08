using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lalim.Core
{
    /// <summary>
    /// Background music that carries across scene loads without restarting. It lives for the whole
    /// session and follows the rules in the MusicLibrary (Resources/Audio/MusicLibrary):
    ///
    ///  - New game: the loading screen starts the intro BGM, which plays on through name input, the
    ///    tutorial and the first teleport scene and into the loading screen after it, never
    ///    restarting (each of those scenes asks for the same track, so it just continues).
    ///  - Continue / Load: the first loading screen starts the BGM of the level being loaded.
    ///  - A loading screen keeps whatever was playing before it (a level's music, the teleport
    ///    scene's), seamlessly.
    ///  - Levels: the level's own music (ProximityMusic) takes the track over at the exact sample
    ///    it's at, and hands it back here when the level unloads, so level → loading is seamless.
    ///  - The main menu plays its own music; the carried music fades out there.
    ///
    /// Plays flat on the Music volume. Scene-wide ambience from the library (tutorial, teleport
    /// scene) is started through ZoneAmbience.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        private const float CrossfadeSeconds = 2.5f;
        private const float LevelTakeoverGrace = 1.5f;   // a level with no music of its own: fade out after this

        private static MusicDirector _instance;
        private static bool _quitting;

        private MusicLibrary _library;
        private AudioSource _a, _b;          // _a current, _b fading out
        private float _mix = 1f;
        private float _level = 1f;           // the carried track's level (a level hands over its own)
        private float _fadeOutUntil = -1f;   // fading the carry to silence
        private float _levelWaitUntil = -1f;
        private string _previousScene;

        public static MusicDirector Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[MusicDirector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MusicDirector>();
            Application.quitting += () => _quitting = true;
        }

        /// <summary>True while the app is shutting down (levels shouldn't hand their music over then).</summary>
        public static bool Quitting => _quitting;

        private void Awake()
        {
            _library = Resources.Load<MusicLibrary>("Audio/MusicLibrary");
            _a = NewSource();
            _b = NewSource();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private AudioSource NewSource()
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.ignoreListenerPause = true;   // keeps playing under the pause menu, like level music
            return src;
        }

        public AudioClip CurrentClip => _a.isPlaying && _fadeOutUntil < 0f ? _a.clip : null;

        // ───────────────────────── Scene rules ─────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MusicLibrary.SceneMusic music = _library != null ? _library.For(scene.name) : null;
            string previous = _previousScene;
            _previousScene = scene.name;
            if (music == null) return;   // not described: leave the music as it is

            switch (music.mode)
            {
                case MusicLibrary.Mode.Track:
                    Play(music.bgm);
                    if (music.ambience != null) ZoneAmbience.Play(music.ambience);
                    break;
                case MusicLibrary.Mode.KeepPrevious:
                    if (music.ambience != null) ZoneAmbience.Play(music.ambience);
                    break;
                case MusicLibrary.Mode.Loading:
                    // From the menu (or from silence) the loading screen opens on the music of
                    // where we're going; otherwise it just carries on. A level still loaded
                    // underneath (the loading screen crossfades in over it) is still playing its
                    // own music: it hands that over as it unloads (Carry), so wait for it.
                    bool fromMenu = previous == GameConstants.Scenes.MAIN_MENU || previous == GameConstants.Scenes.SPLASH;
                    bool levelBeneath = ProximityMusic.Instance != null && ProximityMusic.Instance.IsPlaying;
                    if (fromMenu || (CurrentClip == null && !levelBeneath))
                    {
                        string target = SceneLoader.Instance != null ? SceneLoader.Instance.TargetSceneName : null;
                        MusicLibrary.SceneMusic dest = target != null && _library != null ? _library.For(target) : null;
                        if (dest != null && dest.bgm != null) Play(dest.bgm);
                        else FadeOut();
                    }
                    break;
                case MusicLibrary.Mode.OwnMusic:
                    FadeOut();
                    break;
                case MusicLibrary.Mode.Level:
                    // The level's music takes over (TakeOver); if it has none, the carry fades.
                    _levelWaitUntil = Time.unscaledTime + LevelTakeoverGrace;
                    break;
            }
        }

        // ───────────────────────── Playback ─────────────────────────

        /// <summary>Crossfade to a track; nothing happens if it's already playing.</summary>
        public void Play(AudioClip clip, float level = 1f)
        {
            if (clip == null) return;
            _fadeOutUntil = -1f;
            _levelWaitUntil = -1f;
            _level = level;
            if (_a.isPlaying && _a.clip == clip) return;
            Swap();
            _a.clip = clip;
            _a.time = 0f;
            _a.Play();
            _mix = _b.isPlaying ? 0f : 1f;
        }

        private void FadeOut()
        {
            if (!_a.isPlaying) return;
            _fadeOutUntil = Time.unscaledTime + CrossfadeSeconds;
            _levelWaitUntil = -1f;
        }

        private void Swap()
        {
            AudioSource old = _a;
            _a = _b;
            _b = old;
        }

        /// <summary>
        /// A level's music starting <paramref name="clip"/>. If that's the track being carried, the
        /// level takes it over at the same sample (returned, with the level it was playing at) and
        /// the carry goes quiet; otherwise -1 and the carry fades out under the level's crossfade.
        /// </summary>
        public static int TakeOver(AudioClip clip, out float level)
        {
            level = 1f;
            MusicDirector d = _instance;
            if (d == null) return -1;
            d._levelWaitUntil = -1f;
            if (d._a.isPlaying && d._a.clip == clip && d._fadeOutUntil < 0f)
            {
                int samples = d._a.timeSamples;
                level = d._level;
                d._a.Stop();
                d._b.Stop();
                return samples;
            }
            d.FadeOut();
            return -1;
        }

        /// <summary>A level unloading hands its track back, to carry on through the loading screen.</summary>
        public static void Carry(AudioClip clip, int timeSamples, float level)
        {
            MusicDirector d = _instance;
            if (d == null || clip == null || _quitting) return;
            d._fadeOutUntil = -1f;
            d._levelWaitUntil = -1f;
            d._level = level;
            d._b.Stop();
            d._a.clip = clip;
            d._a.timeSamples = Mathf.Clamp(timeSamples, 0, Mathf.Max(0, clip.samples - 1));
            d._a.Play();
            d._mix = 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_levelWaitUntil > 0f && Time.unscaledTime >= _levelWaitUntil) { _levelWaitUntil = -1f; FadeOut(); }

            _mix = Mathf.MoveTowards(_mix, 1f, dt / CrossfadeSeconds);
            float fade = 1f;
            if (_fadeOutUntil > 0f)
            {
                fade = Mathf.Clamp01((_fadeOutUntil - Time.unscaledTime) / CrossfadeSeconds);
                if (fade <= 0f) { _a.Stop(); _b.Stop(); _fadeOutUntil = -1f; }
            }
            float gain = GameAudio.Gain(AudioChannel.Music) * _level * GameAudio.ClipGain(_a.clip);
            _a.volume = gain * fade * Mathf.Sin(_mix * Mathf.PI * 0.5f);
            _b.volume = GameAudio.Gain(AudioChannel.Music) * _level * GameAudio.ClipGain(_b.clip) * fade * Mathf.Cos(_mix * Mathf.PI * 0.5f);
            if (_mix >= 1f && _b.isPlaying) _b.Stop();
        }
    }
}
