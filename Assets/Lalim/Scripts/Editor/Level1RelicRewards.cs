using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.EditorTools
{
    /// <summary>
    /// The relic gift after each zone's last minigame (RelicRewardStep), placed just before the
    /// zone's closing dialogue:
    ///
    ///   Zone 1  … Sorting       → 04_RelicGift (Radiant Conch)      → 05_Closing → 06_WaterKit → …
    ///   Zone 2  … CoralReplant  → 06_RelicGift (Compass)            → 07_Closing → 08_Cleanse → …
    ///   Zone 3  … GhostNets     → 03_RelicGift (Woven Net Fragment) → 04_Closing → 05_Cleanse → …
    ///
    /// Tools ▸ Lalim ▸ Level 1 ▸ Add Relic Rewards adds them to the open 02_Luzon without touching
    /// anything else; Level1SceneBuilder uses the same pieces. The thank-you lines moved from each
    /// closing into the gift, so a closing still starting with them is trimmed (hand edits kept).
    /// </summary>
    public static class Level1RelicRewards
    {
        public struct ZoneRelic
        {
            public string ZoneId;
            public string Deity;
            public string StepName;
            public string StepId;
            public string Objective;
            public int RelicIndex;   // into RelicAssetBuilder.All
            public int RevealLine;
            public string[][] Renames;   // old → new step object names, applied in order
        }

        public static readonly ZoneRelic[] Zones =
        {
            new ZoneRelic
            {
                ZoneId = GameConstants.Zones.LUZON_BLEACHED_REEF, Deity = Level1DialogueScripts.Amansinaya,
                StepName = "04_RelicGift", StepId = "luzon_z1_relic", Objective = "Return to Amansinaya.",
                RelicIndex = 0, RevealLine = Level1DialogueScripts.AmansinayaRelicLine,
                Renames = new[]
                {
                    new[] { "07_Gate", "08_Gate" }, new[] { "06_Cleanse", "07_Cleanse" },
                    new[] { "05_WaterKit", "06_WaterKit" }, new[] { "04_Closing", "05_Closing" },
                },
            },
            new ZoneRelic
            {
                ZoneId = GameConstants.Zones.LUZON_TOXIN_SITES, Deity = Level1DialogueScripts.Haik,
                StepName = "06_RelicGift", StepId = "luzon_z2_relic", Objective = "Return to Haik.",
                RelicIndex = 1, RevealLine = Level1DialogueScripts.HaikRelicLine,
                Renames = new[]
                {
                    new[] { "08_Gate", "09_Gate" }, new[] { "07_Cleanse", "08_Cleanse" }, new[] { "06_Closing", "07_Closing" },
                },
            },
            new ZoneRelic
            {
                ZoneId = GameConstants.Zones.LUZON_CORAL_BED, Deity = Level1DialogueScripts.Neguno,
                StepName = "03_RelicGift", StepId = "luzon_z3_relic", Objective = "Return to Neguno.",
                RelicIndex = 2, RevealLine = Level1DialogueScripts.NegunoRelicLine,
                Renames = new[]
                {
                    new[] { "06_Exit", "07_Exit" }, new[] { "05_Tunnel", "06_Tunnel" },
                    new[] { "04_Cleanse", "05_Cleanse" }, new[] { "03_Closing", "04_Closing" },
                },
            },
        };

        // Closing lines that moved into the gift (removed) or were reworded (replaced).
        private static readonly Dictionary<string, string> ClosingEdits = new Dictionary<string, string>
        {
            { "Do you feel it? My shallows taste sweet again.", null },
            { "Take this. It reads the water's hidden moods: its sourness, its salt, its warmth.",
              "Take this as well. It reads the water's hidden moods: its sourness, its salt, its warmth." },
            { "The young coral holds. One day fish will shelter here again, and sailors will follow them home.", null },
            { "Free. Every one of them. You came to give, not to take. That is rarer than pearls.", null },
        };

        [MenuItem("Tools/Lalim/Level 1/Add Relic Rewards")]
        public static void InstallMenu() => Debug.Log(InstallInOpenScene());

        public static string InstallInOpenScene()
        {
            var report = new System.Text.StringBuilder();
            foreach (ZoneQuest zone in Object.FindObjectsByType<ZoneQuest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (ZoneRelic z in Zones)
                {
                    if (zone.ZoneId != z.ZoneId) continue;
                    RenameSteps(zone, z);
                    EnsureGiftLines(zone.Statue, report);
                    RelicRewardStep step = EnsureStep(zone, z);
                    InsertBeforeClosing(zone, step, report);
                }
            }
            if (report.Length == 0) return "Relic rewards already installed.";
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return report.ToString();
        }

        /// <summary>Renumbers the zone's later steps to make room for the gift.</summary>
        public static void RenameSteps(ZoneQuest zone, ZoneRelic z)
        {
            foreach (string[] pair in z.Renames)
            {
                Transform from = zone.transform.Find(pair[0]);
                if (from != null && zone.transform.Find(pair[1]) == null) from.name = pair[1];
            }
        }

        /// <summary>Adds the "relic_gift" lines if missing and trims the lines that moved out of the closing.</summary>
        public static void EnsureGiftLines(StatueDialogueRouter statue, System.Text.StringBuilder report)
        {
            if (statue == null) return;
            List<DialogueLine> gift = GiftLinesFor(statue.DeityName);
            if (gift == null) return;

            if (statue.GetBark(RelicRewardStep.GiftBarkKey) == null)
            {
                Undo.RecordObject(statue, "Add relic gift lines");
                statue.EditorSetBark(RelicRewardStep.GiftBarkKey, gift);
                report.AppendLine($"Added {statue.DeityName}'s relic gift lines.");
            }

            var so = new SerializedObject(statue);
            SerializedProperty closing = so.FindProperty("_closingLines");
            int edits = 0;
            for (int i = closing.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty text = closing.GetArrayElementAtIndex(i).FindPropertyRelative("text");
                string replacement;
                if (!ClosingEdits.TryGetValue(text.stringValue, out replacement)) continue;
                if (replacement == null) closing.DeleteArrayElementAtIndex(i);
                else text.stringValue = replacement;
                edits++;
            }
            if (edits > 0)
            {
                so.ApplyModifiedProperties();
                report.AppendLine($"Updated {edits} line(s) of {statue.DeityName}'s closing.");
            }
            EditorUtility.SetDirty(statue);
        }

        public static List<DialogueLine> GiftLinesFor(string deity)
        {
            if (deity == Level1DialogueScripts.Amansinaya) return Level1DialogueScripts.AmansinayaRelicGift();
            if (deity == Level1DialogueScripts.Haik) return Level1DialogueScripts.HaikRelicGift();
            if (deity == Level1DialogueScripts.Neguno) return Level1DialogueScripts.NegunoRelicGift();
            return null;
        }

        /// <summary>The zone's RelicRewardStep object, created if missing and pointed at its relic.</summary>
        public static RelicRewardStep EnsureStep(ZoneQuest zone, ZoneRelic z)
        {
            Transform t = zone.transform.Find(z.StepName);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(z.StepName);
                Undo.RegisterCreatedObjectUndo(go, "Add relic gift step");
                go.transform.SetParent(zone.transform, false);
            }

            RelicRewardStep step = go.GetComponent<RelicRewardStep>();
            bool fresh = step == null;
            if (fresh) step = go.AddComponent<RelicRewardStep>();
            if (string.IsNullOrEmpty(step.StepId)) step.EditorSetup(z.StepId, z.Objective);

            RelicAssetBuilder.Relic relic = RelicAssetBuilder.All[z.RelicIndex];
            var so = new SerializedObject(step);
            if (fresh || so.FindProperty("_relicPrefab").objectReferenceValue == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RelicAssetBuilder.PrefabPath(relic));
                step.EditorSetupRelic(relic.ItemId, prefab, relic.DisplayName, z.RevealLine);
            }
            step.EditorSetupLook(RelicLook());
            EditorUtility.SetDirty(step);
            return step;
        }

        private const string HaloTexturePath = "Assets/Lalim/Textures/Sorting/T_SortHalo.png";

        /// <summary>
        /// The relic's glow materials as assets (so their transparent/additive shader variants ship):
        /// an additive halo, additive sparkles and a see-through Lit material to fade in with.
        /// </summary>
        public static RelicPresentation.Look RelicLook()
        {
            var halo = AssetDatabase.LoadAssetAtPath<Texture2D>(HaloTexturePath);
            return new RelicPresentation.Look
            {
                halo = Transparent("Assets/Lalim/Materials/M_RelicHalo.mat", "Universal Render Pipeline/Unlit", halo, true),
                sparkle = Transparent("Assets/Lalim/Materials/M_RelicSparkle.mat", "Universal Render Pipeline/Particles/Unlit", halo, true),
                fade = Transparent("Assets/Lalim/Materials/M_RelicFade.mat", "Universal Render Pipeline/Lit", null, false),
            };
        }

        private static Material Transparent(string path, string shader, Texture2D texture, bool additive)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(1f, 0.86f, 0.55f, 1f));
            if (texture != null) m.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Puts the gift right before the zone's closing dialogue (or after its last minigame).</summary>
        private static void InsertBeforeClosing(ZoneQuest zone, RelicRewardStep step, System.Text.StringBuilder report)
        {
            var steps = new List<QuestStep>(zone.Steps);
            if (steps.Contains(step)) return;

            int at = -1;
            for (int i = 0; i < steps.Count && at < 0; i++)
            {
                var d = steps[i] as DialogueStep;
                if (d != null && new SerializedObject(d).FindProperty("_kind").enumValueIndex == (int)DialogueStep.Kind.Closing) at = i;
            }
            if (at < 0)
            {
                for (int i = 0; i < steps.Count; i++) if (steps[i] is AreaMinigameStep || steps[i] is SegregationStep) at = i + 1;
            }
            if (at < 0) at = steps.Count;
            steps.Insert(at, step);

            Undo.RecordObject(zone, "Insert relic gift step");
            zone.EditorSetup(zone.ZoneId, zone.DisplayName, zone.Region, zone.Statue, zone.ToxicVolume, steps);
            EditorUtility.SetDirty(zone);
            report.AppendLine($"{zone.DisplayName}: added {step.name} (step {at + 1}).");
        }
    }
}
