using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Challenges;
using Lalim.Core;
using Lalim.DDA;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Zone 1, minigame 1 — trash collection (3rd person). Starts the moment Amansinaya's quest
    /// dialogue ends (no start button) and ends when NO trash is left in the scene:
    ///  - Every piece of trash is exactly one pickup (bottles, bags, glass, cans lying on the sand;
    ///    placed by Tools ▸ Lalim ▸ Level 1 ▸ Scatter Zone 1 Trash).
    ///  - DDA picks how many of them this run uses; the choice is saved. The pieces not picked are
    ///    hidden when the quest starts, so what the player sees is exactly what they collect, and
    ///    collecting the last piece leaves the zone free of trash.
    ///  - The collected pieces become the offerings in the sorting minigame.
    /// Runs as a real ChallengeManager challenge (ThirdPerson) so DDA scores it.
    /// </summary>
    public class TrashCollectionStep : QuestStep
    {
        [Header("Trash")]
        [Tooltip("Parent that holds the trash props (the LuzonScene environment root).")]
        [SerializeField] private Transform _trashRoot;
        [Tooltip("Child names starting with this are trash props.")]
        [SerializeField] private string _trashNamePrefix = "Prop_Trash_Debris";
        [Tooltip("Only trash inside these bounds belongs to this step. Empty = the zone's toxic volume.")]
        [SerializeField] private Collider _area;

        [Header("DDA")]
        [Tooltip("Knobs: count, hiddenShare. Falls back to the values below when empty.")]
        [SerializeField] private MinigameDifficultyProfile _profile;
        [SerializeField, Min(1)] private int _baseCount = 18;
        [SerializeField, Min(1)] private int _minCount = 8;
        [SerializeField, Min(1f)] private float _expectedSecondsPerItem = 12f;

        [Header("Feedback")]
        [SerializeField, Min(1f)] private float _saveThrottleSeconds = 5f;
        [SerializeField] private string _counterFormat = "Trash collected: {0} / {1}";


        private readonly List<OfferingItem> _selected = new List<OfferingItem>();
        private readonly List<OfferingItem> _collected = new List<OfferingItem>();
        private readonly List<GameObject> _allTrash = new List<GameObject>();
        private bool _scanned;
        private float _lastSaveTime = -999f;

        /// <summary>Pieces collected so far, in order (the sorting minigame uses exactly these).</summary>
        public IReadOnlyList<OfferingItem> CollectedItems => _collected;

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        private void Start()
        {
            // Nothing can be picked up before the quest is given.
            ScanTrash();
            if (!IsRunning) foreach (OfferingItem o in AllOfferings()) if (!o.IsCollected) o.SetPickupEnabled(false);
        }

        private void OnEnable()
        {
            OfferingItem.Collected += OnCollected;
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        }

        private void OnDisable()
        {
            OfferingItem.Collected -= OnCollected;
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        }

        // ───────────────────────── Begin / restore ─────────────────────────

        protected override void OnBegin()
        {
            ScanTrash();
            ChooseSelection();
            HideUnselected();
            RestoreCollectedFromSave();

            foreach (OfferingItem o in _selected) if (!o.IsCollected) o.SetPickupEnabled(true);
            HideSomeForSpiritSense();

            int remaining = Remaining();
            if (remaining == 0) { Complete(); return; }

            UpdateCounter();
            ObjectiveTracker.Instance?.SetObjectiveSet(SelectedTransforms(), t => IsItemDone(t));

            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.TrashCollection,
                ChallengeId = StepId,
                TaskQuantity = remaining,
                TimeLimit = 0f,
                HintBudget = 0, // no hint button in this minigame (keeps hint reliance out of its DDA score)
                Perspective = ChallengePerspective.ThirdPerson
            });
            ChallengeManager.Instance?.SetExpectedCompletionTime(remaining * _expectedSecondsPerItem);
            if (_profile != null) ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
        }

        /// <summary>Collection already finished in a previous session: no trash left anywhere.</summary>
        public override void RestoreCompleted()
        {
            ScanTrash();
            ChooseSelection();
            _collected.Clear();
            foreach (OfferingItem o in _selected)
            {
                if (!o.IsCollected) o.MarkCollectedSilently();
                _collected.Add(o);
            }
            foreach (GameObject go in _allTrash)
            {
                if (go == null) continue;
                OfferingItem o = go.GetComponent<OfferingItem>();
                if (o != null && _selected.Contains(o)) continue; // kept (inactive) for the altar tray
                go.SetActive(false);
            }
        }

        // ───────────────────────── Selection ─────────────────────────

        private void ScanTrash()
        {
            if (_scanned) return;
            _scanned = true;

            Bounds area = AreaBounds();
            if (_trashRoot == null) return;
            foreach (Transform child in _trashRoot)
            {
                if (!child.name.StartsWith(_trashNamePrefix)) continue;
                if (!child.gameObject.activeSelf) continue; // switched-off set dressing (e.g. litter piles)
                if (area.size != Vector3.zero && !area.Contains(child.position)) continue;
                _allTrash.Add(child.gameObject);
            }
        }

        private Bounds AreaBounds()
        {
            Collider c = _area;
            if (c == null && Zone != null && Zone.ToxicVolume != null) c = Zone.ToxicVolume.GetComponent<Collider>();
            return c != null ? c.bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        private IEnumerable<OfferingItem> AllOfferings()
        {
            foreach (GameObject go in _allTrash)
            {
                if (go == null) continue;
                OfferingItem o = go.GetComponent<OfferingItem>();
                if (o != null) yield return o;
            }
        }

        private void ChooseSelection()
        {
            if (_selected.Count > 0) return;

            var byId = new Dictionary<string, OfferingItem>();
            foreach (OfferingItem o in AllOfferings()) byId[o.SaveId] = o;

            SaveData save = Save;
            if (save != null && save.selectedTrashIds != null && save.selectedTrashIds.Count > 0)
            {
                foreach (string id in save.selectedTrashIds)
                {
                    if (byId.TryGetValue(id, out OfferingItem o)) _selected.Add(o);
                }
                if (_selected.Count > 0) return;
            }

            var pool = new List<OfferingItem>(byId.Values);
            int count = _profile != null && _profile.Has(Level1DifficultyKnobs.TrashCount)
                ? _profile.GetInt(Level1DifficultyKnobs.TrashCount)
                : (DDAManager.Instance != null ? DDAManager.Instance.GetAdjustedTaskQuantity(_baseCount) : _baseCount);
            count = Mathf.Clamp(count, Mathf.Min(_minCount, pool.Count), pool.Count);

            // Random subset, but spread out: repeatedly take the candidate farthest from those already chosen.
            var rng = new System.Random();
            if (pool.Count > 0)
            {
                OfferingItem first = pool[rng.Next(pool.Count)];
                _selected.Add(first);
                pool.Remove(first);
            }
            while (_selected.Count < count && pool.Count > 0)
            {
                OfferingItem best = null;
                float bestScore = -1f;
                foreach (OfferingItem candidate in pool)
                {
                    float nearest = float.MaxValue;
                    foreach (OfferingItem s in _selected)
                    {
                        nearest = Mathf.Min(nearest, (candidate.transform.position - s.transform.position).sqrMagnitude);
                    }
                    float score = nearest * (0.75f + 0.5f * (float)rng.NextDouble());
                    if (score > bestScore) { bestScore = score; best = candidate; }
                }
                _selected.Add(best);
                pool.Remove(best);
            }

            if (save != null)
            {
                save.selectedTrashIds = new List<string>();
                foreach (OfferingItem o in _selected) save.selectedTrashIds.Add(o.SaveId);
                // A fresh selection (first run, or the trash was re-scattered) starts the count over.
                save.collectedTrashIds?.Clear();
            }
        }

        // Only the chosen pieces stay in the reef: one visible piece of trash = one pickup.
        private void HideUnselected()
        {
            foreach (OfferingItem o in AllOfferings())
            {
                if (_selected.Contains(o)) continue;
                o.SetPickupEnabled(false);
                o.gameObject.SetActive(false);
            }
        }

        private void RestoreCollectedFromSave()
        {
            _collected.Clear();
            SaveData save = Save;
            if (save == null || save.collectedTrashIds == null) return;
            foreach (OfferingItem o in _selected)
            {
                if (!save.collectedTrashIds.Contains(o.SaveId)) continue;
                if (!o.IsCollected) o.MarkCollectedSilently();
                _collected.Add(o);
            }
        }

        // ───────────────────────── Progress ─────────────────────────

        private void OnCollected(OfferingItem item)
        {
            if (!IsRunning || !_selected.Contains(item) || _collected.Contains(item)) return;

            _collected.Add(item);
            UpdateCounter();

            SaveData save = Save;
            if (save != null && !save.collectedTrashIds.Contains(item.SaveId)) save.collectedTrashIds.Add(item.SaveId);
            if (Time.time - _lastSaveTime >= _saveThrottleSeconds)
            {
                _lastSaveTime = Time.time;
                SaveManager.Instance?.AutoSave();
            }

            ChallengeManager.Instance?.ReportTaskCompleted();
            AudioNavigationManager.Instance?.RecordProgress();
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!IsRunning || evt.ChallengeId != StepId) return;
            if (Remaining() > 0) return; // ended early (debug) — keep going
            Complete();
        }

        private int Remaining()
        {
            int n = 0;
            foreach (OfferingItem o in _selected) if (o != null && !o.IsCollected) n++;
            return n;
        }

        private bool IsItemDone(Transform t)
        {
            OfferingItem o = t != null ? t.GetComponent<OfferingItem>() : null;
            return o == null || o.IsCollected;
        }

        private IEnumerable<Transform> SelectedTransforms()
        {
            foreach (OfferingItem o in _selected) if (o != null) yield return o.transform;
        }

        private void UpdateCounter()
        {
            QuestObjectiveUI ui = QuestObjectiveUI.Instance;
            if (ui == null) return;
            ui.Show();
            ui.SetCounter(_collected.Count, _selected.Count, _counterFormat);
        }

        // Harder runs hide part of the trash: only a Spirit Sense pulse reveals it.
        private void HideSomeForSpiritSense()
        {
            if (_profile == null || !_profile.Has(Level1DifficultyKnobs.TrashHiddenShare)) return;
            float share = Mathf.Clamp01(_profile.Get(Level1DifficultyKnobs.TrashHiddenShare));
            int toHide = Mathf.FloorToInt(Remaining() * share);
            foreach (OfferingItem o in _selected)
            {
                if (toHide <= 0) break;
                if (o == null || o.IsCollected || o.GetComponent<HiddenObject>() != null) continue;
                o.gameObject.AddComponent<HiddenObject>();
                toHide--;
            }
        }

        private void DebugForceComplete()
        {
            foreach (OfferingItem o in _selected) if (o != null && !o.IsCollected) o.Collect();
        }

#if UNITY_EDITOR
        public void EditorSetupProfile(MinigameDifficultyProfile profile) => _profile = profile;

        public void EditorSetupTrash(Transform trashRoot, Collider area)
        {
            _trashRoot = trashRoot;
            _area = area;
        }
#endif
    }
}
