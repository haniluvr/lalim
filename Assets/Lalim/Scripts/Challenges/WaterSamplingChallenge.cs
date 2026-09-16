using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>
    /// Water Sampling Challenge controller.
    ///
    /// Setup: Place this on a parent GameObject in the zone. Under it, place:
    ///   - N x SamplePoint children (one per sampling location)
    ///   - Optionally, SpawnPoints for randomized sample point placement
    ///
    /// Flow:
    ///   1. ChallengeStartedEvent activates this (if matching ChallengeType)
    ///   2. Activates sample points based on DDA-adjusted task count
    ///   3. Player equips Water Sample Kit → approaches point → hold-to-sample
    ///   4. SamplePoint reports back here via OnSampleCollected()
    ///   5. When all points sampled, challenge auto-completes via ChallengeManager
    ///
    /// The ChallengeManager handles timer, hint budget, and completion events.
    /// This script manages the challenge-specific activation and result tracking.
    /// </summary>
    public class WaterSamplingChallenge : MonoBehaviour
    {
        [Header("Zone Identification")]
        [Tooltip("Unique zone identifier matching the ZoneTrigger (e.g., luzon_zone_b, luzon_zone_b_2)")]
        [SerializeField] private string _zoneId = "luzon_zone_b";

        [Header("Sample Points")]
        [Tooltip("Pre-placed sample point children. If empty, uses spawn points + prefab.")]
        [SerializeField] private List<SamplePoint> _preplacedPoints = new List<SamplePoint>();

        [Header("Spawn Configuration (alternative to pre-placed)")]
        [SerializeField] private List<Transform> _spawnPoints = new List<Transform>();
        [SerializeField] private SamplePoint _samplePointPrefab;

        [Header("Parameter Distribution")]
        [Tooltip("If true, spawned points cycle through pH → Salinity → Temperature. " +
                 "If false, all spawned points use a random parameter.")]
        [SerializeField] private bool _balanceParameters = true;

        [Header("Zone Visual Indicators (Pre-Challenge)")]
        [Tooltip("Keep sample points and bubble plumes visible before challenge starts so players can spot the anomaly area")]
        [SerializeField] private bool _showPointsBeforeStart = true;

        [Tooltip("Spawn an atmospheric water anomaly particle cloud across the entire zone")]
        [SerializeField] private bool _enableZoneAmbientParticles = true;

        [Header("Feedback")]
        [SerializeField] private AudioClip _challengeStartSound;
        [SerializeField] private AudioClip _challengeCompleteSound;
        [SerializeField] private AudioClip _allSamplesCollectedSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public string ZoneId => _zoneId;
        public void SetZoneId(string zoneId) => _zoneId = zoneId;

        private bool _isActive;
        public bool IsActive => _isActive;

        private int _totalSamples;
        private int _samplesCollected;
        private List<SamplePoint> _activePoints = new List<SamplePoint>();
        private List<SamplePoint> _spawnedPoints = new List<SamplePoint>();
        private ParticleSystem _ambientParticleVFX;

        // Results tracking — which parameters were sampled
        private int _phCount;
        private int _salinityCount;
        private int _temperatureCount;

        // Safety Pop-up Modal State
        private bool _showSafetyPopup;
        private float _safetyPopupExpiryTime;
        private static Texture2D _popupTex;

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
            // Auto-sync ZoneId with child/parent ZoneTrigger if not explicitly customized
            if (string.IsNullOrEmpty(_zoneId) || _zoneId == "luzon_zone_b")
            {
                ZoneTrigger zt = GetComponentInChildren<ZoneTrigger>(true);
                if (zt != null && !string.IsNullOrEmpty(zt.ZoneId))
                {
                    _zoneId = zt.ZoneId;
                }
            }

            // Auto-detect children SamplePoints if list is empty
            if (_preplacedPoints.Count == 0 && _spawnPoints.Count == 0)
            {
                SamplePoint[] found = GetComponentsInChildren<SamplePoint>(true);
                if (found.Length > 0)
                {
                    _preplacedPoints.AddRange(found);
                }
            }

            if (!_showPointsBeforeStart)
            {
                // Hide pre-placed points only if explicitly configured
                foreach (var point in _preplacedPoints)
                {
                    if (point != null) point.gameObject.SetActive(false);
                }
            }
            else
            {
                // Keep sample points active so bubble plumes and beacons indicate the challenge area!
                foreach (var point in _preplacedPoints)
                {
                    if (point != null) point.gameObject.SetActive(true);
                }
            }

            if (_enableZoneAmbientParticles)
            {
                CreateAmbientZoneParticles();
            }
        }

        private void CreateAmbientZoneParticles()
        {
            if (_ambientParticleVFX != null) return;

            GameObject pGO = new GameObject("[WaterAnomaly_ZoneParticles]");
            pGO.transform.SetParent(transform, false);
            pGO.transform.localPosition = Vector3.zero;

            _ambientParticleVFX = pGO.AddComponent<ParticleSystem>();
            var main = _ambientParticleVFX.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 7.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.04f;

            // Shimmering oceanic cyan/teal water anomaly particles
            main.startColor = new Color(0.25f, 0.88f, 1.0f, 0.65f);

            var emission = _ambientParticleVFX.emission;
            emission.rateOverTime = 30f;

            var shape = _ambientParticleVFX.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 16f; // Wide area coverage

            var vel = _ambientParticleVFX.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);

            var renderer = pGO.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _ambientParticleVFX.Play();
        }

        // ─────────────────────────────────────────────
        // Challenge Activation
        // ─────────────────────────────────────────────

        private void OnChallengeStarted(ChallengeStartedEvent evt)
        {
            // Only respond to water sampling challenges
            if (evt.Type != ChallengeType.WaterSampling) return;

            // Only respond to this specific zone if ZoneId is specified
            if (!string.IsNullOrEmpty(_zoneId) && !string.IsNullOrEmpty(evt.ChallengeId) && evt.ChallengeId != _zoneId) return;

            _isActive = true;
            _samplesCollected = 0;
            _phCount = 0;
            _salinityCount = 0;
            _temperatureCount = 0;
            _activePoints.Clear();

            int taskCount = evt.TaskQuantity;

            // Activate sample points (pre-placed or spawned)
            if (_preplacedPoints.Count > 0)
            {
                ActivatePreplacedPoints(taskCount);
            }
            else if (_spawnPoints.Count > 0 && _samplePointPrefab != null)
            {
                SpawnSamplePoints(taskCount);
            }
            else
            {
                Debug.LogError("[WaterSampling] No sample points configured! " +
                    "Set either pre-placed points or spawn points + prefab.");
                return;
            }

            _totalSamples = _activePoints.Count;

            // Play start sound
            if (_challengeStartSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeStartSound, transform.position);
            }

            Debug.Log($"[WaterSampling] Zone '{_zoneId}' started with {_totalSamples} sample points");
        }

        /// <summary>
        /// Activate a subset of pre-placed sample points based on task count.
        /// Uses Fisher-Yates shuffle to randomize which points appear.
        /// Ensures balanced parameter coverage when possible.
        /// </summary>
        private void ActivatePreplacedPoints(int count)
        {
            // Build shuffled list
            List<SamplePoint> shuffled = new List<SamplePoint>(_preplacedPoints);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            int toActivate = Mathf.Min(count, shuffled.Count);

            // First pass: guarantee one of each parameter if possible
            HashSet<SampleParameter> parametersCovered = new HashSet<SampleParameter>();
            List<SamplePoint> prioritized = new List<SamplePoint>();

            foreach (var point in shuffled)
            {
                if (point == null) continue;
                if (!parametersCovered.Contains(point.Parameter) && prioritized.Count < toActivate)
                {
                    prioritized.Add(point);
                    parametersCovered.Add(point.Parameter);
                }
            }

            // Second pass: fill remaining slots
            foreach (var point in shuffled)
            {
                if (prioritized.Count >= toActivate) break;
                if (point == null || prioritized.Contains(point)) continue;
                prioritized.Add(point);
            }

            // Activate selected points
            foreach (var point in prioritized)
            {
                point.gameObject.SetActive(true);
                point.ResetPoint();
                _activePoints.Add(point);
            }
        }

        /// <summary>
        /// Spawn sample point instances from a prefab at randomized positions.
        /// </summary>
        private void SpawnSamplePoints(int count)
        {
            // Shuffle spawn points
            List<Transform> shuffledSpawns = new List<Transform>(_spawnPoints);
            for (int i = shuffledSpawns.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = shuffledSpawns[i];
                shuffledSpawns[i] = shuffledSpawns[j];
                shuffledSpawns[j] = temp;
            }

            int toSpawn = Mathf.Min(count, shuffledSpawns.Count);

            for (int i = 0; i < toSpawn; i++)
            {
                Transform spawnPoint = shuffledSpawns[i];

                SamplePoint spawned = Instantiate(
                    _samplePointPrefab,
                    spawnPoint.position,
                    spawnPoint.rotation,
                    transform
                );

                _spawnedPoints.Add(spawned);
                _activePoints.Add(spawned);
            }
        }

        // ─────────────────────────────────────────────
        // Sample Reporting (called by SamplePoint)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Called by SamplePoint when a sample is successfully collected.
        /// </summary>
        public void OnSampleCollected(SamplePoint point)
        {
            if (!_isActive) return;

            _samplesCollected++;

            // Track parameter distribution
            switch (point.Parameter)
            {
                case SampleParameter.pH:          _phCount++; break;
                case SampleParameter.Salinity:    _salinityCount++; break;
                case SampleParameter.Temperature: _temperatureCount++; break;
            }

            Debug.Log($"[WaterSampling] Sample collected: {point.Parameter} " +
                      $"({_samplesCollected}/{_totalSamples})");

            // Check for challenge completion
            if (_samplesCollected >= _totalSamples)
            {
                if (_allSamplesCollectedSound != null)
                {
                    AudioSource.PlayClipAtPoint(_allSamplesCollectedSound, transform.position);
                }

                Debug.Log($"[WaterSampling] All samples collected in '{_zoneId}'! " +
                          $"pH={_phCount}, Salinity={_salinityCount}, Temp={_temperatureCount}");

                string pubId = !string.IsNullOrEmpty(_zoneId) ? _zoneId : "WaterSampling";
                EventBus.Publish(new ChallengeCompletedEvent
                {
                    ChallengeId = pubId,
                    Type = ChallengeType.WaterSampling,
                    Success = true,
                    ErrorCount = 0
                });
            }
        }

        // ─────────────────────────────────────────────
        // Update & Input Dismissal
        // ─────────────────────────────────────────────

        private void Update()
        {
            if (_showSafetyPopup)
            {
                bool dismiss = false;
                if (Time.time >= _safetyPopupExpiryTime) dismiss = true;

                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) ||
                    Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
                {
                    dismiss = true;
                }

                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                        UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame ||
                        UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
                        UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                    {
                        dismiss = true;
                    }
                }

                if (dismiss)
                {
                    _showSafetyPopup = false;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Challenge End
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.Type != ChallengeType.WaterSampling) return;
            if (!string.IsNullOrEmpty(_zoneId) && !string.IsNullOrEmpty(evt.ChallengeId) && evt.ChallengeId != _zoneId) return;

            _isActive = false;

            if (evt.Success && _challengeCompleteSound != null)
            {
                AudioSource.PlayClipAtPoint(_challengeCompleteSound, transform.position);
            }

            // Trigger Post-Sampling Water Safety Pop-up Modal
            if (evt.Success)
            {
                ShowWaterSafetyPopup();
            }

            CleanupPoints();

            Debug.Log($"[WaterSampling] Zone '{_zoneId}' {(evt.Success ? "COMPLETED" : "ENDED")}: " +
                      $"sampled={_samplesCollected}/{_totalSamples}");
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.Type != ChallengeType.WaterSampling) return;
            if (!string.IsNullOrEmpty(_zoneId) && !string.IsNullOrEmpty(evt.ChallengeId) && evt.ChallengeId != _zoneId) return;

            _isActive = false;
            CleanupPoints();

            Debug.Log($"[WaterSampling] Zone '{_zoneId}' FAILED: {evt.Reason}");
        }

        private void CleanupPoints()
        {
            // Deactivate pre-placed points
            foreach (var point in _preplacedPoints)
            {
                if (point != null) point.gameObject.SetActive(false);
            }

            // Destroy spawned points
            foreach (var point in _spawnedPoints)
            {
                if (point != null) Destroy(point.gameObject);
            }
            _spawnedPoints.Clear();
            _activePoints.Clear();
        }

        // ─────────────────────────────────────────────
        // Water Safety Pop-up Modal
        // ─────────────────────────────────────────────

        public void ShowWaterSafetyPopup(float duration = 12.0f)
        {
            _showSafetyPopup = true;
            _safetyPopupExpiryTime = Time.time + duration;

            if (Lalim.UI.HUDController.Instance != null)
            {
                Lalim.UI.HUDController.Instance.ShowInteractionPrompt("🌿 Water is now safe for replanting! Equip Coral Replanting Kit [2].");
            }
        }

        private static void EnsurePopupTextures()
        {
            if (_popupTex == null)
            {
                _popupTex = new Texture2D(1, 1);
                _popupTex.SetPixel(0, 0, Color.white);
                _popupTex.Apply();
            }
        }

        private void DrawPopupOutline(Rect rect, Color color, int thickness)
        {
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _popupTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), _popupTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _popupTex);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), _popupTex);
            GUI.color = prevColor;
        }

        private void OnGUI()
        {
            if (!_showSafetyPopup) return;

            EnsurePopupTextures();

            float w = 620f;
            float h = 250f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.35f;
            Rect modalRect = new Rect(x, y, w, h);

            // Background glassmorphism
            GUI.color = new Color(0.02f, 0.11f, 0.18f, 0.96f);
            GUI.DrawTexture(modalRect, _popupTex);

            // Glowing cyan/emerald border
            Color borderColor = new Color(0.20f, 0.95f, 0.75f, 0.95f);
            DrawPopupOutline(modalRect, borderColor, 3);
            GUI.color = Color.white;

            // Top banner header
            Rect headerRect = new Rect(modalRect.x + 10f, modalRect.y + 14f, modalRect.width - 20f, 32f);
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.30f, 1.0f, 0.80f);
            GUI.Label(headerRect, "🌿 WATER AREA RESTORED & SAFE FOR REPLANTING!", headerStyle);

            // Subtitle description
            Rect descRect = new Rect(modalRect.x + 20f, modalRect.y + 50f, modalRect.width - 40f, 36f);
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            descStyle.normal.textColor = new Color(0.85f, 0.95f, 1.0f);
            GUI.Label(descRect, "All water parameters verified optimal. Contaminants neutralized and pH stabilized.", descStyle);

            // 3 Stat verification badges
            float badgeW = (modalRect.width - 50f) / 3f;
            float badgeH = 34f;
            float badgeY = modalRect.y + 92f;
            string[] badges = { "✔ pH 8.2 • Optimal", "✔ Salinity 35 PSU • Balanced", "✔ Temp 27.5°C • Stable" };

            for (int i = 0; i < 3; i++)
            {
                Rect bRect = new Rect(modalRect.x + 20f + i * (badgeW + 5f), badgeY, badgeW, badgeH);
                GUI.color = new Color(0.04f, 0.22f, 0.28f, 0.90f);
                GUI.DrawTexture(bRect, _popupTex);
                DrawPopupOutline(bRect, new Color(0.25f, 0.85f, 0.90f, 0.75f), 1);
                GUI.color = Color.white;

                GUIStyle badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                badgeStyle.normal.textColor = new Color(0.4f, 0.95f, 0.85f);
                GUI.Label(bRect, badges[i], badgeStyle);
            }

            // Call to action banner
            Rect calloutRect = new Rect(modalRect.x + 20f, modalRect.y + 138f, modalRect.width - 40f, 44f);
            GUI.color = new Color(0.12f, 0.24f, 0.10f, 0.92f);
            GUI.DrawTexture(calloutRect, _popupTex);
            DrawPopupOutline(calloutRect, new Color(0.40f, 0.95f, 0.35f, 0.85f), 1);
            GUI.color = Color.white;

            GUIStyle calloutStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            calloutStyle.normal.textColor = new Color(1.0f, 0.95f, 0.35f);
            GUI.Label(calloutRect, "👉 NEXT MISSION: Equip Coral Replanting Kit [Press 2] & Proceed to Reef Bed!", calloutStyle);

            // Dismiss Button / Prompt
            Rect buttonRect = new Rect(modalRect.x + (modalRect.width - 240f) * 0.5f, modalRect.y + 194f, 240f, 36f);
            GUI.color = new Color(0.06f, 0.35f, 0.45f, 0.95f);
            GUI.DrawTexture(buttonRect, _popupTex);
            DrawPopupOutline(buttonRect, new Color(0.35f, 1.0f, 0.90f), 2);
            GUI.color = Color.white;

            GUIStyle btnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            btnStyle.normal.textColor = Color.white;

            int remainingSeconds = Mathf.CeilToInt(Mathf.Max(0f, _safetyPopupExpiryTime - Time.time));
            GUI.Label(buttonRect, $"[Space / E] Continue ({remainingSeconds}s)", btnStyle);

            if (Event.current.type == EventType.MouseDown && buttonRect.Contains(Event.current.mousePosition))
            {
                _showSafetyPopup = false;
            }
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Returns the current sampling progress (0 to 1).
        /// </summary>
        public float Progress => _totalSamples > 0 ? (float)_samplesCollected / _totalSamples : 0f;

        /// <summary>
        /// Returns the number of un-sampled points remaining.
        /// </summary>
        public int RemainingPoints => _totalSamples - _samplesCollected;
    }
}
