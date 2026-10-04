using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Waste Segregation Challenge controller.
    /// 
    /// Setup: Place this on a parent GameObject in the zone. Under it, place:
    ///   - 3x SortingBin children (one per DebrisCategory)
    ///   - N x DebrisItem children (mix of categories)
    ///   - Optionally, SpawnPoints for randomized debris placement
    /// 
    /// Flow:
    ///   1. ChallengeStartedEvent activates this (if matching ChallengeType)
    ///   2. Spawns/activates debris items based on DDA-adjusted task count
    ///   3. Player picks up debris → carries to bin → SortingBin validates
    ///   4. SortingBin reports back here via OnDebrisSorted()
    ///   5. When all debris sorted, challenge auto-completes via ChallengeManager
    /// 
    /// The ChallengeManager handles timer, hint budget, and completion events.
    /// This script only manages the challenge-specific spawning and feedback logic.
    /// </summary>
    public class WasteSegregationChallenge : MonoBehaviour
    {
        [Header("Debris Setup")]
        [Tooltip("Pre-placed debris items (children of this object). " +
                 "If empty, uses spawn points + prefabs instead.")]
        [SerializeField] private List<DebrisItem> _preplacedDebris = new List<DebrisItem>();

        [Header("Spawn Configuration (alternative to pre-placed)")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
        [SerializeField] private List<DebrisItem> _debrisPrefabs = new List<DebrisItem>();

        [Header("Sorting Bins")]
        [SerializeField] private SortingBin _plasticBin;
        [SerializeField] private SortingBin _metalBin;
        [SerializeField] private SortingBin _generalBin;

        [Header("Feedback")]
        [SerializeField] private AudioClip _challengeStartSound;
        [SerializeField] private AudioClip _challengeCompleteSound;

        [Header("Timer Configuration")]
        [Tooltip("Time limit in seconds. If 0 or unset, uses time limit from ChallengeStartedEvent (default 90s).")]
        [SerializeField] private float _defaultTimeLimit = 90f;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isActive;
        private int _totalDebris;
        private int _sortedCorrectly;
        private int _sortedIncorrectly;
        private float _timeLimit;
        private float _timeRemaining;
        private bool _hasTimer;
        private List<DebrisItem> _activeDebris = new List<DebrisItem>();
        private List<DebrisItem> _spawnedDebris = new List<DebrisItem>();

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeStartedEvent>(OnChallengeStarted);
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void Start()
        {
            // Auto-detect preplaced debris children if unassigned in Inspector
            if (_preplacedDebris.Count == 0 && _spawnPoints.Count == 0)
            {
                DebrisItem[] found = GetComponentsInChildren<DebrisItem>(true);
                if (found.Length > 0)
                {
                    _preplacedDebris.AddRange(found);
                }
            }

            // Hide all pre-placed debris until challenge starts
            foreach (var debris in _preplacedDebris)
            {
                if (debris != null) debris.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // Sync with ChallengeManager if it is running a timer
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive)
            {
                if (ChallengeManager.Instance.HasTimer)
                {
                    _timeRemaining = ChallengeManager.Instance.TimeRemaining;
                    _hasTimer = true;
                }
            }
            else if (_hasTimer)
            {
                _timeRemaining -= Time.deltaTime;
                if (_timeRemaining <= 0f)
                {
                    _timeRemaining = 0f;
                    _isActive = false;
                    Debug.Log("[WasteSegregation] Time expired! Challenge failed.");
                    EventBus.Publish(new ChallengeFailedEvent
                    {
                        ChallengeId = "WasteSegregation",
                        Type = ChallengeType.WasteSegregation,
                        Reason = "Time Limit Exceeded"
                    });
                }
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Activation
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // Only respond to waste segregation challenges
            if (evt.Type != ChallengeType.WasteSegregation) return;

            _isActive = true;
            _sortedCorrectly = 0;
            _sortedIncorrectly = 0;
            _activeDebris.Clear();

            // Setup timer
            _timeLimit = evt.TimeLimit > 0 ? evt.TimeLimit : _defaultTimeLimit;
            _timeRemaining = _timeLimit;
            _hasTimer = _timeLimit > 0;

            int taskCount = evt.TaskQuantity > 0 ? evt.TaskQuantity : (_preplacedDebris.Count > 0 ? _preplacedDebris.Count : 6);

            // Auto-detect preplaced debris children if empty
            if (_preplacedDebris.Count == 0 && _spawnPoints.Count == 0)
            {
                DebrisItem[] found = GetComponentsInChildren<DebrisItem>(true);
                if (found.Length > 0)
                {
                    _preplacedDebris.AddRange(found);
                }
            }

            // Activate debris (pre-placed or spawned)
            if (_preplacedDebris.Count > 0)
            {
                ActivatePreplacedDebris(taskCount);
            }
            else if (_spawnPoints.Count > 0 && _debrisPrefabs.Count > 0)
            {
                SpawnDebris(taskCount);
            }
            else
            {
                Debug.LogError("[WasteSegregation] No debris configured! " +
                    "Set either pre-placed debris or spawn points + prefabs.");
                return;
            }

            _totalDebris = _activeDebris.Count;

            // Play start sound
            if (_challengeStartSound != null)
            {
                GameAudio.PlayAt(_challengeStartSound, transform.position);
            }

            Debug.Log($"[WasteSegregation] Challenge started with {_totalDebris} debris items. Time limit: {_timeLimit:F0}s");
        }

        /// <summary>
        /// Activate a subset of pre-placed debris based on task count.
        /// Uses Fisher-Yates shuffle to randomize which items appear.
        /// </summary>
        private void ActivatePreplacedDebris(int count)
        {
            // Build shuffled list
            List<DebrisItem> shuffled = new List<DebrisItem>(_preplacedDebris);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            // Activate up to count items, ensuring at least 1 of each category
            int toActivate = Mathf.Min(count, shuffled.Count);

            // First pass: guarantee one of each category if possible
            HashSet<DebrisCategory> categoriesCovered = new HashSet<DebrisCategory>();
            List<DebrisItem> prioritized = new List<DebrisItem>();

            foreach (var debris in shuffled)
            {
                if (debris == null) continue;
                if (!categoriesCovered.Contains(debris.Category) && prioritized.Count < toActivate)
                {
                    prioritized.Add(debris);
                    categoriesCovered.Add(debris.Category);
                }
            }

            // Second pass: fill remaining slots
            foreach (var debris in shuffled)
            {
                if (prioritized.Count >= toActivate) break;
                if (debris == null || prioritized.Contains(debris)) continue;
                prioritized.Add(debris);
            }

            // Activate selected items
            foreach (var debris in prioritized)
            {
                debris.gameObject.SetActive(true);
                _activeDebris.Add(debris);
            }
        }

        /// <summary>
        /// Spawn debris from prefabs at randomized spawn points.
        /// Ensures balanced category distribution.
        /// </summary>
        private void SpawnDebris(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // Pick random spawn point
                Transform spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Count)];

                // Pick prefab (cycle through to balance categories)
                DebrisItem prefab = _debrisPrefabs[i % _debrisPrefabs.Count];

                // Spawn with slight random offset
                Vector3 offset = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    0f,
                    Random.Range(-0.5f, 0.5f)
                );

                DebrisItem spawned = Instantiate(
                    prefab,
                    spawnPoint.position + offset,
                    Quaternion.Euler(0, Random.Range(0f, 360f), 0),
                    transform
                );

                _spawnedDebris.Add(spawned);
                _activeDebris.Add(spawned);
            }
        }

        // ─────────────────────────────────────────────
        // Sorting Feedback (called by SortingBin)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Called by SortingBin when a debris item enters a bin.
        /// </summary>
        public void OnDebrisSorted(DebrisCategory category, bool correct)
        {
            if (!_isActive) return;

            if (correct)
            {
                _sortedCorrectly++;
                Debug.Log($"[WasteSegregation] Progress: {_sortedCorrectly}/{_totalDebris}");
            }
            else
            {
                _sortedIncorrectly++;
                Debug.LogWarning($"[WasteSegregation] Incorrectly sorted {category}! Errors: {_sortedIncorrectly}");
            }

            // Check for challenge completion
            int totalSorted = _sortedCorrectly + _sortedIncorrectly;
            if (totalSorted >= _totalDebris)
            {
                // Consider success if 100% correct, or you can allow a margin of error.
                bool success = _sortedIncorrectly == 0;
                
                Debug.Log($"[WasteSegregation] All items sorted! Firing completion event. Success: {success}");
                
                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = "WasteSegregation", 
                    Type = ChallengeType.WasteSegregation, 
                    Success = success,
                    ErrorCount = _sortedIncorrectly
                });
            }
        }

        // ─────────────────────────────────────────────
        // Challenge End
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.Type != ChallengeType.WasteSegregation) return;

            _isActive = false;

            if (evt.Success && _challengeCompleteSound != null)
            {
                GameAudio.PlayAt(_challengeCompleteSound, transform.position);
            }

            CleanupDebris();

            Debug.Log($"[WasteSegregation] Challenge {(evt.Success ? "COMPLETED" : "ENDED")}: " +
                      $"correct={_sortedCorrectly}, errors={_sortedIncorrectly}");
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.Type != ChallengeType.WasteSegregation) return;

            _isActive = false;
            CleanupDebris();

            Debug.Log($"[WasteSegregation] Challenge FAILED: {evt.Reason}");
        }

        private void CleanupDebris()
        {
            // Deactivate pre-placed debris
            foreach (var debris in _preplacedDebris)
            {
                if (debris != null) debris.gameObject.SetActive(false);
            }

            // Destroy spawned debris
            foreach (var debris in _spawnedDebris)
            {
                if (debris != null) Destroy(debris.gameObject);
            }
            _spawnedDebris.Clear();
            _activeDebris.Clear();
        }

        // ─────────────────────────────────────────────
        // UI & On-Screen Challenge HUD
        // ─────────────────────────────────────────────

        private DebrisItem FindCarriedDebris()
        {
            foreach (var debris in _activeDebris)
            {
                if (debris != null && debris.IsCarried) return debris;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!_isActive) return;

            // Centered HUD card at top of screen
            float boxWidth = 360f;
            float boxHeight = 86f;
            float x = (Screen.width - boxWidth) * 0.5f;
            float y = 18f;

            // Background Card
            GUI.backgroundColor = new Color(0.04f, 0.12f, 0.22f, 0.90f);
            GUIStyle cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.padding = new RectOffset(12, 12, 8, 8);
            GUI.Box(new Rect(x, y, boxWidth, boxHeight), GUIContent.none, cardStyle);

            // Title Style
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 12;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            // Remaining Debris Counter Style
            GUIStyle counterStyle = new GUIStyle(GUI.skin.label);
            counterStyle.fontSize = 15;
            counterStyle.fontStyle = FontStyle.Bold;
            counterStyle.normal.textColor = Color.white;
            counterStyle.alignment = TextAnchor.MiddleLeft;

            // Timer Style
            GUIStyle timerStyle = new GUIStyle(GUI.skin.label);
            timerStyle.fontSize = 16;
            timerStyle.fontStyle = FontStyle.Bold;
            timerStyle.alignment = TextAnchor.MiddleRight;

            // Color-coded urgency for timer
            if (_timeRemaining <= 15f)
            {
                timerStyle.normal.textColor = new Color(1f, 0.3f, 0.3f); // Red (<15s)
            }
            else if (_timeRemaining <= 30f)
            {
                timerStyle.normal.textColor = new Color(1f, 0.85f, 0.25f); // Yellow (<30s)
            }
            else
            {
                timerStyle.normal.textColor = new Color(0.35f, 1f, 0.6f); // Green / Cyan
            }

            int minutes = Mathf.FloorToInt(_timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(_timeRemaining % 60f);
            string timerText = _hasTimer ? $"⏱ {minutes:00}:{seconds:00}" : "⏱ --:--";

            int remainingDebris = Mathf.Max(0, _totalDebris - _sortedCorrectly);
            string progressText = $"♻ Remaining: {remainingDebris} / {_totalDebris}";

            // Draw Header
            GUI.Label(new Rect(x, y + 6f, boxWidth, 20f), "WASTE SEGREGATION CHALLENGE", titleStyle);

            // Draw Stats Row (Remaining Counter on Left, Timer on Right)
            GUI.Label(new Rect(x + 16f, y + 28f, 210f, 24f), progressText, counterStyle);
            GUI.Label(new Rect(x + boxWidth - 124f, y + 28f, 108f, 24f), timerText, timerStyle);

            // Draw Bottom Status (Holding info / instruction)
            GUIStyle subStyle = new GUIStyle(GUI.skin.label);
            subStyle.fontSize = 12;
            subStyle.alignment = TextAnchor.MiddleCenter;

            DebrisItem carried = FindCarriedDebris();
            if (carried != null)
            {
                subStyle.normal.textColor = new Color(1f, 0.9f, 0.4f);
                GUI.Label(new Rect(x, y + 54f, boxWidth, 22f), $"Holding: {carried.Category} Debris → Deposit in {carried.Category} Bin", subStyle);
            }
            else
            {
                subStyle.normal.textColor = new Color(0.75f, 0.85f, 0.95f, 0.8f);
                GUI.Label(new Rect(x, y + 54f, boxWidth, 22f), "Search the seabed for scattered debris", subStyle);
            }
        }
    }
}
