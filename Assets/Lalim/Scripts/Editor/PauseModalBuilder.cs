using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Lalim.UI;

namespace Lalim.Editor
{
    /// <summary>
    /// Builds the pause modal (Resume / Save / Exit + settings icon), the save-slot picker and
    /// the confirm dialog inside GameHUD, using the project's "Square Modal" background and
    /// "rounded-btn" buttons, then drops in the shared SettingsModal prefab.
    /// Re-running replaces the whole "PauseModal" object (including any hand edits to it);
    /// the rest of the HUD is untouched.
    ///
    /// Tools/Lalim/Abilities/6. Build Pause Modal
    /// </summary>
    public static class PauseModalBuilder
    {
        private const string HudPrefabPath = "Assets/Lalim/Prefabs/UI/GameHUD.prefab";
        private const string ModalSpritePath = "Assets/Lalim/Sprites/UI/Buttons/Square Modal.png";
        private const string ButtonSpritePath = "Assets/Lalim/Sprites/UI/Buttons/rounded-btn.png";
        private const string SettingsIconPath = "Assets/Lalim/Sprites/UI/Icons/icon_settings.png";
        private const string TitleFontPath = "Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF.asset";
        private const string BodyFontPath = "Assets/Lalim/Fonts/PlusJakartaSans-SemiBold SDF.asset";
        private const string OutlineMaterialFolder = "Assets/Lalim/Fonts";

        private static readonly Color Ink = new Color(0.02f, 0.2f, 0.3f, 1f);

        private static Sprite _modal, _button;
        private static TMP_FontAsset _titleFont, _bodyFont;
        private static Material _titleOutline, _bodyOutline;

        [MenuItem("Tools/Lalim/Abilities/6. Build Pause Modal", false, 106)]
        public static void Build()
        {
            _modal = AssetDatabase.LoadAssetAtPath<Sprite>(ModalSpritePath);
            _button = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            _bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            if (_modal == null || _button == null) { Debug.LogError("[PauseModalBuilder] Missing Square Modal / rounded-btn sprite."); return; }
            _titleOutline = OutlineMaterial(_titleFont, "Title");
            _bodyOutline = OutlineMaterial(_bodyFont, "Body");

            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                Transform old = root.transform.Find("PauseModal");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                // Own canvas + CanvasGroup that ignores the HUD's group (the HUD hides while paused).
                var modalGo = new GameObject("PauseModal", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
                modalGo.transform.SetParent(root.transform, false);
                Stretch(modalGo.transform);
                var canvas = modalGo.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 100;
                var group = modalGo.GetComponent<CanvasGroup>();
                group.ignoreParentGroups = true;
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;

                // Dim the game behind the modal (also swallows touches).
                var dim = NewImage(modalGo.transform, "Dim", null, new Color(0f, 0.03f, 0.08f, 0.6f));
                Stretch(dim.transform);
                dim.raycastTarget = true;

                var ui = modalGo.AddComponent<PauseModalUI>();
                var so = new SerializedObject(ui);
                so.FindProperty("_root").objectReferenceValue = group;
                Transform hudPause = root.transform.Find("Btn_Pause");
                if (hudPause != null) so.FindProperty("_hudPauseButton").objectReferenceValue = hudPause.GetComponent<Button>();

                // ── Pause panel ─────────────────────────────
                RectTransform pause = Panel(modalGo.transform, "PausePanel", 640f);
                Title(pause, "PAUSED", 150f);
                Button resume = PillButton(pause, "Btn_Resume", "Resume", new Vector2(0f, 45f), new Vector2(440f, 94f), out _);
                Button save = PillButton(pause, "Btn_Save", "Save", new Vector2(0f, -65f), new Vector2(440f, 94f), out _);
                Button menu = PillButton(pause, "Btn_MainMenu", "Exit", new Vector2(0f, -175f), new Vector2(440f, 94f), out _);
                Button settingsIcon = IconButton(pause, "Btn_Settings", AssetDatabase.LoadAssetAtPath<Sprite>(SettingsIconPath), new Vector2(-62f, -62f), 84f);
                TMP_Text status = Text(pause, "Status", "", _bodyFont, _bodyOutline, 26f, new Vector2(0f, -262f), new Vector2(560f, 44f));

                so.FindProperty("_pausePanel").objectReferenceValue = pause.gameObject;
                so.FindProperty("_resumeButton").objectReferenceValue = resume;
                so.FindProperty("_saveButton").objectReferenceValue = save;
                so.FindProperty("_mainMenuButton").objectReferenceValue = menu;
                so.FindProperty("_settingsButton").objectReferenceValue = settingsIcon;
                so.FindProperty("_statusText").objectReferenceValue = status;

                // ── Save slot picker ────────────────────────
                RectTransform slots = Panel(modalGo.transform, "SlotsPanel", 760f);
                Title(slots, "SAVE GAME", 200f);
                var slotButtons = so.FindProperty("_slotButtons");
                var slotLabels = so.FindProperty("_slotLabels");
                slotButtons.arraySize = 3;
                slotLabels.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    Button b = PillButton(slots, $"Btn_Slot{i + 1}", $"Slot {i + 1}\n<size=70%>Empty</size>", new Vector2(0f, 90f - i * 140f), new Vector2(620f, 128f), out TMP_Text label);
                    label.fontSize = 30f;
                    slotButtons.GetArrayElementAtIndex(i).objectReferenceValue = b;
                    slotLabels.GetArrayElementAtIndex(i).objectReferenceValue = label;
                }
                Button slotsBack = PillButton(slots, "Btn_Back", "Back", new Vector2(0f, -300f), new Vector2(300f, 78f), out _);
                so.FindProperty("_slotsPanel").objectReferenceValue = slots.gameObject;
                so.FindProperty("_slotsBackButton").objectReferenceValue = slotsBack;

                // ── Confirm dialog ──────────────────────────
                RectTransform confirm = Panel(modalGo.transform, "ConfirmPanel", 720f);
                TMP_Text confirmTitle = Title(confirm, "Title", 205f);
                TMP_Text confirmMessage = Text(confirm, "Message", "Message", _bodyFont, _bodyOutline, 30f, new Vector2(0f, 70f), new Vector2(600f, 170f));
                var confirmButtons = so.FindProperty("_confirmButtons");
                var confirmLabels = so.FindProperty("_confirmLabels");
                confirmButtons.arraySize = 3;
                confirmLabels.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    Button b = PillButton(confirm, $"Btn_Choice{i + 1}", "Choice", new Vector2(0f, -70f - i * 100f), new Vector2(460f, 86f), out TMP_Text label);
                    confirmButtons.GetArrayElementAtIndex(i).objectReferenceValue = b;
                    confirmLabels.GetArrayElementAtIndex(i).objectReferenceValue = label;
                }
                so.FindProperty("_confirmPanel").objectReferenceValue = confirm.gameObject;
                so.FindProperty("_confirmTitle").objectReferenceValue = confirmTitle;
                so.FindProperty("_confirmMessage").objectReferenceValue = confirmMessage;

                so.ApplyModifiedPropertiesWithoutUndo();

                pause.gameObject.SetActive(false);
                slots.gameObject.SetActive(false);
                confirm.gameObject.SetActive(false);
                modalGo.transform.SetAsLastSibling();

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                Debug.Log("[PauseModalBuilder] Pause, save-slot and confirm modals built in GameHUD.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // Settings live in their own prefab (shared with the main menu).
            GameObject settings = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsModalBuilder.PrefabPath);
            if (settings == null) SettingsModalBuilder.BuildAndInstall();
            else SettingsModalBuilder.InstallInHudOnly(settings);
        }

        // ─────────────────────────────────────────────
        // Pieces
        // ─────────────────────────────────────────────

        private static RectTransform Panel(Transform parent, string name, float size)
        {
            Image img = NewImage(parent, name, _modal, Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            img.raycastTarget = true; // clicks on the panel don't fall through to the dim
            return rt;
        }

        private static TMP_Text Title(RectTransform panel, string text, float y)
        {
            TMP_Text t = Text(panel, "Title", text, _titleFont, _titleOutline, 54f, new Vector2(0f, y), new Vector2(panel.sizeDelta.x - 160f, 80f));
            t.characterSpacing = 6f;
            return t;
        }

        private static Button PillButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, out TMP_Text text)
        {
            Image img = NewImage(parent, name, _button, Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            img.raycastTarget = true;

            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.9f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.targetGraphic = img;

            text = Text(rt, "Label", label, _titleFont, _titleOutline, 34f, Vector2.zero, size - new Vector2(40f, 10f));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        private static Button IconButton(RectTransform parent, string name, Sprite icon, Vector2 topRightOffset, float size)
        {
            Image img = NewImage(parent, name, icon, Color.white);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.anchoredPosition = topRightOffset;
            rt.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, Material outline, float size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (outline != null) t.fontSharedMaterial = outline;
            t.text = value;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>A dark outline so white text reads on the bright aqua modal art.</summary>
        private static Material OutlineMaterial(TMP_FontAsset font, string suffix)
        {
            if (font == null || font.material == null) return null;
            string path = $"{OutlineMaterialFolder}/{font.name} Outline {suffix}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, suffix == "Title" ? 0.22f : 0.18f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.05f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
