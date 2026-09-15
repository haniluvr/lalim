using UnityEngine;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Trigger collider placed at each challenge zone entrance.
    /// When the player enters and confirms, starts the associated challenge:
    /// - Locks player movement
    /// - Publishes ChallengeStartedEvent with DDA-adjusted parameters
    /// - Camera transitions to 1P via CameraController's event subscription
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [Tooltip("Unique zone identifier (use GameConstants.Zones values)")]
        [SerializeField] private string _zoneId;

        [Tooltip("Which region this zone belongs to")]
        [SerializeField] private Region _region;

        [Tooltip("The type of challenge in this zone")]
        [SerializeField] private ChallengeType _challengeType;

        [Header("Challenge Parameters (Base Values — DDA adjusts these)")]
        [Tooltip("Base number of tasks (e.g., trash items, coral slots, sample locations)")]
        [SerializeField] private int _baseTaskQuantity = 10;

        [Tooltip("Base time limit in seconds (0 = no timer)")]
        [SerializeField] private float _baseTimeLimit = 120f;

        [Tooltip("Expected completion time for DDA deviation calculation")]
        [SerializeField] private float _expectedCompletionTime = 90f;

        [Header("Player Positioning")]
        [Tooltip("Where to place the player when the challenge starts")]
        [SerializeField] private Transform _challengeStartPoint;

        [Header("Interaction")]
        [Tooltip("Prompt shown when player is in range")]
        [SerializeField] private string _interactionPrompt = "Press E to begin challenge";

        [Tooltip("Required tool item ID (empty = no tool required)")]
        [SerializeField] private string _requiredToolId = "";

        // ─────────────────────────────────────────────
        // State
        // ─────────────────────────────────────────────

        private bool _isCompleted;
        private bool _playerInRange;
        private bool _challengeActive;
        private float _alertMessageTime = 0f;
        private string _alertMessage = "";
        private static Texture2D _solidTex;

        public string ZoneId => _zoneId;
        public Region Region => _region;
        public ChallengeType ChallengeType => _challengeType;
        public bool IsCompleted => _isCompleted;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start()
        {
            // Check if this zone was already completed (from save)
            if (RegionProgressTracker.Instance != null)
            {
                _isCompleted = RegionProgressTracker.Instance.IsZoneCompleted(_zoneId);
            }

            // Ensure collider is a trigger
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void Update()
        {
            if (!_playerInRange || _isCompleted || _challengeActive) return;

            // Sync interaction prompt with HUDController if available
            if (HUDController.Instance != null)
            {
                if (!IsRequiredToolEquipped())
                {
                    string reqTool = GetEffectiveRequiredToolId();
                    string toolName = GetRequiredToolName(reqTool);
                    string hotkey = GetToolHotkey(reqTool);
                    HUDController.Instance.ShowInteractionPrompt($"⚠ Equip {toolName} ([{hotkey}] or [I]) to enter");
                }
                else
                {
                    HUDController.Instance.ShowInteractionPrompt($"[E] Start {GetChallengeDisplayName()} Challenge");
                }
            }

            // Wait for interact input (via InputManager or direct fallback)
            bool interactPressed = false;
            if (InputManager.Instance != null)
            {
                interactPressed = InputManager.Instance.InteractPressed;
            }
            else
            {
                interactPressed = Input.GetKeyDown(KeyCode.E);
                if (!interactPressed && UnityEngine.InputSystem.Keyboard.current != null)
                {
                    interactPressed = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
                }
            }

            if (interactPressed)
            {
                TryStartChallenge();
            }
        }

        // ─────────────────────────────────────────────
        // Trigger Detection
        // ─────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (_isCompleted || _challengeActive) return;

            if (other.CompareTag(GameConstants.Tags.PLAYER))
            {
                _playerInRange = true;
                Debug.Log($"[ZoneTrigger] Player entered zone: {_zoneId} ({_challengeType}).");

                if (InputManager.Instance == null)
                {
                    Debug.LogWarning("[ZoneTrigger] Note: InputManager is missing from scene. You can add it via Tools > Lalim > Setup Scene Managers.");
                }
                if (ChallengeManager.Instance == null)
                {
                    Debug.LogWarning("[ZoneTrigger] Note: ChallengeManager is missing from scene. You can add it via Tools > Lalim > Setup Scene Managers.");
                }

                if (HUDController.Instance != null)
                {
                    if (!IsRequiredToolEquipped())
                    {
                        string reqTool = GetEffectiveRequiredToolId();
                        string toolName = GetRequiredToolName(reqTool);
                        string hotkey = GetToolHotkey(reqTool);
                        HUDController.Instance.ShowInteractionPrompt($"⚠ Equip {toolName} ([{hotkey}] or [I]) to enter");
                    }
                    else
                    {
                        HUDController.Instance.ShowInteractionPrompt($"[E] Start {GetChallengeDisplayName()} Challenge");
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(GameConstants.Tags.PLAYER))
            {
                _playerInRange = false;

                if (HUDController.Instance != null)
                {
                    HUDController.Instance.SetInteractionPromptVisible(false);
                }
            }
        }

        // ─────────────────────────────────────────────
        // Challenge Start & Requirements
        // ─────────────────────────────────────────────

        public string GetChallengeDisplayName()
        {
            switch (_challengeType)
            {
                case ChallengeType.WasteSegregation: return "Waste Segregation";
                case ChallengeType.WaterSampling: return "Water Sampling";
                case ChallengeType.CoralReplanting: return "Coral Replanting";
                default: return _challengeType.ToString();
            }
        }

        public string GetToolHotkey(string toolId)
        {
            switch (toolId)
            {
                case "07_watersamplekit": return "1";
                case "05_coralreplantingkit": return "2";
                case "04_wastesegregationbag": return "3";
                default: return "I";
            }
        }

        public void ShowAlert(string message, float duration = 2.5f)
        {
            _alertMessage = message;
            _alertMessageTime = Time.time + duration;
        }

        public string GetEffectiveRequiredToolId()
        {
            if (!string.IsNullOrEmpty(_requiredToolId)) return _requiredToolId;

            switch (_challengeType)
            {
                case ChallengeType.WasteSegregation:
                    return "04_wastesegregationbag";
                case ChallengeType.WaterSampling:
                    return "07_watersamplekit";
                case ChallengeType.CoralReplanting:
                    return "05_coralreplantingkit";
                default:
                    return "";
            }
        }

        public string GetRequiredToolName(string toolId)
        {
            switch (toolId)
            {
                case "04_wastesegregationbag": return "Waste Segregation Bag";
                case "07_watersamplekit": return "Water Sample Kit";
                case "05_coralreplantingkit": return "Coral Replanting Kit";
                default: return "Required Conservation Tool";
            }
        }

        public bool IsRequiredToolEquipped()
        {
            string reqTool = GetEffectiveRequiredToolId();
            if (string.IsNullOrEmpty(reqTool)) return true;

            return ToolEquipSystem.Instance != null && ToolEquipSystem.Instance.IsToolEquipped(reqTool);
        }

        private void TryStartChallenge()
        {
            string reqTool = GetEffectiveRequiredToolId();
            if (!string.IsNullOrEmpty(reqTool))
            {
                string toolName = GetRequiredToolName(reqTool);
                string hotkey = GetToolHotkey(reqTool);

                // Verify item is collected in inventory
                if (Inventory.InventoryManager.Instance != null && !Inventory.InventoryManager.Instance.HasItem(reqTool))
                {
                    ShowAlert($"⚠ You need the {toolName} in your inventory to enter!");
                    Debug.LogWarning($"[ZoneTrigger] You need the {toolName} in your inventory to enter this zone!");
                    return;
                }

                // Verify item is actively EQUIPPED
                if (!IsRequiredToolEquipped())
                {
                    ShowAlert($"⚠ Equip {toolName} to start! Press [{hotkey}] or [I]");
                    Debug.LogWarning($"[ZoneTrigger] You must equip the {toolName} to start the {_challengeType} challenge! Press [{hotkey}] or [I].");
                    return;
                }
            }

            // Check region accessibility
            if (GameManager.Instance != null && !GameManager.Instance.IsRegionAccessible(_region))
            {
                ShowAlert($"⚠ Region {_region} is locked!");
                Debug.LogWarning($"[ZoneTrigger] Region {_region} is locked");
                return;
            }

            StartChallenge();
        }

        private void StartChallenge()
        {
            _challengeActive = true;

            if (HUDController.Instance != null)
            {
                HUDController.Instance.SetInteractionPromptVisible(false);
            }

            // Position player at challenge start point
            if (_challengeStartPoint != null)
            {
                PlayerController player = FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    player.SetPosition(_challengeStartPoint.position);
                }
            }

            // Calculate DDA-adjusted parameters
            int adjustedQuantity = _baseTaskQuantity;
            float adjustedTimeLimit = _baseTimeLimit;
            int hintBudget = GameConstants.DDA.DEFAULT_HINT_BUDGET;

            if (DDA.DDAManager.Instance != null)
            {
                adjustedQuantity = DDA.DDAManager.Instance.GetAdjustedTaskQuantity(_baseTaskQuantity);
                adjustedTimeLimit = DDA.DDAManager.Instance.GetAdjustedTimeLimit(_baseTimeLimit);
                hintBudget = DDA.DDAManager.Instance.HintBudget;
            }

            Debug.Log($"[ZoneTrigger] Starting challenge: {_zoneId} ({_challengeType}) " +
                      $"tasks={adjustedQuantity}, time={adjustedTimeLimit:F0}s, hints={hintBudget}");

            // Publish challenge start — CameraController auto-switches to 1P
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = _challengeType,
                ChallengeId = _zoneId,
                TaskQuantity = adjustedQuantity,
                TimeLimit = adjustedTimeLimit,
                HintBudget = hintBudget
            });
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.ChallengeId != _zoneId) return;

            _challengeActive = false;

            if (evt.Success)
            {
                _isCompleted = true;
                Debug.Log($"[ZoneTrigger] Zone completed: {_zoneId}");

                // Notify region progress tracker
                EventBus.Publish(new ZoneCompletedEvent
                {
                    ZoneId = _zoneId,
                    Region = _region,
                    ChallengeType = _challengeType
                });
            }
        }

        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (evt.ChallengeId != _zoneId) return;

            _challengeActive = false;
            Debug.Log($"[ZoneTrigger] Challenge failed in zone: {_zoneId} — Reason: {evt.Reason}");

            // Player can re-enter to retry
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Mark this zone as completed (e.g., when restoring from save).
        /// </summary>
        public void SetCompleted(bool completed)
        {
            _isCompleted = completed;
        }

        // ─────────────────────────────────────────────
        // On-Screen Prompt Rendering
        // ─────────────────────────────────────────────

        private static void EnsureTextures()
        {
            if (_solidTex == null)
            {
                _solidTex = new Texture2D(1, 1);
                _solidTex.SetPixel(0, 0, Color.white);
                _solidTex.Apply();
            }
        }

        private void DrawOutline(Rect rect, Color color, int thickness)
        {
            Color prevColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _solidTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), _solidTex);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _solidTex);
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), _solidTex);
            GUI.color = prevColor;
        }

        private void OnGUI()
        {
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;

            bool showAlert = Time.time < _alertMessageTime && !string.IsNullOrEmpty(_alertMessage);
            if (!_playerInRange && !showAlert) return;
            if (_challengeActive || _isCompleted) return;

            EnsureTextures();

            // Determine prompt position (stack cleanly if PlayerController is also showing a target prompt)
            bool hasPlayerPrompt = PlayerController.Instance != null && !string.IsNullOrEmpty(PlayerController.Instance.CurrentPromptText);
            float boxWidth = 470f;
            float boxHeight = 58f;
            float x = (Screen.width - boxWidth) * 0.5f;
            float y = hasPlayerPrompt ? Screen.height - 185f : Screen.height - 120f;
            Rect boxRect = new Rect(x, y, boxWidth, boxHeight);

            if (showAlert)
            {
                // Amber/Red alert prompt
                GUI.color = new Color(0.22f, 0.06f, 0.04f, 0.95f);
                GUI.DrawTexture(boxRect, _solidTex);
                DrawOutline(boxRect, new Color(1.0f, 0.35f, 0.25f, 0.95f), 2);
                GUI.color = Color.white;

                GUIStyle alertStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                alertStyle.normal.textColor = new Color(1.0f, 0.88f, 0.35f);
                GUI.Label(boxRect, _alertMessage, alertStyle);
            }
            else
            {
                bool isEquipped = IsRequiredToolEquipped();
                string reqTool = GetEffectiveRequiredToolId();
                string toolName = GetRequiredToolName(reqTool);
                string hotkey = GetToolHotkey(reqTool);

                if (!isEquipped)
                {
                    // Warning: Required tool not equipped
                    GUI.color = new Color(0.18f, 0.11f, 0.02f, 0.94f);
                    GUI.DrawTexture(boxRect, _solidTex);
                    DrawOutline(boxRect, new Color(1.0f, 0.72f, 0.15f, 0.92f), 2);
                    GUI.color = Color.white;

                    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperCenter
                    };
                    titleStyle.normal.textColor = new Color(1.0f, 0.82f, 0.25f);

                    GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.LowerCenter
                    };
                    promptStyle.normal.textColor = Color.white;

                    GUI.Label(new Rect(boxRect.x, boxRect.y + 6f, boxRect.width, 18f), $"⚠ {toolName.ToUpper()} REQUIRED TO ENTER", titleStyle);
                    GUI.Label(new Rect(boxRect.x, boxRect.y + 26f, boxRect.width, 24f), $"Press [{hotkey}] to Quick-Equip  •  Press [I] for Inventory", promptStyle);
                }
                else
                {
                    // Ready: Press E to start
                    GUI.color = new Color(0.02f, 0.14f, 0.22f, 0.94f);
                    GUI.DrawTexture(boxRect, _solidTex);
                    DrawOutline(boxRect, new Color(0.20f, 0.88f, 0.75f, 0.95f), 2);
                    GUI.color = Color.white;

                    GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 11,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.UpperCenter
                    };
                    titleStyle.normal.textColor = new Color(0.35f, 0.92f, 1.0f);

                    GUIStyle promptStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.LowerCenter
                    };
                    promptStyle.normal.textColor = Color.white;

                    GUI.Label(new Rect(boxRect.x, boxRect.y + 6f, boxRect.width, 18f), $"ZONE ENTRANCE: {GetChallengeDisplayName().ToUpper()}", titleStyle);
                    GUI.Label(new Rect(boxRect.x, boxRect.y + 26f, boxRect.width, 24f), $"[E] Press to Begin Challenge", promptStyle);
                }
            }
        }

        // ─────────────────────────────────────────────
        // Editor
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color zoneColor = _isCompleted ? Color.green : Color.yellow;
            zoneColor.a = 0.3f;
            Gizmos.color = zoneColor;

            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            }

            // Label
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3f,
                $"{_zoneId}\n{_challengeType}\n{(_isCompleted ? "✓ DONE" : "○ PENDING")}"
            );

            // Draw line to start point
            if (_challengeStartPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _challengeStartPoint.position);
                Gizmos.DrawSphere(_challengeStartPoint.position, 0.3f);
            }
        }
#endif
    }
}
