using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Lalim.UI;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Builds the dialogue UI under a dialogue canvas:
    ///  - DialoguePanel: the long box (NPC / Player sprite), name in the shaded strip
    ///    (top-left for NPCs, top-right for the player), text starting from the left;
    ///  - ReviewOverlay: full-screen dark log (NPC lines left, player lines right);
    ///  - DialogueHUD: Review, Auto, Speed (number in the circle), Pause — top-left, in that order.
    /// Replaces the old two-layout panel; leaves an already-built new panel alone.
    /// </summary>
    public static class DialogueUIBuilder
    {
        private const string BoxNpcPath = "Assets/Lalim/Sprites/UI/Dialogue Boxes/Long Dialogue Box - NPC.png";
        private const string BoxPlayerPath = "Assets/Lalim/Sprites/UI/Dialogue Boxes/Long Dialogue Box - Player.png";
        private const string ReviewPath = "Assets/Lalim/Sprites/UI/Dialogue Boxes/review.png";
        private const string AutoPath = "Assets/Lalim/Sprites/UI/Dialogue Boxes/auto.png";
        private const string SpeedPath = "Assets/Lalim/Sprites/UI/Dialogue Boxes/speed.png";
        private const string PausePath = "Assets/Lalim/Sprites/UI/Icons/icon_pause.png";
        private const string PlayPath = "Assets/Lalim/Sprites/UI/Dialogue Boxes/STORYBOARD WITH UI-15.png";
        private const string ReviewTitle = "Story Review";

        private const float BoxAspect = 3236f / 500f;
        private static readonly Color BodyColor = new Color(0.07f, 0.15f, 0.25f);

        /// <summary>Returns true if anything was built.</summary>
        public static bool Build(Transform canvas, DialogueManager manager)
        {
            if (canvas.Find("DialogueHUD") != null && canvas.Find("DialoguePanel/Box") != null) return Upgrade(canvas);

            foreach (string path in new[] { BoxNpcPath, BoxPlayerPath, ReviewPath, AutoPath, SpeedPath, PausePath }) EnsureSprite(path);

            // Old panel (NPCLayout / PlayerLayout with legacy Text) is replaced.
            Transform old = canvas.Find("DialoguePanel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // ── Dialogue box ──
            var panel = New("DialoguePanel", canvas);
            Stretch(panel);

            var boxGo = New("Box", panel.transform, typeof(Image), typeof(AspectRatioFitter));
            var box = (RectTransform)boxGo.transform;
            box.anchorMin = new Vector2(0.06f, 0f);
            box.anchorMax = new Vector2(0.94f, 0f);
            box.pivot = new Vector2(0.5f, 0f);
            box.anchoredPosition = new Vector2(0f, 24f);
            box.sizeDelta = new Vector2(0f, 260f);
            var fitter = boxGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = BoxAspect;
            var boxImage = boxGo.GetComponent<Image>();
            boxImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoxNpcPath);
            boxImage.color = Color.white;

            var nameBox = New("NameBox", boxGo.transform);
            var nameRt = (RectTransform)nameBox.transform;
            nameRt.anchorMin = new Vector2(0.142f, 0.77f);
            nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            TMP_Text nameText = Text("Name", nameBox.transform, "Amansinaya", 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            var ntr = (RectTransform)nameText.transform;
            Stretch(ntr.gameObject);
            ntr.offsetMin = new Vector2(28f, 0f);
            ntr.offsetMax = new Vector2(-28f, 0f);

            TMP_Text body = Text("Body", boxGo.transform, "", 34f, FontStyles.Normal, BodyColor, TextAlignmentOptions.TopLeft);
            var brt = (RectTransform)body.transform;
            brt.anchorMin = new Vector2(0.04f, 0.1f);
            brt.anchorMax = new Vector2(0.96f, 0.6f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            body.enableAutoSizing = true;
            body.fontSizeMin = 24f;
            body.fontSizeMax = 34f;

            GameObject badge = null;   // no "PAUSED" label: the pause button turns into play instead

            // ── Review overlay (covers everything except the button row) ──
            var overlay = New("ReviewOverlay", canvas, typeof(Image), typeof(CanvasGroup));
            Stretch(overlay);
            overlay.GetComponent<Image>().color = new Color(0.0f, 0.02f, 0.05f, 0.88f);

            TMP_Text title = Text("Title", overlay.transform, "Review", 44f, FontStyles.Bold, new Color(0.85f, 1f, 0.97f), TextAlignmentOptions.Center);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0.3f, 1f);
            trt.anchorMax = new Vector2(0.7f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -40f);
            trt.sizeDelta = new Vector2(0f, 70f);

            var closeGo = New("CloseButton", overlay.transform, typeof(Image), typeof(Button));
            var crt = (RectTransform)closeGo.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-80f, -75f);
            crt.sizeDelta = new Vector2(96f, 96f);
            closeGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            TMP_Text x = Text("X", closeGo.transform, "×", 64f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            Stretch(x.gameObject);

            var scrollGo = New("Scroll", overlay.transform, typeof(ScrollRect));
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = new Vector2(0.1f, 0.08f);
            srt.anchorMax = new Vector2(0.9f, 0.84f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            var viewport = New("Viewport", scrollGo.transform, typeof(RectMask2D), typeof(Image));
            Stretch(viewport);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var content = New("Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 18f;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var review = overlay.AddComponent<ReviewOverlayUI>();
            var rso = new SerializedObject(review);
            rso.FindProperty("_group").objectReferenceValue = overlay.GetComponent<CanvasGroup>();
            rso.FindProperty("_scroll").objectReferenceValue = scroll;
            rso.FindProperty("_content").objectReferenceValue = contentRt;
            rso.FindProperty("_closeButton").objectReferenceValue = closeGo.GetComponent<Button>();
            rso.ApplyModifiedPropertiesWithoutUndo();

            // ── Button row, top-left: Review, Auto, Speed, Pause ──
            var hud = New("DialogueHUD", canvas, typeof(CanvasGroup));
            Stretch(hud);
            Button reviewBtn = IconButton(hud.transform, "Btn_Review", ReviewPath, 0, out Image reviewIcon);
            Button autoBtn = IconButton(hud.transform, "Btn_Auto", AutoPath, 1, out Image autoIcon);
            Button speedBtn = IconButton(hud.transform, "Btn_Speed", SpeedPath, 2, out Image _);
            Button pauseBtn = IconButton(hud.transform, "Btn_Pause", PausePath, 3, out Image pauseIcon);

            // The speed number sits in the centre of the speed icon's circle.
            TMP_Text speedLabel = Text("SpeedValue", speedBtn.transform, "1", 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            var slr = (RectTransform)speedLabel.transform;
            slr.anchorMin = slr.anchorMax = new Vector2(0.53f, 0.53f);
            slr.sizeDelta = new Vector2(80f, 50f);
            speedLabel.enableAutoSizing = true;
            speedLabel.fontSizeMin = 18f;
            speedLabel.fontSizeMax = 34f;

            var dh = hud.AddComponent<DialogueHUD>();
            var hso = new SerializedObject(dh);
            hso.FindProperty("_group").objectReferenceValue = hud.GetComponent<CanvasGroup>();
            hso.FindProperty("_reviewButton").objectReferenceValue = reviewBtn;
            hso.FindProperty("_autoButton").objectReferenceValue = autoBtn;
            hso.FindProperty("_speedButton").objectReferenceValue = speedBtn;
            hso.FindProperty("_pauseButton").objectReferenceValue = pauseBtn;
            hso.FindProperty("_reviewIcon").objectReferenceValue = reviewIcon;
            hso.FindProperty("_autoIcon").objectReferenceValue = autoIcon;
            hso.FindProperty("_pauseIcon").objectReferenceValue = pauseIcon;
            hso.FindProperty("_speedLabel").objectReferenceValue = speedLabel;
            hso.FindProperty("_review").objectReferenceValue = review;
            hso.ApplyModifiedPropertiesWithoutUndo();

            manager.EditorSetup(panel, boxImage,
                AssetDatabase.LoadAssetAtPath<Sprite>(BoxNpcPath), AssetDatabase.LoadAssetAtPath<Sprite>(BoxPlayerPath),
                nameRt, nameText, body, badge);
            EditorUtility.SetDirty(manager);

            panel.SetActive(false);
            Upgrade(canvas);
            return true;
        }

        /// <summary>
        /// Brings an already-built dialogue UI up to date (idempotent):
        /// review overlay above the button row, plain "×" close, "Story Review" title,
        /// pause button that toggles to the play icon. Returns true if anything changed.
        /// </summary>
        public static bool Upgrade(Transform canvas)
        {
            bool changed = false;
            EnsureSprite(PlayPath);
            changed |= EnsurePortrait(canvas);

            // The old "PAUSED" label is gone (the pause button swaps to play instead).
            Transform oldBadge = canvas.Find("DialoguePanel/Box/PausedBadge");
            if (oldBadge != null) { Object.DestroyImmediate(oldBadge.gameObject); changed = true; }

            Transform overlay = canvas.Find("ReviewOverlay");
            Transform hud = canvas.Find("DialogueHUD");
            if (overlay != null && hud != null && overlay.GetSiblingIndex() < hud.GetSiblingIndex())
            {
                overlay.SetSiblingIndex(hud.GetSiblingIndex()); // overlay now draws over the buttons
                changed = true;
            }

            if (overlay != null)
            {
                var title = overlay.Find("Title")?.GetComponent<TMP_Text>();
                if (title != null && title.text != ReviewTitle) { title.text = ReviewTitle; changed = true; }

                var closeBg = overlay.Find("CloseButton")?.GetComponent<Image>();
                if (closeBg != null && closeBg.color.a > 0f)
                {
                    closeBg.color = new Color(1f, 1f, 1f, 0f); // invisible, still tappable
                    changed = true;
                }
            }

            var dh = hud != null ? hud.GetComponent<DialogueHUD>() : null;
            if (dh != null)
            {
                var so = new SerializedObject(dh);
                if (so.FindProperty("_playSprite").objectReferenceValue == null)
                {
                    dh.EditorSetPauseSprites(AssetDatabase.LoadAssetAtPath<Sprite>(PausePath), AssetDatabase.LoadAssetAtPath<Sprite>(PlayPath));
                    changed = true;
                }
                // Icons keep their own colour now (no active tint).
                foreach (string name in new[] { "Btn_Review", "Btn_Auto", "Btn_Speed", "Btn_Pause" })
                {
                    var img = hud.Find(name)?.GetComponent<Image>();
                    if (img != null && img.color != Color.white) { img.color = Color.white; changed = true; }
                }
                EditorUtility.SetDirty(dh);
            }
            return changed;
        }

        private static readonly string[,] Portraits =
        {
            { "Amansinaya", "Assets/Lalim/Sprites/Characters/amansinaya.png" },
            { "Haik", "Assets/Lalim/Sprites/Characters/haik.png" },
            { "Neguno", "Assets/Lalim/Sprites/Characters/neguno.png" },
        };

        /// <summary>
        /// Adds the NPC bust at the box's bottom-left (Box/Portrait) and the speaker → sprite list,
        /// only where missing, so hand-tuned prefabs keep their edits.
        /// </summary>
        public static bool EnsurePortrait(Transform canvas)
        {
            DialogueManager manager = canvas.GetComponentInChildren<DialogueManager>(true);
            if (manager == null) manager = canvas.GetComponentInParent<DialogueManager>();
            Transform box = canvas.Find("DialoguePanel/Box");
            if (manager == null || box == null) return false;
            bool changed = false;

            Transform portraitT = box.Find("Portrait");
            if (portraitT == null)
            {
                var go = New("Portrait", box, typeof(Image), typeof(AspectRatioFitter));
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1.2f);   // the bust rises above the name strip
                rt.pivot = new Vector2(0f, 0f);
                rt.offsetMin = new Vector2(-8f, 0f);
                rt.offsetMax = new Vector2(0f, 0f);
                var fitter = go.GetComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fitter.aspectRatio = 0.78f;   // the busts are ~744×948
                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                Transform badge = box.Find("PausedBadge");
                if (badge != null) go.transform.SetSiblingIndex(badge.GetSiblingIndex());
                go.SetActive(false);
                portraitT = go.transform;
                changed = true;
            }

            var so = new SerializedObject(manager);
            SerializedProperty image = so.FindProperty("_npcPortraitImage");
            SerializedProperty list = so.FindProperty("_speakerPortraits");
            if (image.objectReferenceValue == null) { image.objectReferenceValue = portraitT.GetComponent<Image>(); changed = true; }
            if (list.arraySize == 0)
            {
                for (int i = 0; i < Portraits.GetLength(0); i++)
                {
                    EnsureSprite(Portraits[i, 1]);
                    list.InsertArrayElementAtIndex(i);
                    SerializedProperty e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("speaker").stringValue = Portraits[i, 0];
                    e.FindPropertyRelative("sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Portraits[i, 1]);
                }
                changed = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static Button IconButton(Transform parent, string name, string spritePath, int index, out Image icon)
        {
            var go = New(name, parent, typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(80f + index * 130f, -75f);
            rt.sizeDelta = new Vector2(100f, 100f);
            icon = go.GetComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            icon.preserveAspect = true;
            icon.color = new Color(1f, 1f, 1f, 0.85f);
            return go.GetComponent<Button>();
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = New(name, parent, typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static GameObject New(string name, Transform parent, params System.Type[] components)
        {
            var types = new System.Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            components.CopyTo(types, 1);
            var go = new GameObject(name, types);
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>The new art may be imported as Default textures; UI needs Sprite import.</summary>
        private static void EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
