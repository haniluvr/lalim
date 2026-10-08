using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Challenges.Catching;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Inventory;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Ajuy, Minigame 2: the Kataw lessons (3rd person). The Kataw teach the diver to catch
    /// without harming, with the Woven Net Fragment (hold to aim along a physics arc, release to
    /// throw; the net opens where it lands and closes around everything in its ring).
    ///
    ///  1. The throw (first Root Pool): one lone fish.
    ///  2. Identify (second pool): Spirit Sense shows green targets and red protected animals
    ///     (stingray, giant clam); net the green school.
    ///  3. Patience (third pool): a turtle keeps drifting across the school; wait for clear water.
    ///  Then the Elder briefs the test: one school in each of the four pools (the four schools
    ///  Minigame 3 relocates: tuna, clownfish, reef fish, squid), protected animals sharing every
    ///  pool. Taken schools ride in the net-bag towed behind the diver.
    ///
    /// A protected animal in the net is released unharmed, and so is everything else in that throw;
    /// in the test it is a DDA error, and three of them restart the test. Each lesson is saved;
    /// the test restarts on reload. DDA: net size, throw cooldown, fish speed, protected animals
    /// per pool and their speed, school size. Metrics: time, throws (actions), schools (tasks),
    /// protected catches (errors).
    /// </summary>
    public class SelectiveCatchingStep : QuestStep
    {
        [Serializable]
        public class Species
        {
            public string key;
            public string displayName;
            public GameObject model;
            [Min(0.05f)] public float length = 0.5f;
            public float modelYaw = 180f;
            public bool isProtected;
            public bool isStatic;
            [Tooltip("Height band above the seabed (targets low, protected high).")]
            public Vector2 height = new Vector2(0.8f, 2.5f);
            [Tooltip("Speed relative to the DDA speed (targets) or the protected speed.")]
            public float speedScale = 1f;
        }

        [Header("Pools (0–2 the lessons, all four the test)")]
        [SerializeField] private List<RootPool> _pools = new List<RootPool>();
        [SerializeField] private StatueDialogueRouter _elder;

        [Header("Species")]
        [SerializeField] private List<Species> _species = new List<Species>();
        [SerializeField] private string _lessonThrowFish = "brownfish";
        [SerializeField] private string _lessonSenseFish = "doryfish";
        [SerializeField] private string _lessonPatienceFish = "clownfish";
        [Tooltip("One school per pool in the test, in pool order; these are the schools Minigame 3 relocates.")]
        [SerializeField] private string[] _testSchools = { "tuna", "clownfish", "brownfish", "squid" };
        [SerializeField] private string[] _protectedCycle = { "turtle", "stingray", "clam" };

        [Header("Visuals")]
        [SerializeField] private Material _auraMaterial;
        [SerializeField] private Material _lineMaterial;
        [Tooltip("The bundled net in flight (the Woven Net Fragment's model).")]
        [SerializeField] private GameObject _netModel;
        [Tooltip("The net leaving the hand.")]
        [SerializeField] private AudioClip _netThrowClip;
        [Tooltip("The net settling on the sand where it lands.")]
        [SerializeField] private AudioClip _netLandClip;

        [Header("DDA")]
        [SerializeField] private MinigameDifficultyProfile _profile;
        [SerializeField, Min(1)] private int _strikes = 3;
        [SerializeField, Min(1f)] private float _expectedSecondsPerSchool = 50f;

        [Header("Text")]
        [SerializeField, TextArea(1, 2)] private string _equipNetText = "Open your inventory and equip the Woven Net Fragment.";
        [SerializeField] private string _counterFormat = "Schools in the net-bag: {0} / {1}";

        /// <summary>Saved for each school taken in the test; Minigame 3 lets them out at the sanctuaries.</summary>
        public const string SchoolIdPrefix = "visayas_school_";

        private enum Stage { LessonThrow, LessonSense, LessonPatience, TestBrief, Test, Done }

        private Stage _stage;
        private bool _stageLive;            // fish spawned and the net armed
        private bool _waitingForEquip;
        private readonly List<CatchableSchool> _spawned = new List<CatchableSchool>();
        private Transform _root;
        private int _protectedCatches;
        private int _misses;
        private bool _sensedThisLesson;
        private readonly HashSet<string> _bagged = new HashSet<string>();
        private readonly Dictionary<string, RootPool> _poolOfSchool = new Dictionary<string, RootPool>();

        // ── For Minigame 3, which relocates the schools taken here ──
        public IReadOnlyList<string> TestSchools => _testSchools;
        public Material LineMaterial => _lineMaterial;
        public Material AuraMaterial => _auraMaterial;
        public GameObject NetModel => _netModel;
        public AudioClip NetThrowClip => _netThrowClip;
        public AudioClip NetLandClip => _netLandClip;
        public int SchoolSize => Knob(Level2DifficultyKnobs.SchoolSize, 5);
        public Species SpeciesOf(string key) => Find(key);

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        private static bool Has(string id) => Save != null && Save.HasChallengeId(id);
        private string LessonId(int n) => $"{StepId}_lesson_{n}";

        private void OnEnable()
        {
            NetThrower.Opened += OnNetOpened;
            NetThrower.Thrown += OnThrown;
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<SpiritSenseActivatedEvent>(OnSpiritSense);
        }

        private void OnDisable()
        {
            NetThrower.Opened -= OnNetOpened;
            NetThrower.Thrown -= OnThrown;
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<SpiritSenseActivatedEvent>(OnSpiritSense);
        }

        // ───────────────────────── Begin / restore ─────────────────────────

        protected override void OnBegin()
        {
            EnsureNetOwned();
            NetThrower.Ensure(_lineMaterial, _netModel, _netThrowClip, _netLandClip);
            ConfigureNet();
            _stage = Stage.LessonThrow;
            for (int i = 0; i < 3; i++) if (Has(LessonId(i))) _stage = (Stage)(i + 1);
            BeginStage();
        }

        public override void RestoreCompleted()
        {
            // The schools ride along until Minigame 3 lets them out.
            NetBag bag = null;
            foreach (string key in _testSchools)
            {
                if (!Has(SchoolIdPrefix + key)) continue;
                if (bag == null) bag = NetBag.Ensure(_lineMaterial);
                Species s = Find(key);
                if (s != null) bag.Add(key, s.model, Knob(Level2DifficultyKnobs.SchoolSize, 5), s.modelYaw);
            }
            foreach (RootPool p in _pools) if (p != null && p.Teacher != null) p.Teacher.AllowIdleTalk(true);
        }

        private void EnsureNetOwned()
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null && !inv.HasItem(GameConstants.Relics.WOVEN_NET_FRAGMENT))
            {
                Debug.LogWarning("[SelectiveCatching] The save has no Woven Net Fragment; granting it for this run.");
                inv.AddItem(GameConstants.Relics.WOVEN_NET_FRAGMENT);
            }
        }

        private void ConfigureNet()
        {
            NetThrower net = NetThrower.Instance;
            if (net == null) return;
            net.Radius = Knob(Level2DifficultyKnobs.NetRadius, 3.2f);
            net.Cooldown = Knob(Level2DifficultyKnobs.ThrowCooldown, 1.5f);
        }

        private int Knob(string key, int fallback) => _profile != null && _profile.Has(key) ? _profile.GetInt(key) : fallback;
        private float Knob(string key, float fallback) => _profile != null && _profile.Has(key) ? _profile.Get(key) : fallback;

        private Species Find(string key)
        {
            foreach (Species s in _species) if (s != null && s.key == key) return s;
            return null;
        }

        private RootPool Pool(int i) => i >= 0 && i < _pools.Count ? _pools[i] : null;

        // ───────────────────────── Stages ─────────────────────────

        private void BeginStage()
        {
            ClearSpawned();
            _stageLive = false;
            _misses = 0;
            _sensedThisLesson = false;
            if (NetThrower.Instance != null) NetThrower.Instance.Armed = false;

            switch (_stage)
            {
                case Stage.LessonThrow:
                case Stage.LessonSense:
                case Stage.LessonPatience:
                {
                    int n = (int)_stage;
                    RootPool pool = Pool(n);
                    StatueDialogueRouter teacher = pool != null ? pool.Teacher : null;
                    List<DialogueLine> lines = n == 0 ? Level2DialogueScripts.LessonThrow()
                                             : n == 1 ? Level2DialogueScripts.LessonSense()
                                             : Level2DialogueScripts.LessonPatience();
                    string who = pool != null ? pool.TeacherName : "the Kataw";
                    string where = pool != null ? pool.PoolName : "the pools";
                    SetObjective($"Find {who} at {where}.");
                    if (teacher != null)
                    {
                        ObjectiveTracker.Instance?.SetObjective(teacher.transform);
                        teacher.ArmTalk(lines, null, StartLesson);
                    }
                    else StartLesson();
                    break;
                }
                case Stage.TestBrief:
                    SetObjective("Return to the Kataw Elder in the village square.");
                    if (_elder != null)
                    {
                        ObjectiveTracker.Instance?.SetObjective(_elder.transform);
                        _elder.ArmTalk(Level2DialogueScripts.CatchingTest(), null, () => { _stage = Stage.Test; BeginStage(); });
                    }
                    else { _stage = Stage.Test; BeginStage(); }
                    break;
                case Stage.Test:
                    StartTest();
                    break;
            }
        }

        private void SetObjective(string text) => QuestObjectiveUI.Instance?.SetObjective(DialogueManager.ResolveTokens(text), true);

        /// <summary>Runs once the stage's fish are out: until the net is equipped, the objective says how.</summary>
        private bool NeedEquip()
        {
            if (RelicLoadout.IsEquipped(GameConstants.Relics.WOVEN_NET_FRAGMENT)) return false;
            _waitingForEquip = true;
            SetObjective(_equipNetText);
            return true;
        }

        private void StartLesson()
        {
            int n = (int)_stage;
            RootPool pool = Pool(n);
            if (pool == null) { CompleteLesson(); return; }
            float speed = Knob(Level2DifficultyKnobs.TargetSpeed, 2.2f);
            float slow = Knob(Level2DifficultyKnobs.ProtectedSpeed, 1.1f);
            int size = Knob(Level2DifficultyKnobs.SchoolSize, 5);

            switch (_stage)
            {
                case Stage.LessonThrow:
                    // One lone fish, unhurried, near the middle of the pool.
                    SpawnIn(pool, _lessonThrowFish, 1, pool.PointAt(0.3f, 200f), speed * 0.45f, pool.Radius * 0.45f);
                    break;
                case Stage.LessonSense:
                    SpawnIn(pool, _lessonSenseFish, size, pool.PointAt(0.4f, 30f), speed * 0.8f, pool.Radius);
                    SpawnIn(pool, "stingray", 1, pool.PointAt(0.5f, 150f), slow, pool.Radius);
                    SpawnIn(pool, "clam", 1, pool.PointAt(0.35f, 260f), 0f, pool.Radius);
                    SpawnIn(pool, "turtle", 1, pool.PointAt(0.6f, 320f), slow, pool.Radius);
                    break;
                case Stage.LessonPatience:
                {
                    // The school holds near one spot; the turtle glides back and forth across it.
                    Vector3 spot = pool.PointAt(0.35f, 90f);
                    CatchableSchool school = SpawnIn(pool, _lessonPatienceFish, size, spot, speed * 0.6f, 3f, spot);
                    CatchableSchool turtle = SpawnIn(pool, "turtle", 1, spot, slow, pool.Radius);
                    if (school != null && turtle != null) PatrolAcross(turtle, school.Centre, pool);
                    break;
                }
            }

            string text = _stage == Stage.LessonThrow ? "Hold the net button to aim, release to throw. Catch the lone fish."
                        : _stage == Stage.LessonSense ? "Use Spirit Sense, then net the school that glows green."
                        : "Wait for the turtle to drift clear, then net the school.";
            _stageLive = true;
            ObjectiveTracker.Instance?.SetObjective(pool.transform);
            if (!NeedEquip()) { SetObjective(text); ArmNet(); }
            else _pendingText = text;
        }

        private string _pendingText;

        private void ArmNet()
        {
            _waitingForEquip = false;
            if (NetThrower.Instance != null) NetThrower.Instance.Armed = true;
        }

        private void CompleteLesson()
        {
            int n = (int)_stage;
            if (Save != null) Save.AddChallengeId(LessonId(n));
            RootPool pool = Pool(n);
            if (pool != null && pool.Teacher != null) pool.Teacher.AllowIdleTalk(true);
            _stage = (Stage)(n + 1);
            BeginStage();
        }

        private void PatrolAcross(CatchableSchool turtle, Vector3 through, RootPool pool)
        {
            // Across the line from the pool's middle to the school, at the school's height (so it blocks).
            Vector3 toSchool = through - pool.Centre;
            toSchool.y = 0f;
            Vector3 across = toSchool.sqrMagnitude > 0.1f ? Vector3.Cross(Vector3.up, toSchool.normalized) : Vector3.right;
            float span = Mathf.Min(7f, pool.Radius * 0.8f);
            Vector3 a = through + across * span + Vector3.up * 0.4f;
            Vector3 b = through - across * span + Vector3.up * 0.4f;
            turtle.SetPatrol(a, b);
        }

        // ───────────────────────── The test ─────────────────────────

        private void StartTest()
        {
            _protectedCatches = 0;
            _bagged.Clear();
            _poolOfSchool.Clear();
            NetBag bag = NetBag.Ensure(_lineMaterial);
            bag.Clear();

            float speed = Knob(Level2DifficultyKnobs.TargetSpeed, 2.2f);
            float slow = Knob(Level2DifficultyKnobs.ProtectedSpeed, 1.1f);
            int size = Knob(Level2DifficultyKnobs.SchoolSize, 5);
            int perPool = Mathf.Max(0, Knob(Level2DifficultyKnobs.ProtectedPerPool, 2));
            int total = 0;
            int cycle = 0;
            for (int i = 0; i < _testSchools.Length; i++)
            {
                RootPool pool = Pool(i % Mathf.Max(1, _pools.Count));
                if (pool == null) continue;
                string key = _testSchools[i];
                CatchableSchool school = SpawnIn(pool, key, size, pool.PointAt(0.4f, 40f + i * 70f), speed, pool.Radius);
                if (school == null) continue;
                _poolOfSchool[key] = pool;
                total++;
                for (int k = 0; k < perPool; k++)
                {
                    string p = _protectedCycle[cycle++ % _protectedCycle.Length];
                    CatchableSchool guard = SpawnIn(pool, p, 1, pool.PointAt(0.55f, 160f + k * 110f + i * 30f), slow, pool.Radius);
                    // The first one in each pool keeps crossing the school's water.
                    if (k == 0 && guard != null && p != "clam") PatrolAcross(guard, school.Centre, pool);
                }
            }

            _stageLive = true;
            UpdateCounter();
            PointTestNavigation();
            if (NeedEquip()) _pendingText = ObjectiveText;
            else { SetObjective(ObjectiveText); ArmNet(); }

            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.SelectiveCatching,
                ChallengeId = StepId,
                TaskQuantity = total,
                TimeLimit = 0f,
                HintBudget = 0,
                Perspective = ChallengePerspective.ThirdPerson
            });
            ChallengeManager.Instance?.SetExpectedCompletionTime(total * _expectedSecondsPerSchool);
            if (_profile != null) ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
        }

        private void RestartTest()
        {
            CalloutUI.Say(Level2DialogueScripts.KatawElder, Level2DialogueScripts.CalloutTestRestart, 4.5f);
            ChallengeManager.Instance?.FailChallenge("too_many_protected_catches");
            if (NetBag.Instance != null) NetBag.Instance.Clear();
            foreach (CatchableSchool s in _spawned) if (s != null) s.Release(s.Centre + UnityEngine.Random.onUnitSphere);
            StartCoroutine(RestartAfterFade());
        }

        private System.Collections.IEnumerator RestartAfterFade()
        {
            yield return new WaitForSeconds(1.2f);   // see the animals swim free first
            ScreenFader fader = ScreenFader.Instance;
            if (fader != null) yield return fader.FadeOut(0.45f);
            _stage = Stage.Test;
            BeginStage();
            if (fader != null) yield return fader.FadeIn(0.45f);
        }

        private void UpdateCounter() => QuestObjectiveUI.Instance?.SetCounter(_bagged.Count, _poolOfSchool.Count, _counterFormat);

        private void PointTestNavigation()
        {
            var markers = new List<Transform>();
            foreach (KeyValuePair<string, RootPool> kv in _poolOfSchool)
                if (!_bagged.Contains(kv.Key) && kv.Value != null && !markers.Contains(kv.Value.transform)) markers.Add(kv.Value.transform);
            if (markers.Count == 0) { ObjectiveTracker.Instance?.Clear(); return; }
            ObjectiveTracker.Instance?.SetObjectiveSet(markers, t => !PoolHasLooseSchool(t));
        }

        private bool PoolHasLooseSchool(Transform poolTransform)
        {
            foreach (KeyValuePair<string, RootPool> kv in _poolOfSchool)
                if (kv.Value != null && kv.Value.transform == poolTransform && !_bagged.Contains(kv.Key)) return true;
            return false;
        }

        // ───────────────────────── Spawning ─────────────────────────

        private CatchableSchool SpawnIn(RootPool pool, string key, int count, Vector3 at, float speed, float wanderRadius, Vector3? wanderCentre = null)
        {
            Species s = Find(key);
            if (s == null) { Debug.LogWarning($"[SelectiveCatching] No species '{key}'."); return null; }
            if (_root == null)
            {
                _root = new GameObject("[Catching]").transform;
                _root.SetParent(transform, false);
            }
            var spec = new CatchableSchool.Spec
            {
                Key = s.key,
                Name = s.displayName,
                Protected = s.isProtected,
                Model = s.model,
                Count = s.isProtected ? 1 : count,
                Length = s.length,
                ModelYaw = s.modelYaw,
                Speed = Mathf.Max(0.2f, speed * s.speedScale),
                MinHeight = s.height.x,
                MaxHeight = s.height.y,
                Static = s.isStatic,
            };
            Vector3 centre = wanderCentre ?? pool.Centre;
            CatchableSchool school = CatchableSchool.Spawn(spec, centre, wanderRadius, at, _auraMaterial, _root);
            _spawned.Add(school);
            return school;
        }

        private void ClearSpawned()
        {
            foreach (CatchableSchool s in _spawned) if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }

        // ───────────────────────── Play ─────────────────────────

        private void Update()
        {
            if (!IsRunning || !_stageLive) return;
            if (_waitingForEquip && RelicLoadout.IsEquipped(GameConstants.Relics.WOVEN_NET_FRAGMENT))
            {
                if (!string.IsNullOrEmpty(_pendingText)) SetObjective(_pendingText);
                _pendingText = null;
                ArmNet();
            }
            else if (!_waitingForEquip && !RelicLoadout.IsEquipped(GameConstants.Relics.WOVEN_NET_FRAGMENT))
            {
                if (NetThrower.Instance != null) NetThrower.Instance.Armed = false;
                _pendingText = _stage == Stage.Test ? ObjectiveText : null;
                NeedEquip();
            }
        }

        private void OnThrown() { if (IsRunning) ChallengeManager.Instance?.ReportPlayerAction(); }

        private void OnSpiritSense(SpiritSenseActivatedEvent evt) { if (IsRunning) _sensedThisLesson = true; }

        private void OnNetOpened(Vector3 at, float radius)
        {
            if (!IsRunning || !_stageLive) return;

            var inside = new List<CatchableSchool>();
            foreach (CatchableSchool s in CatchableSchool.All)
                if (s != null && _spawned.Contains(s) && s.Inside(at, radius)) inside.Add(s);

            RootPool pool = PoolAt(at);
            string speaker = pool != null ? pool.TeacherName : Level2DialogueScripts.KatawElder;

            if (inside.Count == 0)
            {
                _misses++;
                if (_stage == Stage.LessonThrow || _misses % 3 == 0) CalloutUI.Say(speaker, Level2DialogueScripts.CalloutMiss);
                return;
            }

            CatchableSchool protectedOne = inside.Find(s => s.IsProtected);
            if (protectedOne != null)
            {
                // Everything in a spoiled net goes free; the protected one first and unharmed.
                foreach (CatchableSchool s in inside) s.Release(at);
                bool alsoTargets = inside.Exists(s => !s.IsProtected);
                Sfx.Play(SfxCue.Wrong);
                CalloutUI.Say(speaker, Level2DialogueScripts.CalloutProtected(protectedOne.SpeciesKey) + (alsoTargets ? " " + Level2DialogueScripts.CalloutSpoiled : ""));
                if (_stage == Stage.Test)
                {
                    _protectedCatches++;
                    ChallengeManager.Instance?.ReportError();
                    if (_protectedCatches >= _strikes) { _stageLive = false; RestartTest(); }
                }
                return;
            }

            foreach (CatchableSchool s in inside) s.Net();
            ChallengeManager.Instance?.ReportCorrectAction();

            if (_stage == Stage.Test) { TakeIntoBag(inside, speaker); return; }

            // A lesson: the teacher takes the catch for the village.
            if (_stage == Stage.LessonSense && !_sensedThisLesson && !CatchableSchool.AnyGlowing)
                CalloutUI.Say(speaker, Level2DialogueScripts.CalloutUseSense);
            Transform to = pool != null && pool.Teacher != null ? pool.Teacher.transform : null;
            string done = _stage == Stage.LessonThrow ? Level2DialogueScripts.CalloutLessonThrowDone
                        : _stage == Stage.LessonSense ? Level2DialogueScripts.CalloutLessonSenseDone
                        : Level2DialogueScripts.CalloutLessonPatienceDone;
            _stageLive = false;
            if (NetThrower.Instance != null) NetThrower.Instance.Armed = false;
            CalloutUI.Say(speaker, done);
            int pending = inside.Count;
            foreach (CatchableSchool s in inside)
            {
                if (to != null) s.PullInto(to, () => { if (--pending == 0) CompleteLesson(); });
                else { Destroy(s.gameObject); if (--pending == 0) CompleteLesson(); }
            }
        }

        private void TakeIntoBag(List<CatchableSchool> schools, string speaker)
        {
            NetBag bag = NetBag.Ensure(_lineMaterial);
            foreach (CatchableSchool s in schools)
            {
                CatchableSchool school = s;
                school.PullInto(bag.Mouth, () =>
                {
                    if (!IsRunning || _stage != Stage.Test) return;
                    Species sp = Find(school.SpeciesKey);
                    bag.Add(school.SpeciesKey, sp != null ? sp.model : school.Model, school.Count, sp != null ? sp.modelYaw : 180f);                    if (!_bagged.Add(school.SpeciesKey)) return;
                    int left = _poolOfSchool.Count - _bagged.Count;
                    UpdateCounter();
                    PointTestNavigation();
                    CalloutUI.Say(speaker, left > 0 ? string.Format(Level2DialogueScripts.CalloutSchoolTaken, left) : Level2DialogueScripts.CalloutLastSchool);
                    ChallengeManager.Instance?.ReportTaskCompleted();   // the last one completes the challenge
                });
            }
        }

        private RootPool PoolAt(Vector3 p)
        {
            RootPool best = null;
            float bestD = float.MaxValue;
            foreach (RootPool pool in _pools)
            {
                if (pool == null) continue;
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(pool.Centre.x, 0f, pool.Centre.z));
                if (d < pool.Radius + 8f && d < bestD) { bestD = d; best = pool; }
            }
            return best;
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!IsRunning || evt.ChallengeId != StepId || !evt.Success) return;
            Finish();
        }

        private void Finish()
        {
            if (Save != null) foreach (string key in _bagged) Save.AddChallengeId(SchoolIdPrefix + key);
            _stageLive = false;
            _stage = Stage.Done;
            if (NetThrower.Instance != null) NetThrower.Instance.Armed = false;
            ClearSpawned();
            foreach (RootPool p in _pools) if (p != null && p.Teacher != null) p.Teacher.AllowIdleTalk(true);
            ObjectiveTracker.Instance?.Clear();
            Complete();
        }

        private void DebugForceComplete()
        {
            NetBag bag = NetBag.Ensure(_lineMaterial);
            foreach (string key in _testSchools)
            {
                _bagged.Add(key);
                Species s = Find(key);
                if (s != null) bag.Add(key, s.model, Knob(Level2DifficultyKnobs.SchoolSize, 5), s.modelYaw);
            }
            for (int i = 0; i < 3; i++) if (Save != null) Save.AddChallengeId(LessonId(i));
            foreach (RootPool p in _pools) if (p != null && p.Teacher != null) p.Teacher.CancelTalk();
            if (_elder != null) _elder.CancelTalk();
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.CompleteChallenge(true);
            Finish();
        }

#if UNITY_EDITOR
        public void EditorSetup(List<RootPool> pools, StatueDialogueRouter elder, List<Species> species, Material aura, Material line,
                                GameObject netModel, MinigameDifficultyProfile profile)
        {
            _pools = pools;
            _elder = elder;
            _species = species;
            _auraMaterial = aura;
            _lineMaterial = line;
            _netModel = netModel;
            _profile = profile;
        }
#endif
    }
}
