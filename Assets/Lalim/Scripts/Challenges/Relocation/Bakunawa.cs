using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Challenges.Gathering;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// Bakunawa, the moon-eating serpent of Padre Burgos (Minigame 3). It sleeps coiled on the
    /// abyss floor until the diver wakes it; then it roars and hunts the diver across the whole map.
    ///
    ///  - Chase: swims after the diver, a little faster than their normal swim, so it takes Surge,
    ///    Riptide, a gap narrower than its body (about 6 m), or cover to get away.
    ///  - Strike: close in, rear back (jaw open) for a moment, then lunge. A strike costs HP (Bubble
    ///    Barrier blocks it) and bursts the net-bag.
    ///  - The Radiant Conch played within reach calms it for a while: it drifts, glowing soft blue.
    ///  - After about a minute chasing outside Padre Burgos it hisses and swims home to its lair
    ///    (the rubber band), giving the diver a safe window. It rests there until the diver comes
    ///    back within its sense range.
    ///  - Watching (the moon ritual): coiled at home, head lifted toward the diver; it never strikes,
    ///    only stirs (a snap and a hiss) at a wrong note. Its glow cools as the moon comes back.
    ///  - Appeased at the end, it settles back into its coil.
    ///
    /// Sound: a roar to wake, a hiss when it gives up, a whoosh-and-snap strike; its body churns
    /// the water as it swims (3D, louder the faster it goes); it growls now and then when close on
    /// the hunt; a low settling growl when the conch calms it or it's appeased. While it hunts, the
    /// chase loop plays (louder as it closes in) and the level music ducks under it.
    /// </summary>
    [RequireComponent(typeof(BakunawaBody))]
    public class Bakunawa : MonoBehaviour, ITalkPrelude
    {
        public enum State { Asleep, Roaring, Chase, Strike, Recover, Calmed, Returning, Resting, Appeased, Watching, Greeting }

        [Header("Lair")]
        [Tooltip("Where it lies coiled (the abyss floor).")]
        [SerializeField] private Transform _lair;
        [Tooltip("Padre Burgos starts at this world X; chasing west of it starts the rubber-band clock.")]
        [SerializeField] private float _zoneMinX = 1300f;

        [Header("Hunting (the minigame sets these from its difficulty profile)")]
        [SerializeField, Min(1f)] private float _chaseSpeed = 6.2f;
        [SerializeField, Min(1f)] private float _cruiseSpeed = 4f;
        [SerializeField, Min(5f)] private float _senseRange = 45f;
        [SerializeField, Min(0f)] private float _strikeDamage = 20f;
        [SerializeField, Min(1f)] private float _calmSeconds = 10f;
        [SerializeField, Min(5f)] private float _returnAfterSeconds = 60f;
        [SerializeField, Min(5f)] private float _calmRange = 30f;
        [Tooltip("Seconds without seeing the diver (hidden, or behind rock) before it gives up and goes home.")]
        [SerializeField, Min(1f)] private float _loseAfterSeconds = 7f;

        [Header("Strike")]
        [SerializeField, Min(2f)] private float _strikeRange = 14f;
        [Tooltip("How far past the head's surface the jaws reach (the body's colliders keep the diver outside the head).")]
        [SerializeField, Min(0.5f)] private float _biteReach = 1.5f;
        [SerializeField, Min(0.1f)] private float _rearSeconds = 0.7f;
        [SerializeField, Min(0.1f)] private float _lungeSeconds = 0.9f;
        [SerializeField, Min(1f)] private float _lungeSpeedScale = 2.4f;
        [SerializeField, Min(0.5f)] private float _recoverSeconds = 4f;

        [Header("Navigation (it is huge: it takes its own way, high over the reef)")]
        [Tooltip("While the diver is far off it swims this far above them, over reefs and mangroves, then dives in.")]
        [SerializeField, Min(0f)] private float _cruiseLift = 10f;
        [Tooltip("Within this (flat) distance of the diver it stops cruising high and comes straight at them.")]
        [SerializeField, Min(5f)] private float _diveWithin = 28f;
        [Tooltip("The water's surface (its body stays under it).")]
        [SerializeField] private float _surfaceY = 1f;
        [Tooltip("It won't swim where the water is shallower than this many body-thicknesses (the mangrove flats, the reef tops): the diver is safe there, and it gives up if they stay.")]
        [SerializeField, Min(0.5f)] private float _minDepthBodies = 1.15f;
        [Tooltip("Going home this far from the diver, out of view, it is simply back in its lair (no long swim back through the shallows).")]
        [SerializeField, Min(30f)] private float _homeOutOfSight = 80f;
        [Tooltip("Seconds it prowls the deep edge while the diver stays in shallows it can't enter, before it goes home.")]
        [SerializeField, Min(2f)] private float _shallowGiveUp = 8f;
        [Tooltip("Barely moving for this long while it means to swim: it is stuck.")]
        [SerializeField, Min(0.5f)] private float _stuckSeconds = 1.6f;
        [Tooltip("Stuck: for this long it forces its way through (roots, coral, ledges) straight at its goal.")]
        [SerializeField, Min(0.5f)] private float _squeezeSeconds = 3f;

        [Header("Prowling (awake, not yet appeased)")]
        [Tooltip("Awake and restless until appeased: it circles the abyss this far out from its lair…")]
        [SerializeField, Min(10f)] private float _prowlRadius = 55f;
        [Tooltip("…this high above the abyss floor (over the ritual's ring of circles).")]
        [SerializeField, Min(5f)] private float _prowlHeight = 40f;
        [SerializeField, Min(0.5f)] private float _prowlSpeed = 4.5f;

        [Header("Sound")]
        [SerializeField] private AudioClip _roarClip;
        [SerializeField] private AudioClip _hissClip;
        [SerializeField] private AudioClip _strikeClip;
        [Tooltip("Body churning through the water: variants played one after another while it swims (3D).")]
        [SerializeField] private AudioClip[] _swimClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float _swimVolume = 0.9f;
        [Tooltip("A short growl now and then while it hunts close by (3D).")]
        [SerializeField] private AudioClip _growlClip;
        [SerializeField] private Vector2 _growlEvery = new Vector2(7f, 14f);
        [SerializeField, Min(5f)] private float _growlWithin = 40f;
        [Tooltip("A low settling growl: calmed by the conch, or appeased.")]
        [SerializeField] private AudioClip _calmClip;
        [Tooltip("The chase music bed while it hunts (flat, louder as it closes in).")]
        [SerializeField] private AudioClip _chaseLoop;
        [SerializeField, Range(0f, 1f)] private float _chaseVolume = 0.85f;
        [Tooltip("The New Moon's melody: it hums from the lair while Bakunawa sleeps (the moon it swallowed).")]
        [SerializeField] private AudioClip _sleepHum;
        [SerializeField, Range(0f, 1f)] private float _sleepHumVolume = 0.7f;
        [SerializeField, Min(10f)] private float _sleepHumRange = 60f;

        /// <summary>A strike landed (harmed = it got through Bubble Barrier).</summary>
        public static event Action<bool> Struck;
        /// <summary>It gave up the chase and is heading home (the rubber band, or lost the diver).</summary>
        public static event Action WentHome;

        public static Bakunawa Instance { get; private set; }
        public State Current { get; private set; } = State.Asleep;
        public bool IsHunting => Current == State.Chase || Current == State.Strike || Current == State.Recover;
        /// <summary>Extra sense range: each school the diver carries makes them easier to sense.</summary>
        public float SenseBonus { get; set; }

        private BakunawaBody _body;
        private Vector3 _velocity;
        private float _stateUntil;
        private float _outsideZone;
        private float _unseenFor;
        private float _unreachableFor;
        private Vector3 _lungeTarget;
        private Renderer[] _glowRenderers;
        private MaterialPropertyBlock _mpb;
        private float _calm01;

        public void Configure(float chaseSpeed, float senseRange, float strikeDamage, float calmSeconds, float returnAfter)
        {
            _chaseSpeed = chaseSpeed;
            _senseRange = senseRange;
            _strikeDamage = strikeDamage;
            _calmSeconds = calmSeconds;
            _returnAfterSeconds = returnAfter;
        }

        private void Awake()
        {
            Instance = this;
            _body = GetComponent<BakunawaBody>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Start()
        {
            var glow = new System.Collections.Generic.List<Renderer>();
            // The glowing extras only (not the body itself: its skinned mesh keeps its own colours).
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (!(r is SkinnedMeshRenderer)) glow.Add(r);
            _glowRenderers = glow.ToArray();
            if (Current == State.Asleep) Sleep();
            BuildChaseAudio();
            if (_sleepHum != null)
            {
                _hum = new GameObject("SleepHum").AddComponent<AudioSource>();
                _hum.transform.position = LairPoint();
                _hum.clip = _sleepHum;
                _hum.loop = true;
                _hum.playOnAwake = false;
                _hum.spatialBlend = 1f;
                _hum.rolloffMode = AudioRolloffMode.Linear;
                _hum.minDistance = 10f;
                _hum.maxDistance = _sleepHumRange;
                _hum.dopplerLevel = 0f;
                _hum.volume = 0f;
            }
        }

        private AudioSource _hum;
        private float _humLevel;

        // ───────────────────────── Chase audio ─────────────────────────

        private AudioSource _swim, _chase;
        private float _swimLevel, _chaseLevel, _nextGrowl;
        private int _lastSwim = -1;

        private void BuildChaseAudio()
        {
            if (_swimClips != null && _swimClips.Length > 0)
            {
                _swim = gameObject.AddComponent<AudioSource>();   // rides on the head
                _swim.playOnAwake = false;
                _swim.loop = false;
                _swim.spatialBlend = 1f;
                _swim.rolloffMode = AudioRolloffMode.Linear;
                _swim.minDistance = 10f;
                _swim.maxDistance = 80f;
                _swim.dopplerLevel = 0f;
                _swim.volume = 0f;
            }
            if (_chaseLoop != null)
            {
                _chase = gameObject.AddComponent<AudioSource>();
                _chase.clip = _chaseLoop;
                _chase.loop = true;
                _chase.playOnAwake = false;
                _chase.spatialBlend = 0f;   // the music of the chase, in the head
                _chase.volume = 0f;
            }
        }

        private void UpdateChaseAudio(float dt, float dist)
        {
            float sfx = GameAudio.Gain(AudioChannel.Sfx);
            bool paused = Time.timeScale <= 0f;

            // The body: louder the faster it swims; one variant after another, never the same twice running.
            if (_swim != null)
            {
                float speed01 = Mathf.Clamp01(_velocity.magnitude / Mathf.Max(1f, _chaseSpeed));
                bool moving = Current != State.Asleep && Current != State.Watching && speed01 > 0.15f;
                _swimLevel = Mathf.MoveTowards(_swimLevel, moving ? Mathf.Lerp(0.35f, 1f, speed01) : 0f, dt * 1.5f);
                if (_swimLevel > 0.01f && !_swim.isPlaying && !paused)
                {
                    int i = _swimClips.Length > 1 ? (_lastSwim + 1 + UnityEngine.Random.Range(0, _swimClips.Length - 1)) % _swimClips.Length : 0;
                    _lastSwim = i;
                    _swim.clip = _swimClips[i];
                    _swim.Play();
                }
                if (_swim.clip != null) _swim.volume = _swimLevel * _swimVolume * sfx * GameAudio.ClipGain(_swim.clip);
                if (_swimLevel <= 0.01f && _swim.isPlaying) _swim.Stop();
            }

            // Hunting close by: a growl now and then.
            if (_growlClip != null && IsHunting && dist <= _growlWithin && Time.time >= _nextGrowl)
            {
                GameAudio.PlayAt(_growlClip, transform.position, 1f, AudioChannel.Sfx, 15f);
                _nextGrowl = Time.time + UnityEngine.Random.Range(_growlEvery.x, _growlEvery.y);
            }
            else if (!IsHunting) _nextGrowl = Mathf.Max(_nextGrowl, Time.time + 3f);   // not straight away when the hunt resumes

            // The chase bed: in while it hunts (louder the closer), out when it gives up or is calmed.
            if (_chase != null)
            {
                float close = 1f - Mathf.Clamp01((dist - 15f) / 120f);
                float target = IsHunting ? Mathf.Lerp(0.45f, 1f, close) : 0f;
                _chaseLevel = Mathf.MoveTowards(_chaseLevel, target, dt / (target > _chaseLevel ? 1.5f : 3f));
                _chase.volume = _chaseLevel * _chaseVolume * GameAudio.Gain(AudioChannel.Sfx) * GameAudio.ClipGain(_chaseLoop);
                bool audible = _chaseLevel > 0.001f && !paused;
                if (audible && !_chase.isPlaying) _chase.Play();
                else if (!audible && _chase.isPlaying) _chase.Pause();
                if (ProximityMusic.Instance != null) ProximityMusic.Instance.SetHush(this, _chaseLevel * 0.65f);
            }
        }

        // Asleep, the lair hums the New Moon; it fades out the moment Bakunawa wakes.
        private void UpdateHum(float dt)
        {
            if (_hum == null) return;
            _humLevel = Mathf.MoveTowards(_humLevel, Current == State.Asleep ? 1f : 0f, dt / 1.5f);
            _hum.volume = _humLevel * _sleepHumVolume * GameAudio.Gain(AudioChannel.Music) * GameAudio.ClipGain(_sleepHum);
            if (_humLevel > 0.001f && !_hum.isPlaying) _hum.Play();
            else if (_humLevel <= 0.001f && _hum.isPlaying) _hum.Stop();
        }

        private void OnDestroy()
        {
            if (ProximityMusic.Instance != null) ProximityMusic.Instance.SetHush(this, 0f);
            if (Instance == this) Instance = null;
            if (_hum != null) Destroy(_hum.gameObject);
        }
        private void OnEnable()
        {
            EventBus.Subscribe<RelicUsedEvent>(OnRelicUsed);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RelicUsedEvent>(OnRelicUsed);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private bool _sleepAfterTalk;

        /// <summary>Appeased and floating to talk: the shot looks up at its face.</summary>
        public bool TryGetTalkFocus(out Vector3 focus)
        {
            focus = transform.position;
            return _sleepAfterTalk && Current == State.Greeting;
        }

        // The end conversation is over (the gift and its farewell may follow one another): it
        // swims back down and coils to sleep.
        private void OnDialogueEnded(DialogueEndedEvent evt)
        {
            if (!_sleepAfterTalk || Current != State.Greeting) return;
            StartCoroutine(SleepAfterTalk());
        }

        private IEnumerator SleepAfterTalk()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (Lalim.UI.DialogueManager.Instance != null && Lalim.UI.DialogueManager.Instance.IsOpen) yield break;   // the next line of it began
            if (!_sleepAfterTalk || Current != State.Greeting) yield break;
            _sleepAfterTalk = false;
            _greetHolding = false;
            Current = State.Appeased;
            _body.SetJaw(0f);
            StartCoroutine(SettleHome());
        }

        // ───────────────────────── Story beats ─────────────────────────

        // ───────────────────────── The first talk ─────────────────────────

        [Header("First talk")]
        [Tooltip("How far in front of the diver's talking spot it comes to hover, and how far above it.")]
        [SerializeField, Min(2f)] private float _greetDistance = 10f;
        [SerializeField] private float _greetHeight = 2f;
        [Tooltip("Longest the swim up may take before the conversation opens anyway.")]
        [SerializeField, Min(1f)] private float _greetMaxSeconds = 6f;

        private Vector3 _greetPoint;
        private bool _greetHolding;

        /// <summary>Only the first time, while it still lies coiled: it wakes and comes to the diver.</summary>
        /// <summary>
        /// Talked to while it floats, appeased, over the abyss (at the end): it comes to face the
        /// diver for the conversation, then goes down and coils back to sleep.
        /// </summary>
        public bool PlayBeforeTalk => Current == State.Asleep || (Current == State.Appeased && _floating);

        /// <summary>
        /// The diver has come to the lair and tapped Talk: it stirs, unrolls from its coil and swims
        /// up to hover in front of where the diver will stand, facing them. The conversation then
        /// opens (the usual fade), and it stays there, watching, until the talk ends and it roars.
        /// </summary>
        public IEnumerator Prelude(Transform talkPoint)
        {
            PlayerController player = PlayerController.Instance;
            Vector3 stand = talkPoint != null ? talkPoint.position : (player != null ? player.transform.position : transform.position);
            Vector3 toward = talkPoint != null ? talkPoint.forward : transform.position - stand;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
            _greetPoint = stand + toward.normalized * _greetDistance + Vector3.up * _greetHeight;
            // Appeased, floating over the abyss: it stays up there and bows its head a few metres
            // toward the diver, so its face (the end of its path) turns to them.
            if (Current == State.Appeased && player != null)
            {
                Vector3 to = player.transform.position + Vector3.up * 0.6f - transform.position;
                _greetPoint = transform.position + to.normalized * Mathf.Min(4f, to.magnitude * 0.3f);
            }

            // Appeased, it rises calmly (no hiss, no shudder) and sleeps again once the talk is over.
            bool calm = Current == State.Appeased;
            _sleepAfterTalk = calm;
            Current = State.Greeting;
            _greetHolding = false;
            _coiled = false;
            _floating = false;
            _body.SetJaw(calm ? 0.05f : 0.25f);
            if (!calm)
            {
                GameAudio.PlayAt(_hissClip, transform.position, 0.6f, AudioChannel.Sfx, 40f);
                CameraController.Instance?.Shake(0.15f, 1.2f);
            }

            // The diver turns to watch it wake, and the camera looks past them at it (behind and
            // above the diver) until the conversation's fade takes over with its own shot.
            if (player != null)
            {
                Vector3 look = transform.position - player.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f) { player.transform.rotation = Quaternion.LookRotation(look); player.SyncFacingToTransform(); }
                if (CameraController.Instance != null)
                {
                    Vector3 diver = player.transform.position + Vector3.up * 0.6f;
                    Vector3 back = diver - Vector3.Lerp(transform.position, _greetPoint, 0.5f);
                    back.y = 0f;
                    back = back.sqrMagnitude > 0.01f ? back.normalized : -player.transform.forward;
                    var shot = new GameObject("[BakunawaWakeShot]").transform;
                    shot.position = diver + back * 7f + Vector3.up * 3f;
                    shot.rotation = Quaternion.LookRotation(Vector3.Lerp(diver, _greetPoint, 0.55f) - shot.position);
                    CameraController.Instance.SetShot(shot);
                    Destroy(shot.gameObject, _greetMaxSeconds + 6f);   // long since replaced by the talk shot
                }
            }

            // Rise off the floor first (it lifts out of the coil), then glide to the spot and stop there.
            Vector3 lift = calm ? _greetPoint : transform.position + Vector3.up * _body.Thickness * 1.2f;
            float t = 0f;
            while (t < _greetMaxSeconds)
            {
                t += Time.deltaTime;
                Vector3 goal = t < 0.9f ? lift : _greetPoint;
                GlideTo(goal, calm ? 1.5f : _cruiseSpeed * 1.8f, Time.deltaTime);
                if (t >= 0.9f && Vector3.Distance(transform.position, _greetPoint) < 0.6f && _velocity.magnitude < 0.8f) break;
                yield return null;
            }
            // Turn to look at the diver.
            _greetHolding = true;
            for (float s = 0f; s < 0.8f; s += Time.deltaTime) { FaceDiver(Time.deltaTime * 3f); yield return null; }
        }

        /// <summary>Swims to <paramref name="goal"/> and slows to a stop there (its chase steering keeps its momentum; this brakes).</summary>
        private void GlideTo(Vector3 goal, float maxSpeed, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 to = goal - pos;
            float dist = to.magnitude;
            // Ease off over the last few metres, so it arrives instead of overshooting.
            float speed = Mathf.Min(maxSpeed, dist * 1.2f);
            Vector3 desired = dist > 1e-3f ? to / dist * speed : Vector3.zero;
            _velocity = Vector3.MoveTowards(_velocity, desired, dt * Mathf.Max(4f, maxSpeed * 1.5f));
            Vector3 next = pos + _velocity * dt;
            float r = _body.Thickness * 0.5f;
            float floor = SwimBody.SeabedY(next, next.y + 20f);
            next.y = Mathf.Min(Mathf.Max(next.y, floor + r), _surfaceY - r);   // the surface wins
            transform.position = next;
            if (_velocity.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_velocity), dt * 3f);
        }

        /// <summary>Padre Burgos starts at this world X (where the diver comes out of Trench B).</summary>
        public float ZoneMinX => _zoneMinX;

        /// <summary>The head's centre (where the diver should look to meet its eyes).</summary>
        public Vector3 HeadPosition => transform.position;

        /// <summary>A deep growl heard over the whole area (the sea shaking as it wakes).</summary>
        public void Rumble(float volume)
        {
            AudioClip clip = _growlClip != null ? _growlClip : _hissClip;
            if (clip != null) GameAudio.PlayFlat(clip, volume);
        }

        // How far the head curls over toward the diver at the top: enough for the head bone (and the
        // neck behind it) to lie along the curl, however long the model's bones are.
        private float NeckBend => Mathf.Max(4f, _body.HeadBoneLength * 1.6f);

        /// <summary>
        /// Where its face stops for the first meeting: <paramref name="faceDistance"/> m in front of
        /// the diver (or further, so its neck rises clear of the cliff edge, over the open abyss) and
        /// <paramref name="faceHeight"/> m above them.
        /// </summary>
        public Vector3 MeetingPoint(Vector3 diver, Vector3 toward, float faceDistance, float faceHeight)
        {
            toward.y = 0f;
            toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
            // The edge: the first spot ahead where the floor drops well below the diver's floor.
            float floor0 = SwimBody.SeabedY(diver + toward, diver.y + 1f);   // (a step ahead: not the diver's own collider)
            float edge = faceDistance;
            for (float d = 2f; d <= 80f; d += 1f)
            {
                Vector3 p = diver + toward * d;
                if (SwimBody.SeabedY(p, diver.y + 1f) < floor0 - 12f) { edge = d; break; }
            }
            // Its neck (a body-thickness wide) must clear the lip; the head curls back NeckBend m from it.
            float neck = Mathf.Max(faceDistance + NeckBend, edge + _body.Thickness * 0.9f);
            return diver + toward * (neck - NeckBend) + Vector3.up * faceHeight;
        }

        /// <summary>
        /// The first meeting: it rises out of the abyss and comes to a stop face to face with the
        /// diver (standing at <paramref name="diver"/>, looking along <paramref name="toward"/>), at
        /// <see cref="MeetingPoint"/>. It climbs in a spiral from deep down, then straight up out of
        /// the dark leaning away from the cliff, and at the top its neck curls over so its face looks
        /// down at the diver (a cobra rearing). The body follows the path the head swam, so it hangs
        /// down into the abyss below the head. It then holds that pose, watching, until it roars.
        /// </summary>
        public IEnumerator RiseToFace(Vector3 diver, Vector3 toward, float faceDistance, float seconds, float faceHeight = 3f)
        {
            toward.y = 0f;
            toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            Vector3 face = MeetingPoint(diver, toward, faceDistance, faceHeight);
            List<Vector3> path = RisePath(diver, toward, side, face);

            transform.position = path[0];
            _body.CoilAt(path[0], toward);
            _velocity = Vector3.zero;
            Current = State.Greeting;
            _greetHolding = false;
            _greetPoint = face;
            _body.SetJaw(0.3f);

            // Arc lengths along the path, to swim it at a set pace: fast down in the dark, slowing
            // to a stop as it rears up in front of the diver.
            var along = new float[path.Count];
            for (int i = 1; i < path.Count; i++) along[i] = along[i - 1] + Vector3.Distance(path[i - 1], path[i]);
            float total = along[path.Count - 1];
            int seg = 0;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / seconds);
                float s = total * (1f - Mathf.Pow(1f - u, 2.2f));
                while (seg < path.Count - 2 && along[seg + 1] < s) seg++;
                float span = Mathf.Max(1e-4f, along[seg + 1] - along[seg]);
                Vector3 at = Vector3.Lerp(path[seg], path[seg + 1], Mathf.Clamp01((s - along[seg]) / span));
                Vector3 heading = at - transform.position;
                transform.position = at;
                if (heading.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(heading);
                yield return null;
            }
            transform.position = face;
            _velocity = Vector3.zero;
            _greetHolding = true;
            _body.SetJaw(0.35f);   // jaws a little open: it means it
            for (float s = 0f; s < 0.5f; s += Time.deltaTime) { FaceDiver(Time.deltaTime * 4f); yield return null; }
        }

        /// <summary>
        /// The rise as a line of points (deepest first): a spiral up from near the abyss floor, out
        /// clear of the cliff; a long, gently weaving climb that leans away from the cliff; and the
        /// curl over at the top, ending at <paramref name="face"/> heading for the diver's face.
        /// </summary>
        private List<Vector3> RisePath(Vector3 diver, Vector3 toward, Vector3 side, Vector3 face)
        {
            var path = new List<Vector3>();
            Vector3 eyes = diver + Vector3.up * 0.6f;
            Vector3 look = (eyes - face).normalized;   // its face ends looking down at the diver
            Vector3 neck = face + toward * NeckBend;
            Vector3 top = neck + Vector3.down * Mathf.Max(3f, NeckBend * 0.75f);   // where the straight climb ends and the curl starts

            // The climb: long enough that what the diver sees is all one rising column.
            const float Climb = 50f, LeanDegrees = 14f;
            float lean = LeanDegrees * Mathf.Deg2Rad;
            Vector3 rise = (Vector3.up * Mathf.Cos(lean) - toward * Mathf.Sin(lean)).normalized;   // up, and in toward the cliff
            Vector3 low = top - rise * Climb;

            // The spiral below it, from near the abyss floor; its turns stay out beyond the climb's foot.
            float deep = face.y - 140f;
            Vector3 below = low + toward * 20f;
            if (Physics.Raycast(below, Vector3.down, out RaycastHit floorHit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                deep = Mathf.Max(deep, floorHit.point.y + _body.Thickness * 1.5f);
            if (deep > low.y - 20f) deep = low.y - 20f;
            const float Radius = 12f, Turns = 2f, Out = 15f;
            const int SpiralSteps = 160;
            for (int i = 0; i <= SpiralSteps; i++)
            {
                float u = i / (float)SpiralSteps;
                float r = Radius * (1f - u);
                float a = u * Turns * Mathf.PI * 2f;
                // Ends heading straight up into the climb, at its foot.
                Vector3 centre = low + toward * (r + Out * (1f - u) * (1f - u));
                centre.y = Mathf.Lerp(deep, low.y, Mathf.SmoothStep(0f, 1f, u));
                path.Add(centre - toward * Mathf.Cos(a) * r + side * Mathf.Sin(a) * r);
            }

            // The climb, weaving a little from side to side as a swimming serpent does.
            const int ClimbSteps = 80;
            for (int i = 1; i <= ClimbSteps; i++)
            {
                float u = i / (float)ClimbSteps;
                float weave = Mathf.Sin(u * Mathf.PI * 2f) * 1.6f * Mathf.Sin(u * Mathf.PI);
                path.Add(Vector3.Lerp(low, top, u) + side * weave);
            }

            // The curl over: up out of the climb, then bending forward and down, and a last straight
            // run (a head bone long) onto the diver, so the head itself ends up looking at them.
            float head = Mathf.Max(1.5f, _body.HeadBoneLength * 1.1f);
            Vector3 end = face - look * head;
            float bend = NeckBend;
            Vector3 p0 = top, p1 = top + rise * bend * 1.5f, p2 = end - look * bend * 1.1f, p3 = end;
            const int CurlSteps = 40;
            for (int i = 1; i <= CurlSteps; i++)
            {
                float u = i / (float)CurlSteps, v = 1f - u;
                path.Add(v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3);
            }
            const int HeadSteps = 8;
            for (int i = 1; i <= HeadSteps; i++) path.Add(Vector3.Lerp(end, face, i / (float)HeadSteps));
            return path;
        }

        private void FaceDiver(float k)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            Vector3 to = player.transform.position - transform.position;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Mathf.Clamp01(k));
        }

        /// <summary>Holding in front of the diver while they talk: a slow sway, always facing them.</summary>
        private void HoverInGreeting(float dt)
        {
            // Calm (the end talk): perfectly still, so the held body and head stay as they are.
            if (_sleepAfterTalk) { _velocity = Vector3.zero; return; }
            // Only the head breathes: the body keeps the pose it rose in (see BakunawaBody.HoldShape).
            Vector3 sway = new Vector3(Mathf.Sin(Time.time * 0.5f) * 0.25f, Mathf.Sin(Time.time * 0.7f) * 0.2f, 0f);
            GlideTo(_greetPoint + sway, 0.6f, dt);
            FaceDiver(dt * 2f);
        }

        /// <summary>Coiled asleep on the abyss floor.</summary>
        public void Sleep()
        {
            Current = State.Asleep;
            _velocity = Vector3.zero;
            CoilAtLair();
        }

        /// <summary>The roar that wakes it: the jaw opens wide, the sea shakes, then the hunt begins.</summary>
        public IEnumerator Roar()
        {
            Current = State.Roaring;
            _body.SetJaw(1f);
            GameAudio.PlayAt(_roarClip, transform.position, 1f, AudioChannel.Sfx, 60f);
            CameraController.Instance?.Shake(0.6f, 2.4f);
            // It rears up out of its coil while it roars.
            Vector3 from = transform.position, up = from + Vector3.up * _body.Thickness * 3f;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(from, up, Mathf.SmoothStep(0f, 1f, t / 1.2f));
                yield return null;
            }
            if (Current != State.Roaring) yield break;   // something else took over meanwhile (e.g. the ritual)
            _body.SetJaw(0.15f);
            StartChase();
            _noStrikeUntil = Time.time + 5f;   // the diver gets a head start out of the abyss
        }

        private float _noStrikeUntil;

        /// <summary>Wakes already hunting (resuming a save after the roar): it starts at home, resting.</summary>
        public void WakeResting()
        {
            // Awake, it is never found lying in its lair: it is already out on its restless circuit.
            Vector3 lair = LairPoint();
            _prowlAngle = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 at = new Vector3(lair.x + Mathf.Cos(_prowlAngle) * _prowlRadius, lair.y + _prowlHeight, lair.z + Mathf.Sin(_prowlAngle) * _prowlRadius);
            transform.position = at;
            _body.CoilAt(at, new Vector3(-Mathf.Sin(_prowlAngle), 0f, Mathf.Cos(_prowlAngle)));
            _coiled = false;
            _velocity = Vector3.zero;
            Current = State.Resting;
            _stateUntil = Time.time + 3f;
        }

        /// <summary>Every school is home: it swims back to its lair to wait for the conch.</summary>
        public void GoHomeAndWait()
        {
            if (Current == State.Appeased) return;
            Current = State.Returning;
            _stateUntil = float.MaxValue;
        }

        /// <summary>
        /// The moon sung back at the end: the anger leaves it. It stops its prowling and floats,
        /// calm, over the middle of the abyss, waiting for the diver to come and talk; after that
        /// talk it goes down and coils to sleep (<see cref="SleepNow"/> on a later reload).
        /// </summary>
        public void Appease()
        {
            if (Current != State.Appeased) GameAudio.PlayAt(_calmClip, transform.position, 1f, AudioChannel.Sfx, 20f);
            Current = State.Appeased;
            _body.SetJaw(0f);
            _coiled = false;
            _floating = false;
            StopAllCoroutines();
            StartCoroutine(FloatAtCentre());
        }

        /// <summary>Appeased and already talked to (a reload after the end): asleep, coiled in its lair.</summary>
        public void SleepNow()
        {
            StopAllCoroutines();
            Current = State.Appeased;
            _body.SetJaw(0f);
            _floating = false;
            _sleepAfterTalk = false;
            CoilAtLair();
            _coiled = true;
        }

        [Tooltip("Appeased, it circles this high over its lair until the diver comes to talk…")]
        [SerializeField, Min(4f)] private float _floatHeight = 22f;
        [Tooltip("…in a slow ring at least this wide (and always well over its own length round, so its head never catches its tail).")]
        [SerializeField, Min(10f)] private float _floatRadius = 30f;
        [SerializeField, Min(0.5f)] private float _floatSpeed = 3f;
        [Tooltip("The diver this close to its head: it slows almost to a stop so they can talk.")]
        [SerializeField, Min(5f)] private float _slowNearDiver = 18f;
        private bool _floating;
        private Vector3 _floatPoint;
        private float _floatAngle;

        /// <summary>Where to talk to it: at its head.</summary>
        public Vector3 TalkSpot => transform.position;

        // Round enough that its whole body fits round the ring with a good gap before the tail.
        private float RingRadius => Mathf.Max(_floatRadius, _body.Length * 1.45f / (Mathf.PI * 2f));

        // To the ring over its lair (joining it where it's nearest), then circling there.
        private IEnumerator FloatAtCentre()
        {
            _floatPoint = LairPoint() + Vector3.up * _floatHeight;
            Vector3 flat = transform.position - _floatPoint; flat.y = 0f;
            _floatAngle = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.z, flat.x) : 0f;
            Vector3 join = _floatPoint + new Vector3(Mathf.Cos(_floatAngle), 0f, Mathf.Sin(_floatAngle)) * RingRadius;
            for (float t = 0f; t < 25f && Vector3.Distance(transform.position, join) > 3f; t += Time.deltaTime)
            {
                GlideTo(join, _cruiseSpeed, Time.deltaTime);
                yield return null;
            }
            _floating = true;
        }

        // Appeased: it keeps swimming, slowly, round and round over its lair (its body following
        // round the ring, rising and dipping a little), until the diver comes to talk.
        private void FloatInPlace(float dt)
        {
            // Slows almost to a stop while the diver is close to its head (so they can talk to it).
            float speed = _floatSpeed;
            PlayerController pc = PlayerController.Instance;
            if (pc != null)
            {
                float d = Vector3.Distance(pc.transform.position, transform.position);
                speed *= Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(_slowNearDiver * 0.5f, _slowNearDiver, d));
            }
            float radius = RingRadius;
            _floatAngle += dt * speed / radius;
            Vector3 goal = _floatPoint + new Vector3(Mathf.Cos(_floatAngle), 0f, Mathf.Sin(_floatAngle)) * radius
                         + Vector3.up * Mathf.Sin(_floatAngle) * 1.5f;
            GlideTo(goal + new Vector3(-Mathf.Sin(_floatAngle), 0f, Mathf.Cos(_floatAngle)) * 4f, speed + 1f, dt);
        }

        /// <summary>The moon ritual: it circles restlessly above the ring and watches the diver (never strikes).</summary>
        public void Watch()
        {
            if (Current == State.Appeased || Current == State.Watching) return;
            Current = State.Watching;
            Ritual01 = 0f;
            _body.SetJaw(0f);
            _coiled = false;
            StopAllCoroutines();
        }

        /// <summary>How far the ritual has come (0…1): its glow cools from ember toward soft blue.</summary>
        public float Ritual01 { get; set; }

        /// <summary>A wrong note in the ritual: a snap of the jaw, a hiss and a shudder through the sea.</summary>
        public void Stir()
        {
            if (Current != State.Watching) return;
            GameAudio.PlayAt(_hissClip, transform.position, 1f, AudioChannel.Sfx, 40f);
            CameraController.Instance?.Shake(0.25f, 0.6f);
            _calm01 = 0f;
            StartCoroutine(Snap());
        }

        private IEnumerator Snap()
        {
            _body.SetJaw(0.7f);
            yield return new WaitForSeconds(0.6f);
            if (Current == State.Watching) _body.SetJaw(0f);
        }

        /// <summary>The diver respawned at a checkpoint: it goes back to its lair.</summary>
        public void ResetToLair()
        {
            if (Current == State.Asleep || Current == State.Appeased || Current == State.Watching) return;
            WakeResting();
            // A fresh start after a death: it rests a while before it can sense the diver again,
            // and gives them a head start once it does.
            _stateUntil = Time.time + 10f;
            _noStrikeUntil = _stateUntil + 5f;
        }

        private void CoilAtLair()
        {
            Vector3 home = LairPoint();
            transform.position = home;
            _body.CoilAt(home, _lair != null ? _lair.forward : Vector3.forward);
        }

        private Vector3 LairPoint()
        {
            Vector3 p = _lair != null ? _lair.position : transform.position;
            float floor = SwimBody.SeabedY(p, p.y + 20f);
            return new Vector3(p.x, floor + _body.Thickness * 0.6f, p.z);
        }

        private bool _coiled;

        /// <summary>
        /// Home to sleep: it swims down and winds itself into its coil, a spiral inward over the
        /// lair floor, so the body lays the coil down as it follows (no jump into place).
        /// </summary>
        private IEnumerator SettleHome()
        {
            _coiled = false;
            Vector3 home = LairPoint();
            float gap = _body.BodyWidth * 1.08f;
            float rMin = _body.BodyWidth * 1.1f;
            // Big enough that the whole body fits in the coil, ring after ring.
            float r0 = Mathf.Sqrt(_body.Length * gap / Mathf.PI + rMin * rMin);
            Vector3 flat = transform.position - home; flat.y = 0f;
            float a0 = flat.sqrMagnitude > 0.01f ? Mathf.Atan2(flat.z, flat.x) : 0f;
            Vector3 radial = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            // Wind the way it's already swimming round.
            float sign = Vector3.Dot(Vector3.Cross(radial, transform.forward), Vector3.up) >= 0f ? 1f : -1f;

            // Down to the edge of the coil first.
            Vector3 entry = home + radial * r0 + Vector3.up * _body.Thickness;
            for (float t = 0f; t < 25f && Vector3.Distance(transform.position, entry) > 2.5f; t += Time.deltaTime)
            {
                Steer(entry, _cruiseSpeed, Time.deltaTime, false);
                yield return null;
            }

            // Then inward round the spiral, sinking to the floor, at a calm, steady pace.
            float turns = (r0 - rMin) / gap;
            const int Steps = 240;
            var path = new List<Vector3>(Steps + 1);
            Vector3 from = transform.position;
            for (int i = 0; i <= Steps; i++)
            {
                float u = i / (float)Steps;
                float a = a0 + sign * u * turns * Mathf.PI * 2f;
                float r = Mathf.Lerp(r0, rMin, u);
                Vector3 p = home + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                p.y = Mathf.Lerp(from.y, home.y, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 2.5f)));
                path.Add(i == 0 ? from : p);
            }
            const float Pace = 8.5f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], b = path[i];
                float len = Vector3.Distance(a, b);
                for (float d = 0f; d < len; d += Pace * Time.deltaTime)
                {
                    if ((b - a).sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(b - a), Time.deltaTime * 4f);
                    transform.position = Vector3.Lerp(a, b, d / Mathf.Max(0.001f, len));
                    yield return null;
                }
            }
            transform.position = path[path.Count - 1];
            _velocity = Vector3.zero;
            _coiled = true;
        }

        /// <summary>
        /// Awake and restless (between hunts, and through the ritual): a wide, weaving circuit high
        /// over the abyss, now and then cutting across or rising and dipping, its head turning to
        /// the diver when they're near. Watching the ritual it keeps up and out of the way of the
        /// circles below.
        /// </summary>
        private void Prowl(float dt, bool watching)
        {
            Vector3 lair = LairPoint();
            _prowlAngle += dt * _prowlSpeed / Mathf.Max(10f, _prowlRadius) * (0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 0.07f, 3.1f));
            float r = _prowlRadius * (0.75f + 0.45f * Mathf.PerlinNoise(Time.time * 0.05f, 7.3f));
            float y = lair.y + _prowlHeight + Mathf.Sin(Time.time * 0.23f) * 8f + (watching ? 6f : 0f);
            Vector3 goal = new Vector3(lair.x + Mathf.Cos(_prowlAngle) * r, y, lair.z + Mathf.Sin(_prowlAngle) * r);
            // Restless: it looks toward the diver while they're close.
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                Vector3 to = player.transform.position - transform.position;
                if (to.sqrMagnitude < 50f * 50f) goal += Vector3.ClampMagnitude(to, 12f) * 0.5f;
            }
            Steer(goal, _prowlSpeed, dt, false);
        }

        private float _prowlAngle;

        // ───────────────────────── Brain ─────────────────────────

        private void StartChase()
        {
            Current = State.Chase;
            _outsideZone = 0f;
            _unseenFor = 0f;
            _unreachableFor = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateGlow(dt);
            UpdateHum(dt);
            PushDiverOut();
            PlayerController diverPc = PlayerController.Instance;
            UpdateChaseAudio(dt, diverPc != null ? Vector3.Distance(transform.position, diverPc.transform.position) : float.MaxValue);
            // The body keeps its shape while only the head moves: reared up to talk, or lying coiled
            // (asleep, or watching the ritual) — so the head's small leans don't slowly unwind the coil.
            _body.HoldShape = (Current == State.Greeting && _greetHolding) || (Current == State.Appeased && _coiled) || Current == State.Asleep;
            // Talking (face to face, held in place): it keeps swimming where it is.
            _body.SwimInPlace = Current == State.Greeting && _greetHolding;
            if (Current == State.Appeased && _floating) { FloatInPlace(dt); return; }
            if (Current == State.Watching) { Prowl(dt, true); return; }
            if (Current == State.Greeting) { if (_greetHolding) HoverInGreeting(dt); return; }
            if (Current == State.Asleep || Current == State.Roaring || Current == State.Appeased) return;

            PlayerController player = PlayerController.Instance;
            Vector3 pos = transform.position;
            Vector3 diver = player != null ? player.transform.position : pos;
            float dist = Vector3.Distance(pos, diver);

            switch (Current)
            {
                case State.Chase:
                {
                    bool seen = player != null && !HidingZone.PlayerHidden && CanSee(diver);
                    _unseenFor = seen ? 0f : _unseenFor + dt;
                    if (diver.x < _zoneMinX) _outsideZone += dt;
                    // In water too shallow for it: it prowls the deep edge, then gives up.
                    _unreachableFor = TooShallow(diver) ? _unreachableFor + dt : 0f;
                    if (_outsideZone >= _returnAfterSeconds || _unseenFor >= _loseAfterSeconds || _unreachableFor >= _shallowGiveUp)
                    {
                        // It gives up. The diver has usually got far away by now, so the hiss
                        // carries (softer) instead of fading out with distance: they should hear it go.
                        if (dist <= 40f) GameAudio.PlayAt(_hissClip, pos, 1f, AudioChannel.Sfx, 40f);
                        else GameAudio.PlayFlat(_hissClip, 0.55f);
                        Current = State.Returning;
                        WentHome?.Invoke();
                        break;
                    }
                    if (seen && dist <= _strikeRange && Time.time >= _noStrikeUntil)
                    {
                        Current = State.Strike;
                        _stateUntil = Time.time + _rearSeconds;
                        _lungeTarget = Vector3.zero;
                        _body.SetJaw(1f);
                    }
                    Steer(diver, _chaseSpeed, dt, true);
                    break;
                }
                case State.Strike:
                    if (_lungeTarget == Vector3.zero)
                    {
                        // Rearing back: slow, lining up.
                        Steer(diver, _chaseSpeed * 0.25f, dt, true);
                        if (Time.time >= _stateUntil)
                        {
                            Vector3 through = diver - pos;
                            _lungeTarget = diver + (through.sqrMagnitude > 0.01f ? through.normalized : transform.forward) * 6f;
                            _stateUntil = Time.time + _lungeSeconds;
                            GameAudio.PlayAt(_strikeClip, pos, 1f, AudioChannel.Sfx, 30f);
                        }
                    }
                    else
                    {
                        Steer(_lungeTarget, _chaseSpeed * _lungeSpeedScale, dt, true, snap: true);
                        if (player != null && dist <= _body.HeadRadius + _biteReach) Bite(player);
                        else if (Time.time >= _stateUntil) Recover();
                    }
                    break;
                case State.Recover:
                    Steer(pos + (pos - diver).normalized * 20f + Vector3.up * 4f, _cruiseSpeed, dt, false);
                    if (Time.time >= _stateUntil) StartChase();
                    break;
                case State.Calmed:
                    Steer(pos + transform.forward * 5f, 1.2f, dt, false);
                    if (Time.time >= _stateUntil) StartChase();
                    break;
                case State.Returning:
                {
                    Vector3 home = LairPoint() + Vector3.up * _body.Thickness * 2f;
                    // Far out of the diver's sight, it doesn't thread its long way back through the
                    // shallows: it sinks into the deep and is home (coiled, resting) when they come.
                    // (Past half again that distance the water hides it even on screen.)
                    if (dist > _homeOutOfSight * 1.5f || (dist > _homeOutOfSight && !OnScreen(pos))) { WakeResting(); break; }
                    Steer(home, _cruiseSpeed * 1.3f, dt, false);
                    // Home: it doesn't settle (it is restless until appeased), it prowls the abyss.
                    // After the relocation (waiting for the ritual) it never hunts again.
                    if (Vector3.Distance(pos, home) < _prowlRadius + 10f) { Current = State.Resting; if (_stateUntil != float.MaxValue) _stateUntil = Time.time + 4f; }
                    break;
                }
                case State.Resting:
                    Prowl(dt, false);
                    // It wakes to the diver coming back within reach (carrying schools makes it easier).
                    if (Time.time >= _stateUntil && player != null && diver.x >= _zoneMinX && dist <= _senseRange + SenseBonus && !HidingZone.PlayerHidden)
                        StartChase();
                    break;
            }
        }

        // The body's colliders stop the diver swimming in, but a moving body doesn't push a
        // character controller: when it swims into the diver, ease them back out.
        private void PushDiverOut()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc == null || !cc.enabled) return;
            float depth = _body.Penetration(player.transform.position, cc.radius, out Vector3 outward);
            if (depth > 0.01f) cc.Move(outward * Mathf.Min(depth, 1.5f));
        }

        private bool CanSee(Vector3 diver)
        {
            Vector3 from = transform.position;
            Vector3 to = diver - from;
            if (to.magnitude > _senseRange * 3f + SenseBonus) return false;
            // Rock between us blocks its sight (it can't see into a cave or around a ridge).
            return !Physics.Raycast(from, to.normalized, out RaycastHit hit, to.magnitude - 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                   || hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        private void Bite(PlayerController player)
        {
            PlayerHealth health = PlayerHealth.Instance;
            bool harmed = false;
            if (health != null && !health.IsInvulnerable)
            {
                float before = health.Current;
                health.TakeDamage(_strikeDamage, DamageKind.Contact);   // Bubble Barrier may block it
                harmed = health.Current < before;
            }
            Vector3 away = player.transform.position - transform.position;
            away.y *= 0.3f;
            if (harmed) player.Knockback(away, 5f, 0.35f);
            CameraController.Instance?.Shake(harmed ? 0.5f : 0.2f, 0.5f);
            Struck?.Invoke(harmed);
            Recover();
        }

        private void Recover()
        {
            Current = State.Recover;
            _stateUntil = Time.time + _recoverSeconds;
            _body.SetJaw(0.15f);
        }

        private void OnRelicUsed(RelicUsedEvent evt)
        {
            if (evt.ItemId != GameConstants.Relics.RADIANT_CONCH || !IsHunting) return;
            PlayerController player = PlayerController.Instance;
            if (player == null || Vector3.Distance(player.transform.position, transform.position) > _calmRange) return;
            Current = State.Calmed;
            _stateUntil = Time.time + _calmSeconds;
            _body.SetJaw(0f);
            GameAudio.PlayAt(_calmClip, transform.position, 1f, AudioChannel.Sfx, 15f);
        }

        /// <summary>Within reach of the conch (the appeasement asks the diver to come this close).</summary>
        public bool InCalmRange(Vector3 p) => Vector3.Distance(p, transform.position) <= _calmRange * 1.5f;

        // ───────────────────────── Movement ─────────────────────────

        private void Steer(Vector3 goal, float speed, float dt, bool hunting, bool snap = false)
        {
            Vector3 pos = transform.position;
            float r = _body.Thickness * 0.5f;
            bool squeezing = Time.time < _squeezeUntil;
            Vector3 aim = snap ? goal : Route(goal, hunting, squeezing);
            Vector3 desired = Vector3.ClampMagnitude(aim - pos, 1f) * speed;
            if (!squeezing) desired += AvoidRock(pos, _velocity) * speed;
            float accel = snap ? speed * 3f : Mathf.Max(2f, speed * 0.7f);
            _velocity = Vector3.MoveTowards(_velocity, desired, dt * accel);
            Vector3 next = pos + _velocity * dt;
            // It doesn't swim up into water too shallow for its body: it stops at the deep edge
            // (it may always swim out of shallows into deeper water). A lunge carries it on.
            if (!snap && TooShallow(next) && WaterDepth(next) < WaterDepth(pos))
            {
                // Slide along the edge: keep the part of its swim that doesn't climb the shelf.
                Vector3 deeper = DeeperWay(pos);
                Vector3 slide = _velocity - deeper * Mathf.Min(0f, Vector3.Dot(_velocity, deeper));
                if (TooShallow(pos + slide * dt) && WaterDepth(pos + slide * dt) < WaterDepth(pos)) slide = deeper * 1.5f;
                _velocity = slide;
                next = pos + _velocity * dt;
            }
            // Its whole body stays off the seabed and under the surface (the surface wins: a
            // canopy of roots near the top must not push it out of the water). Forcing its way
            // through, roots and ledges don't count as seabed.
            if (!squeezing)
            {
                float floor = SwimBody.SeabedY(next, next.y + 20f);
                next.y = Mathf.Max(next.y, floor + r);
            }
            next.y = Mathf.Min(next.y, _surfaceY - r);
            transform.position = next;
            if (_velocity.sqrMagnitude > 0.05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_velocity), dt * 3f);
            CheckStuck(goal, speed, snap);
        }

        // ── Its own way ──
        // Far from its goal it rises into open water above the diver's route (over the reef and
        // the mangrove canopy) and comes down on them only when close. If the way ahead is
        // blocked for a body its size it picks a detour (over, or to one side); if it still makes
        // no headway it forces its way through for a few seconds rather than staying jammed.

        private Vector3 _detour;
        private bool _hasDetour;
        private float _detourUntil, _planAt;
        private Vector3 _stuckFrom;
        private float _stuckAt, _squeezeUntil;

        private Vector3 Route(Vector3 goal, bool hunting, bool squeezing)
        {
            Vector3 pos = transform.position;
            float r = _body.Thickness * 0.5f;
            Vector3 flat = goal - pos; flat.y = 0f;
            // Cruising high while far off (hunting, or on its way home across the map).
            if (flat.magnitude > (hunting ? _diveWithin : 40f))
                goal.y = Mathf.Min(goal.y + _cruiseLift, _surfaceY - r - 1f);
            if (squeezing) { _hasDetour = false; return goal; }

            if (_hasDetour && (Time.time > _detourUntil || (pos - _detour).sqrMagnitude < 36f)) _hasDetour = false;
            if (Time.time >= _planAt)
            {
                _planAt = Time.time + 0.4f;
                if (Clear(pos, goal)) _hasDetour = false;
                else if (!_hasDetour) PlanDetour(pos, goal);
            }
            return _hasDetour ? _detour : goal;
        }

        private void PlanDetour(Vector3 pos, Vector3 goal)
        {
            float r = _body.Thickness * 0.5f;
            Vector3 to = goal - pos;
            Vector3 fwd = new Vector3(to.x, 0f, to.z);
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            float top = _surfaceY - r - 1f;
            float reach = Mathf.Min(30f, to.magnitude);
            var tries = new[]
            {
                new Vector3(pos.x, top, pos.z) + fwd * reach * 0.5f,          // straight up over it
                pos + fwd * reach * 0.6f + side * 25f,                          // round one side…
                pos + fwd * reach * 0.6f - side * 25f,                          // …or the other
                new Vector3(pos.x, top, pos.z) + side * 30f,
                new Vector3(pos.x, top, pos.z) - side * 30f,
                pos - fwd * 15f + Vector3.up * 15f,                             // back out and up
            };
            float best = float.MaxValue;
            foreach (Vector3 c in tries)
            {
                Vector3 t = c;
                t.y = Mathf.Min(t.y, top);
                if (!Clear(pos, t)) continue;
                float score = Vector3.Distance(t, goal) + (Clear(t, goal) ? 0f : 40f);
                if (score < best) { best = score; _detour = t; }
            }
            if (best < float.MaxValue) { _hasDetour = true; _detourUntil = Time.time + 4f; }
        }

        private static bool OnScreen(Vector3 p)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > -0.2f && v.x < 1.2f && v.y > -0.2f && v.y < 1.2f;
        }

        private float WaterDepth(Vector3 p) => _surfaceY - SwimBody.SeabedY(p, _surfaceY + 1f);

        /// <summary>Too shallow here for its body to swim.</summary>
        private bool TooShallow(Vector3 p) => WaterDepth(p) < _body.Thickness * _minDepthBodies;

        /// <summary>The way toward deeper water from here (flat), for backing off a shallow edge.</summary>
        private Vector3 DeeperWay(Vector3 pos)
        {
            Vector3 best = Vector3.zero;
            float bestDepth = WaterDepth(pos);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float depth = WaterDepth(pos + d * 6f);
                if (depth > bestDepth) { bestDepth = depth; best = d; }
            }
            return best;
        }

        /// <summary>A body its size could swim straight from <paramref name="a"/> to <paramref name="b"/>.</summary>
        private bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return true;
            RaycastHit[] hits = Physics.SphereCastAll(a, _body.Thickness * 0.4f, d / len, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit h in hits)
                if (h.collider.GetComponentInParent<PlayerController>() == null && h.collider.GetComponentInParent<BakunawaBody>() == null) return false;
            return true;
        }

        /// <summary>Barely moving while it means to swim: it forces its way through for a while.</summary>
        private void CheckStuck(Vector3 goal, float speed, bool snap)
        {
            if (Time.time < _stuckAt) return;
            Vector3 pos = transform.position;
            bool meantToMove = !snap && speed >= 2f && Vector3.Distance(pos, goal) > 10f;
            if (meantToMove && Time.time >= _squeezeUntil && Vector3.Distance(pos, _stuckFrom) < speed * _stuckSeconds * 0.2f)
            {
                _squeezeUntil = Time.time + _squeezeSeconds;
                _hasDetour = false;
            }
            _stuckFrom = pos;
            _stuckAt = Time.time + _stuckSeconds;
        }

        // It can't fit where its body can't: a sphere the size of its body, cast ahead.
        private Vector3 AvoidRock(Vector3 pos, Vector3 velocity)
        {
            if (velocity.sqrMagnitude < 0.01f) return Vector3.zero;
            Vector3 dir = velocity.normalized;
            float look = 6f + velocity.magnitude;
            if (Physics.SphereCast(pos, _body.Thickness * 0.5f, dir, out RaycastHit hit, look, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerController>() == null)
            {
                Vector3 away = Vector3.ProjectOnPlane(hit.normal, dir).normalized + Vector3.up * 0.5f;
                return away * (1f - hit.distance / look) * 2f;
            }
            return Vector3.zero;
        }

        // Calm: the glowing scales cool from angry ember to soft blue.
        private void UpdateGlow(float dt)
        {
            if (_glowRenderers == null) return;
            bool calm = Current == State.Calmed || Current == State.Appeased || Current == State.Asleep || Current == State.Resting;
            float target = Current == State.Watching ? Mathf.Lerp(0.15f, 0.85f, Ritual01) : calm ? 1f : 0f;
            _calm01 = Mathf.MoveTowards(_calm01, target, dt * 0.8f);
            Color angry = new Color(1f, 0.45f, 0.25f), soft = new Color(0.45f, 0.8f, 1f);
            Color c = Color.Lerp(angry, soft, _calm01) * (Current == State.Strike ? 1.6f : 1f);
            foreach (Renderer r in _glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(_mpb);
            }
        }

#if UNITY_EDITOR
        public void EditorSetSleepHum(AudioClip hum) => _sleepHum = hum;

        public void EditorSetChaseAudio(AudioClip[] swim, AudioClip growl, AudioClip calm, AudioClip chaseLoop)
        {
            _swimClips = swim;
            _growlClip = growl;
            _calmClip = calm;
            _chaseLoop = chaseLoop;
        }

        public void EditorSetup(Transform lair, AudioClip roar, AudioClip hiss, AudioClip strike)
        {
            _lair = lair;
            _roarClip = roar;
            _hissClip = hiss;
            _strikeClip = strike;
        }
#endif
    }
}
