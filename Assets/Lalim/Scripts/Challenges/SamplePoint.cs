using UnityEngine;
using UnityEngine.InputSystem;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.UI;

namespace Lalim.Challenges
{
    /// <summary>
    /// Parameter being measured at this sample point.
    /// Determines the UI label, scientific data readout, and visual feedback color.
    /// </summary>
    public enum SampleParameter
    {
        pH,
        Salinity,
        Temperature
    }

    /// <summary>
    /// World-space water sampling station and environmental anomaly point.
    /// Implements IInteractable so PlayerController targets it via raycasting.
    /// 
    /// Visual Features:
    ///   - Procedural rising bubble seepage column (marks underwater anomaly)
    ///   - Oceanographic mooring cable anchoring buoy to seabed
    ///   - Pulsing illuminated LED beacon light (Amber/Cyan pending -> Green complete)
    ///   - Floating in-world distance & parameter HUD badge
    ///   - Hold-to-sample analyzing progress gauge
    ///   - Real-time Scientific Digital Readout pop-up upon completion
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SamplePoint : MonoBehaviour, IInteractable
    {
        [Header("Sample Configuration")]
        [Tooltip("Which parameter this point measures")]
        [SerializeField] private SampleParameter _parameter = SampleParameter.pH;

        [Tooltip("Display name shown in the interaction prompt")]
        [SerializeField] private string _pointName = "Sample Point";

        [Tooltip("Item ID of the required tool (Water Sample Kit)")]
        [SerializeField] private string _requiredToolId = "07_watersamplekit";

        [Header("Sampling Animation")]
        [Tooltip("Time in seconds the sampling process takes (hold duration)")]
        [SerializeField] private float _samplingDuration = 2.5f;

        [Header("Visual Feedback")]
        [Tooltip("Renderer to tint when the point is available/sampled (auto-detects if empty)")]
        [SerializeField] private Renderer _markerRenderer;

        [SerializeField] private Color _availableColor = new Color(0.2f, 0.7f, 1f, 0.6f);
        [SerializeField] private Color _samplingColor = new Color(1f, 0.9f, 0.2f, 0.8f);
        [SerializeField] private Color _completedColor = new Color(0.2f, 1f, 0.4f, 0.6f);

        [Header("Environmental Visual Indicators")]
        [Tooltip("Spawns a vertical column of fizzy seepage bubbles to mark anomaly from afar")]
        [SerializeField] private bool _enableBubbleSeepage = true;

        [Tooltip("Draws a steel mooring cable from the buoy straight down to the seafloor")]
        [SerializeField] private bool _enableMooringCable = true;
        [SerializeField] private float _cableMaxDepth = 30f;

        [Tooltip("Gently bobs the sensor buoy with ocean swell")]
        [SerializeField] private bool _enableIdleBob = true;

        [Tooltip("Illuminates a pulsing beacon light (blinking when pending, steady when done)")]
        [SerializeField] private bool _enableBeaconLight = true;

        [Tooltip("Shows in-world floating parameter & distance badge when within range")]
        [SerializeField] private bool _enableFloatingHUDTag = true;

        [Header("Particle Effect (Optional)")]
        [Tooltip("Particle system to play when sampling completes")]
        [SerializeField] private ParticleSystem _completeVFX;

        [Header("Audio")]
        [SerializeField] private AudioClip _samplingLoopSound;
        [SerializeField] private AudioClip _sampleCompleteSound;
        [SerializeField] private AudioClip _noToolSound;

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        public SampleParameter Parameter => _parameter;
        public bool IsSampled { get; private set; }
        public bool IsSampling { get; private set; }

        private float _samplingProgress;
        private AudioSource _audioSource;
        private Material _markerMaterial;
        private WaterSamplingChallenge _challenge;

        private Vector3 _initialPosition;
        private float _bobOffset;
        private ParticleSystem _bubbleVFX;
        private LineRenderer _cableLine;
        private Light _beaconLight;
        private static Texture2D _guiSolidTex;

        // Scientific Digital Readout Popup (Shared Static)
        private static float _activeReadoutEndTime = 0f;
        private static ScientificSampleResult _lastReadout;

        public struct ScientificSampleResult
        {
            public SampleParameter Parameter;
            public string PointName;
            public string ParameterLabel;
            public string ValueDisplay;
            public string StatusDiagnosis;
            public string ScientificExplanation;
            public Color ThemeColor;
        }

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            _initialPosition = transform.position;
            _bobOffset = Random.Range(0f, Mathf.PI * 2f);

            // 1. Ensure solid collider for raycasting
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
            }
            else
            {
                SphereCollider sc = gameObject.AddComponent<SphereCollider>();
                sc.radius = 0.8f;
                sc.isTrigger = false;
            }

            // 2. Ensure Interactable layer
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
            }

            // 3. Cache audio source
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1f;
            }

            // 4. Auto-detect renderer if not linked
            if (_markerRenderer == null)
            {
                _markerRenderer = GetComponentInChildren<Renderer>();
            }

            if (_markerRenderer != null)
            {
                _markerMaterial = _markerRenderer.material;
                _markerMaterial.color = GetParameterColor();
            }

            // 5. Environmental Visuals
            if (_enableBubbleSeepage)
            {
                CreateSeepageBubbles();
            }

            if (_enableMooringCable)
            {
                CreateMooringCable();
            }

            if (_enableBeaconLight)
            {
                SetupBeacon();
            }

            // 6. Find parent challenge
            _challenge = GetComponentInParent<WaterSamplingChallenge>();
        }

        private void Update()
        {
            // Ocean bobbing motion
            if (_enableIdleBob)
            {
                float yOffset = Mathf.Sin(Time.time * 1.6f + _bobOffset) * 0.08f;
                transform.position = _initialPosition + Vector3.up * yOffset;

                // Keep mooring line top anchored to buoy
                if (_cableLine != null)
                {
                    _cableLine.SetPosition(0, transform.position);
                }
            }

            // Beacon light pulsing
            if (_enableBeaconLight)
            {
                UpdateBeacon();
            }

            if (!IsSampling) return;

            // Progress sampling hold
            _samplingProgress += Time.deltaTime;

            // Check if player released interact key (E / Gamepad)
            bool isHeld = false;
            if (InputManager.Instance != null && InputManager.Instance.InteractHeld)
            {
                isHeld = true;
            }
            else
            {
                isHeld = Input.GetKey(KeyCode.E);
                if (!isHeld && Keyboard.current != null)
                {
                    isHeld = Keyboard.current.eKey.isPressed;
                }
            }

            if (!isHeld)
            {
                CancelSampling();
                return;
            }

            // Check completion
            if (_samplingProgress >= _samplingDuration)
            {
                CompleteSampling();
            }
        }

        // ─────────────────────────────────────────────
        // Environmental Indicators Setup
        // ─────────────────────────────────────────────

        private void CreateSeepageBubbles()
        {
            if (_bubbleVFX != null) return;

            GameObject bubbleGO = new GameObject("[SeepageBubbles]");
            bubbleGO.transform.SetParent(transform, false);
            bubbleGO.transform.localPosition = new Vector3(0, -0.6f, 0);

            _bubbleVFX = bubbleGO.AddComponent<ParticleSystem>();
            var main = _bubbleVFX.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.12f; // Buoyant upward rising

            Color bubbleColor = GetParameterColor();
            bubbleColor.a = 0.65f;
            main.startColor = bubbleColor;

            var emission = _bubbleVFX.emission;
            emission.rateOverTime = 18f;

            var shape = _bubbleVFX.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;

            var vel = _bubbleVFX.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            var renderer = bubbleGO.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            _bubbleVFX.Play();
        }

        private void CreateMooringCable()
        {
            Vector3 startPos = transform.position;
            Vector3 endPos;

            int layerMask = ~LayerMask.GetMask("Interactable", "Player", "Ignore Raycast");
            if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, _cableMaxDepth, layerMask))
            {
                endPos = hit.point;
            }
            else
            {
                endPos = startPos + Vector3.down * 6f; // Fallback depth
            }

            GameObject cableGO = new GameObject("[MooringCable]");
            cableGO.transform.SetParent(transform, false);

            _cableLine = cableGO.AddComponent<LineRenderer>();
            _cableLine.useWorldSpace = true;
            _cableLine.positionCount = 2;
            _cableLine.SetPosition(0, startPos);
            _cableLine.SetPosition(1, endPos);
            _cableLine.startWidth = 0.035f;
            _cableLine.endWidth = 0.035f;

            // Simple unlit shader material
            Material cableMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            cableMat.color = new Color(0.12f, 0.22f, 0.30f, 0.85f);
            _cableLine.material = cableMat;
        }

        private void SetupBeacon()
        {
            _beaconLight = GetComponent<Light>();
            if (_beaconLight == null)
            {
                _beaconLight = gameObject.AddComponent<Light>();
            }

            _beaconLight.type = LightType.Point;
            _beaconLight.range = 5.0f;
            _beaconLight.intensity = 1.6f;
            _beaconLight.color = GetParameterColor();
        }

        private void UpdateBeacon()
        {
            if (_beaconLight == null) return;

            if (IsSampled)
            {
                _beaconLight.color = _completedColor;
                _beaconLight.intensity = 1.4f;
                return;
            }

            if (IsSampling)
            {
                // Rapid flash when analyzing
                float fastPulse = (Mathf.Sin(Time.time * 16f) + 1f) * 0.5f;
                _beaconLight.color = Color.Lerp(_samplingColor, Color.white, fastPulse);
                _beaconLight.intensity = 2.2f + fastPulse * 1.5f;
            }
            else
            {
                // Rhythmic beacon flash (1.2s cycle)
                float pulse = (Mathf.Sin(Time.time * 2.6f + _bobOffset) + 1f) * 0.5f;
                _beaconLight.color = GetParameterColor();
                _beaconLight.intensity = 0.8f + pulse * 1.6f;
            }
        }

        // ─────────────────────────────────────────────
        // IInteractable
        // ─────────────────────────────────────────────

        public void Interact()
        {
            if (IsSampled || IsSampling) return;

            // Check if parent challenge has started
            if (_challenge != null && !_challenge.IsActive)
            {
                Debug.Log($"[SamplePoint] Please start the Water Sampling Challenge at the Zone Entrance [E] first!");
                return;
            }

            // Check if player has Water Sample Kit equipped
            if (ToolEquipSystem.Instance == null ||
                !ToolEquipSystem.Instance.IsToolEquipped(_requiredToolId))
            {
                Debug.Log($"[SamplePoint] Player needs {_requiredToolId} equipped to sample");

                if (_noToolSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_noToolSound);
                }
                return;
            }

            BeginSampling();
        }

        public string GetInteractionPrompt()
        {
            if (IsSampled) return $"{_pointName} — Already sampled";

            if (_challenge != null && !_challenge.IsActive)
            {
                return $"⚠ Start Water Sampling Challenge at Zone Entrance [E] first";
            }

            if (ToolEquipSystem.Instance == null ||
                !ToolEquipSystem.Instance.IsToolEquipped(_requiredToolId))
            {
                return $"⚠ [1] Equip Water Sample Kit to test {_parameter}";
            }

            return $"Hold [E] to sample {_parameter} at {_pointName}";
        }

        // ─────────────────────────────────────────────
        // Sampling Process
        // ─────────────────────────────────────────────

        private void BeginSampling()
        {
            IsSampling = true;
            _samplingProgress = 0f;

            SetColor(_samplingColor);

            if (_samplingLoopSound != null && _audioSource != null)
            {
                _audioSource.clip = _samplingLoopSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }

            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportPlayerAction();
            }

            Debug.Log($"[SamplePoint] Sampling started at {_pointName} ({_parameter})");
        }

        private void CompleteSampling()
        {
            IsSampling = false;
            IsSampled = true;

            // Stop loop sound
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }

            // Play complete sound
            if (_sampleCompleteSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_sampleCompleteSound);
            }

            SetColor(_completedColor);

            if (_beaconLight != null)
            {
                _beaconLight.color = _completedColor;
                _beaconLight.intensity = 1.4f;
            }

            // Calm bubble emissions down
            if (_bubbleVFX != null)
            {
                var emission = _bubbleVFX.emission;
                emission.rateOverTime = 6f;
            }

            if (_completeVFX != null)
            {
                _completeVFX.Play();
            }

            // Trigger Scientific Digital Readout Pop-up
            _lastReadout = GenerateScientificResult();
            _activeReadoutEndTime = Time.time + 5.0f;

            if (HUDController.Instance != null)
            {
                HUDController.Instance.ShowInteractionPrompt($"✓ {_pointName} ({_lastReadout.ValueDisplay}) — {_lastReadout.StatusDiagnosis}");
            }

            // Report task completed
            if (ChallengeManager.Instance != null)
            {
                ChallengeManager.Instance.ReportTaskCompleted();
            }

            if (_challenge != null)
            {
                _challenge.OnSampleCollected(this);
            }

            Debug.Log($"[SamplePoint] Sample collected: {_parameter} -> {_lastReadout.ValueDisplay}");
        }

        private void CancelSampling()
        {
            IsSampling = false;
            _samplingProgress = 0f;

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
                _audioSource.loop = false;
            }

            SetColor(GetParameterColor());

            Debug.Log($"[SamplePoint] Sampling cancelled at {_pointName}");
        }

        private ScientificSampleResult GenerateScientificResult()
        {
            ScientificSampleResult res = new ScientificSampleResult();
            res.Parameter = _parameter;
            res.PointName = _pointName;

            switch (_parameter)
            {
                case SampleParameter.pH:
                    res.ParameterLabel = "WATER ACIDITY (pH)";
                    float phVal = Random.Range(7.35f, 7.55f);
                    res.ValueDisplay = $"{phVal:F2} pH";
                    res.StatusDiagnosis = "MILD OCEAN ACIDIFICATION DETECTED";
                    res.ScientificExplanation = "Seabed CO₂ seepage lowering local water alkalinity. Approaching critical reef calcification threshold.";
                    res.ThemeColor = new Color(0.72f, 0.96f, 0.28f);
                    break;

                case SampleParameter.Salinity:
                    res.ParameterLabel = "WATER SALINITY (PSU)";
                    float salVal = Random.Range(30.8f, 32.4f);
                    res.ValueDisplay = $"{salVal:F1} PSU";
                    res.StatusDiagnosis = "ESTUARY HALOCLINE GRADIENT";
                    res.ScientificExplanation = "Freshwater aquifer runoff detected mixing with marine layer. Stratification affects localized coral polyp osmosis.";
                    res.ThemeColor = new Color(0.30f, 0.88f, 1.0f);
                    break;

                case SampleParameter.Temperature:
                    res.ParameterLabel = "WATER TEMPERATURE (°C)";
                    float tempVal = Random.Range(29.4f, 30.6f);
                    res.ValueDisplay = $"{tempVal:F1} °C";
                    res.StatusDiagnosis = "THERMAL STRESS ANOMALY WARNING";
                    res.ScientificExplanation = "Localized thermal hotspot (+2.2°C above seasonal baseline). Elevates coral bleaching vulnerability.";
                    res.ThemeColor = new Color(1.0f, 0.68f, 0.22f);
                    break;
            }

            return res;
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        public void ResetPoint()
        {
            IsSampled = false;
            IsSampling = false;
            _samplingProgress = 0f;
            SetColor(GetParameterColor());

            if (_bubbleVFX != null)
            {
                var emission = _bubbleVFX.emission;
                emission.rateOverTime = 18f;
            }
        }

        public float SamplingProgressNormalized =>
            _samplingDuration > 0f ? Mathf.Clamp01(_samplingProgress / _samplingDuration) : 0f;

        public Color GetParameterColor()
        {
            switch (_parameter)
            {
                case SampleParameter.pH:
                    return new Color(0.72f, 0.96f, 0.28f, 1f); // Neon Lime
                case SampleParameter.Salinity:
                    return new Color(0.28f, 0.88f, 1.0f, 1f);  // Cyan
                case SampleParameter.Temperature:
                    return new Color(1.0f, 0.68f, 0.22f, 1f);  // Amber / Coral
                default:
                    return _availableColor;
            }
        }

        private void SetColor(Color color)
        {
            if (_markerMaterial != null)
            {
                _markerMaterial.color = color;
            }
        }

        // ─────────────────────────────────────────────
        // GUI: Scientific Readout, Hold Meter & World Tags
        // ─────────────────────────────────────────────

        private static void EnsureGuiTextures()
        {
            if (_guiSolidTex == null)
            {
                _guiSolidTex = new Texture2D(1, 1);
                _guiSolidTex.SetPixel(0, 0, Color.white);
                _guiSolidTex.Apply();
            }
        }

        private static void DrawGuiOutline(Rect rect, Color color, int thickness)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _guiSolidTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), _guiSolidTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _guiSolidTex);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), _guiSolidTex);
            GUI.color = prev;
        }

        private void OnGUI()
        {
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;
            EnsureGuiTextures();

            // 1. In-World Floating Tag (Distance & Parameter Badge)
            if (_enableFloatingHUDTag)
            {
                DrawFloatingWorldBadge();
            }

            // 2. Sampling Hold Progress Gauge
            if (IsSampling)
            {
                DrawSamplingGauge();
            }

            // 3. Scientific Digital Readout Pop-up (Shared Static Banner)
            if (Time.time < _activeReadoutEndTime)
            {
                DrawScientificReadout();
            }
        }

        private void DrawFloatingWorldBadge()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldTarget = transform.position + Vector3.up * 0.75f;
            float dist = Vector3.Distance(cam.transform.position, worldTarget);
            if (dist < 1.5f || dist > 35.0f) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldTarget);
            if (screenPos.z <= 0) return; // Behind camera

            float gx = screenPos.x;
            float gy = Screen.height - screenPos.y;

            float badgeWidth = 145f;
            float badgeHeight = 22f;
            Rect badgeRect = new Rect(gx - badgeWidth * 0.5f, gy - badgeHeight * 0.5f, badgeWidth, badgeHeight);

            Color paramColor = GetParameterColor();

            // Draw dark ocean pill
            GUI.color = new Color(0.02f, 0.10f, 0.18f, 0.88f);
            GUI.DrawTexture(badgeRect, _guiSolidTex);
            DrawGuiOutline(badgeRect, IsSampled ? new Color(0.2f, 0.9f, 0.4f, 0.8f) : paramColor, 1);
            GUI.color = Color.white;

            GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            tagStyle.normal.textColor = IsSampled ? new Color(0.4f, 1.0f, 0.6f) : paramColor;

            string text = IsSampled
                ? $"✓ {_parameter} ANALYZED"
                : $"⬡ {_parameter} POINT • {dist:F0}m";

            GUI.Label(badgeRect, text, tagStyle);
        }

        private void DrawSamplingGauge()
        {
            float barWidth = 360f;
            float barHeight = 46f;
            float x = (Screen.width - barWidth) * 0.5f;
            float y = Screen.height * 0.5f + 65f;
            Rect boxRect = new Rect(x, y, barWidth, barHeight);

            // Container box
            GUI.color = new Color(0.02f, 0.12f, 0.22f, 0.92f);
            GUI.DrawTexture(boxRect, _guiSolidTex);
            DrawGuiOutline(boxRect, new Color(0.3f, 0.9f, 1.0f, 0.95f), 2);

            // Progress bar
            float pct = SamplingProgressNormalized;
            float innerMargin = 6f;
            Rect fillRect = new Rect(x + innerMargin, y + barHeight - 16f, (barWidth - innerMargin * 2f) * pct, 10f);
            GUI.color = Color.Lerp(new Color(0.2f, 0.8f, 1.0f), new Color(0.3f, 1.0f, 0.6f), pct);
            GUI.DrawTexture(fillRect, _guiSolidTex);
            GUI.color = Color.white;

            // Label
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y + 5f, barWidth, 20f), $"ANALYZING {_parameter.ToString().ToUpper()} SAMPLE... {Mathf.RoundToInt(pct * 100f)}%", labelStyle);
        }

        private static void DrawScientificReadout()
        {
            float cardWidth = 530f;
            float cardHeight = 94f;
            float x = (Screen.width - cardWidth) * 0.5f;
            float y = 24f;
            Rect cardRect = new Rect(x, y, cardWidth, cardHeight);

            // Translucent dark glass backdrop
            GUI.color = new Color(0.02f, 0.08f, 0.16f, 0.96f);
            GUI.DrawTexture(cardRect, _guiSolidTex);
            DrawGuiOutline(cardRect, _lastReadout.ThemeColor, 2);
            GUI.color = Color.white;

            // Top Header: Title & Logged Status
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            headerStyle.normal.textColor = Color.white;

            GUIStyle statusTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            statusTagStyle.normal.textColor = new Color(0.35f, 1.0f, 0.65f);

            GUI.Label(new Rect(x + 16f, y + 8f, 320f, 18f), $"🧪 SCIENTIFIC ANALYSIS RESULT: {_lastReadout.PointName.ToUpper()}", headerStyle);
            GUI.Label(new Rect(x + cardWidth - 140f, y + 8f, 125f, 18f), "[DATA LOGGED ✓]", statusTagStyle);

            // Middle Row: Big Bold Value & Status Diagnosis
            GUIStyle valStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            valStyle.normal.textColor = _lastReadout.ThemeColor;

            GUIStyle diagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            diagStyle.normal.textColor = Color.white;

            GUI.Label(new Rect(x + 16f, y + 27f, 120f, 26f), _lastReadout.ValueDisplay, valStyle);
            GUI.Label(new Rect(x + 140f, y + 31f, cardWidth - 150f, 20f), $"• {_lastReadout.StatusDiagnosis}", diagStyle);

            // Bottom Row: Ecological Detail Note
            GUIStyle noteStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            noteStyle.normal.textColor = new Color(0.70f, 0.85f, 0.95f);
            GUI.Label(new Rect(x + 16f, y + 55f, cardWidth - 32f, 34f), _lastReadout.ScientificExplanation, noteStyle);
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor = GetParameterColor();
            gizmoColor.a = 0.5f;

            if (IsSampled) gizmoColor = new Color(0.2f, 1f, 0.4f, 0.5f);

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw line down to seabed preview
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 4f);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.0f,
                $"{_pointName}\n({_parameter})\n{(IsSampled ? "✓ SAMPLED" : "○ PENDING")}"
            );
        }
#endif
    }
}
