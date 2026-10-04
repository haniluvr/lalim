using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Lalim.UI;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ UI ▸ Build Loading Screen — PF_LoadingScreen (looping video background,
    /// loading %, a small bar under it, and a random fun fact) placed in 04_Loading.
    ///
    /// The prefab is the source of truth: an existing one is kept (edit it directly);
    /// ▸ Rebuild Loading Screen Prefab regenerates its layout. Facts already in the prefab are kept.
    /// </summary>
    public static class LoadingScreenBuilder
    {
        private const string ScenePath = "Assets/Lalim/Scenes/04_Loading/04_Loading.unity";
        private const string PrefabPath = "Assets/Lalim/Prefabs/UI/PF_LoadingScreen.prefab";
        private const string ClipPath = "Assets/Lalim/Videos/LoadingReef_Loop.mp4";
        private const string PosterPath = "Assets/Lalim/Videos/LoadingReef_Poster.jpg";
        private const string SpriteFolder = "Assets/Lalim/Sprites/UI/Loading";
        private const string PillPath = SpriteFolder + "/UI_RoundedPill.png";
        private const string FadePath = SpriteFolder + "/UI_BottomFade.png";
        private const string TitleFont = "Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset";
        private const string BodyFont = "Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset";

        private static readonly Color Navy = new Color32(0x34, 0x46, 0x6E, 0xFF);
        private static readonly Color Blue = new Color32(0x3B, 0x7F, 0xD0, 0xFF);
        private static readonly Color Ink = new Color32(0x16, 0x1C, 0x2A, 0xFF);

        [MenuItem("Tools/Lalim/UI/Build Loading Screen")]
        public static void BuildMenu()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            Debug.Log(Build(false));
        }

        [MenuItem("Tools/Lalim/UI/Rebuild Loading Screen Prefab")]
        public static void RebuildMenu() => Debug.Log(BuildPrefab(true));

        /// <summary>Makes sure the prefab exists and the open loading scene has one instance of it.</summary>
        public static string Build(bool rebuildPrefab)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine(BuildPrefab(rebuildPrefab));
            if (EditorSceneManager.GetActiveScene().path == ScenePath)
            {
                LevelUIPrefabs.EnsureInstance(PrefabPath, typeof(LoadingScreen), new string[0], report);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            return report.ToString();
        }

        public static string BuildPrefab(bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null && !overwrite) return "PF_LoadingScreen already exists (kept).";
            var keptFacts = existing != null ? existing.GetComponent<LoadingScreen>() : null;

            Sprite pill = EnsurePill();
            Sprite fade = EnsureFade();

            var root = new GameObject("PF_LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ── Video background, covering the screen at any aspect ratio ──
            RectTransform bg = Stretch(Rect("Background", root.transform));
            var raw = bg.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            bg.gameObject.AddComponent<VideoPlayer>();
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(ClipPath);
            var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = clip != null && clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f;
            bg.gameObject.AddComponent<VideoBackground>().EditorSetup(clip, AssetDatabase.LoadAssetAtPath<Texture2D>(PosterPath));

            // ── Soft light fade along the bottom so the text reads over the watercolour ──
            RectTransform fadeRt = Rect("BottomFade", root.transform);
            fadeRt.anchorMin = new Vector2(0f, 0f);
            fadeRt.anchorMax = new Vector2(1f, 0.6f);
            fadeRt.offsetMin = fadeRt.offsetMax = Vector2.zero;
            var fadeImg = fadeRt.gameObject.AddComponent<Image>();
            fadeImg.sprite = fade;
            fadeImg.raycastTarget = false;

            // ── Bottom-centre stack: % → bar → fun fact ──
            RectTransform content = Rect("Content", root.transform);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.anchoredPosition = new Vector2(0f, 64f);
            content.sizeDelta = new Vector2(1240f, 0f);
            var stack = content.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.LowerCenter;
            stack.spacing = 10f;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text percent = Text("Percent", content, "0%", TitleFont, 92f, Navy);
            percent.characterSpacing = 2f;

            Slider bar = Bar(content, pill);

            RectTransform gap = Rect("Gap", content);
            gap.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;

            RectTransform card = Rect("FactCard", content);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.sprite = pill;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(1f, 1f, 1f, 0.8f);
            cardImg.raycastTarget = false;
            var cardLayout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(56, 56, 22, 26);
            cardLayout.spacing = 4f;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
            cardLayout.childControlWidth = cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            card.gameObject.AddComponent<LayoutElement>().preferredWidth = 1240f;
            var factGroup = card.gameObject.AddComponent<CanvasGroup>();

            TMP_Text category = Text("Category", card, "DID YOU KNOW?", TitleFont, 22f, Blue);
            category.characterSpacing = 6f;
            TMP_Text fact = Text("Fact", card, "Amansinaya is the Tagalog patron of fishermen.", BodyFont, 30f, Ink);
            fact.textWrappingMode = TextWrappingModes.Normal;
            fact.lineSpacing = -4f;

            // Keep the facts already in the prefab (they may have been edited in the Inspector).
            var facts = keptFacts != null && keptFacts.Facts.Count > 0
                ? new System.Collections.Generic.List<LoadingScreen.Fact>(keptFacts.Facts)
                : LoadingFacts.Default();
            root.AddComponent<LoadingScreen>().EditorSetup(bar, percent, category, fact, factGroup, facts);

            if (!AssetDatabase.IsValidFolder("Assets/Lalim/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Lalim/Prefabs", "UI");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return existing != null ? "Rebuilt PF_LoadingScreen." : "Built PF_LoadingScreen.";
        }

        // ───────────────────────── Pieces ─────────────────────────

        private static Slider Bar(Transform parent, Sprite pill)
        {
            RectTransform barRt = Rect("Bar", parent);
            var le = barRt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 520f;
            le.preferredHeight = 14f;

            RectTransform track = Stretch(Rect("Track", barRt));
            var trackImg = track.gameObject.AddComponent<Image>();
            trackImg.sprite = pill;
            trackImg.type = Image.Type.Sliced;
            trackImg.color = new Color(1f, 1f, 1f, 0.75f);
            trackImg.raycastTarget = false;

            RectTransform area = Stretch(Rect("FillArea", barRt));
            RectTransform fill = Stretch(Rect("Fill", area));
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.sprite = pill;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = Blue;
            fillImg.raycastTarget = false;

            var slider = barRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = null;
            slider.targetGraphic = null;
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            return slider;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static TMP_Text Text(string name, Transform parent, string text, string fontPath, float size, Color color)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        // ───────────────────────── Generated sprites ─────────────────────────

        // 64×64 white circle, 9-sliced so it stretches into a pill or a rounded card.
        private static Sprite EnsurePill()
        {
            if (!File.Exists(PillPath))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var px = new Color32[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d) * 255f));
                }
                tex.SetPixels32(px);
                WritePng(tex, PillPath);
            }
            return ImportSprite(PillPath, new Vector4(31, 31, 31, 31));
        }

        // White, transparent at the top easing to 65% at the bottom.
        private static Sprite EnsureFade()
        {
            if (!File.Exists(FadePath))
            {
                const int h = 256;
                var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
                var px = new Color32[4 * h];
                for (int y = 0; y < h; y++)
                {
                    float t = 1f - y / (float)(h - 1);    // 1 at the bottom row
                    float a = t * t * (3f - 2f * t) * 0.65f;
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                tex.SetPixels32(px);
                WritePng(tex, FadePath);
            }
            return ImportSprite(FadePath, Vector4.zero);
        }

        private static void WritePng(Texture2D tex, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }

        private static Sprite ImportSprite(string path, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
