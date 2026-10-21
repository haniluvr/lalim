using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Abilities;
using Lalim.Challenges.MoonRitual;
using Lalim.Challenges.Relocation;
using Lalim.Core;
using Lalim.DDA;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Padre Burgos, the second minigame: the moon ritual that appeases Bakunawa (3rd person).
    ///
    /// With every school home, Bakunawa coils in its lair and watches. Eight stone circles lie in a
    /// ring around it on the abyss floor, each carved with a moon phase, the phases shuffled among
    /// the circles. The diver swims into a circle's column and holds still a moment to "stand" in
    /// it: it lights and that phase's melody plays (stepping on the next one fades the last one
    /// out, so the diver sets the pace). The phases must be sung in the moon's order: New Moon,
    /// Waxing Crescent, First Quarter, Waxing Gibbous, Full Moon, Waning Gibbous, Last Quarter,
    /// Waning Crescent. A wrong circle sounds sour, every circle goes dark and Bakunawa stirs (it
    /// never strikes); the song starts again from New Moon. All eight: the circles flare, the
    /// lullaby loops over the abyss and Bakunawa is appeased.
    ///
    /// The hint is Spirit Sense, not a button: while it's active its guiding light and glow go to
    /// the next correct circle (its usual orb cost and cooldown keep it from being spammed).
    /// DDA knobs: phase names over the circles, reshuffling the phases after every restart, and a
    /// free pulse after enough restarts in a row. Metrics: time, wrong steps (errors), restarts and
    /// Spirit Sense uses. Only finishing is saved; quitting part way starts the song over.
    /// </summary>
    public class MoonPhaseRitualStep : QuestStep
    {
        private enum Phase { Travel, Ritual, Finale, Done }

        [Header("Cast")]
        [SerializeField] private Bakunawa _bakunawa;
        [Tooltip("The middle of the ring (Bakunawa's lair).")]
        [SerializeField] private Transform _centre;

        [Header("The circles")]
        [SerializeField, Min(10f)] private float _ringRadius = 30f;
        [SerializeField, Min(1f)] private float _circleRadius = 2.4f;
        [Tooltip("How high above the stone the diver still counts as standing in the circle.")]
        [SerializeField, Min(1f)] private float _columnHeight = 7f;
        [Tooltip("Seconds the diver holds still in a circle before it answers.")]
        [SerializeField, Min(0.1f)] private float _holdSeconds = 0.5f;
        [Tooltip("The circles appear (and the ritual starts) when the diver comes this close to the lair.")]
        [SerializeField, Min(20f)] private float _revealRange = 75f;
        [SerializeField] private Material _stoneMaterial;
        [SerializeField] private Material _glowMaterial;

        [Header("Music")]
        [Tooltip("The eight phase melodies, New Moon first.")]
        [SerializeField] private AudioClip[] _melodies = new AudioClip[MoonPhases.Count];
        [Tooltip("Bakunawa's lullaby: loops over the abyss once it is appeased.")]
        [SerializeField] private AudioClip _lullaby;
        [SerializeField, Range(0f, 1f)] private float _melodyVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _lullabyVolume = 0.9f;
        [Tooltip("The lullaby is full within this distance of the lair and fades out by twice it.")]
        [SerializeField, Min(20f)] private float _lullabyRange = 90f;

        [Header("Look")]
        [SerializeField] private TMP_FontAsset _font;

        [Header("DDA")]
        [SerializeField] private MinigameDifficultyProfile _profile;

        private Phase _phase;
        private Transform _root;
        private readonly List<MoonCircle> _circles = new List<MoonCircle>();
        private int _next;                // the phase to step on next (0 New Moon … 8 done)
        private MoonCircle _inside;       // the circle the diver is in now
        private MoonCircle _answered;     // the last circle that answered (must be left before it answers again)
        private float _held;
        private bool _resetting;
        private int _wrongSteps, _restarts, _restartsInARow, _senseUses;
        private string _objective;

        // Music: a few voices, so a new melody can start while the last one fades out.
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private readonly HashSet<AudioSource> _fading = new HashSet<AudioSource>();
        private AudioSource _lullabySource;
        private bool _lullabyOn;
        private float _lullabyLevel;

        // HUD: only the phase names over the circles (the hint is Spirit Sense).
        private Canvas _hud;
        private readonly List<TextMeshProUGUI> _labels = new List<TextMeshProUGUI>();

        private float Knob(string key, float fallback) => _profile != null && _profile.Has(key) ? _profile.Get(key) : fallback;
        private bool Labels => Knob(Level2DifficultyKnobs.RitualLabels, 0f) >= 0.5f;
        private bool Reshuffle => Knob(Level2DifficultyKnobs.RitualReshuffle, 0f) >= 0.5f;
        private int FreeHintAfter => Mathf.RoundToInt(Knob(Level2DifficultyKnobs.RitualFreeHintAfter, 0f));

        private Vector3 Centre => _centre != null ? _centre.position : (_bakunawa != null ? _bakunawa.transform.position : transform.position);

        protected override bool DefaultBoxOpen => false;

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<AbilityActivatedEvent>(OnAbility);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<AbilityActivatedEvent>(OnAbility);
            if (ProximityMusic.Instance != null) ProximityMusic.Instance.SetHush(this, 0f);
        }

        private void OnDestroy()
        {
            foreach (AudioSource s in _voices) if (s != null) GameAudio.Unbind(s);
        }

        // ───────────────────────── Begin / restore ─────────────────────────

        protected override void OnBegin()
        {
            _bakunawa?.Watch();
            BuildCircles();
            Shuffle();
            _phase = Phase.Travel;
            _next = 0;
            SetObjective("Bakunawa waits in the abyss. Swim down to its lair.");
            if (_bakunawa != null) ObjectiveTracker.Instance?.SetObjective(_bakunawa.transform);
        }

        /// <summary>Appeased, it circles over its lair: its Talk goes with it, so it's found wherever it is.</summary>
        private void TalkWhereItIs()
        {
            StatueDialogueRouter statue = Zone != null ? Zone.Statue : null;
            Bakunawa serpent = _bakunawa;
            if (statue != null && serpent != null) statue.FollowSpot(() => serpent.TalkSpot);
        }

        /// <summary>A later step of this zone (the talk at the end) is already done in the save.</summary>
        private bool TalkedToSince()
        {
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            if (Zone == null || save == null) return false;
            bool after = false;
            foreach (QuestStep step in Zone.Steps)
            {
                if (step == this) { after = true; continue; }
                if (after && step != null && !string.IsNullOrEmpty(step.StepId) && save.HasChallengeId(step.StepId)) return true;
            }
            return false;
        }

        public override void RestoreCompleted()
        {
            // The moon is whole: the circles stay lit in order (the lullaby was sung once, at the time).
            BuildCircles();
            for (int i = 0; i < _circles.Count; i++) { _circles[i].SetPhase(i); _circles[i].Show(MoonCircle.Look.Lit); }
            // Not talked to yet: it floats over the abyss, waiting. Talked to: asleep in its lair.
            if (_bakunawa != null)
            {
                if (TalkedToSince()) _bakunawa.SleepNow();
                else { _bakunawa.Appease(); TalkWhereItIs(); }
            }
            _lullabyOn = true;   // its lullaby goes on over the abyss from now on
            _phase = Phase.Done;
        }

        private void BuildCircles()
        {
            if (_circles.Count > 0) return;
            _root = new GameObject("[MoonRitual]").transform;
            _root.SetParent(transform, false);
            Vector3 c = Centre;
            float start = Random.Range(0f, 360f);
            for (int i = 0; i < MoonPhases.Count; i++)
            {
                Vector3 p = FindSpot(c, start + i * 360f / MoonPhases.Count);
                MoonCircle circle = MoonCircle.Create(_root, p, _circleRadius, _stoneMaterial, _glowMaterial);
                // Every moon reads the same way round: its top (the dot) on the outer edge of the
                // ring, its bottom toward Bakunawa in the middle.
                Vector3 outward = p - c; outward.y = 0f;
                if (outward.sqrMagnitude > 0.01f) circle.transform.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
                circle.name = "MoonCircle_" + i;
                // Spirit Sense lights a circle only when it's the one to step on next.
                SpiritGlow.EnsureOn(circle, new Color(0.85f, 0.95f, 1f), SpiritGlow.RelevanceMode.WhenObjective);
                _circles.Add(circle);
            }
        }

        /// <summary>
        /// Open, level floor near this slot of the ring: off the boulders and clear of rock above,
        /// searching a little around the slot (angle and distance) before settling for it as is.
        /// </summary>
        private Vector3 FindSpot(Vector3 centre, float slotDegrees)
        {
            float floorY = SwimBody.SeabedY(centre, centre.y + 30f);
            Vector3 fallback = Vector3.zero;
            bool haveFallback = false;
            for (int ring = 0; ring < 4; ring++)
            for (int k = 0; k < 9; k++)
            {
                float da = (k % 2 == 0 ? 1f : -1f) * ((k + 1) / 2) * 4f;   // 0, ±4°, ±8°, ±12°, ±16°
                float dist = _ringRadius + (ring % 2 == 0 ? 1f : -1f) * ((ring + 1) / 2) * 4f;
                float a = (slotDegrees + da) * Mathf.Deg2Rad;
                Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * dist;
                p.y = SwimBody.SeabedY(p, centre.y + 30f);
                if (!haveFallback) { fallback = p; haveFallback = true; }
                if (Mathf.Abs(p.y - floorY) > 1.5f) continue;          // on a boulder or a slope
                bool level = true;
                for (int e = 0; e < 4 && level; e++)
                {
                    Vector3 rim = p + Quaternion.Euler(0f, e * 90f, 0f) * Vector3.forward * _circleRadius;
                    level = Mathf.Abs(SwimBody.SeabedY(rim, centre.y + 30f) - p.y) < 0.8f;
                }
                if (!level) continue;
                if (Physics.CheckCapsule(p + Vector3.up * (_circleRadius + 0.8f), p + Vector3.up * 5f, _circleRadius + 0.6f,
                                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                return p;
            }
            return fallback;
        }

        /// <summary>The phases move to new circles (so the order can't be learned by position).</summary>
        private void Shuffle()
        {
            var phases = new List<int>();
            for (int i = 0; i < MoonPhases.Count; i++) phases.Add(i);
            // Avoid the moon's own order around the ring (too easy to read off).
            for (int attempt = 0; attempt < 20; attempt++)
            {
                for (int i = phases.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (phases[i], phases[j]) = (phases[j], phases[i]); }
                if (!InRingOrder(phases)) break;
            }
            for (int i = 0; i < _circles.Count; i++) _circles[i].SetPhase(phases[i]);
        }

        private static bool InRingOrder(List<int> phases)
        {
            int forward = 0, backward = 0;
            for (int i = 0; i < phases.Count; i++)
            {
                int d = (phases[(i + 1) % phases.Count] - phases[i] + MoonPhases.Count) % MoonPhases.Count;
                if (d == 1) forward++;
                if (d == MoonPhases.Count - 1) backward++;
            }
            return forward >= 2 || backward >= 2;
        }

        // ───────────────────────── The ritual ─────────────────────────

        private void BeginRitual()
        {
            _phase = Phase.Ritual;
            foreach (MoonCircle c in _circles) c.Show(MoonCircle.Look.Ready);
            if (ProximityMusic.Instance != null) ProximityMusic.Instance.SetHush(this, 1f);
            ObjectiveTracker.Instance?.Clear();
            BuildHud();
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.MoonRitual,
                ChallengeId = StepId,
                TaskQuantity = 1,
                TimeLimit = 0f,
                HintBudget = 0,
                Perspective = ChallengePerspective.ThirdPerson
            });
            if (_profile != null)
            {
                ChallengeManager.Instance?.SetExpectedCompletionTime(_profile.expectedSeconds);
                ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
            }
            UpdateObjective(true);
            PointSpiritSense();
        }

        /// <summary>Spirit Sense's guiding light and glow go to the next circle to step on (it shows
        /// them only while it's active).</summary>
        private void PointSpiritSense()
        {
            SpiritSense sense = SpiritSense.Instance;
            if (sense == null) return;
            MoonCircle next = _phase == Phase.Ritual && _next < MoonPhases.Count ? _circles.Find(c => c.Phase == _next) : null;
            if (next != null) sense.SetObjective(next.transform);
            else sense.ClearObjective();
        }

        private void OnAbility(AbilityActivatedEvent evt)
        {
            if (IsRunning && _phase == Phase.Ritual && evt.Ability == AbilityId.SpiritSense) _senseUses++;
        }

        private void Update()
        {
            UpdateLullaby(Time.unscaledDeltaTime);
            if (!IsRunning) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            Vector3 diver = player.transform.position;

            if (_phase == Phase.Travel)
            {
                if (Vector3.Distance(diver, Centre) <= _revealRange) BeginRitual();
                return;
            }
            if (_phase != Phase.Ritual) return;

            // Which circle is the diver in? The one they just answered must be left first.
            MoonCircle inside = null;
            foreach (MoonCircle c in _circles) if (c.Contains(diver, _columnHeight)) { inside = c; break; }
            if (inside != _inside) { _inside = inside; _held = 0f; }
            if (_answered != null && inside != _answered) _answered = null;

            foreach (MoonCircle c in _circles) c.Hold = 0f;
            bool canAnswer = inside != null && inside != _answered && !_resetting && inside.Current != MoonCircle.Look.Lit;
            if (canAnswer)
            {
                // Holding still (or nearly) fills the rim; swimming hard through doesn't count.
                bool still = player.Velocity.magnitude < 2.5f;
                _held = still ? _held + Time.deltaTime : Mathf.Max(0f, _held - Time.deltaTime);
                inside.Hold = Mathf.Clamp01(_held / _holdSeconds);
                if (_held >= _holdSeconds) Answer(inside);
            }
            UpdateLabels(diver);
        }

        private void Answer(MoonCircle circle)
        {
            _answered = circle;
            _held = 0f;
            ChallengeManager.Instance?.ReportPlayerAction();
            foreach (MoonCircle c in _circles) c.Hinted = false;

            if (circle.Phase == _next)
            {
                circle.Show(MoonCircle.Look.Lit);
                PlayMelody(circle.Phase);
                _next++;
                if (_bakunawa != null) _bakunawa.Ritual01 = _next / (float)MoonPhases.Count;
                ChallengeManager.Instance?.ReportCorrectAction();
                if (_next >= MoonPhases.Count) StartCoroutine(Finale());
                else UpdateObjective(true);
                PointSpiritSense();
            }
            else StartCoroutine(Wrong(circle));
        }

        private IEnumerator Wrong(MoonCircle circle)
        {
            _resetting = true;
            _wrongSteps++;
            _restarts++;
            _restartsInARow++;
            ChallengeManager.Instance?.ReportError();
            Sfx.Play(SfxCue.Wrong);
            FadeMelodies(0.35f);
            circle.Show(MoonCircle.Look.Wrong);
            _bakunawa?.Stir();
            if (_bakunawa != null) _bakunawa.Ritual01 = 0f;
            yield return new WaitForSeconds(0.5f);
            foreach (MoonCircle c in _circles) if (c != circle) c.Show(MoonCircle.Look.Ready);
            yield return new WaitForSeconds(0.9f);
            _next = 0;
            if (Reshuffle) Shuffle();
            if (FreeHintAfter > 0 && _restartsInARow >= FreeHintAfter) { ShowHint(); _restartsInARow = 0; }
            _resetting = false;
            UpdateObjective(true);
            PointSpiritSense();
        }

        private IEnumerator Finale()
        {
            _phase = Phase.Finale;
            _restartsInARow = 0;
            PointSpiritSense();   // nothing left to point at
            yield return new WaitForSeconds(0.6f);
            foreach (MoonCircle c in _circles) c.Flare();
            FadeMelodies(2.5f);
            _lullabyOn = true;
            _bakunawa?.Appease();
            TalkWhereItIs();
            CameraController.Instance?.Shake(0.15f, 1.4f);
            QuestObjectiveUI.Instance?.ClearCounter();
            HideHud();
            yield return new WaitForSeconds(1.5f);
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.ReportTaskCompleted();
            else Finish();
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!IsRunning || evt.ChallengeId != StepId || !evt.Success) return;
            Finish();
        }

        private void Finish()
        {
            if (_phase == Phase.Done) return;
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "ritual_wrong_steps", Value = _wrongSteps });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "ritual_restarts", Value = _restarts });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "ritual_spirit_sense_uses", Value = _senseUses });
            _phase = Phase.Done;
            HideHud();
            SpiritSense.Instance?.ClearObjective();
            ObjectiveTracker.Instance?.Clear();
            Complete();
        }

        private const string RitualText =
            "Sing the moon's melody to appease Bakunawa";

        /// <summary>One text for the whole ritual (a changed text would fold the box away); the counter shows the progress.</summary>
        private void UpdateObjective(bool force)
        {
            if (force || _objective != RitualText)
            {
                _objective = RitualText;
                QuestObjectiveUI.Instance?.SetObjective(DialogueManager.ResolveTokens(RitualText), true);
            }
            QuestObjectiveUI.Instance?.SetCounter(_next, MoonPhases.Count, "{0}/{1}");
        }

        private void SetObjective(string text) => QuestObjectiveUI.Instance?.SetObjective(DialogueManager.ResolveTokens(text), false);

        // ───────────────────────── Free pulse (easy) ─────────────────────────

        /// <summary>The next circle to step on pulses (until the diver answers any circle).</summary>
        private void ShowHint()
        {
            foreach (MoonCircle c in _circles) c.Hinted = c.Phase == _next;
        }

        // ───────────────────────── Music ─────────────────────────

        private AudioSource NewSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private void PlayMelody(int phase)
        {
            if (_melodies == null || phase >= _melodies.Length || _melodies[phase] == null) return;
            // Whatever is playing fades out quickly; the new melody starts on a free voice.
            FadeMelodies(0.4f);
            AudioSource voice = _voices.Find(v => v != null && !v.isPlaying);
            if (voice == null) { voice = NewSource("[Melody]", false); _voices.Add(voice); }
            voice.clip = _melodies[phase];
            GameAudio.Bind(voice, _melodyVolume, AudioChannel.Music);
            voice.Play();
        }

        private void FadeMelodies(float seconds)
        {
            foreach (AudioSource v in _voices)
                if (v != null && v.isPlaying && !_fading.Contains(v)) StartCoroutine(FadeOut(v, seconds));
        }

        private IEnumerator FadeOut(AudioSource s, float seconds)
        {
            _fading.Add(s);
            GameAudio.Unbind(s);   // its volume is driven here now
            float from = s.volume;
            for (float t = 0f; t < seconds && s.isPlaying; t += Time.deltaTime)
            {
                s.volume = from * (1f - t / seconds);
                yield return null;
            }
            s.Stop();
            _fading.Remove(s);
        }

        /// <summary>From the appeasement on, Bakunawa's lullaby loops over the abyss (louder nearer the
        /// lair), through the conversation too, alongside the level music. While the ritual runs, the
        /// level music is hushed for the melodies.</summary>
        private void UpdateLullaby(float dt)
        {
            float near = 0f;
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                float d = Vector3.Distance(player.transform.position, Centre);
                near = 1f - Mathf.Clamp01((d - _lullabyRange) / _lullabyRange);
            }
            float target = _lullabyOn ? near : 0f;
            _lullabyLevel = Mathf.MoveTowards(_lullabyLevel, target, dt / 2.5f);

            if (_lullabyOn && _lullaby != null && _lullabySource == null)
            {
                _lullabySource = NewSource("[Lullaby]", true);
                _lullabySource.clip = _lullaby;
                _lullabySource.ignoreListenerPause = true;   // it keeps singing through the conversation
                _lullabySource.Play();
            }
            if (_lullabySource != null)
            {
                _lullabySource.volume = _lullabyVolume * _lullabyLevel * GameAudio.Gain(AudioChannel.Music) * GameAudio.ClipGain(_lullaby);
                if (_lullabyLevel > 0.001f && !_lullabySource.isPlaying) _lullabySource.Play();
            }

            if (ProximityMusic.Instance != null)
            {
                bool ritual = IsRunning && (_phase == Phase.Ritual || _phase == Phase.Finale);
                float hush = ritual ? 1f : 0f;   // the lullaby plays alongside the level music
                ProximityMusic.Instance.SetHush(this, hush);
            }
        }

        // ───────────────────────── HUD ─────────────────────────

        private void BuildHud()
        {
            if (_hud != null) return;
            var go = new GameObject("[MoonRitualHud]", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            _hud = go.GetComponent<Canvas>();
            _hud.renderMode = RenderMode.ScreenSpaceOverlay;
            _hud.sortingOrder = 5;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            for (int i = 0; i < _circles.Count; i++)
            {
                var l = new GameObject("PhaseName_" + i, typeof(RectTransform));
                l.transform.SetParent(root, false);
                var rt = (RectTransform)l.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(320f, 44f);
                var t = l.AddComponent<TextMeshProUGUI>();
                if (_font != null) t.font = _font;
                t.fontSize = 28f;
                t.alignment = TextAlignmentOptions.Center;
                t.color = new Color(0.85f, 0.93f, 1f, 0f);
                t.raycastTarget = false;
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(4, 20, 36, 220);
                _labels.Add(t);
            }
        }

        private void HideHud()
        {
            if (_hud != null) Destroy(_hud.gameObject);
            _hud = null;
            _labels.Clear();
        }

        /// <summary>Phase names over the circles: always over the one the diver is in; over all
        /// nearby ones when the difficulty shows names.</summary>
        private void UpdateLabels(Vector3 diver)
        {
            if (_hud == null) return;
            Camera cam = Camera.main;
            var root = (RectTransform)_hud.transform;
            for (int i = 0; i < _labels.Count && i < _circles.Count; i++)
            {
                MoonCircle c = _circles[i];
                TextMeshProUGUI t = _labels[i];
                t.text = MoonPhases.Names[c.Phase];
                float d = Vector3.Distance(diver, c.transform.position);
                bool show = c == _inside || (Labels && d < 28f);
                Vector3 world = c.transform.position + Vector3.up * 1.2f;
                Vector3 sp = cam != null ? cam.WorldToScreenPoint(world) : Vector3.back;
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out Vector2 at))
                    t.rectTransform.anchoredPosition = at;
                else show = false;
                Color col = t.color;
                col.a = Mathf.MoveTowards(col.a, show ? 1f : 0f, Time.deltaTime * 4f);
                t.color = col;
            }
        }

        // ───────────────────────── Debug ─────────────────────────

        private void DebugForceComplete()
        {
            foreach (MoonCircle c in _circles) { c.Show(MoonCircle.Look.Lit); }
            _lullabyOn = true;
            _bakunawa?.Appease();
            TalkWhereItIs();
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.CompleteChallenge(true);
            else Finish();
        }

#if UNITY_EDITOR
        public void EditorSetup(Bakunawa bakunawa, Transform centre, AudioClip[] melodies, AudioClip lullaby,
                                Material stone, Material glow, TMP_FontAsset font, MinigameDifficultyProfile profile)
        {
            _bakunawa = bakunawa;
            _centre = centre;
            _melodies = melodies;
            _lullaby = lullaby;
            _stoneMaterial = stone;
            _glowMaterial = glow;
            _font = font;
            _profile = profile;
        }
#endif
    }
}
