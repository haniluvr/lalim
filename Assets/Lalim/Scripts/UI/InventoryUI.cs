using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Lalim.Core;
using Lalim.Inventory;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// Interactive underwater inventory screen.
    /// Opens/closes with the 'I' key or Escape.
    /// Pauses diver movement while active and enables equipping/unequipping
    /// conservation tools (Water Sample Kit, Coral Replanting Kit, etc.).
    /// Also renders an equipped tool badge on the gameplay HUD.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        private static InventoryUI _instance;
        public static InventoryUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<InventoryUI>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[InventoryUI]");
                        _instance = go.AddComponent<InventoryUI>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public enum CategoryTab
        {
            All,
            Tools,
            Relics
        }

        [Header("State")]
        [SerializeField] private bool _isOpen;
        public bool IsOpen => _isOpen;

        [Header("Category Filtering")]
        [SerializeField] private CategoryTab _activeTab = CategoryTab.Tools;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _closeSound;
        [SerializeField] private AudioClip _equipSound;

        // GUI Styles and Texture Cache
        private Vector2 _scrollPos = Vector2.zero;
        private Texture2D _bgTexture;
        private Texture2D _cardTexture;
        private Texture2D _equippedCardTexture;
        private Texture2D _buttonTexture;
        private Texture2D _equippedButtonTexture;
        private Texture2D _pillTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            var ui = Instance;
            Debug.Log("[InventoryUI] Ready. Press [I] to open equipment inventory.");
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private int _lastToggleFrame = -1;

        /// <summary>
        /// Toggle the inventory window open/closed with frame debouncing.
        /// </summary>
        public void ToggleInventory()
        {
            if (Time.frameCount == _lastToggleFrame) return;
            _lastToggleFrame = Time.frameCount;
            SetInventoryOpen(!_isOpen);
        }

        private void Update()
        {
            // Direct key fallback check (works even if InputManager is not active)
            bool iPressed = Input.GetKeyDown(KeyCode.I);
            if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame) iPressed = true;

            if (iPressed)
            {
                ToggleInventory();
                return;
            }

            // Close check with Escape
            if (_isOpen)
            {
                bool closePressed = Input.GetKeyDown(KeyCode.Escape);
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) closePressed = true;

                if (closePressed)
                {
                    SetInventoryOpen(false);
                    return;
                }

                // Hotkeys 1-5 to equip tools directly while inventory is open
                HandleNumberHotkeys();
            }
            else
            {
                // Quick-equip hotkeys 1-3 directly during gameplay
                HandleNumberHotkeys();
            }
        }

        private void HandleNumberHotkeys()
        {
            KeyCode[] numKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
            for (int i = 0; i < numKeys.Length; i++)
            {
                bool pressed = Input.GetKeyDown(numKeys[i]);
                if (!pressed && Keyboard.current != null)
                {
                    switch (i)
                    {
                        case 0: pressed = Keyboard.current.digit1Key.wasPressedThisFrame; break;
                        case 1: pressed = Keyboard.current.digit2Key.wasPressedThisFrame; break;
                        case 2: pressed = Keyboard.current.digit3Key.wasPressedThisFrame; break;
                        case 3: pressed = Keyboard.current.digit4Key.wasPressedThisFrame; break;
                        case 4: pressed = Keyboard.current.digit5Key.wasPressedThisFrame; break;
                    }
                }

                if (pressed)
                {
                    EquipSlotIndex(i);
                    break;
                }
            }
        }

        private void EquipSlotIndex(int index)
        {
            if (!_isOpen)
            {
                // In gameplay, 1-3 always directly equip the primary conservation tools
                string[] primaryTools = { "07_watersamplekit", "05_coralreplantingkit", "04_wastesegregationbag" };
                if (index >= 0 && index < primaryTools.Length)
                {
                    if (ToolEquipSystem.Instance != null)
                    {
                        ToolEquipSystem.Instance.ToggleEquip(primaryTools[index]);
                        PlaySound(_equipSound);
                    }
                }
                return;
            }

            var items = GetFilteredItems();
            if (index >= 0 && index < items.Count)
            {
                string itemId = items[index].itemId;
                if (ToolEquipSystem.Instance != null)
                {
                    ToolEquipSystem.Instance.ToggleEquip(itemId);
                    PlaySound(_equipSound);
                }
            }
        }

        /// <summary>
        /// Open or close the inventory screen.
        /// </summary>
        public void SetInventoryOpen(bool open)
        {
            if (_isOpen == open) return;
            _isOpen = open;
            Debug.Log($"[InventoryUI] SetInventoryOpen: {open}");

            if (_isOpen)
            {
                PlaySound(_openSound);

                // Pause player movement and gameplay input
                PlayerController player = PlayerController.Instance != null ? PlayerController.Instance : FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    player.SetMovementEnabled(false);
                }
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.SetInputEnabled(false);
                }

                // Unlock cursor for GUI interaction
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                PlaySound(_closeSound);

                // Resume player movement and gameplay input
                PlayerController player = PlayerController.Instance != null ? PlayerController.Instance : FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    player.SetMovementEnabled(true);
                }
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.SetInputEnabled(true);
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }

        // ─────────────────────────────────────────────
        // Data Retrieval
        // ─────────────────────────────────────────────

        private List<ItemData> GetFilteredItems()
        {
            if (InventoryManager.Instance == null) return new List<ItemData>();

            List<ItemData> allCollected = InventoryManager.Instance.GetAllCollectedItems();
            if (_activeTab == CategoryTab.All)
            {
                return allCollected;
            }

            List<ItemData> filtered = new List<ItemData>();
            foreach (var item in allCollected)
            {
                if (item == null) continue;

                if (_activeTab == CategoryTab.Tools)
                {
                    if (item.itemType == ItemType.Tool ||
                        item.itemType == ItemType.ConsumableKit ||
                        item.itemType == ItemType.CompanionItem)
                    {
                        filtered.Add(item);
                    }
                }
                else if (_activeTab == CategoryTab.Relics)
                {
                    if (item.itemType == ItemType.Relic ||
                        item.itemType == ItemType.QuestItem)
                    {
                        filtered.Add(item);
                    }
                }
            }

            if (_activeTab == CategoryTab.Tools)
            {
                string[] primaryToolOrder = { "07_watersamplekit", "05_coralreplantingkit", "04_wastesegregationbag" };
                List<ItemData> ordered = new List<ItemData>();
                foreach (string pid in primaryToolOrder)
                {
                    ItemData found = filtered.Find(x => x.itemId == pid);
                    if (found != null)
                    {
                        ordered.Add(found);
                        filtered.Remove(found);
                    }
                    else if (InventoryManager.Instance != null)
                    {
                        ItemData fallback = InventoryManager.Instance.GetItemData(pid);
                        if (fallback != null) ordered.Add(fallback);
                    }
                }
                ordered.AddRange(filtered);
                return ordered;
            }

            return filtered;
        }

        // ─────────────────────────────────────────────
        // GUI Rendering
        // ─────────────────────────────────────────────

        private void EnsureTextures()
        {
            if (_bgTexture == null) _bgTexture = MakeSolidTexture(new Color(0.03f, 0.10f, 0.18f, 0.95f));
            if (_cardTexture == null) _cardTexture = MakeSolidTexture(new Color(0.06f, 0.16f, 0.28f, 0.90f));
            if (_equippedCardTexture == null) _equippedCardTexture = MakeSolidTexture(new Color(0.06f, 0.28f, 0.26f, 0.95f));
            if (_buttonTexture == null) _buttonTexture = MakeSolidTexture(new Color(0.12f, 0.45f, 0.70f, 1.0f));
            if (_equippedButtonTexture == null) _equippedButtonTexture = MakeSolidTexture(new Color(0.15f, 0.65f, 0.42f, 1.0f));
            if (_pillTexture == null) _pillTexture = MakeSolidTexture(new Color(0.02f, 0.08f, 0.16f, 0.88f));
        }

        private Texture2D MakeSolidTexture(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            EnsureTextures();

            // Always render HUD equipped tool pill when inventory is closed
            if (!_isOpen)
            {
                DrawHUDToolBadge();
                return;
            }

            // --- Fullscreen Dark Marine Overlay Backdrop ---
            GUI.color = new Color(0.01f, 0.03f, 0.07f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // --- Centered Modal Container ---
            float modalWidth = Mathf.Min(740f, Screen.width - 40f);
            float modalHeight = Mathf.Min(560f, Screen.height - 40f);
            float modalX = (Screen.width - modalWidth) * 0.5f;
            float modalY = (Screen.height - modalHeight) * 0.5f;
            Rect modalRect = new Rect(modalX, modalY, modalWidth, modalHeight);

            // Modal Background
            GUI.DrawTexture(modalRect, _bgTexture);

            // Border outline
            DrawOutline(modalRect, new Color(0.20f, 0.78f, 0.92f, 0.85f), 2);

            // Content Area with padding
            GUILayout.BeginArea(new Rect(modalX + 24f, modalY + 20f, modalWidth - 48f, modalHeight - 40f));

            // Header Row: Title & Close Button
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(0.35f, 0.92f, 1.0f);
            GUILayout.Label("CONSERVATION INVENTORY & TOOLS", titleStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = new Color(0.70f, 0.85f, 0.95f);
            GUILayout.Label("Select a tool to equip for underwater challenges • Press [I] or [Esc] to Close", subStyle);
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Close button [X]
            GUIStyle closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                fixedWidth = 36f,
                fixedHeight = 36f,
                alignment = TextAnchor.MiddleCenter
            };
            closeBtnStyle.normal.textColor = Color.white;
            if (GUILayout.Button("✕", closeBtnStyle))
            {
                SetInventoryOpen(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);

            // Category Filter Tabs
            GUILayout.BeginHorizontal();
            CategoryTab[] tabs = { CategoryTab.Tools, CategoryTab.All, CategoryTab.Relics };
            string[] tabNames = { "Conservation Tools", "All Items", "Relics & Quests" };

            for (int i = 0; i < tabs.Length; i++)
            {
                bool isSelected = _activeTab == tabs[i];
                GUIStyle tabStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
                    fixedHeight = 32f
                };
                tabStyle.normal.textColor = isSelected ? new Color(0.2f, 1.0f, 0.8f) : new Color(0.8f, 0.85f, 0.9f);

                Color originalBg = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.18f, 0.55f, 0.70f, 1.0f);

                if (GUILayout.Button(tabNames[i], tabStyle, GUILayout.Width(170f)))
                {
                    _activeTab = tabs[i];
                }
                GUI.backgroundColor = originalBg;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);

            // Item List ScrollView
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

            List<ItemData> items = GetFilteredItems();
            string currentlyEquippedId = ToolEquipSystem.Instance != null ? ToolEquipSystem.Instance.EquippedItemId : "";

            if (items.Count == 0)
            {
                GUILayout.Space(40f);
                GUIStyle emptyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Italic,
                    alignment = TextAnchor.MiddleCenter
                };
                emptyStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
                GUILayout.Label("No items found in this category.", emptyStyle);

                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add Starter Conservation Tools", GUILayout.Width(250f), GUILayout.Height(36f)))
                {
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.AddItem("07_watersamplekit");
                        InventoryManager.Instance.AddItem("05_coralreplantingkit");
                        InventoryManager.Instance.AddItem("04_wastesegregationbag");
                    }
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ItemData item = items[i];
                    if (item == null) continue;

                    bool isEquipped = (item.itemId == currentlyEquippedId);
                    DrawItemCard(item, i + 1, isEquipped);
                    GUILayout.Space(8f);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8f);

            // Bottom Footer Info
            GUILayout.BeginHorizontal();
            GUIStyle footerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            footerStyle.normal.textColor = new Color(0.55f, 0.72f, 0.85f);
            GUILayout.Label($"Currently Equipped: {(string.IsNullOrEmpty(currentlyEquippedId) ? "None" : ToolEquipSystem.Instance?.EquippedItem?.displayName)} | Press [1]-[{items.Count}] to quick-equip", footerStyle);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close Inventory", GUILayout.Width(130f), GUILayout.Height(28f)))
            {
                SetInventoryOpen(false);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawItemCard(ItemData item, int hotkeyIndex, bool isEquipped)
        {
            // Card Container
            Rect cardRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(74f));

            // Background & Border
            GUI.DrawTexture(cardRect, isEquipped ? _equippedCardTexture : _cardTexture);
            DrawOutline(cardRect, isEquipped ? new Color(0.2f, 1.0f, 0.6f, 0.9f) : new Color(0.18f, 0.38f, 0.55f, 0.7f), isEquipped ? 2 : 1);

            // Card Inner Layout
            GUILayout.BeginArea(new Rect(cardRect.x + 12f, cardRect.y + 8f, cardRect.width - 24f, cardRect.height - 16f));
            GUILayout.BeginHorizontal();

            // Hotkey Badge
            GUIStyle hotkeyStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = 26f,
                fixedHeight = 26f
            };
            hotkeyStyle.normal.textColor = isEquipped ? new Color(0.2f, 1.0f, 0.6f) : Color.white;
            GUILayout.Box($"[{hotkeyIndex}]", hotkeyStyle);

            GUILayout.Space(10f);

            // Title, Type, and Description
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            nameStyle.normal.textColor = isEquipped ? new Color(0.3f, 1.0f, 0.7f) : Color.white;
            GUILayout.Label(item.displayName, nameStyle, GUILayout.ExpandWidth(false));

            GUILayout.Space(8f);

            // Type Tag
            GUIStyle tagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            tagStyle.normal.textColor = GetTypeColor(item.itemType);
            GUILayout.Label($"[{FormatItemType(item.itemType)}]", tagStyle);

            string challengeLabel = GetItemChallengeLabel(item.itemId);
            if (!string.IsNullOrEmpty(challengeLabel))
            {
                GUILayout.Space(6f);
                GUIStyle chalStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                chalStyle.normal.textColor = new Color(1.0f, 0.82f, 0.25f);
                GUILayout.Label($"• {challengeLabel}", chalStyle);
            }

            GUILayout.EndHorizontal();

            // Description
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };
            descStyle.normal.textColor = new Color(0.75f, 0.85f, 0.92f);
            string desc = !string.IsNullOrEmpty(item.description) ? item.description : GetDefaultDescription(item.itemId);
            GUILayout.Label(desc, descStyle);

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Action Button (Equip / Unequip)
            bool isEquippable = (item.itemType == ItemType.Tool ||
                                item.itemType == ItemType.ConsumableKit ||
                                item.itemType == ItemType.Relic);

            if (isEquippable)
            {
                GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedWidth = 150f,
                    fixedHeight = 44f
                };
                btnStyle.normal.textColor = Color.white;

                Color origColor = GUI.backgroundColor;
                GUI.backgroundColor = isEquipped ? new Color(0.15f, 0.65f, 0.40f, 1.0f) : new Color(0.12f, 0.48f, 0.78f, 1.0f);

                string btnLabel = isEquipped ? $"★ EQUIPPED\n(Click/[{hotkeyIndex}] to Unequip)" : $"EQUIP TOOL\n(Press [{hotkeyIndex}])";

                if (GUILayout.Button(btnLabel, btnStyle))
                {
                    if (ToolEquipSystem.Instance != null)
                    {
                        ToolEquipSystem.Instance.ToggleEquip(item.itemId);
                        PlaySound(_equipSound);
                    }
                }
                GUI.backgroundColor = origColor;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private string GetItemChallengeLabel(string itemId)
        {
            switch (itemId)
            {
                case "04_wastesegregationbag": return "FOR WASTE SEGREGATION CHALLENGE";
                case "07_watersamplekit": return "FOR WATER SAMPLING CHALLENGE";
                case "05_coralreplantingkit": return "FOR CORAL REPLANTING CHALLENGE";
                default: return "";
            }
        }

        private Color GetTypeColor(ItemType type)
        {
            switch (type)
            {
                case ItemType.Tool: return new Color(0.2f, 0.85f, 1.0f);
                case ItemType.ConsumableKit: return new Color(1.0f, 0.7f, 0.2f);
                case ItemType.Relic: return new Color(0.85f, 0.45f, 1.0f);
                case ItemType.QuestItem: return new Color(1.0f, 0.85f, 0.2f);
                default: return Color.white;
            }
        }

        private string FormatItemType(ItemType type)
        {
            switch (type)
            {
                case ItemType.Tool: return "CONSERVATION TOOL";
                case ItemType.ConsumableKit: return "CONTAINER KIT";
                case ItemType.Relic: return "CULTURAL RELIC";
                case ItemType.QuestItem: return "KEY ITEM";
                default: return type.ToString().ToUpper();
            }
        }

        private string GetDefaultDescription(string itemId)
        {
            switch (itemId)
            {
                case "07_watersamplekit":
                    return "Required for Water Sampling. Collects and analyzes water quality metrics (pH, Salinity, Temperature) at survey points.";
                case "05_coralreplantingkit":
                    return "Required for Coral Replanting. Safely carries nursery coral fragments to anchor securely into degraded reef substrate slots.";
                case "04_wastesegregationbag":
                    return "Required for Waste Segregation. Mesh bag used to collect ocean debris and sort it into plastic, metal, and food bins.";
                default:
                    return "Conservation equipment for marine exploration and ecosystem restoration.";
            }
        }

        // ─────────────────────────────────────────────
        // Gameplay In-Game HUD Tool Toolbar
        // ─────────────────────────────────────────────

        private void DrawHUDToolBadge()
        {
            string equippedId = ToolEquipSystem.Instance != null ? ToolEquipSystem.Instance.EquippedItemId : "";
            string equippedName = ToolEquipSystem.Instance != null && ToolEquipSystem.Instance.HasEquippedTool
                ? ToolEquipSystem.Instance.EquippedItem.displayName
                : "None";

            bool hasTool = ToolEquipSystem.Instance != null && ToolEquipSystem.Instance.HasEquippedTool;

            float badgeWidth = 330f;
            float badgeHeight = 74f;
            float x = 20f;
            float y = Screen.height - badgeHeight - 20f;

            Rect badgeRect = new Rect(x, y, badgeWidth, badgeHeight);

            // Draw translucent dark ocean pill
            GUI.DrawTexture(badgeRect, _pillTexture);
            DrawOutline(badgeRect, hasTool ? new Color(0.20f, 0.88f, 1.0f, 0.90f) : new Color(0.35f, 0.50f, 0.65f, 0.50f), 1);

            // Header Bar: Active Tool & [I] hint
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            headerStyle.normal.textColor = hasTool ? new Color(0.35f, 1.0f, 0.75f) : new Color(0.70f, 0.80f, 0.90f);

            GUIStyle invHintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            invHintStyle.normal.textColor = new Color(0.35f, 0.90f, 1.0f);

            string statusPrefix = hasTool ? "★ ACTIVE: " : "TOOL: ";
            GUI.Label(new Rect(x + 12f, y + 6f, badgeWidth - 100f, 18f), statusPrefix + equippedName, headerStyle);
            GUI.Label(new Rect(x + badgeWidth - 92f, y + 6f, 80f, 18f), "[I] Inventory", invHintStyle);

            // Quick-Slots Buttons Row: [1] Water Kit  [2] Coral Kit  [3] Waste Bag
            GUIStyle slotActiveStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            slotActiveStyle.normal.textColor = new Color(0.25f, 1.0f, 0.70f);

            GUIStyle slotInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };
            slotInactiveStyle.normal.textColor = new Color(0.80f, 0.90f, 0.95f);

            float slotY = y + 26f;
            string[] toolShortNames = { "1: Water Kit", "2: Coral Kit", "3: Waste Bag" };
            string[] toolIds = { "07_watersamplekit", "05_coralreplantingkit", "04_wastesegregationbag" };
            float totalSlotWidth = badgeWidth - 24f;
            float slotWidth = (totalSlotWidth - 8f) / 3f;

            for (int i = 0; i < 3; i++)
            {
                bool isThisEquipped = (equippedId == toolIds[i]);
                Rect slotRect = new Rect(x + 12f + i * (slotWidth + 4f), slotY, slotWidth, 22f);

                if (isThisEquipped)
                {
                    Color origBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.12f, 0.60f, 0.40f, 0.95f);
                    GUI.Box(slotRect, $"★ {toolShortNames[i]}", slotActiveStyle);
                    GUI.backgroundColor = origBg;
                }
                else
                {
                    Color origBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.08f, 0.22f, 0.36f, 0.85f);
                    if (GUI.Button(slotRect, toolShortNames[i], slotInactiveStyle))
                    {
                        if (ToolEquipSystem.Instance != null)
                        {
                            ToolEquipSystem.Instance.ToggleEquip(toolIds[i]);
                            PlaySound(_equipSound);
                        }
                    }
                    GUI.backgroundColor = origBg;
                }
            }

            // Subtitle
            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.60f, 0.75f, 0.85f);
            GUI.Label(new Rect(x + 10f, y + 52f, badgeWidth - 20f, 16f), "Press [1]-[3] to Quick-Equip • Press [I] for Full Menu", subStyle);
        }

        private void DrawOutline(Rect rect, Color color, int thickness)
        {
            Color prevColor = GUI.color;
            GUI.color = color;

            // Top
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            // Bottom
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            // Left
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            // Right
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);

            GUI.color = prevColor;
        }
    }
}
