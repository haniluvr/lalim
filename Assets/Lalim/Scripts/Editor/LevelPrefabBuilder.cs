using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Challenges;
using Lalim.Challenges.GhostNet;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Build Reusable Level Prefabs — saves the Level 1 building blocks as prefabs in
    /// Assets/Lalim/Prefabs/LevelFlow so Levels 2 and 3 can be assembled from them:
    ///
    ///   PF_LevelSystems         director, objective tracker, proximity music, pollution, banners, achievements
    ///   PF_Zone                 zone quest + toxic water volume (level-rate drain) + murk particles
    ///   PF_FoggyBarrier         the foggy-water barrier with its "Complete Zone N to pass through." message
    ///   PF_Station_*            area stations: water calibration, coral site prep, ghost net, placeholder
    ///   PF_LevelExit            end-of-level trigger (LevelExitStep wires it)
    ///   PF_DialogueSystem       dialogue box + Story Review + dialogue buttons + DialogueManager
    ///   PF_ToastSystem          achievement-title toast (ActToastUI) with its canvas
    ///   PF_QuestObjectiveUI     quest objective box
    ///
    /// Built from 02_Luzon (which must be open). Prefabs that already exist are kept, because
    /// they are the source of truth once made (scenes use linked instances, see LevelUIPrefabs);
    /// delete one to regenerate it. The scene itself is not modified.
    /// </summary>
    public static class LevelPrefabBuilder
    {
        private const string Folder = "Assets/Lalim/Prefabs/LevelFlow";
        private const string MusicPath = "Assets/NekoLegends/Music/PianoSoloSingles/Fairy Water Cave.wav";

        [MenuItem("Tools/Lalim/Build Reusable Level Prefabs")]
        public static void BuildFromMenu() => Debug.Log(Build());

        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            if (EditorSceneManager.GetActiveScene().name != GameConstants.Scenes.LUZON) return $"Open {GameConstants.Scenes.LUZON} first.";
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Lalim/Prefabs", "LevelFlow");

            BuildLevelSystems(report);
            BuildZone(report);
            BuildBarrier(report);
            BuildStations(report);
            BuildLevelExit(report);
            BuildDialogueSystem(report);
            if (!Exists("PF_ToastSystem", report)) CloneGroup($"{Folder}/PF_ToastSystem.prefab", "PF_ToastSystem", report, "ActToastCanvas", "[ActToastUI]");
            if (!Exists("PF_QuestObjectiveUI", report)) report.AppendLine(LevelUIPrefabs.BuildQuestPrefab());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report.ToString();
        }

        // ───────────────────────── Pieces ─────────────────────────

        private static void BuildLevelSystems(System.Text.StringBuilder report)
        {
            var root = new GameObject("[LevelSystems]");
            root.AddComponent<ObjectiveTracker>();
            var music = root.AddComponent<ProximityMusic>();
            var so = new SerializedObject(music);
            so.FindProperty("_defaultTrack").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<ZoneBannerUI>().EditorSetFont(AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset"));
            root.AddComponent<AchievementService>();
            root.AddComponent<LevelFlowDirector>();
            root.AddComponent<PollutionDirector>();
            Save(root, "PF_LevelSystems", report);
        }

        private static void BuildZone(System.Text.StringBuilder report)
        {
            var root = new GameObject("Zone");
            var zone = root.AddComponent<ZoneQuest>();

            var toxicGo = new GameObject("ToxicWater");
            toxicGo.transform.SetParent(root.transform, false);
            toxicGo.tag = GameConstants.Tags.TOXIC_WATER;
            var box = toxicGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(100f, 60f, 60f);
            var toxic = toxicGo.AddComponent<ToxicWaterVolume>();
            var tso = new SerializedObject(toxic);
            tso.FindProperty("_severity").enumValueIndex = (int)ToxicSeverity.Light;
            tso.FindProperty("_useLevelRate").boolValue = true;
            tso.FindProperty("_slowsStaminaRegen").boolValue = false;
            tso.ApplyModifiedPropertiesWithoutUndo();

            Transform murk = FindInScene("[Level1Flow]/Zone1_Amansinaya/Murk");
            if (murk != null)
            {
                GameObject m = Object.Instantiate(murk.gameObject, root.transform);
                m.name = "Murk";
                m.transform.localPosition = Vector3.zero;
            }

            zone.EditorSetup("zone_id", "Zone Name", Region.Luzon, null, toxic, new List<QuestStep>());
            Save(root, "PF_Zone", report);
        }

        private static void BuildBarrier(System.Text.StringBuilder report)
        {
            // Zone2_Barrier is the reference barrier (with its label); every level reuses its prefab.
            Transform src = FindInScene("Zone2_Barrier");
            if (src == null) { report.AppendLine("No Zone2_Barrier in scene to copy."); return; }
            GameObject b = Object.Instantiate(src.gameObject);
            b.name = "PF_FoggyBarrier";
            b.transform.position = Vector3.zero;
            var note = b.GetComponent<BarrierNotification>();
            if (note != null) note.Message = "Complete the previous zone to pass through.";
            b.SetActive(true);
            Save(b, "PF_FoggyBarrier", report);
        }

        private static void BuildStations(System.Text.StringBuilder report)
        {
            StationFrom("[Level1Flow]/Zone2_Haik/02_WaterReadings/Water_Area_A", "PF_Station_WaterCalibration", report, null);
            StationFrom("[Level1Flow]/Zone2_Haik/05_CoralReplanting/Coral_Area_A", "PF_Station_CoralSitePrep", report, null);
            StationFrom("[Level1Flow]/Zone3_Neguno/02_GhostNets/Net_Area_A", "PF_Station_GhostNet", report, go =>
            {
                // Scene props belong to Level 1's set dressing.
                var net = go.GetComponent<GhostNetMinigame>();
                if (net != null)
                {
                    var so = new SerializedObject(net);
                    so.FindProperty("_sceneProps").ClearArray();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            });
            StationFrom("[Level1Flow]/Zone2_Haik/02_WaterReadings/Water_Area_A", "PF_Station_Placeholder", report, go =>
            {
                Object.DestroyImmediate(go.GetComponent<WaterCalibrationMinigame>());
                go.AddComponent<PlaceholderAreaMinigame>();
            });
        }

        private static void StationFrom(string path, string prefabName, System.Text.StringBuilder report, System.Action<GameObject> adjust)
        {
            Transform src = FindInScene(path);
            if (src == null) { report.AppendLine($"Missing {path}."); return; }
            GameObject go = Object.Instantiate(src.gameObject);
            go.name = prefabName;
            go.transform.position = Vector3.zero;
            adjust?.Invoke(go);
            Save(go, prefabName, report);
        }

        private static void BuildLevelExit(System.Text.StringBuilder report)
        {
            var go = new GameObject("PF_LevelExit");
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(18f, 10f, 18f);
            go.AddComponent<LevelExitTrigger>();
            Save(go, "PF_LevelExit", report);
        }

        private static void BuildDialogueSystem(System.Text.StringBuilder report)
        {
            if (Exists("PF_DialogueSystem", report)) return;
            var go = new GameObject("PF_DialogueSystem", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;
            var dm = go.AddComponent<DialogueManager>();
            DialogueUIBuilder.Build(go.transform, dm);
            Save(go, "PF_DialogueSystem", report);
        }

        // ───────────────────────── Helpers ─────────────────────────

        /// <summary>Copies several scene roots under one prefab root and re-points references between them.</summary>
        private static void CloneGroup(string path, string rootName, System.Text.StringBuilder report, params string[] sceneRoots)
        {
            var root = new GameObject(rootName);
            var map = new Dictionary<Object, Object>();
            foreach (string name in sceneRoots)
            {
                Transform src = FindInScene(name);
                if (src == null) { report.AppendLine($"{rootName}: missing {name}."); continue; }
                GameObject clone = Object.Instantiate(src.gameObject, root.transform);
                clone.name = src.name;
                MapHierarchy(src, clone.transform, map);
            }

            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;
                var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    if (map.TryGetValue(it.objectReferenceValue, out Object replacement))
                    {
                        it.objectReferenceValue = replacement;
                        changed = true;
                    }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.AppendLine($"Saved {System.IO.Path.GetFileName(path)}.");
        }

        private static void MapHierarchy(Transform original, Transform clone, Dictionary<Object, Object> map)
        {
            map[original.gameObject] = clone.gameObject;
            Component[] a = original.GetComponents<Component>();
            Component[] b = clone.GetComponents<Component>();
            for (int i = 0; i < a.Length && i < b.Length; i++) if (a[i] != null) map[a[i]] = b[i];
            for (int i = 0; i < original.childCount && i < clone.childCount; i++) MapHierarchy(original.GetChild(i), clone.GetChild(i), map);
        }

        private static bool Exists(string name, System.Text.StringBuilder report)
        {
            if (!System.IO.File.Exists($"{Folder}/{name}.prefab")) return false;
            report.AppendLine($"Kept existing {name}.prefab");
            return true;
        }

        private static void Save(GameObject go, string name, System.Text.StringBuilder report)
        {
            if (Exists(name, report)) { Object.DestroyImmediate(go); return; }
            PrefabUtility.SaveAsPrefabAsset(go, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            report.AppendLine($"Saved {name}.prefab");
        }

        private static Transform FindInScene(string path)
        {
            GameObject go = GameObject.Find(path);
            if (go != null) return go.transform;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == path) return root.transform;
                Transform t = root.transform.Find(path);
                if (t != null) return t;
                int slash = path.IndexOf('/');
                if (slash > 0 && root.name == path.Substring(0, slash))
                {
                    t = root.transform.Find(path.Substring(slash + 1));
                    if (t != null) return t;
                }
            }
            return null;
        }
    }
}
