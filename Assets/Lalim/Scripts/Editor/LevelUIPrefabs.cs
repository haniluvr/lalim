using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lalim.UI;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Shared level UI lives in prefabs so one edit reaches every level:
    ///
    ///   PF_DialogueSystem     dialogue box, Story Review, dialogue buttons
    ///   PF_QuestObjectiveUI   quest key button + quest text box
    ///   PF_ToastSystem        achievement-title toast
    ///
    /// Scenes hold linked prefab instances of these, never their own copies.
    /// Tools ▸ Lalim ▸ UI ▸ Use UI Prefabs In Open Scene swaps a scene's hand-built copies for
    /// instances; Tools ▸ Lalim ▸ UI ▸ Rebuild Quest Box Prefab regenerates the quest box layout.
    /// </summary>
    public static class LevelUIPrefabs
    {
        public const string Folder = "Assets/Lalim/Prefabs/LevelFlow";
        public const string DialoguePrefab = Folder + "/PF_DialogueSystem.prefab";
        public const string QuestPrefab = Folder + "/PF_QuestObjectiveUI.prefab";
        public const string ToastPrefab = Folder + "/PF_ToastSystem.prefab";

        private const string QuestButtonSprite = "Assets/Lalim/Sprites/UI/Dialogue Boxes/quest-btn.png";
        private const string QuestBoxSprite = "Assets/Lalim/Sprites/UI/Dialogue Boxes/quest-textbox.png";
        // Padded SDF copies: the stock Plus Jakarta Sans assets have 0 atlas padding and render blurry.
        private const string TitleFont = "Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset";
        private const string BodyFont = "Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset";

        private static readonly Color Navy = new Color32(0x34, 0x46, 0x6E, 0xFF);
        private static readonly Color Blue = new Color32(0x3B, 0x7F, 0xD0, 0xFF);
        private static readonly Color Ink = new Color32(0x16, 0x1C, 0x2A, 0xFF);

        [MenuItem("Tools/Lalim/UI/Use UI Prefabs In Open Scene")]
        public static void UseInOpenSceneMenu() => Debug.Log(UseInOpenScene());

        [MenuItem("Tools/Lalim/UI/Rebuild Quest Box Prefab")]
        public static void RebuildQuestPrefabMenu() => Debug.Log(BuildQuestPrefab());

        /// <summary>
        /// Replaces the open scene's own dialogue / quest / toast UI with prefab instances.
        /// Scenes that already use the prefabs are left alone.
        /// </summary>
        public static string UseInOpenScene()
        {
            var report = new System.Text.StringBuilder();
            if (!System.IO.File.Exists(QuestPrefab) || !HasQuestLayout()) report.AppendLine(BuildQuestPrefab());

            EnsureInstance(DialoguePrefab, typeof(DialogueManager), new[] { "DialogueCanvas", "[DialogueManager]" }, report);
            EnsureInstance(QuestPrefab, typeof(QuestObjectiveUI), new[] { "QuestObjectiveCanvas", "[QuestObjectiveUI]" }, report);
            EnsureInstance(ToastPrefab, typeof(ActToastUI), new[] { "ActToastCanvas", "[ActToastUI]" }, report);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return report.Length > 0 ? report.ToString() : "Scene already uses the UI prefabs.";
        }

        /// <summary>
        /// Makes sure the scene has exactly one instance of <paramref name="prefabPath"/>. Any
        /// hand-built copy (an object with <paramref name="component"/> that isn't part of the
        /// prefab, plus its named companions) is removed first.
        /// </summary>
        public static void EnsureInstance(string prefabPath, System.Type component, string[] legacyRoots, System.Text.StringBuilder report)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { report.AppendLine($"Missing {prefabPath}."); return; }

            bool hasInstance = false;
            var legacy = new List<GameObject>();
            foreach (Object o in Object.FindObjectsByType(component, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var c = (Component)o;
                GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(c.gameObject);
                bool fromPrefab = source != null && AssetDatabase.GetAssetPath(source) == prefabPath;
                if (fromPrefab) hasInstance = true;
                else legacy.Add(c.transform.root.gameObject);
            }
            if (hasInstance && legacy.Count == 0) return;

            foreach (string name in legacyRoots)
            {
                foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.name == name && !PrefabUtility.IsPartOfPrefabInstance(root) && !legacy.Contains(root)) legacy.Add(root);
            }
            foreach (GameObject go in legacy)
            {
                report.AppendLine($"Removed scene copy {go.name}.");
                Undo.DestroyObjectImmediate(go);
            }

            if (!hasInstance)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Add " + prefab.name);
                report.AppendLine($"Added {prefab.name} (linked prefab).");
            }
        }

        // ───────────────────────── Quest box prefab ─────────────────────────

        private static bool HasQuestLayout()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestPrefab);
            return prefab != null && prefab.transform.Find("QuestWidget/QuestButton") != null;
        }

        /// <summary>Writes PF_QuestObjectiveUI with the key button + quest text box layout.</summary>
        public static string BuildQuestPrefab()
        {
            var root = new GameObject("PF_QuestObjectiveUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Box 560×210 (quest-textbox.png is 1135×425), button 120 overlapping its top-left corner.
            RectTransform widget = Rect("QuestWidget", root.transform);
            widget.anchorMin = widget.anchorMax = new Vector2(0f, 1f);
            widget.pivot = new Vector2(0f, 1f);
            widget.anchoredPosition = new Vector2(28f, -24f);
            widget.sizeDelta = new Vector2(600f, 225f);

            RectTransform box = Rect("QuestBox", widget);
            box.anchorMin = box.anchorMax = new Vector2(0f, 1f);
            box.pivot = new Vector2(0.07f, 0.8f);   // grows out from behind the key button
            box.sizeDelta = new Vector2(561f, 210f);
            box.anchoredPosition = new Vector2(39f + 561f * 0.07f, -15f - 210f * (1f - 0.8f));   // top-left at (39, -15)
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuestBoxSprite);
            boxImage.preserveAspect = true;
            boxImage.raycastTarget = false;
            box.gameObject.AddComponent<CanvasGroup>();

            TMP_Text title = Text("Title", box, "QUEST", TitleFont, 30f, Navy, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(92f, -40f), new Vector2(-170f, 40f));
            title.characterSpacing = 2f;

            TMP_Text counter = Text("Counter", box, "0/3", TitleFont, 30f, Blue, TextAlignmentOptions.TopRight);
            Place(counter.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-44f, -40f), new Vector2(140f, 40f));
            counter.rectTransform.pivot = new Vector2(1f, 1f);

            TMP_Text body = Text("Objective", box, "Identify the three secret locations of pollution in the reef.", BodyFont, 32f, Ink, TextAlignmentOptions.TopLeft);
            Place(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            body.rectTransform.offsetMin = new Vector2(92f, 34f);
            body.rectTransform.offsetMax = new Vector2(-60f, -80f);
            body.enableAutoSizing = true;
            body.fontSizeMin = 22f;
            body.fontSizeMax = 32f;
            body.lineSpacing = -6f;
            body.textWrappingMode = TextWrappingModes.Normal;

            RectTransform btn = Rect("QuestButton", widget);
            btn.anchorMin = btn.anchorMax = new Vector2(0f, 1f);
            btn.pivot = new Vector2(0f, 1f);
            btn.anchoredPosition = Vector2.zero;
            btn.sizeDelta = new Vector2(120f, 120f);
            var btnImage = btn.gameObject.AddComponent<Image>();
            btnImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(QuestButtonSprite);
            btnImage.preserveAspect = true;
            var button = btn.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            var ui = root.AddComponent<QuestObjectiveUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("_group").objectReferenceValue = root.GetComponent<CanvasGroup>();
            so.FindProperty("_objectiveText").objectReferenceValue = body;
            so.FindProperty("_counterText").objectReferenceValue = counter;
            so.FindProperty("_counterFormat").stringValue = "{0}/{1}";
            so.FindProperty("_questBoxContent").objectReferenceValue = box;
            so.FindProperty("_toggleButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Lalim/Prefabs", "LevelFlow");
            PrefabUtility.SaveAsPrefabAsset(root, QuestPrefab);
            Object.DestroyImmediate(root);
            return "Built PF_QuestObjectiveUI (quest button + quest text box).";
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TMP_Text Text(string name, Transform parent, string text, string fontPath, float size, Color color, TextAlignmentOptions align)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
