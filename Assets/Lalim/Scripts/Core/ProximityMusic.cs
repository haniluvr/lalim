using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Core
{
    /// <summary>
    /// Level BGM that doubles as navigation: the closer the player is to the current objective
    /// (ObjectiveTracker), the louder the music. It never goes silent, changes smoothly, ducks
    /// during dialogue and holds steady during first-person minigames.
    ///
    /// Two looping sources crossfade when the zone track changes (polluted → cleansed, or a new zone).
    /// An optional "arrival" stem fades in only when the player is very close.
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

        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _arrival;
        private bool _aIsCurrent = true;
        private float _crossfade = 1f; // 1 = fully on the current source

        private float _volume;
        private float _arrivalLevel;
        private bool _dialogueOpen;

        public float CurrentVolume => _volume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _a = CreateLoop("[BGM_A]");
            _b = CreateLoop("[BGM_B]");
            _arrival = CreateLoop("[BGM_Arrival]");
            _volume = _noObjectiveVolume;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

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

            _aIsCurrent = !_aIsCurrent;
            Current.clip = clip;
            Current.time = 0f;
            Current.Play();
            _crossfade = instant ? 1f : 0f;
        }

        private void Update()
        {
            float target = ComputeTargetVolume();
            float step = Time.unscaledDeltaTime / _smoothSeconds;
            _volume = Mathf.MoveTowards(_volume, target, step);

            if (_crossfade < 1f) _crossfade = Mathf.MoveTowards(_crossfade, 1f, Time.unscaledDeltaTime / _crossfadeSeconds);

            float gain = GameAudio.Gain(AudioChannel.Music);
            Current.volume = _volume * _crossfade * gain;
            Previous.volume = _volume * (1f - _crossfade) * gain;
            if (_crossfade >= 1f && Previous.isPlaying) Previous.Stop();

            if (_arrival.clip != null)
            {
                float d = ObjectiveTracker.Instance != null ? ObjectiveTracker.Instance.DistanceToTarget : float.PositiveInfinity;
                float arrive = Mathf.Clamp01(1f - (d - _nearDistance) / Mathf.Max(0.01f, _arrivalRadius - _nearDistance));
                bool allowed = !_dialogueOpen && !InFirstPersonMinigame();
                _arrivalLevel = Mathf.MoveTowards(_arrivalLevel, allowed ? arrive : 0f, step);
                _arrival.volume = _arrivalLevel * gain;
            }
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
