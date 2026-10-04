using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Editor
{
    /// <summary>
    /// Builds SettingsModal.prefab to match the settings mock-ups (Audio / Quality / Controls /
    /// System tabs, slide switches, C·B·A choices) on the "Rectangle Modal" art, and installs it in:
    ///  - GameHUD ▸ PauseModal (only the settings object is replaced; your pause-modal edits stay),
    ///    plus HudHandedness on the HUD root for Right Handed Mode, and
    ///  - 02_MainMenu on its own overlay canvas, wired to MainMenuUI's Settings button.
    ///
    /// Re-running rebuilds the prefab from scratch, so tweak layout in the prefab only once
    /// you're done re-running this.
    ///
    /// Tools/Lalim/Abilities/7. Build Settings Modal
    /// </summary>
    public static class SettingsModalBuilder
    {
        public const string PrefabPath = "Assets/Lalim/Prefabs/UI/SettingsModal.prefab";
        private const string HudPrefabPath = "Assets/Lalim/Prefabs/UI/GameHUD.prefab";
        private const string MainMenuScenePath = "Assets/Lalim/Scenes/02_MainMenu/02_MainMenu.unity";

        private const string ModalSpritePath = "Assets/Lalim/Sprites/UI/Buttons/Rectangle Modal.png";
        private const string ConfirmSpritePath = "Assets/Lalim/Sprites/UI/Buttons/Square Modal.png";
        private const string OnSpritePath = "Assets/Lalim/Sprites/UI/Buttons/rounded-btn.png";
        private const string OffSpritePath = "Assets/Lalim/Sprites/UI/Buttons/rounded-btn-secondary.png";
        private const string WarningSpritePath = "Assets/Lalim/Sprites/UI/Buttons/rounded-btn-warning.png";
        private const string GeneratedFolder = "Assets/Lalim/Sprites/UI/Settings";
        private const string CapsulePath = GeneratedFolder + "/ui_capsule.png";
        private const string PearlArtPath = GeneratedFolder + "/pearl.png";     // your pearl art (switch knobs, slider handles)
        private const string PearlPath = GeneratedFolder + "/ui_pearl.png";     // generated fallback if pearl.png is missing
        private const string TitleFontPath = "Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF.asset";
        private const string BodyFontPath = "Assets/Lalim/Fonts/PlusJakartaSans-SemiBold SDF.asset";
        private const string TitleMaterialPath = "Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF Outline Title.mat";
        private const string BodyMaterialPath = "Assets/Lalim/Fonts/PlusJakartaSans-SemiBold SDF Outline Body.mat";
        private const string UserAgreementPath = "Assets/Lalim/Data/Legal/UserAgreement.txt";
        private const string PrivacyPolicyPath = "Assets/Lalim/Data/Legal/PrivacyPolicy.txt";

        private static readonly Color Foam = new Color(0.86f, 0.97f, 0.98f, 1f);
        private static readonly Color BarBack = new Color(0.1f, 0.27f, 0.32f, 0.85f);
        private static readonly Color BarFill = new Color(0.66f, 0.92f, 0.92f, 1f);
        private static readonly Color PillLine = new Color(0.3f, 0.6f, 0.64f, 1f);

        // Panel geometry (1920×1080 reference units).
        private const float PanelW = 1280f, PanelH = 861f;

        // Page content columns (content is 860 wide, x = -430…430).
        private const float HeaderX = -430f;
        private const float LabelX = -400f;
        private const float ColC = 150f, ColB = 265f, ColA = 380f;  // C · B · A / switches
        private const float ValueX = -60f, SliderX = 150f, SliderW = 290f;
        private const float RowH = 62f, NoteH = 26f, HeaderH = 58f;

        private static Sprite _modal, _confirmModal, _on, _off, _warning, _capsule, _pearl;
        private static TMP_FontAsset _titleFont, _bodyFont;
        private static Material _titleMat, _bodyMat;

        [MenuItem("Tools/Lalim/Abilities/7. Build Settings Modal", false, 107)]
        public static void BuildAndInstall()
        {
            GameObject prefab = BuildPrefab();
            if (prefab == null) return;
            InstallInHud(prefab);
            InstallInMainMenu(prefab);
        }

        // ─────────────────────────────────────────────
        // Prefab
        // ─────────────────────────────────────────────

        public static GameObject BuildPrefab()
        {
            if (!LoadAssets()) return null;

            var root = new GameObject("SettingsModal", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            try
            {
                Stretch(root.transform);
                var canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 120;

                Image dim = NewImage(root.transform, "Dim", null, new Color(0f, 0.03f, 0.08f, 0.45f));
                Stretch(dim.transform);
                dim.raycastTarget = true; // swallow touches behind the modal

                var ui = root.AddComponent<SettingsModalUI>();
                var so = new SerializedObject(ui);

                // Panel ────────────────────────────────
                Image panelImg = NewImage(root.transform, "Panel", _modal, Color.white);
                RectTransform panel = panelImg.rectTransform;
                Center(panel, Vector2.zero, new Vector2(PanelW, PanelH));
                panelImg.raycastTarget = true;
                panel.gameObject.AddComponent<FitInsideParent>();

                TMP_Text title = Text(panel, "Title", "Settings", _titleFont, _titleMat, 62f, new Vector2(0f, 345f), new Vector2(700f, 90f));
                title.characterSpacing = 4f;

                // Tabs + pages ─────────────────────────
                string[] tabNames = { "Audio", "Quality", "Controls", "System" };
                var tabs = so.FindProperty("_tabButtons");
                var pages = so.FindProperty("_pages");
                var scrolls = so.FindProperty("_pageScrolls");
                tabs.arraySize = pages.arraySize = scrolls.arraySize = 4;
                var content = new RectTransform[4];
                for (int i = 0; i < 4; i++)
                {
                    Button tab = Pill(panel, "Tab_" + tabNames[i], tabNames[i], new Vector2(-455f, 200f - i * 105f), new Vector2(250f, 86f), 34f, out _);
                    tabs.GetArrayElementAtIndex(i).objectReferenceValue = tab;

                    ScrollRect scroll = ScrollPage(panel, "Page_" + tabNames[i], new Vector2(140f, 20f), new Vector2(900f, 560f), out content[i]);
                    pages.GetArrayElementAtIndex(i).objectReferenceValue = scroll.gameObject;
                    scrolls.GetArrayElementAtIndex(i).objectReferenceValue = scroll;
                }

                BuildAudioPage(content[0], so);
                BuildQualityPage(content[1], so);
                BuildControlsPage(content[2], so);
                BuildSystemPage(content[3], so);

                // Footer ──────────────────────────────
                TMP_Text status = Text(panel, "Status", "", _bodyFont, null, 24f, new Vector2(140f, -290f), new Vector2(860f, 40f));
                status.color = Foam;
                Button save = Pill(panel, "Btn_Save", "Save", new Vector2(-160f, -362f), new Vector2(280f, 88f), 38f, out _);
                Button close = Pill(panel, "Btn_Close", "Close", new Vector2(160f, -362f), new Vector2(280f, 88f), 38f, out _);
                ((Image)close.targetGraphic).sprite = _warning;
                so.FindProperty("_statusText").objectReferenceValue = status;
                so.FindProperty("_saveButton").objectReferenceValue = save;
                so.FindProperty("_closeButton").objectReferenceValue = close;

                // Dialogs ─────────────────────────────
                BuildInfo(root.transform, so);
                BuildConfirm(root.transform, so);

                so.FindProperty("_onSprite").objectReferenceValue = _on;
                so.FindProperty("_offSprite").objectReferenceValue = _off;
                so.FindProperty("_warningSprite").objectReferenceValue = _warning;
                so.FindProperty("_choiceSelected").colorValue = PillLine;
                so.FindProperty("_userAgreementText").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(UserAgreementPath);
                so.FindProperty("_privacyPolicyText").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(PrivacyPolicyPath);
                so.ApplyModifiedPropertiesWithoutUndo();

                for (int i = 1; i < 4; i++) ((RectTransform)content[i].parent.parent).gameObject.SetActive(false);
                root.SetActive(false);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SettingsModalBuilder] Built {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ── Audio ──────────────────────────────────

        private static void BuildAudioPage(RectTransform c, SerializedObject so)
        {
            float y = 0f;
            Header(c, "Volume", ref y);

            string[] names = { "Music", "Voice", "SFX", "Amb." };
            AudioChannel[] channels = { AudioChannel.Music, AudioChannel.Voice, AudioChannel.Sfx, AudioChannel.Ambient };
            var rows = so.FindProperty("_volumes");
            rows.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                float cy = Row(c, names[i], null, ref y);
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("channel").enumValueIndex = (int)channels[i];
                row.FindPropertyRelative("value").objectReferenceValue = ValueText(c, names[i], cy);
                row.FindPropertyRelative("slider").objectReferenceValue = PearlSlider(c, names[i] + "Slider", cy);
                row.FindPropertyRelative("enabled").objectReferenceValue = Switch(c, names[i] + "Switch", cy);
            }

            y += 14f;
            Header(c, "Others", ref y);
            SwitchRow(c, so, "_spatialAudio", "3D Spatial Audio", "*Once enabled, sounds come from where they happen in the world", ref y);
            SwitchRow(c, so, "_headphones", "Headphone Optimization", "*Sharper left / right placement; turn off for phone speakers", ref y);
            Finish(c, y);
        }

        // ── Quality ────────────────────────────────

        private static void BuildQualityPage(RectTransform c, SerializedObject so)
        {
            float y = 0f;
            Header(c, "Graphics Quality", ref y);

            float[] presetX = { -335f, -190f, -60f, 60f, 215f };
            float[] presetW = { 140f, 140f, 110f, 110f, 180f };
            var presets = so.FindProperty("_presetButtons");
            presets.arraySize = GameSettings.GraphicsPresetNames.Length;
            float py = y + RowH * 0.5f;
            for (int i = 0; i < presets.arraySize; i++)
                presets.GetArrayElementAtIndex(i).objectReferenceValue =
                    ChoicePill(c, "Preset_" + GameSettings.GraphicsPresetNames[i], GameSettings.GraphicsPresetNames[i], presetX[i], py, presetW[i]);
            y += RowH + 6f;

            string[] rowNames = { "Texture Quality", "Mapping Quality", "Shadow Quality", "Ambient Effect", "SFX Quality", "Resolution" };
            string[] notes =
            {
                null,
                "*Sharper surfaces at an angle and more model detail",
                null,
                "*Glow, colour, caustics and god rays",
                "*How many sound effects can play at once",
                null,
            };
            var tierRows = so.FindProperty("_tierRows");
            tierRows.arraySize = rowNames.Length;
            float[] cols = { ColC, ColB, ColA };
            for (int r = 0; r < rowNames.Length; r++)
            {
                float cy = Row(c, rowNames[r], notes[r], ref y);
                SerializedProperty row = tierRows.GetArrayElementAtIndex(r);
                row.FindPropertyRelative("row").enumValueIndex = r;
                SerializedProperty tiers = row.FindPropertyRelative("tiers");
                tiers.arraySize = 3;
                for (int t = 0; t < 3; t++)
                    tiers.GetArrayElementAtIndex(t).objectReferenceValue =
                        ChoicePill(c, $"{rowNames[r].Replace(" ", "")}_{GameSettings.TierNames[t]}", GameSettings.TierNames[t], cols[t], cy, 96f);
            }

            float sy = Row(c, "SSS Quality", "*Subsurface scattering — for future character shaders", ref y);
            var sss = so.FindProperty("_sssButtons");
            sss.arraySize = 2;
            sss.GetArrayElementAtIndex(0).objectReferenceValue = ChoicePill(c, "SSS_Off", "OFF", ColC, sy, 96f);
            sss.GetArrayElementAtIndex(1).objectReferenceValue = ChoicePill(c, "SSS_On", "ON", ColB, sy, 96f);

            float fy = Row(c, "Frame Rate", "*30 saves battery and keeps the phone cooler", ref y);
            var fps = so.FindProperty("_frameRateButtons");
            fps.arraySize = 2;
            fps.GetArrayElementAtIndex(0).objectReferenceValue = ChoicePill(c, "Fps_30", "30", ColB, fy, 96f);
            fps.GetArrayElementAtIndex(1).objectReferenceValue = ChoicePill(c, "Fps_60", "60", ColA, fy, 96f);
            Finish(c, y);
        }

        // ── Controls ───────────────────────────────

        private static void BuildControlsPage(RectTransform c, SerializedObject so)
        {
            float y = 0f;
            Header(c, "Control", ref y);
            SwitchRow(c, so, "_gyroscope", "Gyroscope", "*Once enabled, tilting your device turns the camera", ref y);
            SwitchRow(c, so, "_rightHanded", "Right Handed Mode", "*Turn off to swap the joystick and skills for left hands", ref y);
            SwitchRow(c, so, "_autoSwim", "Auto-Swim/Auto-Forward", "*Keep swimming forward while the joystick is idle", ref y);
            SwitchRow(c, so, "_haptics", "Haptic Feedback", null, ref y);

            y += 14f;
            Header(c, "Camera", ref y);
            float cy = Row(c, "Camera Speed", null, ref y);
            so.FindProperty("_sensitivityValue").objectReferenceValue = ValueText(c, "CameraSpeed", cy);
            so.FindProperty("_sensitivity").objectReferenceValue = PearlSlider(c, "CameraSpeedSlider", cy);
            SwitchRow(c, so, "_invertY", "Invert Camera", "*Swipe up to look down", ref y);
            Finish(c, y);
        }

        // ── System ─────────────────────────────────

        private static void BuildSystemPage(RectTransform c, SerializedObject so)
        {
            float y = 0f;
            Header(c, "About", ref y);

            float ua = Row(c, "User Agreement", null, ref y);
            so.FindProperty("_userAgreementButton").objectReferenceValue = OutlinePill(c, "Btn_UserAgreement", "VIEW", ColA, ua, 110f, 26f);
            float pp = Row(c, "Privacy Policy", null, ref y);
            so.FindProperty("_privacyPolicyButton").objectReferenceValue = OutlinePill(c, "Btn_PrivacyPolicy", "VIEW", ColA, pp, 110f, 26f);
            float av = Row(c, "App Version", null, ref y);
            TMP_Text version = Text(c, "AppVersion", "v1.0.0", _bodyFont, _bodyMat, 28f, TopCenter(310f, av), new Vector2(240f, 50f));
            SetTopAnchor(version.rectTransform);
            version.alignment = TextAlignmentOptions.Right;
            so.FindProperty("_versionText").objectReferenceValue = version;

            y += 14f;
            Header(c, "Others", ref y);
            ActionRow(c, so, "_manageResourceButton", "Manage Resource", "Check storage and clear cached files", false, ref y);
            ActionRow(c, so, "_quickCleanupButton", "Quick Cleanup", "One-tap cleanup of cache and memory", false, ref y);
            ActionRow(c, so, "_performanceFixButton", "Performance Fix", "Improve gameplay stability", false, ref y);
            ActionRow(c, so, "_resetDefaultsButton", "Reset Settings", "Back to the default settings", false, ref y);
            TMP_Text eraseNote = ActionRow(c, so, "_eraseSavesButton", "Erase Save Data", "Delete every save slot for good", true, ref y);
            so.FindProperty("_eraseNote").objectReferenceValue = eraseNote;
            Finish(c, y);
        }

        // ── Dialogs ────────────────────────────────

        private static void BuildInfo(Transform root, SerializedObject so)
        {
            var layer = Layer(root, "InfoLayer");
            Image img = NewImage(layer.transform, "Panel", _modal, Color.white);
            RectTransform panel = img.rectTransform;
            Center(panel, Vector2.zero, new Vector2(1100f, 740f));
            img.raycastTarget = true;
            panel.gameObject.AddComponent<FitInsideParent>();

            TMP_Text title = Text(panel, "Title", "Title", _titleFont, _titleMat, 50f, new Vector2(0f, 290f), new Vector2(900f, 70f));

            // Scrolling body text.
            var scrollGo = new GameObject("Body", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(panel, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            Center(scrollRt, new Vector2(0f, 10f), new Vector2(920f, 450f));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollRt, false);
            Stretch(viewport.transform);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // catches drags
            TMP_Text body = Text(viewport.transform, "Text", "Body", _bodyFont, null, 26f, Vector2.zero, new Vector2(880f, 100f));
            RectTransform bodyRt = body.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.offsetMin = new Vector2(20f, 0f);
            bodyRt.offsetMax = new Vector2(-30f, 0f);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.richText = true;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = bodyRt;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = Scrollbar(scrollRt);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            Button action = Pill(panel, "Btn_Action", "Action", new Vector2(-170f, -300f), new Vector2(280f, 84f), 34f, out TMP_Text actionLabel);
            Button close = Pill(panel, "Btn_Close", "Close", new Vector2(170f, -300f), new Vector2(280f, 84f), 34f, out _);
            ((Image)close.targetGraphic).sprite = _off;

            so.FindProperty("_infoPanel").objectReferenceValue = layer;
            so.FindProperty("_infoTitle").objectReferenceValue = title;
            so.FindProperty("_infoBody").objectReferenceValue = body;
            so.FindProperty("_infoScroll").objectReferenceValue = scroll;
            so.FindProperty("_infoActionButton").objectReferenceValue = action;
            so.FindProperty("_infoActionLabel").objectReferenceValue = actionLabel;
            so.FindProperty("_infoCloseButton").objectReferenceValue = close;
            layer.SetActive(false);
        }

        private static void BuildConfirm(Transform root, SerializedObject so)
        {
            var layer = Layer(root, "ConfirmLayer");
            Image img = NewImage(layer.transform, "Panel", _confirmModal, Color.white);
            RectTransform panel = img.rectTransform;
            Center(panel, Vector2.zero, new Vector2(680f, 680f));
            img.preserveAspect = true;
            img.raycastTarget = true;
            panel.gameObject.AddComponent<FitInsideParent>();

            TMP_Text title = Text(panel, "Title", "Title", _titleFont, _titleMat, 46f, new Vector2(0f, 205f), new Vector2(560f, 70f));
            TMP_Text message = Text(panel, "Message", "Message", _bodyFont, _bodyMat, 28f, new Vector2(0f, 85f), new Vector2(560f, 150f));
            var buttons = so.FindProperty("_confirmButtons");
            var labels = so.FindProperty("_confirmLabels");
            buttons.arraySize = labels.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                Button b = Pill(panel, $"Btn_Choice{i + 1}", "Choice", new Vector2(0f, -55f - i * 95f), new Vector2(420f, 80f), 30f, out TMP_Text label);
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = b;
                labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
            }

            so.FindProperty("_confirmPanel").objectReferenceValue = layer;
            so.FindProperty("_confirmTitle").objectReferenceValue = title;
            so.FindProperty("_confirmMessage").objectReferenceValue = message;
            layer.SetActive(false);
        }

        private static GameObject Layer(Transform root, string name)
        {
            var layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(root, false);
            Stretch(layer.transform);
            Image dim = NewImage(layer.transform, "Dim", null, new Color(0f, 0.03f, 0.08f, 0.5f));
            Stretch(dim.transform);
            dim.raycastTarget = true;
            return layer;
        }

        // ─────────────────────────────────────────────
        // Install
        // ─────────────────────────────────────────────

        public static void InstallInHudOnly(GameObject prefab) => InstallInHud(prefab);

        private static void InstallInHud(GameObject prefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                Transform pauseModal = root.transform.Find("PauseModal");
                if (pauseModal == null) { Debug.LogWarning("[SettingsModalBuilder] GameHUD has no PauseModal — run '6. Build Pause Modal' first."); return; }

                foreach (string oldName in new[] { "SettingsPanel", "SettingsModal" })
                {
                    Transform old = pauseModal.Find(oldName);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                }

                SettingsModalUI modal = Place(prefab, pauseModal);
                var pause = pauseModal.GetComponent<PauseModalUI>();
                if (pause != null)
                {
                    var so = new SerializedObject(pause);
                    so.FindProperty("_settings").objectReferenceValue = modal;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // Right Handed Mode mirrors the HUD at runtime (layout in the prefab is untouched).
                if (root.GetComponent<HudHandedness>() == null) root.AddComponent<HudHandedness>();

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                Debug.Log("[SettingsModalBuilder] Installed in GameHUD ▸ PauseModal.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void InstallInMainMenu(GameObject prefab)
        {
            Scene scene = SceneManager.GetSceneByPath(MainMenuScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

            try
            {
                MainMenuUI menu = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    menu = go.GetComponentInChildren<MainMenuUI>(true);
                    if (menu != null) break;
                }
                if (menu == null) { Debug.LogWarning("[SettingsModalBuilder] No MainMenuUI in 02_MainMenu."); return; }

                // The menu canvas is Screen Space - Camera, so the 3D dugong / character would draw
                // over a modal placed in it. Give the modal its own overlay canvas (same scaler as the HUD).
                Canvas menuCanvas = menu.GetComponentInParent<Canvas>(true);
                if (menuCanvas != null)
                {
                    Transform stale = menuCanvas.rootCanvas.transform.Find("SettingsModal");
                    if (stale != null) Object.DestroyImmediate(stale.gameObject);
                }
                GameObject overlay = null;
                foreach (GameObject go in scene.GetRootGameObjects())
                    if (go.name == "SettingsCanvas") { overlay = go; break; }
                if (overlay != null) Object.DestroyImmediate(overlay);

                overlay = new GameObject("SettingsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(overlay, scene);
                var overlayCanvas = overlay.GetComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 200;
                var scaler = overlay.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                SettingsModalUI modal = Place(prefab, overlay.transform);

                var so = new SerializedObject(menu);
                so.FindProperty("_settingsModal").objectReferenceValue = modal;
                SerializedProperty button = so.FindProperty("_settingsButton");
                if (button.objectReferenceValue == null)
                {
                    Button found = FindButton(scene, "SettingsButton");
                    if (found != null) button.objectReferenceValue = found;
                    else Debug.LogWarning("[SettingsModalBuilder] Couldn't find a 'SettingsButton' in 02_MainMenu — assign MainMenuUI ▸ Settings Button by hand.");
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[SettingsModalBuilder] Installed in 02_MainMenu.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static SettingsModalUI Place(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "SettingsModal";
            Stretch(instance.transform);
            instance.transform.SetAsLastSibling();
            instance.SetActive(false);
            return instance.GetComponent<SettingsModalUI>();
        }

        private static Button FindButton(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                foreach (Button b in go.GetComponentsInChildren<Button>(true))
                    if (b.name == name) return b;
            return null;
        }

        // ─────────────────────────────────────────────
        // Rows (y grows downward from the top of a page's content)
        // ─────────────────────────────────────────────

        private static void Header(RectTransform c, string text, ref float y)
        {
            TMP_Text t = Text(c, "Header_" + text.Replace(" ", ""), text, _bodyFont, _bodyMat, 34f, Vector2.zero, new Vector2(860f, HeaderH));
            PlaceLeft(t.rectTransform, HeaderX, y + HeaderH * 0.5f);
            t.alignment = TextAlignmentOptions.Left;
            t.color = Foam;
            y += HeaderH;
        }

        /// <summary>Label (and optional small note under it). Returns the row's centre line for its controls.</summary>
        private static float Row(RectTransform c, string label, string note, ref float y)
        {
            float cy = y + RowH * 0.5f;
            TMP_Text t = Text(c, label.Replace(" ", "").Replace("/", "") + "Label", label, _bodyFont, _bodyMat, 31f, Vector2.zero, new Vector2(520f, RowH));
            PlaceLeft(t.rectTransform, LabelX, cy);
            t.alignment = TextAlignmentOptions.Left;
            y += RowH;

            if (!string.IsNullOrEmpty(note))
            {
                TMP_Text n = Text(c, "Note", note, _bodyFont, null, 19f, Vector2.zero, new Vector2(700f, NoteH));
                PlaceLeft(n.rectTransform, LabelX, y - 8f + NoteH * 0.5f);
                n.alignment = TextAlignmentOptions.Left;
                n.color = Foam;
                y += NoteH - 4f;
            }
            return cy;
        }

        private static void SwitchRow(RectTransform c, SerializedObject so, string field, string label, string note, ref float y)
        {
            float cy = Row(c, label, note, ref y);
            so.FindProperty(field).objectReferenceValue = Switch(c, label.Replace(" ", "").Replace("/", "") + "Switch", cy);
        }

        /// <summary>System ▸ Others style: outlined pill on the left, description to its right.</summary>
        private static TMP_Text ActionRow(RectTransform c, SerializedObject so, string field, string label, string description, bool warning, ref float y)
        {
            float cy = y + RowH * 0.5f;
            Button b = OutlinePill(c, "Btn_" + label.Replace(" ", ""), label, -272f, cy, 256f, 24f);
            if (warning)
            {
                ((Image)b.targetGraphic).color = new Color(0.62f, 0.12f, 0.1f, 1f);
                var colors = b.colors;
                colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
                b.colors = colors;
            }
            so.FindProperty(field).objectReferenceValue = b;

            TMP_Text d = Text(c, label.Replace(" ", "") + "Desc", description, _bodyFont, null, 22f, Vector2.zero, new Vector2(540f, RowH));
            PlaceLeft(d.rectTransform, -125f, cy);
            d.alignment = TextAlignmentOptions.Left;
            d.color = Foam;
            y += RowH;
            return d;
        }

        private static void Finish(RectTransform content, float y)
        {
            content.sizeDelta = new Vector2(content.sizeDelta.x, y + 24f);
        }

        // ─────────────────────────────────────────────
        // Controls
        // ─────────────────────────────────────────────

        private static TMP_Text ValueText(RectTransform c, string name, float cy)
        {
            TMP_Text t = Text(c, name + "Value", "5", _bodyFont, _bodyMat, 31f, TopCenter(ValueX, cy), new Vector2(70f, 50f));
            SetTopAnchor(t.rectTransform);
            return t;
        }

        /// <summary>Slide switch: capsule track + pearl knob.</summary>
        private static PillSwitch Switch(RectTransform c, string name, float cy)
        {
            Image track = NewImage(c, name, _capsule, PillLine);
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 64f / 40f;
            Center(track.rectTransform, TopCenter(ColA, cy), new Vector2(100f, 40f));
            SetTopAnchor(track.rectTransform);
            track.raycastTarget = true;
            var button = track.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = track;

            Image knob = NewImage(track.rectTransform, "Knob", _pearl, Color.white);
            knob.preserveAspect = true;
            Center(knob.rectTransform, new Vector2(30f, 0f), new Vector2(52f, 52f));

            var sw = track.gameObject.AddComponent<PillSwitch>();
            var so = new SerializedObject(sw);
            so.FindProperty("_track").objectReferenceValue = track;
            so.FindProperty("_knob").objectReferenceValue = knob.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return sw;
        }

        /// <summary>Choice text (preset, C/B/A, OFF/ON) with a capsule that shows only when selected.</summary>
        private static Button ChoicePill(RectTransform c, string name, string label, float x, float cy, float width)
        {
            Image bg = NewImage(c, name, _capsule, new Color(1f, 1f, 1f, 0f));
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 64f / 48f;
            Center(bg.rectTransform, TopCenter(x, cy), new Vector2(width, 48f));
            SetTopAnchor(bg.rectTransform);
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;
            TMP_Text t = Text(bg.rectTransform, "Label", label, _bodyFont, _bodyMat, 29f, Vector2.zero, new Vector2(width + 20f, 48f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        /// <summary>Outlined capsule button (VIEW, Manage Resource…).</summary>
        private static Button OutlinePill(RectTransform c, string name, string label, float x, float cy, float width, float fontSize)
        {
            Image bg = NewImage(c, name, _capsule, PillLine);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 64f / 48f;
            Center(bg.rectTransform, TopCenter(x, cy), new Vector2(width, 48f));
            SetTopAnchor(bg.rectTransform);
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.85f, 0.88f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            button.targetGraphic = bg;
            TMP_Text t = Text(bg.rectTransform, "Label", label, _bodyFont, _bodyMat, fontSize, Vector2.zero, new Vector2(width - 16f, 48f));
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = fontSize;
            return button;
        }

        /// <summary>Capsule bar with a pearl handle (volume, camera speed).</summary>
        private static Slider PearlSlider(RectTransform c, string name, float cy)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(c, false);
            var rt = (RectTransform)go.transform;
            Center(rt, TopCenter(SliderX, cy), new Vector2(SliderW, 22f));
            SetTopAnchor(rt);

            Image background = NewImage(rt, "Background", _capsule, BarBack);
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 64f / 22f;
            Stretch(background.transform);
            background.raycastTarget = true; // tap anywhere on the bar to jump

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(rt, false);
            Stretch(fillArea);
            Image fill = NewImage(fillArea, "Fill", _capsule, BarFill);
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 64f / 22f;
            Stretch(fill.transform);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(rt, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);
            Image handle = NewImage(handleArea, "Handle", _pearl, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(48f, 48f);
            handle.preserveAspect = true;
            handle.raycastTarget = true;

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 10f;
            slider.wholeNumbers = true;
            slider.value = 5f;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            return slider;
        }

        /// <summary>A page: ScrollRect → Viewport (mask) → Content (top-anchored), with a thin scrollbar.</summary>
        private static ScrollRect ScrollPage(RectTransform panel, string name, Vector2 pos, Vector2 size, out RectTransform content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            go.transform.SetParent(panel, false);
            var rt = (RectTransform)go.transform;
            Center(rt, pos, size);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(rt, false);
            Stretch(viewport.transform);
            ((RectTransform)viewport.transform).offsetMax = new Vector2(-24f, 0f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); // catches drags between rows

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(860f, size.y);

            var scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = Scrollbar(rt);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private static Scrollbar Scrollbar(RectTransform parent)
        {
            Image track = NewImage(parent, "Scrollbar", _capsule, new Color(1f, 1f, 1f, 0.22f));
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 64f / 10f;
            RectTransform rt = track.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(10f, -20f);
            rt.anchoredPosition = Vector2.zero;
            track.raycastTarget = true;

            var area = new GameObject("Sliding Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(rt, false);
            Stretch(area);
            Image handle = NewImage(area, "Handle", _capsule, new Color(0.95f, 1f, 1f, 0.85f));
            handle.type = Image.Type.Sliced;
            handle.pixelsPerUnitMultiplier = 64f / 10f;
            Stretch(handle.transform);
            handle.raycastTarget = true;

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            var nav = bar.navigation;
            nav.mode = Navigation.Mode.None;
            bar.navigation = nav;
            return bar;
        }

        private static Button Pill(Transform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize, out TMP_Text text)
        {
            Image img = NewImage(parent, name, _on, Color.white);
            Center(img.rectTransform, pos, size);
            img.raycastTarget = true;

            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.9f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;
            button.targetGraphic = img;

            text = Text(img.rectTransform, "Label", label, _titleFont, _titleMat, fontSize, Vector2.zero, size - new Vector2(24f, 8f));
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = fontSize;
            return button;
        }

        // ─────────────────────────────────────────────
        // Low-level helpers
        // ─────────────────────────────────────────────

        private static Vector2 TopCenter(float x, float cy) => new Vector2(x, -cy);

        /// <summary>Anchors an element to the top-centre of the page content (keeps its anchoredPosition).</summary>
        private static void SetTopAnchor(RectTransform rt)
        {
            Vector2 pos = rt.anchoredPosition;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
        }

        /// <summary>Places a box so its LEFT edge sits at x and its centre line at cy (content coordinates).</summary>
        private static void PlaceLeft(RectTransform rt, float leftX, float cy)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(leftX, -cy);
        }

        private static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, Material material, float size, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Center((RectTransform)go.transform, pos, box);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            if (material != null) t.fontSharedMaterial = material;
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

        private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Stretch(Transform t)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────────
        // Assets
        // ─────────────────────────────────────────────

        private static bool LoadAssets()
        {
            EnsureGeneratedSprites();

            _modal = AssetDatabase.LoadAssetAtPath<Sprite>(ModalSpritePath);
            _confirmModal = AssetDatabase.LoadAssetAtPath<Sprite>(ConfirmSpritePath);
            _on = AssetDatabase.LoadAssetAtPath<Sprite>(OnSpritePath);
            _off = AssetDatabase.LoadAssetAtPath<Sprite>(OffSpritePath);
            _warning = AssetDatabase.LoadAssetAtPath<Sprite>(WarningSpritePath);
            _capsule = AssetDatabase.LoadAssetAtPath<Sprite>(CapsulePath);
            _pearl = AssetDatabase.LoadAssetAtPath<Sprite>(PearlArtPath);
            if (_pearl == null) _pearl = AssetDatabase.LoadAssetAtPath<Sprite>(PearlPath);
            _titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            _bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            _titleMat = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
            _bodyMat = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);

            if (_modal == null || _on == null || _off == null || _capsule == null || _pearl == null)
            {
                Debug.LogError("[SettingsModalBuilder] Missing sprites (Rectangle Modal / rounded-btn / generated capsule & pearl).");
                return false;
            }
            _warning ??= _on;
            _confirmModal ??= _modal;
            return true;
        }

        /// <summary>
        /// Draws the two small sprites the mock-ups need and we don't have as art yet:
        /// a white 9-sliced capsule (tinted per use) and a pearl knob. Existing files are kept,
        /// so you can paint over them.
        /// </summary>
        private static void EnsureGeneratedSprites()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder("Assets/Lalim/Sprites/UI", "Settings");

            if (!File.Exists(CapsulePath))
            {
                File.WriteAllBytes(CapsulePath, DrawCapsule(128, 64).EncodeToPNG());
                AssetDatabase.ImportAsset(CapsulePath);
                ConfigureSprite(CapsulePath, new Vector4(31f, 31f, 31f, 31f));
            }
            if (!File.Exists(PearlPath))
            {
                File.WriteAllBytes(PearlPath, DrawPearl(96).EncodeToPNG());
                AssetDatabase.ImportAsset(PearlPath);
                ConfigureSprite(PearlPath, Vector4.zero);
            }
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        /// <summary>White capsule with a brighter rim; tinting the Image colours both.</summary>
        private static Texture2D DrawCapsule(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float r = h * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, r, w - r);
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, r)) - (r - 1f); // <0 inside
                float alpha = Mathf.Clamp01(0.5f - d);
                float rim = Mathf.Clamp01((d + 4f) / 1.5f);     // 1 near the edge
                float shade = Mathf.Lerp(0.72f, 1f, rim);
                shade = Mathf.Lerp(shade, 1f, Mathf.Clamp01((py - h * 0.72f) / (h * 0.28f)) * 0.25f); // soft top light
                tex.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
            tex.Apply();
            return tex;
        }

        /// <summary>Soft iridescent pearl, lit from the top-left.</summary>
        private static Texture2D DrawPearl(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f - 2f;
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            Vector3 light = new Vector3(-0.45f, 0.6f, 0.65f).normalized;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float dist = p.magnitude;
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                if (alpha <= 0f) { tex.SetPixel(x, y, Color.clear); continue; }

                Vector2 n2 = p / r;
                float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - n2.sqrMagnitude));
                var n = new Vector3(n2.x, n2.y, nz);
                float diffuse = Mathf.Clamp01(Vector3.Dot(n, light));
                float spec = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(n, (light + Vector3.forward).normalized)), 40f);

                // Pearly sheen: warm pink at the rim, cool blue-green lower right.
                Color baseCol = new Color(0.93f, 0.9f, 0.9f);
                Color rimCol = new Color(0.86f, 0.74f, 0.8f);
                Color coolCol = new Color(0.78f, 0.88f, 0.9f);
                float edge = Mathf.Pow(1f - nz, 1.6f);
                Color col = Color.Lerp(baseCol, rimCol, edge);
                col = Color.Lerp(col, coolCol, Mathf.Clamp01(n2.x - n2.y) * 0.35f);
                col *= Mathf.Lerp(0.72f, 1.05f, diffuse);
                col = Color.Lerp(col, Color.white, spec);
                col.a = alpha;
                tex.SetPixel(x, y, col);
            }
            tex.Apply();
            return tex;
        }
    }
}
