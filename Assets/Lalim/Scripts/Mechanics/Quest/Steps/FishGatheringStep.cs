using System.Collections.Generic;
using UnityEngine;
using Lalim.Challenges.Gathering;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Inventory;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Caluya, Minigame 1 — the gathering (3rd person). Manunubo's herds are scattered across the
    /// zone, panicked; the diver blows the Radiant Conch to call the ones in reach, swims the
    /// ribbon of calmed fish back to Manunubo's light, and avoids the sharks and hammerheads
    /// patrolling the open sand (hide in thickets and crevices, or ride the dugong with Tidebond).
    /// A bite costs HP and scatters every herd in the ribbon.
    ///
    ///  - Needs the conch equipped; until it is, the objective says how.
    ///  - DDA picks the herd count, fish per herd, conch reach and cooldown, and the predators'
    ///    speed, sight and bite. Each herd brought home is one ChallengeManager task; bites are errors.
    ///  - Delivered herds are saved, so a reload continues where the player left off.
    /// </summary>
    public class FishGatheringStep : QuestStep
    {
        [Header("Herds")]
        [Tooltip("Fallback spawn points, used only when there are no herd sites; children in order of how spread out they are.")]
        [SerializeField] private Transform _spawnPoints;
        [Tooltip("Open floor across the whole zone (clear of arches, statues and rocks) where herds may be placed; baked in the editor.")]
        [SerializeField] private List<Vector3> _herdSites = new List<Vector3>();
        [Tooltip("Herds are at least this far apart (relaxed only if the zone can't fit them).")]
        [SerializeField, Min(0f)] private float _herdSpacing = 60f;
        [Tooltip("…and at least this far from Manunubo, so every herd is a real swim away.")]
        [SerializeField, Min(0f)] private float _awayFromManunubo = 45f;
        [SerializeField] private GameObject[] _fishModels;
        [SerializeField, Min(0.1f)] private float _fishLength = 0.35f;
        [SerializeField] private float _fishModelYaw = 180f;
        [SerializeField] private Material _moteMaterial;

        [Header("Manunubo")]
        [SerializeField] private ManunuboWisp _manunubo;
        [Tooltip("Swim this close to Manunubo with the ribbon to bring the herds home.")]
        [SerializeField, Min(2f)] private float _deliverRadius = 9f;

        [Header("Predators (inactive until the gathering starts)")]
        [SerializeField] private List<Predator> _predators = new List<Predator>();

        [Header("DDA")]
        [SerializeField] private MinigameDifficultyProfile _profile;
        [SerializeField, Min(1)] private int _baseHerds = 5;
        [SerializeField, Min(1f)] private float _expectedSecondsPerHerd = 45f;
        [SerializeField] private string _counterFormat = "Schools home: {0} / {1}";
        [SerializeField, TextArea(1, 2)] private string _equipConchText = "Open your inventory and equip the Radiant Conch.";

        [Header("Sound (the conch call itself is on ConchCall)")]
        [SerializeField] private AudioClip _herdCalmedClip;
        [SerializeField] private AudioClip _herdJoinsClip;
        [Tooltip("The ribbon reaches Manunubo.")]
        [SerializeField] private AudioClip _deliveredClip;
        [Tooltip("Each herd that dissolves into Manunubo's light.")]
        [SerializeField] private AudioClip _brightensClip;
        [SerializeField] private AudioClip _sharkSpotsClip;
        [SerializeField] private AudioClip _sharkGrowlClip;
        [SerializeField] private AudioClip _sharkBiteClip;
        [SerializeField] private AudioClip _herdScatterClip;
        [SerializeField] private AudioClip _sharkGivesUpClip;
        [SerializeField] private AudioClip _hideRustleClip;
        [SerializeField] private AudioClip _chaseTensionLoop;
        [Tooltip("Heartbeat loop while the diver hides with a predator near.")]
        [SerializeField] private AudioClip _hiddenClip;

        private readonly List<Herd> _herds = new List<Herd>();
        private Transform _herdRoot;
        private int _total;
        private int _delivered;
        private bool _started;
        private bool _hadFollowers;

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        private void Awake()
        {
            // Quiet until the gathering begins (and after it's done).
            foreach (Predator p in _predators) if (p != null) p.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            Predator.Bit += OnBit;
            Predator.ChaseStarted += OnChaseStarted;
            Predator.ClosingIn += OnClosingIn;
            Predator.GaveUp += OnGaveUp;
            Herd.Calmed += OnHerdCalmed;
            Herd.Joined += OnHerdJoined;
            HidingZone.PlayerEntered += OnEnteredCover;
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        }

        private void OnDisable()
        {
            Predator.Bit -= OnBit;
            Predator.ChaseStarted -= OnChaseStarted;
            Predator.ClosingIn -= OnClosingIn;
            Predator.GaveUp -= OnGaveUp;
            Herd.Calmed -= OnHerdCalmed;
            Herd.Joined -= OnHerdJoined;
            HidingZone.PlayerEntered -= OnEnteredCover;
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        }

        // ───────────────────────── Begin / restore ─────────────────────────

        protected override void OnBegin()
        {
            _started = false;
            EnsureConchOwned();
            if (!RelicLoadout.IsEquipped(GameConstants.Relics.RADIANT_CONCH))
            {
                QuestObjectiveUI.Instance?.SetObjective(_equipConchText, true);
                ObjectiveTracker.Instance?.Clear();
                return;   // Update starts the gathering once it's equipped
            }
            StartGathering();
        }

        public override void RestoreCompleted()
        {
            foreach (Predator p in _predators) if (p != null) p.gameObject.SetActive(false);
            if (_manunubo != null) _manunubo.SetBrightness(1f);
        }

        private void EnsureConchOwned()
        {
            // Level 1 gives the conch; a test run that skipped it still needs one to play.
            InventoryManager inv = InventoryManager.Instance;
            if (inv != null && !inv.HasItem(GameConstants.Relics.RADIANT_CONCH))
            {
                Debug.LogWarning("[FishGathering] The save has no Radiant Conch (Level 1 reward); granting it for this run.");
                inv.AddItem(GameConstants.Relics.RADIANT_CONCH);
            }
        }

        private void StartGathering()
        {
            _started = true;
            _total = Mathf.Max(1, Knob(Level2DifficultyKnobs.Herds, DDAManager.Instance != null ? DDAManager.Instance.GetAdjustedTaskQuantity(_baseHerds) : _baseHerds));
            PickHerdPlaces();
            _total = Mathf.Min(_total, _herdPlaces.Count);
            _delivered = 0;
            for (int i = 0; i < _total; i++) if (IsHerdHome(i)) _delivered++;

            if (_manunubo != null) _manunubo.SetBrightness(Brightness());
            int remaining = _total - _delivered;
            if (remaining <= 0) { Complete(); return; }

            SpawnHerds();
            WakePredators();
            ConfigureConch();
            PredatorAlertUI.Ensure(_chaseTensionLoop, _hiddenClip);

            QuestObjectiveUI.Instance?.SetObjective(DialogueManager.ResolveTokens(ObjectiveText), true);
            UpdateCounter();
            PointNavigation(force: true);

            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.FishGathering,
                ChallengeId = StepId,
                TaskQuantity = remaining,
                TimeLimit = 0f,
                HintBudget = 0,
                Perspective = ChallengePerspective.ThirdPerson
            });
            ChallengeManager.Instance?.SetExpectedCompletionTime(remaining * _expectedSecondsPerHerd);
            if (_profile != null) ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
        }

        private int Knob(string key, int fallback) => _profile != null && _profile.Has(key) ? _profile.GetInt(key) : fallback;
        private float Knob(string key, float fallback) => _profile != null && _profile.Has(key) ? _profile.Get(key) : fallback;

        private string HerdId(int index) => $"{StepId}_herd_{index}";
        private static bool Has(string id) => Save != null && Save.HasChallengeId(id);
        private bool IsHerdHome(int index) => Has(HerdId(index));

        private float Brightness() => Mathf.Lerp(0.45f, 1f, _total > 0 ? (float)_delivered / _total : 1f);

        private void SpawnHerds()
        {
            if (_herdRoot == null)
            {
                _herdRoot = new GameObject("[Herds]").transform;
                _herdRoot.SetParent(transform, false);
            }
            int fish = Knob(Level2DifficultyKnobs.FishPerHerd, 5);
            for (int i = 0; i < _total; i++)
            {
                if (IsHerdHome(i) || i >= _herdPlaces.Count) continue;
                GameObject model = _fishModels != null && _fishModels.Length > 0 ? _fishModels[i % _fishModels.Length] : null;
                if (model == null) continue;
                _herds.Add(Herd.Spawn(i, _herdPlaces[i], model, fish, _fishLength, _fishModelYaw, _moteMaterial, _herdRoot));
            }
        }

        private readonly List<Vector3> _herdPlaces = new List<Vector3>();

        /// <summary>
        /// Where the herds are this playthrough: drawn at random from the zone's open floor, each
        /// far from the others and from Manunubo. Seeded from the save, so a reload (and the herds
        /// already brought home, which are saved by index) keeps the same places.
        /// </summary>
        private void PickHerdPlaces()
        {
            _herdPlaces.Clear();
            if (_herdSites == null || _herdSites.Count == 0)
            {
                if (_spawnPoints != null) foreach (Transform t in _spawnPoints) _herdPlaces.Add(t.position);
                return;
            }

            SaveData save = Save;
            int seed = save != null ? save.siteShuffleSeed : 0;
            if (seed == 0)
            {
                seed = UnityEngine.Random.Range(1, int.MaxValue);
                if (save != null) save.siteShuffleSeed = seed;
            }
            foreach (char c in StepId ?? "") seed = seed * 31 + c;   // a different draw from Level 1's sites
            var rng = new System.Random(seed);

            Vector3 home = _manunubo != null ? _manunubo.transform.position : transform.position;
            var pool = new List<Vector3>();
            foreach (Vector3 p in _herdSites) if (Flat(p - home) >= _awayFromManunubo) pool.Add(p);
            if (pool.Count == 0) pool.AddRange(_herdSites);

            int want = Mathf.Max(1, _total);
            float spacing = _herdSpacing;
            while (_herdPlaces.Count < want && pool.Count > 0)
            {
                var ok = new List<int>();
                for (int i = 0; i < pool.Count; i++)
                {
                    bool far = true;
                    foreach (Vector3 q in _herdPlaces) if (Flat(pool[i] - q) < spacing) { far = false; break; }
                    if (far) ok.Add(i);
                }
                if (ok.Count == 0) { spacing *= 0.8f; continue; }   // crowded: relax a little
                int take = ok[rng.Next(ok.Count)];
                _herdPlaces.Add(pool[take]);
                pool.RemoveAt(take);
            }
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        private void WakePredators()
        {
            float speed = Knob(Level2DifficultyKnobs.PredatorSpeed, 5.6f);
            float sight = Knob(Level2DifficultyKnobs.PredatorSight, 16f);
            float bite = Knob(Level2DifficultyKnobs.BiteDamage, 14f);
            foreach (Predator p in _predators)
            {
                if (p == null) continue;
                p.Configure(speed, sight, bite);
                p.gameObject.SetActive(true);
            }
        }

        private void ConfigureConch()
        {
            ConchCall conch = ConchCall.Instance;
            if (conch == null) return;
            conch.Radius = Knob(Level2DifficultyKnobs.ConchRadius, 12f);
            conch.Cooldown = Knob(Level2DifficultyKnobs.ConchCooldown, 6f);
        }

        // ───────────────────────── Play ─────────────────────────

        private void Update()
        {
            if (!IsRunning) return;
            if (!_started)
            {
                if (RelicLoadout.IsEquipped(GameConstants.Relics.RADIANT_CONCH)) StartGathering();
                return;
            }

            // Bring the ribbon home.
            HerdRibbon ribbon = HerdRibbon.Instance;
            if (ribbon.HasFollowers && _manunubo != null && PlayerController.Instance != null &&
                Vector3.Distance(PlayerController.Instance.transform.position, _manunubo.transform.position) <= _deliverRadius)
            {
                GameAudio.PlayAt(_deliveredClip, _manunubo.transform.position);
                foreach (Herd h in new List<Herd>(ribbon.Herds)) h.Deliver(_manunubo.transform, () => OnHerdHome(h));
            }

            PointNavigation(force: false);
        }

        private void OnHerdHome(Herd herd)
        {
            _herds.Remove(herd);
            if (Save != null) Save.AddChallengeId(HerdId(herd.Index));
            _delivered++;
            if (_manunubo != null)
            {
                _manunubo.SetBrightness(Brightness());
                GameAudio.PlayAt(_brightensClip, _manunubo.transform.position);
            }
            UpdateCounter();
            ChallengeManager.Instance?.ReportTaskCompleted();   // the last one completes the challenge
        }

        private void OnBit(Predator predator, bool harmed)
        {
            if (!IsRunning || !_started) return;
            if (!harmed) return;   // Bubble Barrier took it (and plays its own pop)
            ChallengeManager.Instance?.ReportError();
            Vector3 at = PlayerController.Instance != null ? PlayerController.Instance.transform.position : predator.transform.position;
            GameAudio.PlayAt(_sharkBiteClip, at);
            if (HerdRibbon.Instance.HasFollowers) GameAudio.PlayAt(_herdScatterClip, at, 0.85f);
            foreach (Herd h in new List<Herd>(HerdRibbon.Instance.Herds)) h.Scatter(at);
        }

        // ───────────────────────── Sound cues ─────────────────────────

        private bool Live => IsRunning && _started;

        private void OnHerdCalmed(Herd h) { if (Live) PlayAfterConch(_herdCalmedClip, 0.85f); }
        private void OnHerdJoined(Herd h) { if (Live) PlayAfterConch(_herdJoinsClip, 0.75f); }

        // The conch call plays in full first; the success sound follows it (once for herds called together).
        private readonly HashSet<AudioClip> _waitingForConch = new HashSet<AudioClip>();

        private void PlayAfterConch(AudioClip clip, float volume)
        {
            if (clip == null) return;
            float wait = ConchCall.SoundingUntil - Time.time;
            if (wait <= 0f) { GameAudio.PlayAt(clip, PlayerController.Instance != null ? PlayerController.Instance.transform.position : transform.position, volume); return; }
            if (_waitingForConch.Add(clip)) StartCoroutine(PlayWhenConchEnds(clip, volume));
        }

        private System.Collections.IEnumerator PlayWhenConchEnds(AudioClip clip, float volume)
        {
            while (Time.time < ConchCall.SoundingUntil) yield return null;
            _waitingForConch.Remove(clip);
            if (Live) GameAudio.PlayAt(clip, PlayerController.Instance != null ? PlayerController.Instance.transform.position : transform.position, volume);
        }
        private readonly Dictionary<Predator, float> _spottedAt = new Dictionary<Predator, float>();
        private float _gaveUpAt = -100f;

        private void OnChaseStarted(Predator p)
        {
            if (!Live) return;
            // A predator that loses you and finds you again a moment later doesn't announce itself twice.
            if (_spottedAt.TryGetValue(p, out float last) && Time.time - last < 6f) return;
            _spottedAt[p] = Time.time;
            GameAudio.PlayAt(_sharkSpotsClip, p.transform.position);
        }
        private void OnClosingIn(Predator p) { if (Live) GameAudio.PlayAt(_sharkGrowlClip, p.transform.position); }

        private void OnGaveUp(Predator p)
        {
            if (!Live) return;
            foreach (Predator other in _predators)
                if (other != null && other != p && other.isActiveAndEnabled && other.CurrentMode == Predator.Mode.Chase) return;   // still being chased
            if (Time.time - _gaveUpAt < 4f) return;
            _gaveUpAt = Time.time;
            GameAudio.PlayFlat(_sharkGivesUpClip, 0.8f);
        }

        private void OnEnteredCover(HidingZone zone)
        {
            if (Live && PlayerController.Instance != null) GameAudio.PlayAt(_hideRustleClip, PlayerController.Instance.transform.position, 0.8f);
        }

        /// <summary>
        /// Spirit Sense (through the tracker) leads to whatever is nearer: the closest scattered
        /// school, or — once fish follow the diver — Manunubo if it is closer than any of them.
        /// The tracker re-picks the nearest as the diver swims.
        /// </summary>
        private void PointNavigation(bool force)
        {
            bool followers = HerdRibbon.Instance.HasFollowers;
            if (!force && followers == _hadFollowers) return;
            _hadFollowers = followers;
            ObjectiveTracker tracker = ObjectiveTracker.Instance;
            if (tracker == null) return;
            var markers = new List<Transform>();
            if (followers && _manunubo != null) markers.Add(_manunubo.transform);
            foreach (Herd h in _herds) if (h != null) markers.Add(h.Marker);
            // A school is done once it stops being scattered (calmed, following, home); Manunubo never is.
            Transform manunubo = _manunubo != null ? _manunubo.transform : null;
            tracker.SetObjectiveSet(markers, t =>
            {
                if (t == null) return true;
                if (t == manunubo) return false;
                Herd herd = t.GetComponentInParent<Herd>();
                return herd == null || herd.State != Herd.HerdState.Scattered;
            });
        }

        private void UpdateCounter() => QuestObjectiveUI.Instance?.SetCounter(_delivered, _total, _counterFormat);

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!IsRunning || evt.ChallengeId != StepId) return;
            Finish();
        }

        private void Finish()
        {
            foreach (Predator p in _predators) if (p != null) p.gameObject.SetActive(false);
            PredatorAlertUI.Remove();
            foreach (Herd h in _herds) if (h != null) Destroy(h.gameObject);
            _herds.Clear();
            if (_manunubo != null) _manunubo.SetBrightness(1f);
            ObjectiveTracker.Instance?.Clear();
            Complete();
        }

        private void DebugForceComplete()
        {
            for (int i = 0; i < _total; i++) if (Save != null) Save.AddChallengeId(HerdId(i));
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.CompleteChallenge(true);
            Finish();
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform spawnPoints, GameObject[] fishModels, Material moteMaterial, ManunuboWisp manunubo,
                                List<Predator> predators, MinigameDifficultyProfile profile)
        {
            _spawnPoints = spawnPoints;
            _fishModels = fishModels;
            _moteMaterial = moteMaterial;
            _manunubo = manunubo;
            _predators = predators;
            _profile = profile;
        }

        public void EditorSetHerdSites(List<Vector3> sites) => _herdSites = sites;

        public void EditorSetAudio(AudioClip calmed, AudioClip joins, AudioClip delivered, AudioClip brightens, AudioClip spots,
                                   AudioClip growl, AudioClip bite, AudioClip scatter, AudioClip givesUp, AudioClip rustle,
                                   AudioClip tension, AudioClip hidden)
        {
            _herdCalmedClip = calmed; _herdJoinsClip = joins; _deliveredClip = delivered; _brightensClip = brightens;
            _sharkSpotsClip = spots; _sharkGrowlClip = growl; _sharkBiteClip = bite; _herdScatterClip = scatter;
            _sharkGivesUpClip = givesUp; _hideRustleClip = rustle; _chaseTensionLoop = tension; _hiddenClip = hidden;
        }
#endif
    }
}
