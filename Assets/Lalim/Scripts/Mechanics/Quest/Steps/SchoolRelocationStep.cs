using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Lalim.Challenges.Catching;
using Lalim.Challenges.MoonRitual;
using Lalim.Challenges.Relocation;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Inventory;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Padre Burgos, Minigame 3 — relocation under the chase (3rd person, the whole map).
    ///
    ///  1. Awakening: after Bakunawa's angry words (the talk before this step) it roars. The sea
    ///     shakes, rocks fall from the wall, every barrier drops and the sanctuaries light up in all
    ///     three zones. The diver swims away from Bakunawa first ("Swim away from Bakunawa").
    ///  2. Relocation: the schools caught in Ajuy ride in the net-bag. Every school and every
    ///     sanctuary belongs to a moon phase: each glows that phase's colour and the sanctuary sings
    ///     its melody (the ritual's songs, so this teaches the ritual). In a sanctuary of a school's
    ///     colour the interact button offers "Release the fish": the school pours out of the bag and
    ///     swims a figure of eight around the diver and the dugong under its moon symbol, singing
    ///     along, then swims into its new home. A place that matches nothing dims for a moment with
    ///     an off-key note. No cards or buttons. There are
    ///     eight, one per phase, and they go home in the moon's order (New Moon first, as the ritual
    ///     is sung): the Compass (equipped) points to the next one's home and fades within 60 m, so
    ///     the colours and songs do the matching. A school's home reached out of turn only stirs. Each extra school carried slows the diver by 10% and
    ///     lets Bakunawa sense them from further away.
    ///  3. Bakunawa hunts the diver everywhere; a strike bursts the bag too. Burst schools scatter,
    ///     glowing panicked yellow: the Radiant Conch calms them and calls them close, and the
    ///     Woven Net catches them back.
    ///  4. With every school home, Bakunawa swims back to its lair and the step ends; the moon
    ///     ritual (MoonPhaseRitualStep) appeases it.
    ///
    /// Saved: the awakening and each school delivered. DDA: knobs for Bakunawa's speed, sense range,
    /// strike damage, calm and rubber-band times. Metrics: time, deliveries (tasks), strikes (errors),
    /// dispersals, places visited that matched nothing, and the most schools carried at once.
    /// </summary>
    public class SchoolRelocationStep : QuestStep
    {
        private enum Phase { Idle, Waking, Escape, Relocate, Done }

        [Header("Cast")]
        [SerializeField] private Bakunawa _bakunawa;
        [Tooltip("The Ajuy catching step: the schools, their models and the net.")]
        [SerializeField] private SelectiveCatchingStep _catching;
        [SerializeField] private List<Sanctuary> _sanctuaries = new List<Sanctuary>();

        [Header("The awakening")]
        [Tooltip("Barriers that drop at the roar (the trench gates; not the exit tunnel).")]
        [SerializeField] private List<GameObject> _barriers = new List<GameObject>();
        [Tooltip("Rock models that break off the wall and fall when it roars.")]
        [SerializeField] private GameObject[] _rockModels = new GameObject[0];
        [SerializeField, Range(0, 24)] private int _fallingRocks = 10;
        [Tooltip("Rock breaking off the wall: once in the head as the roar shakes the sea, then from a few of the falling rocks.")]
        [SerializeField] private AudioClip _rockCollapseClip;
        [Tooltip("Above this height the diver has escaped the abyss.")]
        [SerializeField] private float _escapeHeight = -110f;
        [Tooltip("The diver has got away once this far from Bakunawa (out of the abyss)…")]
        [SerializeField, Min(10f)] private float _escapeDistance = 40f;
        [Tooltip("…or has kept swimming this long since the roar.")]
        [SerializeField, Min(3f)] private float _escapeSeconds = 15f;
        [Tooltip("Where a diver who died (or resumed a save) down in the abyss during the chase comes back: up on the rim, out of its reach.")]
        [SerializeField] private Transform _rimRespawn;

        [Header("Play")]
        [SerializeField, Range(0f, 0.3f)] private float _slowPerExtraSchool = 0.1f;
        [SerializeField, Min(0f)] private float _sensePerExtraSchool = 12f;
        [SerializeField, Min(10f)] private float _compassCloseRange = 60f;
        [SerializeField, Min(2f)] private float _scatterRadius = 10f;
        [Tooltip("Schools go home in the moon's order (New Moon first); the Compass points to the next one's home.")]
        [SerializeField] private bool _inOrder = true;

        [Header("Quest box (what, not how: the player works out the how)")]
        [SerializeField] private string _escapeText = "Swim away from Bakunawa.";
        [SerializeField] private string _equipCompassText = "Equip the compass in your inventory.";
        [SerializeField] private string _relocateText = "Bring each school home to the place that glows and sings like it.";
        [SerializeField] private string _scatteredText = "Your schools have scattered.";

        [Header("Look and sound")]
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;
        [Tooltip("A school swimming into its new home.")]
        [SerializeField] private AudioClip _deliveredClip;
        [Tooltip("An unlit see-through material for the schools' glow in the net-bag.")]
        [SerializeField] private Material _glowMaterial;
        [Tooltip("Seconds a released school swirls around the diver and dugong before it swims home.")]
        [SerializeField, Min(2f)] private float _releaseSeconds = 7f;
        [Tooltip("The release button: {0} is the school (as named in Release Names).")]
        [SerializeField] private string _releasePrompt = "Release the {0}";
        [Tooltip("What the release button calls each school: \"key=name\" (others use their species name).")]
        [SerializeField] private string[] _releaseNames =
        {
            "eel=eels", "lobster=lobsters", "doryfish=blue tangs", "squid=squids", "tuna=tunas",
            "crab=mud crabs", "clownfish=clownfishes", "brownfish=rabbitfishes",
        };

        [Header("DDA")]
        [SerializeField] private MinigameDifficultyProfile _profile;
        [SerializeField, Min(10f)] private float _expectedSecondsPerSchool = 90f;

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
        private static bool Has(string id) => Save != null && Save.HasChallengeId(id);
        private string AwakeId => $"{StepId}_awake";
        private string HomeId(string key) => $"{StepId}_home_{key}";

        private Phase _phase;
        private NetBag _bag;
        private RelocationHud _hud;
        private readonly List<CatchableSchool> _scattered = new List<CatchableSchool>();
        private readonly List<string> _carried = new List<string>();
        private Transform _root;
        private Transform _tracked;
        private string _objective;
        private int _mismatchedVisits, _strikes, _dispersals, _maxCarried;
        private readonly HashSet<Sanctuary> _visited = new HashSet<Sanctuary>();
        private readonly HashSet<string> _goingHome = new HashSet<string>();
        private Sanctuary _inside;
        private ReleaseSpot _spot;
        private bool _releasing;
        private float _escapeFrom;

        private void OnEnable()
        {
            NetThrower.Opened += OnNetOpened;
            Bakunawa.Struck += OnStruck;
            EventBus.Subscribe<RelicUsedEvent>(OnRelicUsed);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnRespawned);
        }

        private void OnDisable()
        {
            NetThrower.Opened -= OnNetOpened;
            Bakunawa.Struck -= OnStruck;
            EventBus.Unsubscribe<RelicUsedEvent>(OnRelicUsed);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnRespawned);
            if (PlayerController.Instance != null) PlayerController.Instance.ClearSpeedMultiplier(this);
        }

        // ───────────────────────── Begin / restore ─────────────────────────

        protected override void OnBegin()
        {
            EnsureRelics();
            ConfigureBakunawa();
            if (_catching != null) NetThrower.Ensure(_catching.LineMaterial, _catching.NetModel, _catching.NetThrowClip, _catching.NetLandClip);
            NetBag.GlowMaterial = _glowMaterial;
            _bag = NetBag.Ensure(_catching != null ? _catching.LineMaterial : null);
            RefillBag();
            if (_hud == null)
            {
                _hud = RelocationHud.Create(_titleFont, _bodyFont);
                _hud.transform.SetParent(transform, false);
            }
            if (_spot == null)
            {
                _spot = ReleaseSpot.Create(transform);
                _spot.Prompt = () => { string key = ReleaseCandidate(); return key != null ? string.Format(_releasePrompt, ReleaseName(key)) : ""; };
                _spot.Released = Release;
            }

            if (Has(AwakeId))
            {
                OpenMap();
                _bakunawa?.WakeResting();
                LeaveThePit();
                BeginRelocation();
            }
            else StartCoroutine(Wake());
        }

        public override void RestoreCompleted()
        {
            Sanctuary.SilenceAll();
            OpenMap();
            foreach (string key in Schools) if (Has(HomeId(key))) Settle(key, null);
            // Bakunawa's state after this is the ritual's to set (watching, or appeased).
            if (NetBag.Instance != null) NetBag.Instance.Clear();
        }

        private IReadOnlyList<string> Schools => _catching != null ? _catching.TestSchools : (IReadOnlyList<string>)new string[0];

        private void EnsureRelics()
        {
            InventoryManager inv = InventoryManager.Instance;
            if (inv == null) return;
            foreach (string id in new[] { GameConstants.Relics.RADIANT_CONCH, GameConstants.Relics.WOVEN_NET_FRAGMENT, GameConstants.Relics.COMPASS })
            {
                if (inv.HasItem(id)) continue;
                Debug.LogWarning($"[SchoolRelocation] The save has no {id}; granting it for this run.");
                inv.AddItem(id);
            }
        }

        private void ConfigureBakunawa()
        {
            if (_bakunawa == null) return;
            _bakunawa.Configure(Knob(Level2DifficultyKnobs.BakunawaSpeed, 6.2f), Knob(Level2DifficultyKnobs.BakunawaSense, 45f),
                                Knob(Level2DifficultyKnobs.StrikeDamage, 20f), Knob(Level2DifficultyKnobs.CalmSeconds, 10f),
                                Knob(Level2DifficultyKnobs.ReturnSeconds, 60f));
        }

        private float Knob(string key, float fallback) => _profile != null && _profile.Has(key) ? _profile.Get(key) : fallback;

        /// <summary>Every school caught in Ajuy that isn't home yet rides in the bag.</summary>
        private void RefillBag()
        {
            // Schools caught in Ajuy are already aboard: give them their colours.
            foreach (string key in new List<string>(_bag.SchoolKeys)) _bag.Tint(key, ColorOf(key));
            foreach (string key in Schools)
            {
                if (Has(HomeId(key)) || _bag.Has(key) || _scattered.Exists(s => s != null && s.SpeciesKey == key)) continue;
                SelectiveCatchingStep.Species sp = _catching.SpeciesOf(key);
                if (sp != null) _bag.Add(key, sp.model, _catching.SchoolSize, sp.modelYaw, ColorOf(key));
            }
        }

        // ───────────────────────── Moon phases ─────────────────────────

        /// <summary>The sanctuary a school belongs to (its moon phase, colour and song come from it).</summary>
        private Sanctuary HomeOf(string key) => _sanctuaries.Find(s => s != null && s.Accepts(key));

        private Color ColorOf(string key)
        {
            Sanctuary home = HomeOf(key);
            return home != null ? home.GlowColor : Color.white;
        }

        /// <summary>The schools in the moon's order (their homes' phases, New Moon first).</summary>
        private List<string> InOrder()
        {
            var keys = new List<string>(Schools);
            keys.Sort((a, b) => PhaseOf(a).CompareTo(PhaseOf(b)));
            return keys;
        }

        private int PhaseOf(string key)
        {
            Sanctuary home = HomeOf(key);
            return home != null && home.MoonPhase >= 0 ? home.MoonPhase : 99;
        }

        /// <summary>The next school to bring home (the earliest phase not home yet), or null.</summary>
        private string NextSchool()
        {
            foreach (string key in InOrder()) if (!Has(HomeId(key))) return key;
            return null;
        }

        /// <summary>The nearest switched-on sanctuary the diver hasn't been into yet (all visited: start over).</summary>
        private Sanctuary NearestUnvisited(Vector3 from)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Sanctuary best = null;
                float bestD = float.MaxValue;
                foreach (Sanctuary s in _sanctuaries)
                {
                    if (s == null || !s.IsOn || _visited.Contains(s)) continue;
                    float d = Vector3.Distance(from, s.Centre);
                    if (d < bestD) { bestD = d; best = s; }
                }
                if (best != null) return best;
                _visited.Clear();
                if (_inside != null) _visited.Add(_inside);
            }
            return null;
        }

        // ───────────────────────── The awakening ─────────────────────────

        private IEnumerator Wake()
        {
            _phase = Phase.Waking;
            SetObjective("Bakunawa is waking…");
            PlayerController.Instance?.SetMovementEnabled(false);
            if (Save != null) Save.AddChallengeId(AwakeId);
            OpenMap();
            StartCoroutine(Rockfall());
            if (_bakunawa != null) yield return _bakunawa.Roar();
            else yield return new WaitForSeconds(2f);
            PlayerController.Instance?.SetMovementEnabled(true);
            if (!IsRunning) yield break;
            _phase = Phase.Escape;
            _escapeFrom = Time.time;
            ObjectiveTracker.Instance?.Clear();
            SetObjective(_escapeText, true);
        }

        /// <summary>Out of the abyss and either well away from Bakunawa or still swimming after a while.</summary>
        private bool Escaped(PlayerController pc)
        {
            if (pc == null || pc.transform.position.y < _escapeHeight) return false;
            if (Time.time - _escapeFrom >= _escapeSeconds) return true;
            return _bakunawa == null || Vector3.Distance(pc.transform.position, _bakunawa.HeadPosition) >= _escapeDistance;
        }

        /// <summary>Barriers down and every sanctuary lit: from the roar on, the minigame spans the whole map.</summary>
        private void OpenMap()
        {
            foreach (GameObject b in _barriers) if (b != null) b.SetActive(false);
            foreach (Sanctuary s in _sanctuaries) if (s != null) s.Show(true);
        }

        private IEnumerator Rockfall()
        {
            if (_bakunawa == null) yield break;
            GameAudio.PlayFlat(_rockCollapseClip, 0.8f);
            if (_rockModels == null || _rockModels.Length == 0) yield break;
            Vector3 lair = _bakunawa.transform.position;
            var falling = new List<(Transform t, float floor, float v)>();
            for (int i = 0; i < _fallingRocks; i++)
            {
                // Break off the wall somewhere around the pit, part way up.
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 start = lair + Vector3.up * Random.Range(40f, 90f);
                if (Physics.Raycast(start, dir, out RaycastHit wall, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    start = wall.point - dir * 4f;
                GameObject model = _rockModels[Random.Range(0, _rockModels.Length)];
                if (model == null) continue;
                GameObject rock = Instantiate(model, start, Random.rotation, _root != null ? _root : transform);
                rock.transform.localScale *= Random.Range(0.6f, 1.4f);
                foreach (Collider c in rock.GetComponentsInChildren<Collider>()) Destroy(c);
                falling.Add((rock.transform, SwimBody.SeabedY(start + dir * 3f, start.y - 2f), 0f));
                if (i % 3 == 1) GameAudio.PlayAt(_rockCollapseClip, start, 0.9f, AudioChannel.Sfx, 30f);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.25f));
            }
            // They tumble down through the water (slower than in air) and settle on the floor.
            float until = Time.time + 12f;
            while (falling.Count > 0 && Time.time < until)
            {
                for (int i = falling.Count - 1; i >= 0; i--)
                {
                    var f = falling[i];
                    if (f.t == null) { falling.RemoveAt(i); continue; }
                    f.v = Mathf.Min(f.v + 9f * Time.deltaTime, 14f);
                    f.t.position += Vector3.down * f.v * Time.deltaTime;
                    f.t.Rotate(new Vector3(40f, 25f, 10f) * Time.deltaTime);
                    if (f.t.position.y <= f.floor + 1f) { falling.RemoveAt(i); continue; }
                    falling[i] = f;
                }
                yield return null;
            }
        }

        // ───────────────────────── Relocation ─────────────────────────

        private void BeginRelocation()
        {
            _phase = Phase.Relocate;
            int left = 0;
            foreach (string key in Schools) if (!Has(HomeId(key))) left++;
            if (left == 0) { Finish(); return; }
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.SchoolRelocation,
                ChallengeId = StepId,
                TaskQuantity = left,       // each school home
                TimeLimit = 0f,
                HintBudget = 0,
                Perspective = ChallengePerspective.ThirdPerson
            });
            ChallengeManager.Instance?.SetExpectedCompletionTime(left * _expectedSecondsPerSchool);
            if (_profile != null) ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
            UpdateObjective(true);
        }

        private void Update()
        {
            if (!IsRunning) return;
            if (_phase == Phase.Escape)
            {
                if (Escaped(PlayerController.Instance)) BeginRelocation();
                return;
            }
            if (_phase != Phase.Relocate) return;

            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            _carried.Clear();
            if (_bag != null) _carried.AddRange(_bag.SchoolKeys);
            _maxCarried = Mathf.Max(_maxCarried, _carried.Count);

            // Carrying more costs speed and draws Bakunawa from further away.
            int extra = Mathf.Max(0, _carried.Count - 1);
            player.SetSpeedMultiplier(this, 1f - _slowPerExtraSchool * extra);
            if (_bakunawa != null) _bakunawa.SenseBonus = _sensePerExtraSchool * extra;

            _scattered.RemoveAll(s => s == null);
            if (NetThrower.Instance != null)
                NetThrower.Instance.Armed = _scattered.Count > 0 && RelicLoadout.IsEquipped(GameConstants.Relics.WOVEN_NET_FRAGMENT);

            bool compass = RelicLoadout.IsEquipped(GameConstants.Relics.COMPASS);
            Sanctuary target = null;
            if (_carried.Count > 0)
            {
                string next = _inOrder ? NextSchool() : null;
                target = next != null ? HomeOf(next) : NearestUnvisited(player.transform.position);
            }
            Camera cam = Camera.main;
            _hud?.SetCompass(compass, target, cam != null ? cam.transform : player.transform, _compassCloseRange);
            UpdateArrival(player.transform.position);
            _spot?.Follow(player.transform.position, ReleaseCandidate() != null);

            Transform track = _scattered.Count > 0 ? _scattered[0].transform
                            : compass && target != null ? target.transform : null;
            if (track != _tracked)
            {
                _tracked = track;
                if (track != null) ObjectiveTracker.Instance?.SetObjective(track);
                else ObjectiveTracker.Instance?.Clear();
            }
            UpdateObjective(false);
        }

        private string ReleaseName(string key)
        {
            foreach (string entry in _releaseNames)
            {
                int eq = entry != null ? entry.IndexOf('=') : -1;
                if (eq > 0 && entry.Substring(0, eq).Trim() == key) return entry.Substring(eq + 1).Trim();
            }
            return NameOf(key);
        }

        private string NameOf(string key)
        {
            SelectiveCatchingStep.Species sp = _catching != null ? _catching.SpeciesOf(key) : null;
            return sp != null && !string.IsNullOrEmpty(sp.displayName) ? sp.displayName : key;
        }

        private void UpdateObjective(bool force)
        {
            string text;
            if (_scattered.Count > 0) text = _scatteredText;
            else if (!RelicLoadout.IsEquipped(GameConstants.Relics.COMPASS)) text = _equipCompassText;
            else text = _relocateText;
            if (!force && text == _objective) return;
            _objective = text;
            SetObjective(text);
            int home = 0;
            foreach (string key in Schools) if (Has(HomeId(key))) home++;
            // The count belongs to the homecoming itself, not to "equip the compass" or "scattered".
            if (_phase == Phase.Relocate && text == _relocateText) QuestObjectiveUI.Instance?.SetCounter(home, Schools.Count);
            else QuestObjectiveUI.Instance?.ClearCounter();
        }

        private void SetObjective(string text, bool openBox = false) => QuestObjectiveUI.Instance?.SetObjective(DialogueManager.ResolveTokens(text), openBox);

        /// <summary>
        /// Swimming into a sanctuary: if a school aboard belongs here it brightens (its turn: the
        /// "Release the fish" button shows; not yet: the place only stirs); if none does, the place
        /// and the bag dim with an off-key note.
        /// </summary>
        private void UpdateArrival(Vector3 diver)
        {
            Sanctuary here = Sanctuary.At(diver);
            if (here == _inside) return;
            _inside = here;
            if (here == null || _bag == null) return;
            _visited.Add(here);
            ChallengeManager.Instance?.ReportPlayerAction();
            bool any = false;
            foreach (string key in _carried)
            {
                if (!here.Accepts(key) || _goingHome.Contains(key)) continue;
                any = true;
                here.Stir();
                _bag.Brighten(key);
            }
            if (any || _carried.Count == 0) return;
            _mismatchedVisits++;
            here.Dim();
            _bag.Dim();
            Sfx.Play(SfxCue.Wrong, 0.35f);
        }

        /// <summary>The school that can be released here now (aboard, belongs here, its turn), or null.</summary>
        private string ReleaseCandidate()
        {
            if (_releasing || _phase != Phase.Relocate || _inside == null || _bag == null) return null;
            string next = _inOrder ? NextSchool() : null;
            foreach (string key in _carried)
            {
                if (!_inside.Accepts(key) || _goingHome.Contains(key) || !_bag.Has(key)) continue;
                if (next != null && key != next) continue;
                return key;
            }
            return null;
        }

        /// <summary>"Release the fish": the school whose home this is goes home.</summary>
        private void Release()
        {
            string key = ReleaseCandidate();
            if (key == null) return;
            ChallengeManager.Instance?.ReportPlayerAction();
            StartCoroutine(GoHome(key, _inside));
        }

        private IEnumerator GoHome(string key, Sanctuary here)
        {
            _goingHome.Add(key);
            _releasing = true;
            _bag.Brighten(key);
            GameAudio.PlayFlat(_deliveredClip);   // the moment it's let go
            ChallengeManager.Instance?.ReportCorrectAction();
            yield return new WaitForSeconds(0.3f);
            // A strike may have burst the bag meanwhile: then it's scattered, not home.
            SelectiveCatchingStep.Species sp = _catching != null ? _catching.SpeciesOf(key) : null;
            if (!IsRunning || _bag == null || !_bag.Has(key) || sp == null || sp.model == null)
            {
                _goingHome.Remove(key);
                _releasing = false;
                yield break;
            }
            Vector3 from = _bag.PositionOf(key);
            _bag.Remove(key);
            if (Save != null) Save.AddChallengeId(HomeId(key));   // out of the bag: it is going home whatever happens now
            StartCoroutine(SingAlong(here));
            here.Celebrate();

            // It circles the diver and the dugong, then swims into its home. Crabs and lobsters walk
            // the circle on the sand instead (crabs sideways, as crabs do).
            bool swirling = true;
            PlayerController diver = PlayerController.Instance;
            Transform dugong = Lalim.Abilities.DugongCompanion.Instance != null ? Lalim.Abilities.DugongCompanion.Instance.transform : null;
            SchoolReleaseSwirl.Gait gait = key == "crab" ? SchoolReleaseSwirl.Gait.Sideways
                                         : key == "lobster" ? SchoolReleaseSwirl.Gait.Walk
                                         : SchoolReleaseSwirl.Gait.Swim;
            SchoolReleaseSwirl.Play(sp.model, Mathf.Max(6, _catching.SchoolSize), sp.length, sp.modelYaw, from,
                                    diver != null ? diver.transform : here.transform, dugong, null, _releaseSeconds,   // the moon shows on the place itself
                                    at => { here.AddSchoolAt(sp.model, sp.length, sp.modelYaw, at); swirling = false; }, gait);
            UpdateObjective(true);   // the counter moves on, and the Compass to the next home
            while (swirling) yield return null;

            _goingHome.Remove(key);
            _releasing = false;
            bool allHome = true;
            foreach (string k in Schools) if (!Has(HomeId(k))) allHome = false;
            ChallengeManager.Instance?.ReportTaskCompleted();   // the last one completes the challenge → Finish
            if (allHome) { if (_phase != Phase.Done) Finish(); }
            else UpdateObjective(true);
        }

        /// <summary>The school joins in: the place's song swells close up, in step with it, then fades.</summary>
        private IEnumerator SingAlong(Sanctuary here)
        {
            if (here.Song == null) yield break;
            var go = new GameObject("[SingAlong]");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = here.Song;
            src.spatialBlend = 0f;
            src.playOnAwake = false;
            if (here.SongSource != null) src.time = here.SongSource.time;
            src.volume = 0f;
            src.Play();
            float full = 0.7f * GameAudio.Gain(AudioChannel.Music) * GameAudio.ClipGain(here.Song);
            float hold = _releaseSeconds;
            for (float t = 0f; t < hold + 1.5f && src != null; t += Time.deltaTime)
            {
                float k = t < 0.4f ? t / 0.4f : t > hold ? 1f - (t - hold) / 1.5f : 1f;
                src.volume = full * Mathf.Clamp01(k);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        /// <summary>A school delivered (or, restoring a save, already home) joins its sanctuary's residents.</summary>
        private void Settle(string key, Sanctuary at, Vector3? from = null)
        {
            if (at == null) at = _sanctuaries.Find(s => s != null && s.Accepts(key));
            SelectiveCatchingStep.Species sp = _catching != null ? _catching.SpeciesOf(key) : null;
            if (at == null || sp == null || sp.model == null) return;
            at.AddSchool(sp.model, _catching.SchoolSize, sp.length, sp.modelYaw, from ?? at.Centre);
        }

        // ───────────────────────── Dispersal ─────────────────────────

        /// <summary>The net-bag bursts: every school carried scatters near <paramref name="at"/>, panicked.</summary>
        private void Burst(Vector3 at)
        {
            if (_bag == null || _bag.SchoolCount == 0) return;
            _dispersals++;
            if (_root == null)
            {
                _root = new GameObject("[Relocation]").transform;
                _root.SetParent(transform, false);
            }
            var keys = new List<string>(_bag.SchoolKeys);
            _bag.Clear();
            foreach (string key in keys)
            {
                SelectiveCatchingStep.Species sp = _catching != null ? _catching.SpeciesOf(key) : null;
                if (sp == null) continue;
                var spec = new CatchableSchool.Spec
                {
                    Key = key,
                    Name = sp.displayName,
                    Protected = false,
                    Model = sp.model,
                    Count = _catching.SchoolSize,
                    Length = sp.length,
                    ModelYaw = sp.modelYaw,
                    Speed = 2.4f * sp.speedScale,
                    MinHeight = 1f,
                    MaxHeight = 3.5f,
                    Static = false,
                };
                Vector2 off = Random.insideUnitCircle * _scatterRadius * 0.6f;
                Vector3 spot = at + new Vector3(off.x, 0f, off.y);
                CatchableSchool s = CatchableSchool.Spawn(spec, at, _scatterRadius, spot, _catching.AuraMaterial, _root);
                s.SetMidwater(true);   // they stay at the depth the bag burst, not sinking into the abyss
                s.Release(at);         // bolt away from the burst first
                s.SetTint(ColorOf(key));
                s.SetPanicked(true);   // its own colour, flickering
                _scattered.Add(s);
            }
            UpdateObjective(true);
        }

        private void OnNetOpened(Vector3 at, float radius)
        {
            if (!IsRunning || _scattered.Count == 0 || _bag == null) return;
            foreach (CatchableSchool s in _scattered.ToArray())
            {
                if (s == null || !s.Inside(at, radius)) continue;
                _scattered.Remove(s);
                s.Net();
                ChallengeManager.Instance?.ReportCorrectAction();
                CatchableSchool school = s;
                school.PullInto(_bag.Mouth, () =>
                {
                    SelectiveCatchingStep.Species sp = _catching.SpeciesOf(school.SpeciesKey);
                    if (sp != null && _bag != null) _bag.Add(school.SpeciesKey, sp.model, _catching.SchoolSize, sp.modelYaw, ColorOf(school.SpeciesKey));
                    UpdateObjective(true);
                });
            }
        }

        private void OnRelicUsed(RelicUsedEvent evt)
        {
            if (!IsRunning || evt.ItemId != GameConstants.Relics.RADIANT_CONCH) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            Vector3 p = player.transform.position;

            // The call calms scattered schools in reach and draws them in close to the diver.
            float reach = Lalim.Challenges.Gathering.ConchCall.Instance != null ? Lalim.Challenges.Gathering.ConchCall.Instance.Radius + 4f : 16f;
            foreach (CatchableSchool s in _scattered)
            {
                if (s == null || Vector3.Distance(s.Centre, p) > reach) continue;
                s.SetPanicked(false);
                s.MoveHome(p + player.transform.forward * 4f, 4f);
            }
        }

        private void OnStruck(bool harmed)
        {
            if (!IsRunning) return;
            _strikes++;
            ChallengeManager.Instance?.ReportError();
            PlayerController player = PlayerController.Instance;
            if (player != null && _bag != null && _bag.SchoolCount > 0) Burst(player.transform.position);
        }

        private void OnRespawned(PlayerRespawnedEvent evt)
        {
            if (!IsRunning || evt.IsResume) return;
            _bakunawa?.ResetToLair();
            LeaveThePit();   // the screen is still black here
        }

        /// <summary>
        /// The Zone 3 checkpoint is on the abyss floor, beside the lair: coming back there after
        /// the roar would wake Bakunawa at once. Start up on the rim instead.
        /// </summary>
        private void LeaveThePit()
        {
            PlayerController pc = PlayerController.Instance;
            if (_rimRespawn == null || pc == null || pc.transform.position.y >= _escapeHeight) return;
            pc.SetPosition(_rimRespawn.position);
            pc.transform.rotation = _rimRespawn.rotation;
            pc.SyncFacingToTransform();
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!IsRunning || evt.ChallengeId != StepId || !evt.Success) return;
            Finish();
        }

        private void Finish()
        {
            if (_phase == Phase.Done) return;
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_dispersals", Value = _dispersals });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_max_carried", Value = _maxCarried });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_mismatched_visits", Value = _mismatchedVisits });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_strikes", Value = _strikes });
            _phase = Phase.Done;
            PlayerController.Instance?.ClearSpeedMultiplier(this);
            if (NetThrower.Instance != null) NetThrower.Instance.Armed = false;
            foreach (CatchableSchool s in _scattered) if (s != null) Destroy(s.gameObject);
            _scattered.Clear();
            if (_bag != null) Destroy(_bag.gameObject);   // empty now: the diver stops towing it
            if (_hud != null) Destroy(_hud.gameObject);
            if (_spot != null) Destroy(_spot.gameObject);
            ObjectiveTracker.Instance?.Clear();
            QuestObjectiveUI.Instance?.ClearCounter();
            _bakunawa?.GoHomeAndWait();   // home to its lair, where the ritual waits
            Sanctuary.SilenceAll();       // every school is home: the places stop singing
            Complete();
        }

        private void DebugForceComplete()
        {
            StopAllCoroutines();
            PlayerController.Instance?.SetMovementEnabled(true);
            if (Save != null)
            {
                Save.AddChallengeId(AwakeId);
                foreach (string key in Schools) Save.AddChallengeId(HomeId(key));
            }
            OpenMap();
            if (NetBag.Instance != null) NetBag.Instance.Clear();
            foreach (string key in Schools) Settle(key, null);
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.CompleteChallenge(true);
            else Finish();
        }

#if UNITY_EDITOR
        public void EditorSetup(Bakunawa bakunawa, SelectiveCatchingStep catching, List<Sanctuary> sanctuaries, List<GameObject> barriers,
                                GameObject[] rockModels, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, AudioClip deliveredClip, MinigameDifficultyProfile profile)
        {
            _bakunawa = bakunawa;
            _catching = catching;
            _sanctuaries = sanctuaries;
            _barriers = barriers;
            _rockModels = rockModels;
            _titleFont = titleFont;
            _bodyFont = bodyFont;
            _deliveredClip = deliveredClip;
            _profile = profile;
        }

        public void EditorSetGlowMaterial(Material glow) => _glowMaterial = glow;

        public void EditorSetRockCollapse(AudioClip clip) => _rockCollapseClip = clip;

        public void EditorSetRimRespawn(Transform rim) => _rimRespawn = rim;
#endif
    }
}
