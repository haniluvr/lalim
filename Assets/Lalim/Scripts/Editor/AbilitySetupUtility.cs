using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Mechanics;
using Lalim.UI;

namespace Lalim.Editor
{
    /// <summary>
    /// One-click setup for the skills / stamina / HP / DDA system. Every step is idempotent —
    /// re-running it only adds what's missing.
    ///
    ///  Tools/Lalim/Abilities/1. Generate Placeholder Icons
    ///  Tools/Lalim/Abilities/2. Setup Player Prefab
    ///  Tools/Lalim/Abilities/3. Create Dugong, Checkpoint & Toxic Water Prefabs
    ///  Tools/Lalim/Abilities/4. Build Ability HUD
    ///  Tools/Lalim/Abilities/5. Setup Active Region Scene
    ///  Tools/Lalim/Abilities/Run All (1-4)
    /// </summary>
    public static class AbilitySetupUtility
    {
        private const string AbilityConfigPath = "Assets/Lalim/Data/AbilityConfig.asset";
        private const string DDAConfigPath = "Assets/Lalim/Data/DDAConfig.asset";
        private const string PlayerPrefabPath = "Assets/Lalim/Prefabs/Player_final_final.prefab";
        private const string HudPrefabPath = "Assets/Lalim/Prefabs/UI/GameHUD.prefab";
        private const string CharactersFolder = "Assets/Lalim/Prefabs/Characters";
        private const string HazardsFolder = "Assets/Lalim/Prefabs/Hazards";
        private const string DugongPrefabPath = CharactersFolder + "/PF_Dugong.prefab";
        private const string ToxicPrefabPath = HazardsFolder + "/PF_ToxicWaterVolume.prefab";
        private const string CheckpointPrefabPath = HazardsFolder + "/PF_Checkpoint.prefab";
        private const string DugongModelPath = "Assets/Lalim/Models/Dugong/dugong.fbx";
        private const string DugongAnimatorPath = "Assets/Lalim/Animations/DugongAnimator.controller";
        private const string PlaceholderFolder = "Assets/Lalim/Sprites/UI/Placeholders";
        /// <summary>Drop final art here using the same file names as the placeholders; it's used automatically.</summary>
        private const string FinalArtFolder = "Assets/Lalim/Sprites/UI/Abilities";
        private const string ParticlesUnlitPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
        private const string CircleButtonPath = "Assets/Lalim/Sprites/UI/Buttons/Circle Button.png";
        private const string CooldownRingPath = "Assets/Lalim/Sprites/UI/Game Mechanics/cooldown_ring.png";
        private const string SpiritSenseIconPath = "Assets/Lalim/Sprites/UI/Game Mechanics/icon_spiritsense.png";
        private const string DugongPortraitPath = "Assets/Lalim/Sprites/Characters/dugong.png";
        private const string CardFontPath = "Assets/Lalim/Fonts/PlusJakartaSans-Bold SDF.asset";

        /// <summary>Uniform scale for the dugong model — the imported mesh is only ~1.3m long.</summary>
        private const float DugongModelScale = 2.5f;

        [MenuItem("Tools/Lalim/Abilities/Run All (1-4)", false, 100)]
        public static void RunAll()
        {
            EnsureTagsAndLayers();
            GeneratePlaceholderIcons();
            SetupPlayerPrefab();
            CreatePrefabs();
            BuildAbilityHud();
            Debug.Log("[AbilitySetup] Done. Open each region scene and run 'Setup Active Region Scene'.");
        }

        // ─────────────────────────────────────────────
        // 0. Tags & layers
        // ─────────────────────────────────────────────

        public static void EnsureTagsAndLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");
            foreach (string tag in new[] { GameConstants.Tags.TOXIC_WATER, GameConstants.Tags.CHECKPOINT })
            {
                bool exists = false;
                for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == tag) exists = true;
                if (exists) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }

            SerializedProperty layers = tagManager.FindProperty("layers");
            bool hasHazard = false;
            for (int i = 0; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).stringValue == GameConstants.Layers.HAZARD) hasHazard = true;
            if (!hasHazard)
            {
                for (int i = 8; i < layers.arraySize; i++)
                {
                    SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(layer.stringValue)) { layer.stringValue = GameConstants.Layers.HAZARD; break; }
                }
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        // ─────────────────────────────────────────────
        // 1. Placeholder icons
        // ─────────────────────────────────────────────

        [MenuItem("Tools/Lalim/Abilities/1. Generate Placeholder Icons", false, 101)]
        public static void GeneratePlaceholderIcons()
        {
            Directory.CreateDirectory(PlaceholderFolder);
            Color disc = new Color(0.04f, 0.16f, 0.26f, 0.85f);
            Color rim = new Color(0.75f, 0.95f, 1f, 0.95f);
            Color glyph = new Color(0.85f, 1f, 1f, 1f);

            WriteIcon("icon_surge", 256, disc, rim, (x, y) =>
                Max(Seg(x, y, 95, 70, 160, 128, 14), Seg(x, y, 160, 128, 95, 186, 14),
                    Seg(x, y, 55, 105, 80, 105, 7), Seg(x, y, 45, 128, 80, 128, 7), Seg(x, y, 55, 151, 80, 151, 7)), glyph);

            WriteIcon("icon_riptide", 256, disc, rim, (x, y) =>
                Max(Seg(x, y, 70, 70, 130, 128, 13), Seg(x, y, 130, 128, 70, 186, 13),
                    Seg(x, y, 125, 70, 185, 128, 13), Seg(x, y, 185, 128, 125, 186, 13)), glyph);

            WriteIcon("icon_tidebond", 256, disc, rim, (x, y) =>
                Max(Mathf.Min(Circle(x, y, 128, 118, 62), 1f - Circle(x, y, 150, 100, 55)),
                    Wave(x, y, 60, 196, 136, 9)), glyph);

            WriteIcon("icon_mend", 256, disc, rim, (x, y) =>
                Max(Seg(x, y, 128, 70, 128, 186, 20), Seg(x, y, 70, 128, 186, 128, 20)), new Color(0.6f, 1f, 0.7f, 1f));

            WriteIcon("icon_barrier", 256, disc, rim, (x, y) =>
                Max(Ring(x, y, 128, 128, 66, 9), Ring(x, y, 128, 128, 40, 6), Circle(x, y, 105, 100, 12)), glyph);

            WriteIcon("icon_spiritsense", 256, disc, rim, (x, y) =>
                Max(Mathf.Min(Circle(x, y, 128, 175, 90), Circle(x, y, 128, 81, 90)) - Circle(x, y, 128, 128, 30), Circle(x, y, 128, 128, 16)), glyph);

            WriteIcon("icon_lock", 128, Color.clear, Color.clear, (x, y) =>
                Max(Rect(x, y, 68, 116, 188, 208, 12), Mathf.Min(Ring(x, y, 128, 112, 44, 14), y < 120f ? 1f : 0f)), new Color(1f, 1f, 1f, 0.95f));

            WriteShape("orb_full", 128, (x, y) => Circle(x, y, 64, 64, 60), (x, y, a) =>
            {
                float d = Mathf.Sqrt((x - 52) * (x - 52) + (y - 50) * (y - 50)) / 70f;
                return new Color(1f, 1f, 1f, a) * new Color(1f - d * 0.25f, 1f - d * 0.25f, 1f - d * 0.25f, 1f);
            });
            WriteShape("orb_empty", 128, (x, y) => Max(Ring(x, y, 64, 64, 56, 6), Circle(x, y, 64, 64, 54) * 0.25f), (x, y, a) => new Color(1f, 1f, 1f, a));
            WriteShape("ui_circle", 128, (x, y) => Circle(x, y, 64, 64, 62), (x, y, a) => new Color(1f, 1f, 1f, a));
            WriteShape("hp_frame", 128, (x, y) => Max(RectOutline(x, y, 2, 2, 126, 30, 10, 3), 0f), (x, y, a) => new Color(1f, 1f, 1f, a), height: 32, border: new Vector4(14, 14, 14, 14));
            WriteShape("ui_bar", 64, (x, y) => Rect(x, y, 0, 0, 64, 16, 0), (x, y, a) => new Color(1f, 1f, 1f, a), height: 16);

            // Four-point sparkle star for stamina (✦), supersampled for clean edges.
            WriteShape("star", 128, (x, y) => Supersample(x, y, (sx, sy) =>
            {
                float dx = Mathf.Abs(sx - 64f), dy = Mathf.Abs(sy - 64f);
                return Mathf.Sqrt(dx) + Mathf.Sqrt(dy) <= Mathf.Sqrt(60f) ? 1f : 0f;
            }), (x, y, a) => new Color(1f, 1f, 1f, a));

            // Scallop seashell for stamina (and the cost label). Ribbed fan + hinge "ears".
            WriteShape("shell", 128, (x, y) =>
            {
                float cover = Supersample(x, y, (sx, sy) => InShell(sx, sy) ? 1f : 0f);
                return cover <= 0f ? 0f : cover * ShellShade(x, y);
            }, (x, y, a) => new Color(1f, 1f, 1f, a));

            // Soft glow ring for the ultimate button when it's ready.
            WriteShape("glow_ring", 256, (x, y) =>
            {
                float r = Mathf.Sqrt((x - 128f) * (x - 128f) + (y - 128f) * (y - 128f));
                return Mathf.Exp(-Mathf.Pow((r - 104f) / 16f, 2f));
            }, (x, y, a) => new Color(1f, 1f, 1f, a));

            // Thin frame with heavier corner brackets (dugong card). 9-sliced so it scales to any card size.
            WriteShape("card_frame", 96, (x, y) =>
            {
                float thin = RectOutline(x, y, 2, 2, 94, 94, 0, 0.8f) * 0.55f;
                bool corner = (x < 20 || x > 76) && (y < 20 || y > 76);
                float bracket = corner ? RectOutline(x, y, 2, 2, 94, 94, 0, 2f) : 0f;
                return Mathf.Max(thin, bracket);
            }, (x, y, a) => new Color(1f, 1f, 1f, a), border: new Vector4(22, 22, 22, 22));

            AssetDatabase.Refresh();

            // The stamina shell uses the project's own sea_shell.svg when it exists (see WhiteShellFromSvg).
            WhiteShellFromSvg();

            foreach (string file in Directory.GetFiles(PlaceholderFolder, "*.png")) ConfigureSpriteImporter(file.Replace('\\', '/'));
            Debug.Log("[AbilitySetup] Placeholder icons written to " + PlaceholderFolder);
        }

        private const string SeaShellSvgPath = "Assets/Lalim/Sprites/UI/Game Mechanics/sea_shell.svg";

        /// <summary>
        /// Rasterises sea_shell.svg into a WHITE copy at Placeholders/shell.png. The SVG's shapes are
        /// black, and UI tinting multiplies colour, so black can't be tinted or shown filling up.
        /// Keeps the SVG's alpha (the exact outline); the original SVG file is not modified.
        /// Overwriting shell.png keeps its GUID, so everything already using the shell updates.
        /// </summary>
        private static bool WhiteShellFromSvg()
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(SeaShellSvgPath);
            if (source == null)
            {
                // Imported as a sprite instead of a texture: use the sprite's texture.
                var sprite = AssetDatabase.LoadAllAssetsAtPath(SeaShellSvgPath).OfType<Sprite>().FirstOrDefault();
                if (sprite != null) source = sprite.texture;
            }
            if (source == null) return false;

            // The imported texture isn't CPU-readable, so read it back through a RenderTexture.
            int w = source.width, h = source.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            Color32[] px = readable.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a);
            readable.SetPixels32(px);

            File.WriteAllBytes($"{PlaceholderFolder}/shell.png", readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset($"{PlaceholderFolder}/shell.png");
            return true;
        }

        private delegate float Shape(float x, float y);
        private delegate Color Shader2D(float x, float y, float alpha);

        private static void WriteIcon(string name, int size, Color disc, Color rim, Shape glyph, Color glyphColor)
        {
            float c = size * 0.5f;
            float scale = size / 256f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                // Glyph coordinates are authored on a 256px grid with y pointing down.
                float gx = (px + 0.5f) / scale;
                float gy = 256f - (py + 0.5f) / scale;
                Color col = Color.clear;
                float discA = Circle(gx, gy, 128, 128, 124);
                col = Over(col, new Color(disc.r, disc.g, disc.b, disc.a * discA));
                float rimA = Ring(gx, gy, 128, 128, 120, 5);
                col = Over(col, new Color(rim.r, rim.g, rim.b, rim.a * rimA));
                float g = Mathf.Clamp01(glyph(gx, gy));
                col = Over(col, new Color(glyphColor.r, glyphColor.g, glyphColor.b, glyphColor.a * g));
                pixels[py * size + px] = col;
            }
            tex.SetPixels(pixels);
            File.WriteAllBytes($"{PlaceholderFolder}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void WriteShape(string name, int width, Shape shape, Shader2D shade, int height = -1, Vector4 border = default)
        {
            if (height < 0) height = width;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            for (int px = 0; px < width; px++)
            {
                float x = px + 0.5f, y = height - (py + 0.5f);
                float a = Mathf.Clamp01(shape(x, y));
                pixels[py * width + px] = a <= 0f ? Color.clear : shade(x, y, a);
            }
            tex.SetPixels(pixels);
            string path = $"{PlaceholderFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            if (border != default) s_PendingBorders[path] = border;
        }

        private static readonly System.Collections.Generic.Dictionary<string, Vector4> s_PendingBorders = new System.Collections.Generic.Dictionary<string, Vector4>();

        private static void ConfigureSpriteImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            if (s_PendingBorders.TryGetValue(path, out Vector4 border)) importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        // Anti-aliased signed-distance helpers (1px soft edge). Return coverage 0..1.
        private static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Clamp01(r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + 0.5f);
        private static float Ring(float x, float y, float cx, float cy, float r, float halfWidth) => Mathf.Clamp01(halfWidth - Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) + 0.5f);
        private static float Max(params float[] v) => v.Max();

        // Seashell geometry on a 128px grid (y down): fan opening upward from a hinge at (64, 104).
        private const float ShellHingeX = 64f, ShellHingeY = 104f, ShellHalfAngle = 62f, ShellRadius = 70f;
        private const int ShellRibs = 9;

        private static bool InShell(float x, float y)
        {
            // Hinge "ears" at the base.
            if (x >= 44f && x <= 84f && y >= 98f && y <= 116f) return true;

            float dx = x - ShellHingeX, dy = ShellHingeY - y;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            if (Mathf.Abs(ang) > ShellHalfAngle || r < 3f) return false;
            float frac = RibPhase(ang);
            return r <= ShellRadius + 5f * Mathf.Sin(Mathf.PI * frac);
        }

        /// <summary>Darker grooves between the ribs, so the shell reads as a scallop when tinted.</summary>
        private static float ShellShade(float x, float y)
        {
            if (y >= 98f) return 0.8f;
            float dx = x - ShellHingeX, dy = ShellHingeY - y;
            float ang = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            float frac = RibPhase(ang);
            return 1f - 0.4f * Mathf.Pow(1f - Mathf.Sin(Mathf.PI * frac), 3f);
        }

        private static float RibPhase(float angleDegrees)
        {
            float ribWidth = ShellHalfAngle * 2f / ShellRibs;
            float phase = (angleDegrees + ShellHalfAngle) / ribWidth;
            return phase - Mathf.Floor(phase);
        }

        private static float Supersample(float x, float y, System.Func<float, float, float> inside)
        {
            float sum = 0f;
            for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
                sum += inside(x - 0.5f + (i + 0.5f) / 4f, y - 0.5f + (j + 0.5f) / 4f);
            return sum / 16f;
        }

        private static float Seg(float x, float y, float ax, float ay, float bx, float by, float halfWidth)
        {
            Vector2 p = new Vector2(x, y), a = new Vector2(ax, ay), b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Mathf.Clamp01(halfWidth - Vector2.Distance(p, a + ab * t) + 0.5f);
        }

        private static float Wave(float x, float y, float x0, float x1, float cy, float halfWidth)
        {
            if (x < x0 - halfWidth || x > x1 + halfWidth) return 0f;
            float wy = cy + Mathf.Sin((Mathf.Clamp(x, x0, x1) - x0) / 22f) * 10f;
            return Mathf.Clamp01(halfWidth - Mathf.Abs(y - wy) + 0.5f);
        }

        private static float RoundedRectDistance(float x, float y, float x0, float y0, float x1, float y1, float radius)
        {
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            float hx = (x1 - x0) * 0.5f - radius, hy = (y1 - y0) * 0.5f - radius;
            float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static float Rect(float x, float y, float x0, float y0, float x1, float y1, float radius) =>
            Mathf.Clamp01(-RoundedRectDistance(x, y, x0, y0, x1, y1, radius) + 0.5f);

        private static float RectOutline(float x, float y, float x0, float y0, float x1, float y1, float radius, float halfWidth) =>
            Mathf.Clamp01(halfWidth - Mathf.Abs(RoundedRectDistance(x, y, x0, y0, x1, y1, radius)) + 0.5f);

        private static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return Color.clear;
            Color c = (src * src.a + dst * dst.a * (1f - src.a)) / a;
            c.a = a;
            return c;
        }

        private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        private static Sprite Placeholder(string name) => FinalArtIn(FinalArtFolder, name) ?? LoadSprite($"{PlaceholderFolder}/{name}.png");

        private static Sprite FinalArtIn(string folder, string name)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return LoadSprite(path);
            }
            return null;
        }

        private const string GameMechanicsFolder = "Assets/Lalim/Sprites/UI/Game Mechanics";

        /// <summary>
        /// Final art wins over the generated placeholder. Looks in the Game Mechanics folder (where the
        /// HUD icons live) and FinalArtFolder, under any of the given names.
        /// </summary>
        private static Sprite FinalArt(params string[] names)
        {
            string[] folders = new[] { GameMechanicsFolder, FinalArtFolder }.Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length == 0) return null;
            foreach (string name in names)
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite", folders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return LoadSprite(path);
            }
            return null;
        }

        // ─────────────────────────────────────────────
        // 2. Player prefab
        // ─────────────────────────────────────────────

        [MenuItem("Tools/Lalim/Abilities/2. Setup Player Prefab", false, 102)]
        public static void SetupPlayerPrefab()
        {
            var config = AssetDatabase.LoadAssetAtPath<AbilityConfig>(AbilityConfigPath);
            if (config == null) { Debug.LogError("[AbilitySetup] Missing " + AbilityConfigPath); return; }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                AssignConfig(GetOrAdd<AbilityController>(root), "_config", config);
                AssignConfig(GetOrAdd<PlayerStamina>(root), "_config", config);
                AssignConfig(GetOrAdd<PlayerHealth>(root), "_config", config);
                GetOrAdd<ToxicWaterTracker>(root);
                AssignConfig(GetOrAdd<SurgeAbility>(root), "Config", config);
                AssignConfig(GetOrAdd<RiptideAbility>(root), "Config", config);
                AssignConfig(GetOrAdd<SpiritSense>(root), "Config", config);
                AssignConfig(GetOrAdd<TidebondAbility>(root), "Config", config);
                AssignConfig(GetOrAdd<SeagrassMendAbility>(root), "Config", config);
                var barrier = GetOrAdd<BubbleBarrierAbility>(root);
                AssignConfig(barrier, "Config", config);
                SetRef(barrier, "_bubbleMaterial", EnsureBubbleMaterial());

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                Debug.Log("[AbilitySetup] Player prefab updated with health, stamina and all six abilities.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private const string BubbleMaterialPath = "Assets/Lalim/Materials/M_BubbleBarrier.mat";

        /// <summary>Material asset for the Bubble Barrier (keeps the Lalim/BubbleRim shader in builds).</summary>
        private static Material EnsureBubbleMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BubbleMaterialPath);
            if (mat != null) return mat;
            Shader shader = Shader.Find("Lalim/BubbleRim");
            if (shader == null) { Debug.LogError("[AbilitySetup] Shader Lalim/BubbleRim not found"); return null; }
            mat = new Material(shader) { name = "M_BubbleBarrier" };
            AssetDatabase.CreateAsset(mat, BubbleMaterialPath);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void AssignConfig(Object component, string field, Object value)
        {
            var so = new SerializedObject(component);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"[AbilitySetup] {component.GetType().Name} has no field '{field}'"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Object component, string field, Object value) => AssignConfig(component, field, value);

        // ─────────────────────────────────────────────
        // 3. Prefabs: dugong, checkpoint, toxic water
        // ─────────────────────────────────────────────

        [MenuItem("Tools/Lalim/Abilities/3. Create Dugong, Checkpoint & Toxic Water Prefabs", false, 103)]
        public static void CreatePrefabs()
        {
            Directory.CreateDirectory(CharactersFolder);
            Directory.CreateDirectory(HazardsFolder);
            AssetDatabase.Refresh();

            AddDugongAnimatorParameters();
            CreateDugongPrefab();
            EnsureDugongHealth();
            CreateCheckpointPrefab();
            CreateToxicPrefab();
        }

        private static void AddDugongAnimatorParameters()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DugongAnimatorPath);
            if (controller == null) return;

            void Ensure(string name, AnimatorControllerParameterType type)
            {
                if (controller.parameters.Any(p => p.name == name)) return;
                controller.AddParameter(name, type);
            }

            Ensure("Speed", AnimatorControllerParameterType.Float);
            Ensure("Mounted", AnimatorControllerParameterType.Bool);
            Ensure("Heal", AnimatorControllerParameterType.Trigger);
            Ensure("Tired", AnimatorControllerParameterType.Bool);
            EditorUtility.SetDirty(controller);
        }

        /// <summary>The dugong's own HP (the green bar under the dugong card).</summary>
        private static void EnsureDugongHealth()
        {
            if (!File.Exists(DugongPrefabPath)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(DugongPrefabPath);
            try
            {
                var health = GetOrAdd<DugongHealth>(root);
                SetRef(health, "_config", AssetDatabase.LoadAssetAtPath<AbilityConfig>(AbilityConfigPath));
                PrefabUtility.SaveAsPrefabAsset(root, DugongPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateDugongPrefab()
        {
            if (File.Exists(DugongPrefabPath)) { Debug.Log("[AbilitySetup] PF_Dugong already exists — skipped."); return; }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DugongModelPath);
            if (model == null) { Debug.LogError("[AbilitySetup] Missing " + DugongModelPath); return; }

            var root = new GameObject("PF_Dugong");
            try
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                modelInstance.name = "Model";
                modelInstance.transform.SetParent(root.transform, false);
                modelInstance.transform.localScale = Vector3.one * DugongModelScale;

                var animator = GetOrAdd<Animator>(modelInstance);
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DugongAnimatorPath);
                animator.applyRootMotion = false;

                Bounds b = new Bounds(root.transform.position, Vector3.zero);
                bool first = true;
                foreach (Renderer r in modelInstance.GetComponentsInChildren<Renderer>())
                {
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }

                // The player's pivot sits on the dugong's back, slightly forward of centre.
                var seat = new GameObject("SeatAnchor").transform;
                seat.SetParent(root.transform, false);
                seat.localPosition = new Vector3(b.center.x, b.max.y - 0.15f, b.center.z + b.extents.z * 0.1f);

                ParticleSystem heal = CreateParticles(root.transform, "VFX_Heal", new Color(0.45f, 0.95f, 0.5f, 0.9f), 0.12f, 1.2f, 25f, loop: false, shapeRadius: 0.8f);
                heal.transform.localPosition = new Vector3(b.center.x, b.max.y, b.center.z);
                ParticleSystem ride = CreateParticles(root.transform, "VFX_Ride", new Color(0.8f, 0.95f, 1f, 0.6f), 0.08f, 0.8f, 30f, loop: true, shapeRadius: 0.5f);
                ride.transform.localPosition = new Vector3(b.center.x, b.center.y, b.min.z);

                var companion = root.AddComponent<DugongCompanion>();
                SetRef(companion, "_seatAnchor", seat);
                SetRef(companion, "_animator", animator);
                SetRef(companion, "_healVfx", heal);
                SetRef(companion, "_rideVfx", ride);

                PrefabUtility.SaveAsPrefabAsset(root, DugongPrefabPath);
                Debug.Log("[AbilitySetup] Created " + DugongPrefabPath + ". Check the model's facing in the prefab; adjust DugongCompanion._modelRotationOffset if it swims sideways.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void CreateCheckpointPrefab()
        {
            if (File.Exists(CheckpointPrefabPath)) return;
            var go = new GameObject("PF_Checkpoint");
            try
            {
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(12f, 12f, 4f);
                go.AddComponent<Checkpoint>();
                TrySetTag(go, GameConstants.Tags.CHECKPOINT);
                PrefabUtility.SaveAsPrefabAsset(go, CheckpointPrefabPath);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static void CreateToxicPrefab()
        {
            if (File.Exists(ToxicPrefabPath)) return;
            var config = AssetDatabase.LoadAssetAtPath<AbilityConfig>(AbilityConfigPath);
            var go = new GameObject("PF_ToxicWaterVolume");
            try
            {
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(30f, 12f, 30f);
                var volume = go.AddComponent<ToxicWaterVolume>();
                SetRef(volume, "_config", config);
                TrySetTag(go, GameConstants.Tags.TOXIC_WATER);
                int hazard = LayerMask.NameToLayer(GameConstants.Layers.HAZARD);
                if (hazard >= 0) go.layer = hazard;

                ParticleSystem murk = CreateParticles(go.transform, "Murk", new Color(0.45f, 0.6f, 0.2f, 0.35f), 0.6f, 6f, 60f, loop: true, shapeRadius: 0f);
                var shape = murk.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = box.size;
                var main = murk.main;
                main.startSpeed = 0.2f;
                main.maxParticles = 400;
                main.playOnAwake = true;

                PrefabUtility.SaveAsPrefabAsset(go, ToxicPrefabPath);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static ParticleSystem CreateParticles(Transform parent, string name, Color color, float size, float lifetime, float rate, bool loop, float shapeRadius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.duration = loop ? 1f : 3f;
            main.playOnAwake = false;
            main.startColor = color;
            main.startSize = size;
            main.startLifetime = lifetime;
            main.startSpeed = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, shapeRadius);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticlesUnlitPath);
            return ps;
        }

        private static void TrySetTag(GameObject go, string tag)
        {
            if (UnityEditorInternal.InternalEditorUtility.tags.Contains(tag)) go.tag = tag;
        }

        // ─────────────────────────────────────────────
        // 4. HUD
        // ─────────────────────────────────────────────

        [MenuItem("Tools/Lalim/Abilities/4. Build Ability HUD", false, 104)]
        public static void BuildAbilityHud()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                // Phase 0 fix needs a CanvasGroup on the HUD root.
                var group = GetOrAdd<CanvasGroup>(root);
                var hud = root.GetComponent<HUDController>();
                if (hud != null) SetRef(hud, "_rootCanvasGroup", group);
                GetOrAdd<ToxicScreenEffect>(root);

                Transform cluster = root.transform.Find("BottomRight_Cluster");
                if (cluster == null) { Debug.LogError("[AbilitySetup] GameHUD has no BottomRight_Cluster"); return; }

                // Moon Tide is gone. Every other button keeps its current (possibly hand-placed) layout.
                DestroyChild(cluster, "Btn_MoonTide");

                Sprite circle = Placeholder("ui_circle");
                Sprite ring = FinalArt("cooldown_ring") ?? LoadSprite(CooldownRingPath) ?? Placeholder("orb_empty");
                Sprite lockIcon = Placeholder("icon_lock");
                Sprite spiritIcon = FinalArt("icon_spiritsense") ?? LoadSprite(SpiritSenseIconPath) ?? Placeholder("icon_spiritsense");

                // Arc around the primary Surge/Riptide button (bottom-right, where Moon Tide was).
                // Starting positions — verify thumb reach on a real phone.
                var surge = BuildAbilityButton<SurgeRiptideButton>(cluster, "Btn_SurgeRiptide", AbilityId.Surge,
                    new Vector2(-230, 190), 200, FinalArt("icon_surge") ?? Placeholder("icon_surge"), circle, ring, lockIcon);
                // One icon for both: tap = Surge, hold = Riptide. The charge ring shows the hold building up.
                Image chargeRing = FindOrCreateRadial(surge.transform, "ChargeRing", ring, new Color(1f, 0.85f, 0.35f, 1f), 1.12f);
                chargeRing.transform.SetSiblingIndex(Mathf.Min(2, surge.transform.childCount - 1));
                SetRef(surge, "_chargeRing", chargeRing);

                var barrier = BuildAbilityButton<UltimateButtonUI>(cluster, "Btn_BubbleBarrier", AbilityId.BubbleBarrier, new Vector2(-489, 167), 150, FinalArt("icon_bubblebarrier", "icon_barrier") ?? Placeholder("icon_barrier"), circle, ring, lockIcon);
                SetupUltimateCharge(barrier, circle);

                if (root.GetComponentsInChildren<DugongCardButton>(true).Length == 0) BuildDugongCard(root.transform, cluster, lockIcon);
                // The card's green bar is the dugong's HP now (was Tidebond stamina).
                Transform oldBar = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "DugongStamina");
                if (oldBar != null) oldBar.name = "DugongHP";
                BuildAbilityButton<AbilityButtonUI>(cluster, "Btn_SpiritSense", AbilityId.SpiritSense, new Vector2(-364, 413), 120, spiritIcon, circle, ring, lockIcon);
                BuildAbilityButton<AbilityButtonUI>(cluster, "Btn_Tidebond", AbilityId.Tidebond, new Vector2(-225, 450), 120, FinalArt("icon_tidebond") ?? Placeholder("icon_tidebond"), circle, ring, lockIcon);

                // Codex moves to middle-left under Navigation, freeing the arc.
                Transform codex = cluster.Find("Btn_Codex");
                if (codex != null)
                {
                    codex.SetParent(root.transform, false);
                    var rt = (RectTransform)codex;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(190f, 60f);
                    rt.sizeDelta = new Vector2(140f, 140f);
                }

                BuildBottomCenter(root.transform);

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                Debug.Log("[AbilitySetup] GameHUD updated in place: HP bar, stamina stars, countdowns and cost labels (button art and layout kept).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/Lalim/Abilities/4b. Rebuild Dugong Card Only", false, 104)]
        public static void RebuildDugongCardOnly()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                Transform cluster = root.transform.Find("BottomRight_Cluster") ?? root.transform;
                BuildDugongCard(root.transform, cluster, Placeholder("icon_lock"));
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
                Debug.Log("[AbilitySetup] Dugong card rebuilt (nothing else touched).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static void PlaceButton(Transform t, Transform parent, Vector2 pos, float size)
        {
            if (t == null) return;
            var rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
        }

        /// <summary>
        /// Upgrades an existing ability button in place (keeps its GameObject, sprite, colour, size and
        /// position) or creates it if missing, then (re)wires the helper children: cooldown overlay,
        /// countdown number, duration ring, lock icon and the ✦N cost label.
        /// </summary>
        private static T BuildAbilityButton<T>(Transform parent, string name, AbilityId id, Vector2 pos, float size,
            Sprite icon, Sprite circle, Sprite ring, Sprite lockSprite) where T : AbilityButtonUI
        {
            RectTransform rt = parent.root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
            if (rt == null)
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                created.transform.SetParent(parent, false);
                PlaceButton(created.transform, parent, pos, size);
                var img = created.GetComponent<Image>();
                img.sprite = icon;
                img.color = new Color(1f, 1f, 1f, 0.686f); // same opacity as the rest of the HUD's buttons
                rt = (RectTransform)created.transform;
            }

            GameObject go = rt.gameObject;
            size = rt.sizeDelta.x;
            GetOrAdd<CanvasGroup>(go);

            // Swap the UI component type if needed (e.g. Barrier: AbilityButtonUI → UltimateButtonUI).
            foreach (AbilityButtonUI existingUi in go.GetComponents<AbilityButtonUI>())
            {
                if (existingUi.GetType() != typeof(T)) Object.DestroyImmediate(existingUi);
            }
            T ui = GetOrAdd<T>(go);

            DestroyChild(go.transform, "CostPips"); // replaced by the ✦N label

            Image cooldown = FindOrCreateRadial(go.transform, "CooldownOverlay", circle, new Color(0f, 0f, 0f, 0.6f), 0.94f);
            DestroyChild(go.transform, "DurationRing"); // timers only during cooldown, never for a skill's duration
            TMP_Text countdown = FindOrCreateCountdown(go.transform, size);
            GameObject lockGo = FindOrCreateLock(go.transform, size, lockSprite);
            (GameObject costRoot, Image costStar, TMP_Text costText) = FindOrCreateCost(go.transform);

            // Draw order: overlay, ring, number, lock on top.
            cooldown.transform.SetAsLastSibling();
            countdown.transform.SetAsLastSibling();
            lockGo.transform.SetAsLastSibling();
            costRoot.transform.SetAsLastSibling();

            var so = new SerializedObject(ui);
            so.FindProperty("_ability").enumValueIndex = (int)id;
            so.FindProperty("_icon").objectReferenceValue = go.GetComponent<Image>();
            so.FindProperty("_cooldownOverlay").objectReferenceValue = cooldown;
            so.FindProperty("_countdownText").objectReferenceValue = countdown;
            so.FindProperty("_lockIcon").objectReferenceValue = lockGo;
            so.FindProperty("_canvasGroup").objectReferenceValue = go.GetComponent<CanvasGroup>();
            so.FindProperty("_costRoot").objectReferenceValue = costRoot;
            so.FindProperty("_costIcon").objectReferenceValue = costStar;
            so.FindProperty("_costText").objectReferenceValue = costText;
            so.FindProperty("_costUnaffordable").colorValue = new Color(0.88f, 0.9f, 0.93f, 0.95f);
            so.FindProperty("_costAffordable").colorValue = HudPalette.ShellReady;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        private static Image FindOrCreateRadial(Transform parent, string name, Sprite sprite, Color color, float scale)
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.GetComponent<Image>() != null) return existing.GetComponent<Image>();
            return CreateRadial(parent, name, sprite, color, scale);
        }

        private static TMP_Text FindOrCreateCountdown(Transform parent, float size)
        {
            Transform existing = parent.Find("Countdown");
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                var go = new GameObject("Countdown", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                Stretch((RectTransform)go.transform);
                text = go.GetComponent<TextMeshProUGUI>();
            }
            text.fontSize = Mathf.Round(size * 0.36f);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "";
            text.gameObject.SetActive(false);
            return text;
        }

        private static GameObject FindOrCreateLock(Transform parent, float size, Sprite lockSprite)
        {
            Transform existing = parent.Find("Lock");
            if (existing != null) return existing.gameObject;

            var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lockGo.transform.SetParent(parent, false);
            var lrt = (RectTransform)lockGo.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = Vector2.one * size * 0.4f;
            var lockImage = lockGo.GetComponent<Image>();
            lockImage.sprite = lockSprite;
            lockImage.raycastTarget = false;
            lockGo.SetActive(false);
            return lockGo;
        }

        /// <summary>"✦2" under the button: star sprite + number.</summary>
        private static (GameObject, Image, TMP_Text) FindOrCreateCost(Transform parent)
        {
            Transform existing = parent.Find("Cost");
            if (existing != null)
            {
                if (existing.GetComponent<Image>() == null) AddCostBacking(existing.gameObject);
                Transform icon = existing.Find("Star") ?? existing.Find("Shell");
                if (icon != null)
                {
                    icon.name = "Shell";
                    icon.GetComponent<Image>().sprite = Placeholder("shell");
                    icon.GetComponent<Image>().preserveAspect = true;
                    ((RectTransform)icon).sizeDelta = new Vector2(18f, 18f);
                }
                return (existing.gameObject,
                        existing.Find("Shell") != null ? existing.Find("Shell").GetComponent<Image>() : null,
                        existing.GetComponentInChildren<TMP_Text>(true));
            }

            var costGo = new GameObject("Cost", typeof(RectTransform));
            costGo.transform.SetParent(parent, false);
            var crt = (RectTransform)costGo.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -2f);
            crt.sizeDelta = new Vector2(44f, 22f);

            AddCostBacking(costGo);

            var starGo = new GameObject("Shell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            starGo.transform.SetParent(crt, false);
            var srt = (RectTransform)starGo.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(-9f, 0f);
            srt.sizeDelta = new Vector2(18f, 18f);
            var star = starGo.GetComponent<Image>();
            star.sprite = Placeholder("shell");
            star.preserveAspect = true;
            star.color = new Color(1f, 0.82f, 0.35f, 1f);
            star.raycastTarget = false;

            var textGo = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(crt, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(9f, 0f);
            trt.sizeDelta = new Vector2(22f, 22f);
            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.fontSize = 20f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.82f, 0.35f, 1f);
            text.raycastTarget = false;
            text.text = "1";

            costGo.SetActive(false);
            return (costGo, star, text);
        }

        /// <summary>Dark backing so the ✦N label reads on bright sand.</summary>
        private static void AddCostBacking(GameObject costGo)
        {
            costGo.AddComponent<CanvasRenderer>();
            var backing = costGo.AddComponent<Image>();
            backing.sprite = Placeholder("ui_bar");
            backing.color = new Color(0f, 0.05f, 0.1f, 0.5f);
            backing.raycastTarget = false;
        }

        /// <summary>
        /// Barrier charge visuals: the button's art moves to an "Icon" child so a charge disc can sit
        /// behind it; the root Image stays as an invisible hit area. Keeps the art's sprite and colour.
        /// </summary>
        private static void SetupUltimateCharge(UltimateButtonUI ui, Sprite circle)
        {
            Transform t = ui.transform;
            Image rootImage = t.GetComponent<Image>();

            Image icon = t.Find("Icon") != null ? t.Find("Icon").GetComponent<Image>() : null;
            if (icon == null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(t, false);
                Stretch((RectTransform)iconGo.transform);
                icon = iconGo.GetComponent<Image>();
                icon.sprite = rootImage.sprite;
                icon.color = rootImage.color;
                icon.preserveAspect = rootImage.preserveAspect;
                icon.raycastTarget = false;

                rootImage.sprite = circle;
                rootImage.color = new Color(0f, 0f, 0f, 0f); // invisible, still receives presses
            }

            DestroyChild(t, "ReadyGlow"); // no glow: it just turns gold, like the Mend card

            Image fill = FindOrCreateRadial(t, "ChargeFill", circle, new Color(0.42f, 0.37f, 0.26f, 1f), 0.92f);
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 0.5f;

            // Charge disc BEHIND the icon — the bubble art is semi-transparent, so the fill shows through.
            fill.transform.SetSiblingIndex(0);
            icon.transform.SetSiblingIndex(1);

            var so = new SerializedObject(ui);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.FindProperty("_chargeFill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Where a HUD element was placed (possibly by hand), so a rebuild doesn't move it.</summary>
        private struct SavedLayout
        {
            public Transform Parent;
            public int SiblingIndex;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
            /// <summary>The button's own Image sprite/colour — hand-picked art must survive a rebuild.</summary>
            public Sprite IconSprite;
            public Color IconColor;
            public bool HasIcon;

            public void ApplyTo(RectTransform rt, bool keepSize)
            {
                rt.SetParent(Parent, false);
                rt.SetSiblingIndex(Mathf.Min(SiblingIndex, Parent.childCount - 1));
                rt.anchorMin = AnchorMin;
                rt.anchorMax = AnchorMax;
                rt.pivot = Pivot;
                rt.anchoredPosition = Position;
                if (keepSize) rt.sizeDelta = Size;
            }
        }

        /// <summary>Finds 'name' anywhere under root, records its layout, and destroys it.</summary>
        private static SavedLayout? TakeExisting(Transform root, string name)
        {
            RectTransform existing = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
            if (existing == null) return null;
            var layout = new SavedLayout
            {
                Parent = existing.parent,
                SiblingIndex = existing.GetSiblingIndex(),
                AnchorMin = existing.anchorMin,
                AnchorMax = existing.anchorMax,
                Pivot = existing.pivot,
                Position = existing.anchoredPosition,
                Size = existing.sizeDelta
            };
            var image = existing.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                layout.IconSprite = image.sprite;
                layout.IconColor = image.color;
                layout.HasIcon = true;
            }
            Object.DestroyImmediate(existing.gameObject);
            return layout;
        }

        // ─────────────────────────────────────────────
        // Dugong card (Seagrass Mend)
        // ─────────────────────────────────────────────

        private static readonly Vector2 CardSize = new Vector2(160f, 194f);

        /// <summary>
        /// Seagrass Mend as a portrait card: desaturated dugong underneath, full-colour dugong
        /// revealed bottom-up as the cooldown recovers, name strip, status text, and the dugong's
        /// stamina as a green bar at the bottom. Keeps the button's current position.
        /// </summary>
        private static void BuildDugongCard(Transform root, Transform defaultParent, Sprite lockSprite)
        {
            Sprite portrait = LoadSprite(DugongPortraitPath);
            if (portrait == null) { Debug.LogError("[AbilitySetup] Missing " + DugongPortraitPath); return; }

            RectTransform old = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Btn_SeagrassMend");
            bool wasCard = old != null && old.GetComponent<DugongCardButton>() != null;
            SavedLayout? saved = TakeExisting(root, "Btn_SeagrassMend");

            var card = new GameObject("Btn_SeagrassMend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)card.transform;
            if (saved.HasValue)
            {
                saved.Value.ApplyTo(rt, keepSize: wasCard);
                rt.pivot = new Vector2(0.5f, 0.5f);
                if (!wasCard) rt.sizeDelta = CardSize;
            }
            else
            {
                rt.SetParent(defaultParent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-108f, 660f);
                rt.sizeDelta = CardSize;
            }

            // Invisible hit area covering the whole card.
            var hit = card.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CardFontPath);

            // Portrait window: everything above the bottom stamina strip.
            var window = new GameObject("Window", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            window.transform.SetParent(rt, false);
            var wrt = (RectTransform)window.transform;
            wrt.anchorMin = Vector2.zero;
            wrt.anchorMax = Vector2.one;
            wrt.offsetMin = new Vector2(8f, 30f);
            wrt.offsetMax = new Vector2(-8f, -8f);
            var backdrop = window.GetComponent<Image>();
            backdrop.color = new Color(0.02f, 0.08f, 0.14f, 1f); // solid, so the scene never shows through
            backdrop.raycastTarget = false;

            // Background that fills from the bottom as the skill recharges (desaturated), then turns
            // saturated when ready. The portrait on top always stays in full colour.
            Image chargeFill = CreatePortrait(wrt, "ChargeFill", Placeholder("ui_bar"), new Color(1f, 0.76f, 0.2f, 1f));
            chargeFill.type = Image.Type.Filled;
            chargeFill.fillMethod = Image.FillMethod.Vertical;
            chargeFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            chargeFill.fillAmount = 1f;

            Image portraitImg = CreatePortrait(wrt, "Portrait", portrait, Color.white);
            // Slightly inset and sitting on the bottom, so the charge colour shows around the dugong.
            var prt = portraitImg.rectTransform;
            prt.anchorMin = new Vector2(0.1f, 0f);
            prt.anchorMax = new Vector2(0.9f, 0.8f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            portraitImg.preserveAspect = true;

            // Glowing line at the recharge level.
            var edge = new GameObject("FillEdge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            edge.transform.SetParent(wrt, false);
            var ert = (RectTransform)edge.transform;
            ert.anchorMin = new Vector2(0f, 1f);
            ert.anchorMax = new Vector2(1f, 1f);
            ert.pivot = new Vector2(0.5f, 0.5f);
            ert.sizeDelta = new Vector2(0f, 4f);
            var edgeImg = edge.GetComponent<Image>();
            edgeImg.sprite = Placeholder("ui_bar");
            edgeImg.color = new Color(0.75f, 1f, 0.95f, 0.9f);
            edgeImg.raycastTarget = false;
            edge.SetActive(false);

            // Name strip across the bottom of the portrait.
            var strip = new GameObject("NameStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            strip.transform.SetParent(wrt, false);
            var srt = (RectTransform)strip.transform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.sizeDelta = new Vector2(0f, 20f);
            var stripImg = strip.GetComponent<Image>();
            stripImg.color = new Color(0.02f, 0.08f, 0.14f, 0.8f);
            stripImg.raycastTarget = false;
            TMP_Text nameLabel = CreateLabel(srt, "Name", "DUGONG", 13f, font, TextAlignmentOptions.Center, new Color(0.92f, 0.98f, 1f, 1f));
            nameLabel.characterSpacing = 8f;

            // Frame + corner brackets on top of everything.
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(rt, false);
            Stretch((RectTransform)frameGo.transform);
            var frame = frameGo.GetComponent<Image>();
            frame.sprite = Placeholder("card_frame");
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;

            // Two small glowing dots at the top-left, like the reference card.
            for (int i = 0; i < 2; i++)
            {
                var dot = new GameObject("Dot" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dot.transform.SetParent(rt, false);
                var drt = (RectTransform)dot.transform;
                drt.anchorMin = drt.anchorMax = new Vector2(0f, 1f);
                drt.sizeDelta = new Vector2(5f, 5f);
                drt.anchoredPosition = new Vector2(-6f + i * 9f, 6f);
                var dimg = dot.GetComponent<Image>();
                dimg.sprite = Placeholder("ui_circle");
                dimg.color = new Color(0.8f, 1f, 0.6f, 0.9f);
                dimg.raycastTarget = false;
            }

            // Status text above the top-right corner ("READY" / "23s" / "HEALING").
            var status = CreateLabel(rt, "Status", "READY", 12f, font, TextAlignmentOptions.BottomRight, new Color(0.85f, 0.95f, 1f, 0.9f));
            var strt = status.rectTransform;
            strt.anchorMin = strt.anchorMax = new Vector2(1f, 1f);
            strt.pivot = new Vector2(1f, 0f);
            strt.sizeDelta = new Vector2(120f, 18f);
            strt.anchoredPosition = new Vector2(0f, 2f);
            status.characterSpacing = 4f;

            // Dugong stamina bar along the bottom of the card.
            var bar = new GameObject("DugongHP", typeof(RectTransform));
            bar.transform.SetParent(rt, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.offsetMin = new Vector2(8f, 10f);
            brt.offsetMax = new Vector2(-8f, 22f);
            NewImage(brt, "Track", Placeholder("ui_bar"), new Color(1f, 1f, 1f, 0.18f), Image.Type.Simple);
            var fill = NewImage(brt, "Fill", Placeholder("ui_bar"), new Color(0.62f, 0.9f, 0.2f, 1f), Image.Type.Filled);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var barFrame = NewImage(brt, "Outline", Placeholder("hp_frame"), new Color(1f, 1f, 1f, 0.6f), Image.Type.Sliced);
            barFrame.pixelsPerUnitMultiplier = 3f;

            // Lock icon for "not allowed here".
            var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            lockGo.transform.SetParent(rt, false);
            var lrt = (RectTransform)lockGo.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.55f);
            lrt.sizeDelta = new Vector2(44f, 44f);
            var lockImg = lockGo.GetComponent<Image>();
            lockImg.sprite = lockSprite;
            lockImg.raycastTarget = false;
            lockGo.SetActive(false);

            var ui = card.AddComponent<DugongCardButton>();
            var so = new SerializedObject(ui);
            so.FindProperty("_ability").enumValueIndex = (int)AbilityId.SeagrassMend;
            so.FindProperty("_icon").objectReferenceValue = portraitImg;
            so.FindProperty("_lockIcon").objectReferenceValue = lockGo;
            so.FindProperty("_canvasGroup").objectReferenceValue = card.GetComponent<CanvasGroup>();
            so.FindProperty("_dimmedAlpha").floatValue = 0.75f;
            so.FindProperty("_chargeFill").objectReferenceValue = chargeFill;
            so.FindProperty("_frame").objectReferenceValue = frame;
            so.FindProperty("_fillEdge").objectReferenceValue = ert;
            so.FindProperty("_nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("_statusLabel").objectReferenceValue = status;
            so.FindProperty("_dugongHpFill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Image CreatePortrait(RectTransform window, string name, Sprite sprite, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(window, false);
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = tint;
            img.preserveAspect = false;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text CreateLabel(RectTransform parent, string name, string text, float size, TMP_FontAsset font, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.alignment = align;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Image CreateRadial(Transform parent, string name, Sprite sprite, Color color, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float inset = (1f - scale) * 0.5f;
            rt.anchorMin = new Vector2(inset, inset);
            rt.anchorMax = new Vector2(1f - inset, 1f - inset);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = false;
            img.fillAmount = 0f;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Landscape layout: HP bar bottom-centre with the stamina orbs directly under it, between the
        /// joystick (bottom-left) and the ability arc (bottom-right). Existing positions are kept.
        /// </summary>
        private static void BuildBottomCenter(Transform root)
        {
            DestroyChild(root, "DugongFatigue");
            DestroyChild(root, "HealthBar");
            DestroyChild(root, "StaminaProgress");
            DestroyChild(root, "StaminaOrbs");

            Sprite bar = Placeholder("ui_bar");
            const float width = 430f;
            Color lime = new Color(0.62f, 0.9f, 0.2f, 1f);

            // HP: thin lime bar with white bracket caps.
            var hp = NewRect(root, "HealthBar", new Vector2(0f, 84f), new Vector2(width, 8f));
            NewImage(hp, "Background", bar, new Color(0f, 0.04f, 0.08f, 0.55f), Image.Type.Simple);
            var ghost = NewImage(hp, "Ghost", bar, new Color(1f, 1f, 1f, 0.55f), Image.Type.Filled);
            var fill = NewImage(hp, "Fill", bar, lime, Image.Type.Filled);
            ghost.fillMethod = fill.fillMethod = Image.FillMethod.Horizontal;
            ghost.fillOrigin = fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            AddCaps(hp, bar, 16f, new Color(1f, 1f, 1f, 0.9f));

            var hpUi = hp.gameObject.AddComponent<HealthBarUI>();
            SetRef(hpUi, "_fill", fill);
            SetRef(hpUi, "_ghostFill", ghost);
            var hpSo = new SerializedObject(hpUi);
            hpSo.FindProperty("_normalColor").colorValue = lime;
            hpSo.FindProperty("_ghostColor").colorValue = new Color(1f, 1f, 1f, 0.55f);
            hpSo.ApplyModifiedPropertiesWithoutUndo();

            // Thin line under the HP bar: Riptide's remaining time. Hidden unless Riptide is active.
            DestroyChild(root, "RiptideTimer");
            var riptideLine = NewRect(root, "RiptideTimer", new Vector2(0f, 70f), new Vector2(width, 3f));
            NewImage(riptideLine, "Track", bar, new Color(0f, 0.04f, 0.08f, 0.6f), Image.Type.Simple);
            var riptideFill = NewImage(riptideLine, "Fill", bar, HudPalette.Riptide, Image.Type.Filled);
            riptideFill.fillMethod = Image.FillMethod.Horizontal;
            riptideFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            AddCaps(riptideLine, bar, 8f, new Color(1f, 1f, 1f, 0.6f));
            riptideLine.gameObject.AddComponent<CanvasGroup>();
            var riptideUi = riptideLine.gameObject.AddComponent<RiptideTimerUI>();
            SetRef(riptideUi, "_fill", riptideFill);

            // Seashells: fill up like the Mend card / Barrier, bright when ready to spend.
            const int shells = GameConstants.Abilities.MAX_STAMINA_ORBS;
            const float shellSize = 38f, spacing = 20f;
            var orbs = NewRect(root, "StaminaOrbs", new Vector2(0f, 18f), new Vector2(shells * shellSize + (shells - 1) * spacing, shellSize));
            var orbsUi = orbs.gameObject.AddComponent<StaminaOrbsUI>();
            SetRef(orbsUi, "_container", orbs);
            SetRef(orbsUi, "_shellSprite", Placeholder("shell"));
            SetRef(orbsUi, "_dotSprite", Placeholder("ui_circle"));
        }

        private static void AddCaps(RectTransform parent, Sprite sprite, float height, Color color)
        {
            foreach (float x in new[] { 0f, 1f })
            {
                var cap = NewImage(parent, x == 0f ? "CapLeft" : "CapRight", sprite, color, Image.Type.Simple);
                var crt = cap.rectTransform;
                crt.anchorMin = crt.anchorMax = new Vector2(x, 0.5f);
                crt.sizeDelta = new Vector2(2f, height);
                crt.anchoredPosition = new Vector2(x == 0f ? -3f : 3f, 0f);
            }
        }

        private static RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────────
        // 5. Region scene
        // ─────────────────────────────────────────────

        [MenuItem("Tools/Lalim/Abilities/5. Setup Active Region Scene", false, 105)]
        public static void SetupActiveRegionScene()
        {
            var scene = EditorSceneManager.GetActiveScene();

            // Managers.
            GameObject systems = GameObject.Find("[AbilitySystems]") ?? new GameObject("[AbilitySystems]");
            GetOrAdd<CheckpointManager>(systems);
            var tracker = GetOrAdd<ExplorationMetricsTracker>(systems);
            SetRef(tracker, "_config", AssetDatabase.LoadAssetAtPath<DDAConfig>(DDAConfigPath));

            PlayerController player = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

            // Spirit Sense now lives on the player prefab. A standalone copy would measure its radius
            // and compass from the wrong position and fight the player's for SpiritSense.Instance.
            foreach (SpiritSense sense in Object.FindObjectsByType<SpiritSense>(FindObjectsInactive.Include))
            {
                if (sense.GetComponent<PlayerController>() != null) continue;
                Debug.Log($"[AbilitySetup] Removing standalone SpiritSense on '{sense.gameObject.name}' (the player prefab has one).");
                GameObject host = sense.gameObject;
                Object.DestroyImmediate(sense);
                if (host.GetComponents<Component>().Length == 1 && host.transform.childCount == 0) Object.DestroyImmediate(host);
            }

            // Moon Tide was removed from the kit.
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (mb != null && mb.GetType().Name == "MoonTideAbility") Object.DestroyImmediate(mb);
            }

            // Dugong near the player spawn.
            if (Object.FindAnyObjectByType<DugongCompanion>(FindObjectsInactive.Include) == null)
            {
                var dugongPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DugongPrefabPath);
                if (dugongPrefab != null)
                {
                    var dugong = (GameObject)PrefabUtility.InstantiatePrefab(dugongPrefab, scene);
                    if (player != null)
                    {
                        dugong.transform.position = player.transform.position - player.transform.forward * 5f + player.transform.right * 2f;
                        dugong.transform.rotation = player.transform.rotation;
                    }
                }
            }

            // Checkpoints: one at the spawn, one at every zone entrance.
            var checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
            Transform folder = (GameObject.Find("Checkpoints") ?? new GameObject("Checkpoints")).transform;
            if (checkpointPrefab != null)
            {
                if (player != null) EnsureCheckpoint(checkpointPrefab, folder, "cp_spawn", "start", player.transform.position, player.transform.rotation);
                foreach (ZoneTrigger zone in Object.FindObjectsByType<ZoneTrigger>(FindObjectsInactive.Include))
                {
                    if (string.IsNullOrEmpty(zone.ZoneId)) continue;
                    // Offset a little toward the spawn so respawning doesn't drop the player inside the zone trigger.
                    Vector3 pos = zone.transform.position;
                    if (player != null) pos += (player.transform.position - pos).normalized * 8f;
                    EnsureCheckpoint(checkpointPrefab, folder, "cp_" + zone.ZoneId, zone.ZoneId, pos, zone.transform.rotation);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AbilitySetup] Region scene '{scene.name}' set up. Place PF_ToxicWaterVolume instances by hand " +
                      "(heavy ≤150m, light ≤250m, never inside non-moving minigames) with a checkpoint before each crossing.");
        }

        private static void EnsureCheckpoint(GameObject prefab, Transform folder, string id, string section, Vector3 position, Quaternion rotation)
        {
            foreach (Checkpoint existing in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include))
            {
                if (existing.CheckpointId == id) return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, folder);
            go.name = id;
            go.transform.SetPositionAndRotation(position, rotation);
            var so = new SerializedObject(go.GetComponent<Checkpoint>());
            so.FindProperty("_checkpointId").stringValue = id;
            so.FindProperty("_sectionId").stringValue = section;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
