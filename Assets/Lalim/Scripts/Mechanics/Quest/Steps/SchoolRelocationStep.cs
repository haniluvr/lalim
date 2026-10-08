using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Lalim.Challenges.Catching;
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
    ///     three zones. The diver escapes the abyss first.
    ///  2. Relocation: the schools caught in Ajuy ride in the net-bag. Inside a sanctuary, a card
    ///     offers to release each one: the right home takes it in; a wrong one bursts the bag.
    ///     The Compass (equipped) points to the nearest home for what's carried, and stops pointing
    ///     within 60 m, so the player matches school to habitat themselves. Each extra school carried
    ///     slows the diver by 10% and lets Bakunawa sense them from further away.
    ///  3. Bakunawa hunts the diver everywhere; a strike bursts the bag too. Burst schools scatter,
    ///     glowing panicked yellow: the Radiant Conch calms them and calls them close, and the
    ///     Woven Net catches them back.
    ///  4. Appeasement: with every school home, Bakunawa returns to its lair; the diver plays the
    ///     Radiant Conch for it there, and the sea calms.
    ///
    /// Saved: the awakening and each school delivered. DDA: knobs for Bakunawa's speed, sense range,
    /// strike damage, calm and rubber-band times. Metrics: time, deliveries (tasks), wrong drops and
    /// strikes (errors), plus dispersals and the most schools carried at once.
    /// </summary>
    public class SchoolRelocationStep : QuestStep
    {
        private enum Phase { Idle, Waking, Escape, Relocate, Appease, Done }

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
        [Tooltip("Above this height the diver has escaped the abyss.")]
        [SerializeField] private float _escapeHeight = -110f;

        [Header("Play")]
        [SerializeField, Range(0f, 0.3f)] private float _slowPerExtraSchool = 0.1f;
        [SerializeField, Min(0f)] private float _sensePerExtraSchool = 12f;
        [SerializeField, Min(10f)] private float _compassCloseRange = 60f;
        [SerializeField, Min(2f)] private float _scatterRadius = 10f;

        [Header("Look and sound")]
        [SerializeField] private TMP_FontAsset _titleFont;
        [SerializeField] private TMP_FontAsset _bodyFont;
        [Tooltip("A school swimming into its new home.")]
        [SerializeField] private AudioClip _deliveredClip;

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
        private int _wrongDrops, _strikes, _dispersals, _maxCarried;

        private void OnEnable()
        {
            NetThrower.Opened += OnNetOpened;
            Bakunawa.Struck += OnStruck;
            Bakunawa.WentHome += OnWentHome;
            EventBus.Subscribe<RelicUsedEvent>(OnRelicUsed);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnRespawned);
        }

        private void OnDisable()
        {
            NetThrower.Opened -= OnNetOpened;
            Bakunawa.Struck -= OnStruck;
            Bakunawa.WentHome -= OnWentHome;
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
            _bag = NetBag.Ensure(_catching != null ? _catching.LineMaterial : null);
            RefillBag();
            if (_hud == null)
            {
                _hud = RelocationHud.Create(_titleFont, _bodyFont);
                _hud.transform.SetParent(transform, false);
                _hud.ReleasePressed += Release;
            }

            if (Has(AwakeId))
            {
                OpenMap();
                _bakunawa?.WakeResting();
                BeginRelocation();
            }
            else StartCoroutine(Wake());
        }

        public override void RestoreCompleted()
        {
            OpenMap();
            foreach (string key in Schools) if (Has(HomeId(key))) Settle(key, null);
            if (_bakunawa != null) _bakunawa.Appease();
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
            foreach (string key in Schools)
            {
                if (Has(HomeId(key)) || _bag.Has(key) || _scattered.Exists(s => s != null && s.SpeciesKey == key)) continue;
                SelectiveCatchingStep.Species sp = _catching.SpeciesOf(key);
                if (sp != null) _bag.Add(key, sp.model, _catching.SchoolSize, sp.modelYaw);
            }
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
            _phase = Phase.Escape;
            SetObjective("Bakunawa is awake! Escape the abyss: swim up the wall.");
            ObjectiveTracker.Instance?.Clear();
        }

        /// <summary>Barriers down and every sanctuary lit: from the roar on, the minigame spans the whole map.</summary>
        private void OpenMap()
        {
            foreach (GameObject b in _barriers) if (b != null) b.SetActive(false);
            foreach (Sanctuary s in _sanctuaries) if (s != null) s.Show(true);
        }

        private IEnumerator Rockfall()
        {
            if (_rockModels == null || _rockModels.Length == 0 || _bakunawa == null) yield break;
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
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.SchoolRelocation,
                ChallengeId = StepId,
                TaskQuantity = left + 1,   // each school home, then the appeasement
                TimeLimit = 0f,
                HintBudget = 0,
                Perspective = ChallengePerspective.ThirdPerson
            });
            ChallengeManager.Instance?.SetExpectedCompletionTime(left * _expectedSecondsPerSchool + 60f);
            if (_profile != null) ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
            if (left == 0) BeginAppease();
            else UpdateObjective(true);
        }

        private void Update()
        {
            if (!IsRunning) return;
            if (_phase == Phase.Escape)
            {
                PlayerController pc = PlayerController.Instance;
                if (pc != null && pc.transform.position.y >= _escapeHeight) BeginRelocation();
                return;
            }
            if (_phase != Phase.Relocate && _phase != Phase.Appease) return;

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

            bool relocating = _phase == Phase.Relocate;
            bool compass = RelicLoadout.IsEquipped(GameConstants.Relics.COMPASS);
            Sanctuary target = relocating && _carried.Count > 0 ? Sanctuary.NearestFor(_carried, player.transform.position) : null;
            Camera cam = Camera.main;
            _hud?.SetCompass(relocating && compass, target, cam != null ? cam.transform : player.transform, _compassCloseRange);
            _hud?.SetRelease(relocating ? Sanctuary.At(player.transform.position) : null, _carried, NameOf);

            Transform track = _phase == Phase.Appease ? (_bakunawa != null ? _bakunawa.transform : null)
                            : _scattered.Count > 0 ? _scattered[0].transform
                            : compass && target != null ? target.transform : null;
            if (track != _tracked)
            {
                _tracked = track;
                if (track != null) ObjectiveTracker.Instance?.SetObjective(track);
                else ObjectiveTracker.Instance?.Clear();
            }
            UpdateObjective(false);
        }

        private string NameOf(string key)
        {
            SelectiveCatchingStep.Species sp = _catching != null ? _catching.SpeciesOf(key) : null;
            return sp != null && !string.IsNullOrEmpty(sp.displayName) ? sp.displayName : key;
        }

        private void UpdateObjective(bool force)
        {
            string text;
            if (_phase == Phase.Appease) text = "Every school is home. Return to Bakunawa in the abyss and play the Radiant Conch for it.";
            else if (_scattered.Count > 0) text = "Your schools scattered! Calm them with the Radiant Conch, then catch them with the Woven Net.";
            else if (!RelicLoadout.IsEquipped(GameConstants.Relics.COMPASS)) text = "Equip the Compass to find the sanctuaries, then bring each school home.";
            else text = "Bring the schools in your net-bag to their sanctuaries. The Compass shows the way.";
            if (!force && text == _objective) return;
            _objective = text;
            SetObjective(text);
            int home = 0;
            foreach (string key in Schools) if (Has(HomeId(key))) home++;
            if (_phase == Phase.Relocate) QuestObjectiveUI.Instance?.SetCounter(home, Schools.Count);
        }

        private void SetObjective(string text) => QuestObjectiveUI.Instance?.SetObjective(DialogueManager.ResolveTokens(text), false);

        /// <summary>The release card: let <paramref name="key"/> out at the sanctuary the diver is in.</summary>
        private void Release(string key)
        {
            PlayerController player = PlayerController.Instance;
            if (_phase != Phase.Relocate || player == null || _bag == null || !_bag.Has(key)) return;
            Sanctuary here = Sanctuary.At(player.transform.position);
            if (here == null) return;
            ChallengeManager.Instance?.ReportPlayerAction();

            if (here.Accepts(key))
            {
                Vector3 from = _bag.Mouth.position;
                _bag.Remove(key);
                Settle(key, here, from);
                if (Save != null) Save.AddChallengeId(HomeId(key));
                GameAudio.PlayFlat(_deliveredClip);
                ZoneBannerUI.Post($"The {NameOf(key)} are home at {here.DisplayName}.");
                ChallengeManager.Instance?.ReportCorrectAction();
                ChallengeManager.Instance?.ReportTaskCompleted();
                bool allHome = true;
                foreach (string k in Schools) if (!Has(HomeId(k))) allHome = false;
                if (allHome) BeginAppease();
                else UpdateObjective(true);
            }
            else
            {
                _wrongDrops++;
                Sfx.Play(SfxCue.Wrong);
                ChallengeManager.Instance?.ReportError();
                ZoneBannerUI.Post($"The {NameOf(key)} don't belong at {here.DisplayName}! The net-bag burst open.");
                Burst(player.transform.position);
            }
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
                s.SetPanicked(true);
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
                    if (sp != null && _bag != null) _bag.Add(school.SpeciesKey, sp.model, _catching.SchoolSize, sp.modelYaw);
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

            if (_phase == Phase.Appease && _bakunawa != null && _bakunawa.InCalmRange(p))
            {
                AppeaseNow();
                return;
            }

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
            if (player != null && _bag != null && _bag.SchoolCount > 0)
            {
                ZoneBannerUI.Post("Bakunawa's strike burst the net-bag!");
                Burst(player.transform.position);
            }
        }

        private void OnWentHome()
        {
            if (IsRunning && _phase == Phase.Relocate) CalloutUI.Say(Level2DialogueScripts.Bakunawa, "Hsss… swim, then. I will be waiting.");
        }

        private void OnRespawned(PlayerRespawnedEvent evt)
        {
            if (!IsRunning || evt.IsResume) return;
            _bakunawa?.ResetToLair();
        }

        // ───────────────────────── Appeasement ─────────────────────────

        private void BeginAppease()
        {
            _phase = Phase.Appease;
            _hud?.SetCompass(false, null, null, 0f);
            _hud?.SetRelease(null, null, NameOf);
            _bakunawa?.GoHomeAndWait();
            UpdateObjective(true);
            QuestObjectiveUI.Instance?.ClearCounter();
        }

        private void AppeaseNow()
        {
            _phase = Phase.Done;
            _bakunawa?.Appease();
            CameraController.Instance?.Shake(0.15f, 1.2f);
            ZoneBannerUI.Post("The sea grows calm. Bakunawa's anger is gone.");
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
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_dispersals", Value = _dispersals });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_max_carried", Value = _maxCarried });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_wrong_drops", Value = _wrongDrops });
            EventBus.Publish(new PlayerMetricRecordedEvent { MetricName = "relocation_strikes", Value = _strikes });
            _phase = Phase.Done;
            PlayerController.Instance?.ClearSpeedMultiplier(this);
            if (NetThrower.Instance != null) NetThrower.Instance.Armed = false;
            foreach (CatchableSchool s in _scattered) if (s != null) Destroy(s.gameObject);
            _scattered.Clear();
            if (_hud != null) Destroy(_hud.gameObject);
            ObjectiveTracker.Instance?.Clear();
            Complete();
        }

        private void DebugForceComplete()
        {
            if (Save != null)
            {
                Save.AddChallengeId(AwakeId);
                foreach (string key in Schools) Save.AddChallengeId(HomeId(key));
            }
            OpenMap();
            if (NetBag.Instance != null) NetBag.Instance.Clear();
            foreach (string key in Schools) Settle(key, null);
            _bakunawa?.Appease();
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
#endif
    }
}
