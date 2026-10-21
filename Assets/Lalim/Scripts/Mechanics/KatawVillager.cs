using System.Collections.Generic;
using UnityEngine;
using Lalim.UI;

namespace Lalim.Mechanics
{
    /// <summary>
    /// A Kataw of the root village: floats in place on its idle loop with a slow bob, and turns to
    /// watch the diver swim past. Quest Kataw (the elder, the pool teachers) carry a soft amber rim
    /// light so they read through the silt (design doc). Put it on the body, not on the object holding
    /// the talk framing (TalkPoint, TalkCamera), which must not turn.
    ///
    /// Villagers (not the quest Kataw, who talk through their StatueDialogueRouter) can be talked to:
    /// grown-ups say something about the village (what, depends on whether Ajuy is clean yet);
    /// children sing and dance in a ring round the diver and the dugong, and the children nearby
    /// join in.
    /// </summary>
    public class KatawVillager : MonoBehaviour, IInteractable, IInteractionIcon
    {
        [SerializeField, Min(0f)] private float _watchWithin = 10f;
        [SerializeField, Min(0f)] private float _bob = 0.18f;
        [SerializeField] private bool _questGiver;
        [SerializeField] private Color _rimColor = new Color(1f, 0.72f, 0.38f);

        [Header("Hum")]
        [Tooltip("A soft loop heard only near this Kataw (silent beyond the range).")]
        [SerializeField] private AudioClip _hum;
        [SerializeField, Min(2f)] private float _humRange = 14f;
        [SerializeField, Range(0f, 1f)] private float _humVolume = 0.6f;

        [Header("Talking (villagers only)")]
        [SerializeField] private bool _chatty = true;
        [SerializeField, Min(1f)] private float _danceSeconds = 8f;
        [SerializeField, Min(0.5f)] private float _danceRadius = 1.8f;
        [Tooltip("Children this close to the one you talk to join the dance.")]
        [SerializeField, Min(0f)] private float _danceJoinWithin = 14f;

        private AudioSource _humSource;
        private enum Kind { Adult, Elder, Child }
        private Kind _kind;
        private bool _male;
        private float _danceUntil = -1f;
        private float _danceAngle;
        private float _returnBlend = 1f;   // 0 just stopped dancing … 1 back home
        private Vector3 _dancePos;
        private AudioSource _song;
        private int _lastLine = -1;
        private static readonly List<KatawVillager> _all = new List<KatawVillager>();

        private bool CanTalk => _chatty && !_questGiver && GetComponentInParent<StatueDialogueRouter>() == null;
        private bool Dancing => _danceUntil > Time.time;

        private Vector3 _home;
        private float _homeYaw;
        private float _phase;
        private Light _rim;

        private void Start()
        {
            _home = transform.position;
            _homeYaw = transform.eulerAngles.y;
            _phase = Random.value * 10f;
            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null) anim.Play(anim.GetCurrentAnimatorStateInfo(0).shortNameHash, 0, Random.value);   // villagers don't bob in step
            if (_questGiver) AddRim();
            if (_hum != null) StartHum();

            string n = name;
            _kind = n.Contains("Child") ? Kind.Child : n.Contains("Old") || n.Contains("Elder") ? Kind.Elder : Kind.Adult;
            _male = !n.Contains("Female");
            if (CanTalk) AddTalkTrigger();
        }

        private void OnEnable() => _all.Add(this);
        private void OnDisable() => _all.Remove(this);

        /// <summary>A trigger round the body, so the diver can find (and tap Talk on) this Kataw.</summary>
        private void AddTalkTrigger()
        {
            var col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            col.center = transform.InverseTransformPoint(any ? b.center : transform.position + Vector3.up * 0.5f);
            col.radius = Mathf.Max(0.6f, any ? b.extents.y : 0.6f);
        }

        // ───────────────────────── Talking ─────────────────────────

        public InteractionIcon Icon => InteractionIcon.Chat;

        public string GetInteractionPrompt()
        {
            if (!CanTalk || Dancing) return "";
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return "";
            return "Talk";
        }

        public void Interact()
        {
            if (!CanTalk || Dancing) return;
            if (_kind == Kind.Child) { StartDance(); return; }
            if (DialogueManager.Instance == null || DialogueManager.Instance.IsOpen) return;
            FacePlayerToMe();
            DialogueManager.Instance.StartDialogue(new List<DialogueLine> { new DialogueLine { speakerName = SpeakerName, text = RandomLine() } });
        }

        /// <summary>The diver turns to whoever they're talking to (the camera follows behind).</summary>
        private void FacePlayerToMe()
        {
            PlayerController pc = PlayerController.Instance;
            if (pc == null) return;
            Vector3 to = transform.position - pc.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            pc.transform.rotation = Quaternion.LookRotation(to);
            pc.SyncFacingToTransform();
        }

        private string SpeakerName =>
            _kind == Kind.Elder ? (_male ? "Lolo Kataw" : "Lola Kataw") : (_male ? "Kataw Fisher" : "Kataw Weaver");

        private static readonly string[] AdultPolluted =
        {
            "The water's gone cloudy since the boats came. The little ones cough at night.",
            "Mind the roots, little diver. They hold this whole village together.",
            "We take only what we need. That is how Ajuy has always fed itself.",
            "The plastic tangles in the roots. We pull it out, but the tide keeps bringing more.",
            "Have you met Inang Sela? She has waited longer than any of us for the fish to come back.",
            "A dugong! It's been years since one swam this far into the roots.",
        };
        private static readonly string[] AdultClean =
        {
            "Look how clear it is! I can see all the way to the far pools.",
            "The fish are back in the roots. You did this, little diver.",
            "The little ones are swimming out to the pools again. Listen to them laugh.",
            "We'll fish carefully now, the way the Elder taught us. The way you learned too.",
        };
        private static readonly string[] ElderPolluted =
        {
            "When I was young, the roots were so full of fish you couldn't see the sand.",
            "The mangroves are our mothers. They shelter the young until they are ready for the open sea.",
            "Slow down, child. The sea is old; it is never in a hurry.",
            "The Elder worries. So do I. But you have kind eyes.",
        };
        private static readonly string[] ElderClean =
        {
            "I didn't think I'd see the water this clear again. Salamat, little one.",
            "Listen, the roots are singing again, just as they did when I was young.",
            "Now the young ones will grow up the way we did. That is the best gift of all.",
        };

        private string RandomLine()
        {
            bool clean = false;
            var flow = Quest.LevelFlowDirector.Instance;
            if (flow != null)
            {
                Quest.ZoneQuest zone = flow.ZoneAtPlayer();
                clean = zone != null && zone.IsCleansed;
            }
            string[] pool = _kind == Kind.Elder ? (clean ? ElderClean : ElderPolluted) : (clean ? AdultClean : AdultPolluted);
            int i = Random.Range(0, pool.Length);
            if (pool.Length > 1 && i == _lastLine) i = (i + 1) % pool.Length;   // not the same line twice running
            _lastLine = i;
            return pool[i];
        }

        // ───────────────────────── Children: sing and dance ─────────────────────────

        private void StartDance()
        {
            int ring = 0;
            var dancers = new List<KatawVillager> { this };
            foreach (KatawVillager v in _all)
                if (v != this && v != null && v._kind == Kind.Child && v.CanTalk && !v.Dancing
                    && (v._home - _home).sqrMagnitude < _danceJoinWithin * _danceJoinWithin && dancers.Count < 5)
                    dancers.Add(v);
            foreach (KatawVillager v in dancers) v.BeginDance(ring++ * 360f / dancers.Count, v == this);
        }

        private void BeginDance(float startAngle, bool sings)
        {
            _danceUntil = Time.time + _danceSeconds;
            _danceAngle = startAngle;
            _dancePos = transform.position;
            _returnBlend = 0f;
            AudioClip song = _hum;
            if (song == null) foreach (KatawVillager v in _all) if (v != null && v._hum != null) { song = v._hum; break; }
            if (sings && song != null)
            {
                if (_song == null)
                {
                    _song = gameObject.AddComponent<AudioSource>();
                    _song.loop = true;
                    _song.playOnAwake = false;
                    _song.spatialBlend = 0.6f;
                    _song.dopplerLevel = 0f;
                    _song.minDistance = 3f;
                    _song.maxDistance = 25f;
                    Lalim.Core.GameAudio.Bind(_song, 0.9f, Lalim.Core.AudioChannel.Ambient);
                }
                _song.clip = song;
                _song.pitch = 1.35f;   // a children's song: the village hum, sung higher and brighter
                _song.Play();
            }
        }

        /// <summary>Round and round the diver and the dugong, bobbing and twirling; then home.</summary>
        private bool UpdateDance(float dt)
        {
            PlayerController pc = PlayerController.Instance;
            if (Dancing && pc != null)
            {
                Vector3 centre = pc.transform.position;
                var dugong = Lalim.Abilities.DugongCompanion.Instance;
                if (dugong != null && (dugong.transform.position - centre).sqrMagnitude < 25f) centre = (centre + dugong.transform.position) * 0.5f;
                float left = _danceUntil - Time.time;
                float ease = Mathf.Clamp01((_danceSeconds - left) / 0.8f);   // swim out to the ring first
                _danceAngle += dt * 70f;
                float a = _danceAngle * Mathf.Deg2Rad;
                Vector3 ringPos = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (_danceRadius + 0.3f * Mathf.Sin(Time.time * 2.3f + _phase))
                                + Vector3.up * (0.4f + Mathf.Abs(Mathf.Sin(Time.time * 3.2f + _phase)) * 0.45f);
                _dancePos = Vector3.Lerp(_dancePos, ringPos, 1f - Mathf.Exp(-6f * dt));
                transform.position = Vector3.Lerp(transform.position, _dancePos, ease);
                // Facing along the ring, with a twirl now and then.
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                float twirl = Mathf.Repeat(Time.time * 0.6f + _phase, 3f) < 0.6f ? Mathf.Repeat(Time.time * 0.6f + _phase, 3f) / 0.6f * 360f : 0f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, twirl, 0f), 1f - Mathf.Exp(-8f * dt));
                return true;
            }
            if (_song != null && _song.isPlaying) _song.Stop();
            if (_returnBlend < 1f)
            {
                // Swim back home after the dance.
                _returnBlend = Mathf.MoveTowards(_returnBlend, 1f, dt / 1.5f);
                Vector3 homeNow = _home + Vector3.up * Mathf.Sin(Time.time * 0.8f + _phase) * _bob;
                transform.position = Vector3.Lerp(transform.position, homeNow, Mathf.SmoothStep(0f, 1f, _returnBlend));
                if (_returnBlend < 1f) return true;
            }
            return false;
        }

        private void StartHum()
        {
            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.clip = _hum;
            _humSource.loop = true;
            _humSource.playOnAwake = false;
            _humSource.spatialBlend = 1f;
            _humSource.rolloffMode = AudioRolloffMode.Linear;   // fully silent past the range
            _humSource.minDistance = 2f;
            _humSource.maxDistance = _humRange;
            _humSource.dopplerLevel = 0f;
            _humSource.time = Random.value * Mathf.Max(0f, _hum.length - 0.1f);   // villagers out of step
            Lalim.Core.GameAudio.Bind(_humSource, _humVolume, Lalim.Core.AudioChannel.Ambient);
            _humSource.Play();
        }

        private void OnDestroy()
        {
            if (_humSource != null) Lalim.Core.GameAudio.Unbind(_humSource);
            if (_song != null) Lalim.Core.GameAudio.Unbind(_song);
        }

        private void AddRim()
        {
            _rim = new GameObject("RimLight").AddComponent<Light>();
            _rim.transform.SetParent(transform, false);
            _rim.transform.localPosition = new Vector3(0.4f, 1.4f, 1.3f);   // in front, so the face reads
            _rim.type = LightType.Point;
            _rim.color = _rimColor;
            _rim.range = 4.5f;
            _rim.intensity = 2.6f;
            _rim.shadows = LightShadows.None;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (UpdateDance(dt)) return;
            transform.position = _home + Vector3.up * Mathf.Sin(Time.time * 0.8f + _phase) * _bob;

            float yaw = _homeYaw;
            PlayerController pc = PlayerController.Instance;
            if (pc != null)
            {
                Vector3 to = pc.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < _watchWithin * _watchWithin && to.sqrMagnitude > 0.01f) yaw = Quaternion.LookRotation(to).eulerAngles.y;
            }
            Quaternion want = Quaternion.Euler(0f, yaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 70f * dt);
            if (_rim != null) _rim.intensity = 2.4f + 0.3f * Mathf.Sin(Time.time * 1.7f + _phase);
        }

#if UNITY_EDITOR
        public void EditorSetup(bool questGiver) => _questGiver = questGiver;
        public void EditorSetHum(AudioClip hum) => _hum = hum;
#endif
    }
}
