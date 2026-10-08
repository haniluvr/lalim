using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Core
{
    /// <summary>
    /// Level BGM that IS the navigation (audio instead of visual guidance):
    ///
    ///  - The track itself plays flat (2D) at full level, as level music always has — it can
    ///    never go missing, whatever the speakers or the 3D settings.
    ///  - Direction: on top of it, a 3D "guide" copy (in step) sits at the current objective
    ///    (ObjectiveTracker). When the objective changes it glides through the water to the new
    ///    one, so the player hears the music move and can follow it by ear.
    ///  - Distance: the closer the player is to the objective, the louder the music.
    ///  - Facing: stereo speakers can't tell front from back, so music coming from behind the
    ///    camera is muffled (low-pass) and a little quieter — turning toward it opens it up.
    ///
    /// It changes smoothly, ducks during dialogue and holds steady during first-person minigames.
    /// With no objective the guide comes back to the player (music without a direction). With the
    /// 3D Spatial Audio setting off, only the flat track plays.
    ///
    /// Two looping bed sources crossfade when the zone track changes (polluted → cleansed, or a new
    /// zone); the guide switches with them. An optional "arrival" stem fades in only when the
    /// player is very close.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProximityMusic : MonoBehaviour
    {
        public static ProximityMusic Instance { get; private set; }

        [Header("Track")]
        [SerializeField] private AudioClip _defaultTrack;
        [SerializeField] private AudioClip _arrivalStem;

        [Header("Distance → volume")]
        [SerializeField, Min(0f)] private float _nearDistance = 8f;
        [SerializeField, Min(1f)] private float _farDistance = 120f;
        [Tooltip("Volume when far away. Never 0, so the level always has music.")]
        [SerializeField, Range(0f, 1f)] private float _minVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _maxVolume = 1f;
        [Tooltip("x = closeness (0 far … 1 near), y = blend from min to max. Ease-in makes the last stretch swell.")]
        [SerializeField] private AnimationCurve _closenessCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0.4f), new Keyframe(1f, 1f, 2f, 0f));
        [Tooltip("Seconds to move between volume levels — no jumps on dashes or new objectives.")]
        [SerializeField, Min(0.05f)] private float _smoothSeconds = 1.5f;

        [Header("Context levels")]
        [SerializeField, Range(0f, 1f)] private float _noObjectiveVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _minigameVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float _dialogueDuck = 0.4f;

        [Header("Arrival stem")]
        [SerializeField, Min(0f)] private float _arrivalRadius = 15f;

        [Header("Crossfade")]
        [SerializeField, Min(0.1f)] private float _crossfadeSeconds = 3f;

        [Header("Guide (the music's direction)")]
        [Tooltip("Level of the 3D guide on top of the flat track (1 = as loud as it). Higher = clearer direction.")]
        [SerializeField, Range(0f, 1f)] private float _guideShare = 0.7f;
        [Tooltip("How fast the music travels to a new objective (m/s), so the player hears it move.")]
        [SerializeField, Min(1f)] private float _guideGlideSpeed = 18f;
        [Tooltip("Width of the guide's sound (degrees): 0 = a pin-point, wider = softer direction.")]
        [SerializeField, Range(0f, 180f)] private float _guideSpread = 20f;
        [Tooltip("Spread when Headphone Optimization is off (phone/laptop speakers): still clearly left or right.")]
        [SerializeField, Range(0f, 180f)] private float _guideSpeakerSpread = 50f;
        [Tooltip("Low-pass cutoff (Hz) when the music is straight behind the camera (muffled).")]
        [SerializeField, Min(200f)] private float _behindCutoff = 1200f;
        [Tooltip("Volume of the guide when it's straight behind the camera.")]
        [SerializeField, Range(0f, 1f)] private float _behindVolume = 0.6f;
        [Tooltip("Height above the objective's pivot the music comes from.")]
        [SerializeField] private float _guideHeight = 1.5f;

        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _arrival;
        private AudioSource _guide;
        private AudioLowPassFilter _guideFilter;
        private bool _guidePlaced;
        private float _syncTimer;
        private bool _aIsCurrent = true;
        private float _crossfade = 1f; // 1 = fully on the current source

        private float _volume;
        private float _arrivalLevel;
        private bool _dialogueOpen;

        public float CurrentVolume => _volume;

        /// <summary>The level's music is playing (it hands its track to MusicDirector as the level unloads).</summary>
        public bool IsPlaying => Current != null && Current.isPlaying && Current.clip != null;

        /// <summary>Where the music is coming from right now (gliding toward the objective).</summary>
        public Vector3 GuidePosition => _guide != null ? _guide.transform.position : transform.position;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _a = CreateLoop("[BGM_A]");
            _b = CreateLoop("[BGM_B]");
            _arrival = CreateLoop("[BGM_Arrival]");
            _guide = CreateLoop("[BGM_Guide]");
            _guide.transform.SetParent(null, true);   // moves through the world on its own
            _guide.rolloffMode = AudioRolloffMode.Custom;  // loudness is set here (distance → volume), the source only gives direction
            _guide.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
            _guide.minDistance = 1f;
            _guide.maxDistance = 1000f;
            _guide.dopplerLevel = 0f;
            _guideFilter = _guide.gameObject.AddComponent<AudioLowPassFilter>();
            _guideFilter.cutoffFrequency = 22000f;
            _volume = _noObjectiveVolume;
        }

        private void OnDestroy()
        {
            // The level is unloading: hand the track over so it plays on through the loading screen.
            if (Current != null && Current.isPlaying && Current.clip != null && !MusicDirector.Quitting)
                MusicDirector.Carry(Current.clip, Current.timeSamples, Mathf.Max(0.3f, _volume * _crossfade));
            if (Instance == this) Instance = null;
            if (_guide != null) Destroy(_guide.gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void Start()
        {
            if (_defaultTrack != null && Current.clip == null) PlayTrack(_defaultTrack, instant: true);
            if (_arrivalStem != null)
            {
                _arrival.clip = _arrivalStem;
                _arrival.Play();
            }
        }

        private AudioSource CreateLoop(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private AudioSource Current => _aIsCurrent ? _a : _b;
        private AudioSource Previous => _aIsCurrent ? _b : _a;

        /// <summary>Crossfade to a zone track (no-op if it's already playing).</summary>
        public void PlayTrack(AudioClip clip, bool instant = false)
        {
            if (clip == null || Current.clip == clip) return;

            // The first track of the level picks up the music carried in from the loading screen at
            // the very sample it's at, if it's the same track (seamless); otherwise that fades out.
            int carried = -1;
            if (!_tookOver)
            {
                _tookOver = true;
                carried = MusicDirector.TakeOver(clip, out float carriedLevel);
                if (carried >= 0) _volume = carriedLevel;
            }

            _aIsCurrent = !_aIsCurrent;
            Current.clip = clip;
            Current.timeSamples = Mathf.Max(0, carried);
            Current.Play();
            _crossfade = instant || carried >= 0 ? 1f : 0f;

            _guide.clip = clip;
            _guide.timeSamples = Current.timeSamples;
            _guide.Play();
        }

        private bool _tookOver;

        private void Update()
        {
            float target = ComputeTargetVolume();
            float step = Time.unscaledDeltaTime / _smoothSeconds;
            _volume = Mathf.MoveTowards(_volume, target, step);

            if (_crossfade < 1f) _crossfade = Mathf.MoveTowards(_crossfade, 1f, Time.unscaledDeltaTime / _crossfadeSeconds);

            float gain = GameAudio.Gain(AudioChannel.Music) * GameAudio.ClipGain(Current.clip);
            GameSettings settings = SettingsManager.Active;
            float guideShare = settings.spatialAudio ? _guideShare : 0f;   // flat audio: no guide
            Current.volume = _volume * _crossfade * gain;   // the flat track always plays at full level
            Previous.volume = _volume * (1f - _crossfade) * gain;
            if (_crossfade >= 1f && Previous.isPlaying) Previous.Stop();
            UpdateGuide(_volume * _crossfade * gain * guideShare, settings);

            if (_arrival.clip != null)
            {
                float d = ObjectiveTracker.Instance != null ? ObjectiveTracker.Instance.DistanceToTarget : float.PositiveInfinity;
                float arrive = Mathf.Clamp01(1f - (d - _nearDistance) / Mathf.Max(0.01f, _arrivalRadius - _nearDistance));
                bool allowed = !_dialogueOpen && !InFirstPersonMinigame();
                _arrivalLevel = Mathf.MoveTowards(_arrivalLevel, allowed ? arrive : 0f, step);
                _arrival.volume = _arrivalLevel * gain;
            }
        }

        // The guide glides to the objective (or back to the player without one) and stays in step with the bed.
        private void UpdateGuide(float volume, GameSettings settings)
        {
            Transform listener = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            Transform target = ObjectiveTracker.Instance != null ? ObjectiveTracker.Instance.CurrentTarget : null;
            Vector3? goal = target != null ? target.position + Vector3.up * _guideHeight
                          : listener != null ? listener.position : (Vector3?)null;
            if (goal.HasValue)
            {
                if (!_guidePlaced) { _guide.transform.position = goal.Value; _guidePlaced = true; }
                else _guide.transform.position = Vector3.MoveTowards(_guide.transform.position, goal.Value, _guideGlideSpeed * Time.deltaTime);
            }

            _guide.spatialBlend = settings.spatialAudio ? 1f : 0f;
            _guide.spread = settings.headphoneOptimization ? _guideSpread : _guideSpeakerSpread;

            // In front of the camera: open and full; behind it: muffled and softer.
            float front = 1f;
            Camera cam = Camera.main;
            if (settings.spatialAudio && cam != null && target != null)
            {
                Vector3 to = _guide.transform.position - cam.transform.position;
                if (to.sqrMagnitude > 0.01f) front = (Vector3.Dot(cam.transform.forward, to.normalized) + 1f) * 0.5f;   // 1 ahead … 0 behind
            }
            float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(_behindCutoff), Mathf.Log(22000f), front));
            _guideFilter.cutoffFrequency = Mathf.Lerp(_guideFilter.cutoffFrequency, cutoff, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            _guide.volume = volume * Mathf.Lerp(_behindVolume, 1f, front);

            // Keep it on the same beat as the bed (they're the same track).
            _syncTimer -= Time.unscaledDeltaTime;
            if (_syncTimer > 0f || Current.clip == null || !Current.isPlaying) return;
            _syncTimer = 2f;
            if (_guide.clip != Current.clip) { _guide.clip = Current.clip; _guide.Play(); }
            else if (!_guide.isPlaying) _guide.Play();
            if (Mathf.Abs(_guide.time - Current.time) > 0.05f) _guide.timeSamples = Current.timeSamples;
        }

        private float ComputeTargetVolume()
        {
            float v;
            if (InFirstPersonMinigame())
            {
                v = _minigameVolume;
            }
            else
            {
                float d = ObjectiveTracker.Instance != null ? ObjectiveTracker.Instance.DistanceToTarget : float.PositiveInfinity;
                v = float.IsInfinity(d) ? _noObjectiveVolume : VolumeForDistance(d);
            }
            return _dialogueOpen ? v * _dialogueDuck : v;
        }

        /// <summary>Distance → volume mapping (public for tests).</summary>
        public float VolumeForDistance(float distance)
        {
            float span = Mathf.Max(0.01f, _farDistance - _nearDistance);
            float closeness = 1f - Mathf.Clamp01((distance - _nearDistance) / span);
            float shaped = Mathf.Clamp01(_closenessCurve.Evaluate(closeness));
            return Mathf.Lerp(_minVolume, _maxVolume, shaped);
        }

        private static bool InFirstPersonMinigame() =>
            ChallengeManager.Instance != null && ChallengeManager.Instance.IsFirstPersonChallengeActive;

        private void OnDialogueStarted(DialogueStartedEvent evt) => _dialogueOpen = true;
        private void OnDialogueEnded(DialogueEndedEvent evt) => _dialogueOpen = false;
    }
}
