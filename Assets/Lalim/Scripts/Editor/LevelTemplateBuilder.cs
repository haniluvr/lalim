using System.Collections.Generic;
using System.IO;
using Lalim.Abilities;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Everything a new level needs, as prefabs, plus a ready-made template scene.
    ///
    /// Tools ▸ Lalim ▸ Level Template ▸
    ///   1. Build Template Prefabs        (02_Luzon open) copies Luzon's pieces into prefabs:
    ///        Prefabs/Core/            one prefab per manager (they stay separate scene roots:
    ///                                 each one keeps itself across scene loads)
    ///        Prefabs/LevelTemplate/   sun light, map bounds, sorting altar + station, tunnel
    ///                                 darkness, guardian statue, and the zone templates:
    ///          PF_ZoneTemplate        a zone (variant of PF_Zone) with its guardian statue, a
    ///                                 checkpoint, its gate barrier and the usual step chain wired:
    ///                                 Talk → Minigame (placeholder station) → Report back →
    ///                                 Relic gift → Closing → Cleanse → Gate
    ///          PF_ZoneTemplate_Final  the same, ending in the tunnel barrier and the level exit
    ///   2. Create Template Scene         Scenes/Levels/_Template/00_LevelTemplate.unity, every
    ///                                 piece placed and wired on a plain seabed. Duplicate it for
    ///                                 a new level, swap the seabed for the level's environment,
    ///                                 then run Fix Up Level IDs (and set the region on [LevelFlow]).
    ///   Add Missing Level Pieces To Open Scene
    ///                                 the same set-up added to an existing level scene (e.g.
    ///                                 03_Visayas): only what's missing, found by type or name,
    ///                                 so the scene's own objects and edits are left alone.
    ///   Fix Up Level IDs                 zone, step and checkpoint IDs from the scene's region
    ///                                 (they're save keys, so every level needs its own).
    ///
    /// Existing prefabs are kept (they're the source of truth once made): delete one to rebuild it.
    /// </summary>
    public static class LevelTemplateBuilder
    {
        private const string CoreFolder = "Assets/Lalim/Prefabs/Core";
        private const string TemplateFolder = "Assets/Lalim/Prefabs/LevelTemplate";
        private const string FlowFolder = "Assets/Lalim/Prefabs/LevelFlow";
        private const string TemplateScenePath = "Assets/Lalim/Scenes/Levels/_Template/00_LevelTemplate.unity";

        private const string PlayerPrefab = "Assets/Lalim/Prefabs/Player_final_final.prefab";
        private const string CameraPrefab = "Assets/Lalim/Prefabs/PF_GameCamera.prefab";
        private const string DugongPrefab = "Assets/Lalim/Prefabs/Characters/PF_Dugong.prefab";
        private const string HudPrefab = "Assets/Lalim/Prefabs/UI/GameHUD.prefab";
        private const string CheckpointPrefab = "Assets/Lalim/Prefabs/Hazards/PF_Checkpoint.prefab";
        private const string PlanktonPrefab = "Assets/Lalim/Prefabs/Environment/PF_PlanktonParticles.prefab";
        private const string EffectsPrefab = "Assets/Lalim/Prefabs/Environment/PF_UnderwaterEffectsVolume.prefab";

        private const float ZoneSpacing = 120f;   // template layout: zones in a row along +Z

        // Luzon's managers: scene root name → prefab name.
        private static readonly string[,] Managers =
        {
            { "[GameManager]", "PF_GameManager" },
            { "[ChallengeManager]", "PF_ChallengeManager" },
            { "[InputManager]", "PF_InputManager" },
            { "[InventoryManager]", "PF_InventoryManager" },
            { "[RegionProgressTracker]", "PF_RegionProgressTracker" },
            { "[SaveManager]", "PF_SaveManager" },
            { "[AudioNavigationManager]", "PF_AudioNavigationManager" },
            { "[DDAManager]", "PF_DDAManager" },
            { "[AbilitySystems]", "PF_AbilitySystems" },
            { "EventSystem", "PF_EventSystem" },
        };

        // ═════════════════════════ 1. Prefabs ═════════════════════════

        [MenuItem("Tools/Lalim/Level Template/1. Build Template Prefabs (open 02_Luzon)")]
        public static void BuildPrefabsMenu() => Debug.Log("[LevelTemplate]\n" + BuildPrefabs());

        public static string BuildPrefabs()
        {
            var report = new System.Text.StringBuilder();
            if (SceneManager.GetActiveScene().name != GameConstants.Scenes.LUZON) return $"Open {GameConstants.Scenes.LUZON} first.";
            EnsureFolder(CoreFolder);
            EnsureFolder(TemplateFolder);

            for (int i = 0; i < Managers.GetLength(0); i++)
                CopyRoot(Managers[i, 0], $"{CoreFolder}/{Managers[i, 1]}.prefab", report, null);

            CopyRoot("Directional Light", $"{TemplateFolder}/PF_SunLight.prefab", report, null);
            CopyRoot("MapBounds", $"{TemplateFolder}/PF_MapBounds.prefab", report, null);
            CopyRoot("Altar_AmanSinaya", $"{TemplateFolder}/PF_SortingAltar.prefab", report, go =>
            {
                go.name = "SortingAltar";
                ClearSceneRefs(go);
            });
            SortingStation station = Object.FindFirstObjectByType<SortingStation>(FindObjectsInactive.Include);
            if (station != null) CopyObject(station.gameObject, $"{TemplateFolder}/PF_SortStation.prefab", report, null);
            CopyRoot("[Level1Flow]/TunnelEntrance", $"{TemplateFolder}/PF_TunnelEntrance.prefab", report, ClearSceneRefs);

            BuildGuardian(report);
            BuildZoneTemplate(false, report);
            BuildZoneTemplate(true, report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report.ToString();
        }

        /// <summary>
        /// A stand-in deity statue: a stone capsule that talks, with its conversation framing
        /// (where the diver and dugong float, and the fixed 3/4 camera) beside it. Swap the capsule
        /// for the real statue model; the dialogue lines are placeholders to replace in its inspector.
        /// </summary>
        private static void BuildGuardian(System.Text.StringBuilder report)
        {
            string path = $"{TemplateFolder}/PF_GuardianStatue.prefab";
            if (Kept(path, report)) return;

            var root = new GameObject("Guardian");
            GameObject statue = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            statue.name = "Statue";
            statue.transform.SetParent(root.transform, false);
            statue.transform.localScale = new Vector3(1.6f, 2.2f, 1.6f);
            statue.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            statue.GetComponent<Renderer>().sharedMaterial = PlaceholderMaterial();

            // The diver floats in front (+Z) at face height, the dugong beside, the camera off to the side.
            Transform talk = Child(root.transform, "TalkPoint", new Vector3(0f, 3.4f, 3.4f));
            talk.rotation = Quaternion.LookRotation(Vector3.back);
            Transform dugong = Child(root.transform, "TalkDugong", new Vector3(1.9f, 3.0f, 4.3f));
            dugong.rotation = Quaternion.LookRotation(Vector3.back);
            Transform cam = Child(root.transform, "TalkCamera", new Vector3(-3.6f, 4.0f, 7.4f));
            cam.rotation = Quaternion.LookRotation(new Vector3(0.6f, 3.6f, 2.2f) - cam.localPosition);

            var router = statue.AddComponent<StatueDialogueRouter>();
            router.EditorReplaceScripts("Guardian",
                Lines("(The guardian gives the zone's quest — replace these lines on the Statue.)"),
                Lines("(Closing words once the zone is restored.)"),
                Lines("(What the guardian says when talked to again.)"),
                new List<StatueDialogueRouter.NamedLines>
                {
                    new StatueDialogueRouter.NamedLines { key = ReportKey, lines = Lines("(The diver reports back after the minigame.)") },
                    new StatueDialogueRouter.NamedLines { key = RelicRewardStep.GiftBarkKey, lines = Lines("(The guardian offers its relic.)", "(Take it, young diver.)") },
                });
            router.EditorSetTalkFraming(talk, dugong, cam);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            report.AppendLine("Saved PF_GuardianStatue.prefab");
        }

        private const string ReportKey = "report";

        /// <summary>
        /// A whole zone (a variant of PF_Zone): its guardian, a checkpoint, the step chain, and
        /// either its gate barrier or (final zone) the tunnel barrier and the level exit.
        /// </summary>
        private static void BuildZoneTemplate(bool final, System.Text.StringBuilder report)
        {
            string name = final ? "PF_ZoneTemplate_Final" : "PF_ZoneTemplate";
            string path = $"{TemplateFolder}/{name}.prefab";
            if (Kept(path, report)) return;

            var zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FlowFolder}/PF_Zone.prefab");
            var guardianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TemplateFolder}/PF_GuardianStatue.prefab");
            var stationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FlowFolder}/PF_Station_Placeholder.prefab");
            var barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FlowFolder}/PF_FoggyBarrier.prefab");
            var checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefab);
            if (zonePrefab == null || guardianPrefab == null || stationPrefab == null || barrierPrefab == null || checkpointPrefab == null)
            {
                report.AppendLine($"{name}: missing PF_Zone / PF_GuardianStatue / PF_Station_Placeholder / PF_FoggyBarrier / PF_Checkpoint.");
                return;
            }

            var zoneGo = (GameObject)PrefabUtility.InstantiatePrefab(zonePrefab);
            zoneGo.name = "Zone";
            Transform zone = zoneGo.transform;

            var guardian = (GameObject)PrefabUtility.InstantiatePrefab(guardianPrefab, zone);
            guardian.name = "Guardian";
            StatueDialogueRouter statue = guardian.GetComponentInChildren<StatueDialogueRouter>();

            var checkpoint = (GameObject)PrefabUtility.InstantiatePrefab(checkpointPrefab, zone);
            checkpoint.name = "Checkpoint_Guardian";
            checkpoint.transform.localPosition = new Vector3(0f, 3f, 9f);
            SetString(checkpoint.GetComponent<Checkpoint>(), "_checkpointId", "cp_zone_guardian");

            var steps = new List<QuestStep>();
            T Step<T>(string goName, string id, string objective) where T : QuestStep
            {
                var go = new GameObject(goName);
                go.transform.SetParent(zone, false);
                var step = go.AddComponent<T>();
                step.EditorSetup(id, objective);
                steps.Add(step);
                return step;
            }

            Step<TalkStep>("01_Talk", "zone_talk", "Swim to the guardian's statue.");

            var minigame = Step<AreaMinigameStep>("02_Minigame", "zone_minigame", "Finish the task at every site.");
            var station = (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab, minigame.transform);
            station.name = "Site_A";
            station.transform.localPosition = new Vector3(14f, 0f, 16f);
            minigame.EditorSetupStations(new List<AreaStation> { station.GetComponent<AreaStation>() });

            Step<DialogueStep>("03_Report", "zone_report", "Return to the guardian.").EditorSetupDialogue(DialogueStep.Kind.Talk, ReportKey);
            Step<RelicRewardStep>("04_RelicGift", "zone_relic", "Return to the guardian.").EditorSetupRelic("", null, "", 1);
            Step<DialogueStep>("05_Closing", "zone_closing", "").EditorSetupDialogue(DialogueStep.Kind.Closing, "");
            Step<CleanseZoneStep>("06_Cleanse", "zone_cleanse", "").EditorSetupToast("Quest Complete", "The zone is clean again");

            var barrier = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab, zone);
            barrier.name = final ? "Tunnel_Barrier" : "Gate_Barrier";
            barrier.transform.localPosition = new Vector3(0f, 0f, 50f);
            Step<UnlockBarrierStep>(final ? "07_Tunnel" : "07_Gate", final ? "zone_tunnel" : "zone_gate", "").EditorSetupBarrier(barrier, null);

            if (final)
            {
                var tunnelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TemplateFolder}/PF_TunnelEntrance.prefab");
                Transform entrance = tunnelPrefab != null
                    ? ((GameObject)PrefabUtility.InstantiatePrefab(tunnelPrefab, zone)).transform
                    : new GameObject().transform;
                entrance.SetParent(zone, false);
                entrance.name = "TunnelEntrance";
                entrance.localPosition = new Vector3(0f, 0f, 60f);

                var exit = new GameObject("LevelExit_Trigger");
                exit.transform.SetParent(zone, false);
                exit.transform.localPosition = new Vector3(0f, -10f, 75f);
                var box = exit.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(12f, 8f, 12f);

                Step<LevelExitStep>("08_Exit", "zone_exit", "Swim down the deep path.").EditorSetupExit(entrance, box);
            }

            ZoneQuest quest = zoneGo.GetComponent<ZoneQuest>();
            var qso = new SerializedObject(quest);
            var toxic = (ToxicWaterVolume)qso.FindProperty("_toxicVolume").objectReferenceValue;
            if (toxic == null) toxic = zoneGo.GetComponentInChildren<ToxicWaterVolume>();
            quest.EditorSetup("zone_id", "Zone Name", Region.Visayas, statue, toxic, steps);

            PrefabUtility.SaveAsPrefabAsset(zoneGo, path);
            Object.DestroyImmediate(zoneGo);
            report.AppendLine($"Saved {name}.prefab ({steps.Count} steps, variant of PF_Zone).");
        }

        // ═════════════════════════ 2. Template scene ═════════════════════════

        [MenuItem("Tools/Lalim/Level Template/2. Create Template Scene")]
        public static void CreateTemplateSceneMenu() => Debug.Log("[LevelTemplate]\n" + CreateTemplateScene());

        public static string CreateTemplateScene()
        {
            if (File.Exists(TemplateScenePath)) return $"{TemplateScenePath} already exists — delete it to rebuild.";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "Cancelled.";
            EnsureFolder(Path.GetDirectoryName(TemplateScenePath).Replace('\\', '/'));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var seabed = GameObject.CreatePrimitive(PrimitiveType.Plane);
            seabed.name = "ENVIRONMENT (replace with the level's model)";
            seabed.transform.position = new Vector3(0f, 0f, ZoneSpacing);
            seabed.transform.localScale = new Vector3(40f, 1f, 60f);
            seabed.isStatic = true;

            // IDs stay neutral (zone_…): a level made from this copy gets its own with Fix Up Level IDs.
            string report = Install(Region.Visayas, "Level Template", nameIds: false);
            EditorSceneManager.SaveScene(scene, TemplateScenePath);
            return $"Created {TemplateScenePath}.\n" + report;
        }

        // ═════════════════════════ Install into a level scene ═════════════════════════

        [MenuItem("Tools/Lalim/Level Template/Add Missing Level Pieces To Open Scene")]
        public static void InstallMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Region region = RegionOf(scene.name);
            if (!EditorUtility.DisplayDialog("Add level pieces",
                    $"Add whatever is missing (managers, player, camera, dugong, UI, level flow with 3 template zones, …) to {scene.name} as a {region} level?\n\nExisting objects are left alone.",
                    "Add", "Cancel")) return;
            Debug.Log("[LevelTemplate]\n" + Install(region, LevelName(region), nameIds: true));
        }

        [MenuItem("Tools/Lalim/Level Template/Fix Up Level IDs In Open Scene")]
        public static void FixIdsMenu() => Debug.Log("[LevelTemplate]\n" + FixIds(RegionOf(SceneManager.GetActiveScene().name)));

        /// <summary>Adds only what the open scene lacks, then wires it together.</summary>
        public static string Install(Region region, string levelName, bool nameIds)
        {
            var report = new System.Text.StringBuilder();
            Scene scene = SceneManager.GetActiveScene();

            // Core managers: one root each.
            for (int i = 0; i < Managers.GetLength(0); i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CoreFolder}/{Managers[i, 1]}.prefab");
                if (prefab == null) { report.AppendLine($"Missing {Managers[i, 1]} — run step 1 first."); continue; }
                if (SceneHas(prefab)) continue;
                Place(prefab, null, Managers[i, 0], Vector3.zero, report);
            }

            // Light, camera, player, dugong.
            Light sun = FirstOfType<Light>(l => l.type == LightType.Directional);
            if (sun == null) sun = Place(Load($"{TemplateFolder}/PF_SunLight.prefab"), null, "Directional Light", Vector3.zero, report)?.GetComponent<Light>();

            Vector3 spawn = new Vector3(0f, 6f, -20f);
            PlayerController player = FirstOfType<PlayerController>();
            if (player == null) player = Place(Load(PlayerPrefab), null, "Player", spawn, report)?.GetComponent<PlayerController>();

            CameraController camera = FirstOfType<CameraController>();
            if (camera == null)
            {
                // A plain camera from the empty scene would fight the game camera for "MainCamera".
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!c.CompareTag("MainCamera")) continue;
                    Undo.RecordObject(c.gameObject, "Disable old camera");
                    c.gameObject.SetActive(false);
                    report.AppendLine($"Switched off the old camera '{c.name}' (the game camera replaces it).");
                }
                camera = Place(Load(CameraPrefab), null, "Main Camera", spawn + new Vector3(0f, 1f, -3f), report)?.GetComponent<CameraController>();
            }
            if (camera != null && player != null) SetRef(camera, "_playerTarget", player.transform, onlyIfEmpty: true);

            if (FirstOfType<DugongCompanion>() == null) Place(Load(DugongPrefab), null, "PF_Dugong", spawn + new Vector3(1.5f, 0f, -1f), report);

            // UI.
            if (FirstOfType<HUDController>() == null) Place(Load(HudPrefab), null, "GameHUD", Vector3.zero, report);
            if (FirstOfType<DialogueManager>() == null) Place(Load($"{FlowFolder}/PF_DialogueSystem.prefab"), null, "PF_DialogueSystem", Vector3.zero, report);
            if (FirstOfType<QuestObjectiveUI>() == null) Place(Load($"{FlowFolder}/PF_QuestObjectiveUI.prefab"), null, "PF_QuestObjectiveUI", Vector3.zero, report);
            if (FirstOfType<ActToastUI>() == null) Place(Load($"{FlowFolder}/PF_ToastSystem.prefab"), null, "PF_ToastSystem", Vector3.zero, report);

            // Water look.
            if (FirstOfType<FollowMainCamera>() == null) Place(Load(PlanktonPrefab), null, "PF_PlanktonParticles", Vector3.zero, report);
            if (FirstOfType<UnderwaterDepthFog>() == null) Place(Load(EffectsPrefab), null, "PF_UnderwaterEffectsVolume", Vector3.zero, report);
            if (FirstOfType<UnderwaterBounds>() == null) Place(Load($"{TemplateFolder}/PF_MapBounds.prefab"), null, "MapBounds", Vector3.zero, report);

            // Checkpoints: the spawn.
            Transform checkpoints = RootNamed("Checkpoints") ?? NewRoot("Checkpoints", report);
            if (checkpoints.Find("cp_spawn") == null)
            {
                GameObject cp = Place(Load(CheckpointPrefab), checkpoints, "cp_spawn", player != null ? player.transform.position : spawn, report);
                if (cp != null) SetString(cp.GetComponent<Checkpoint>(), "_checkpointId", "cp_spawn");
            }

            // Level flow: director + three zones (gate → gate → tunnel and exit).
            InstallFlow(region, levelName, player, sun, report);

            // A little life.
            Transform life = RootNamed("[MarineLife]");
            if (life == null)
            {
                life = NewRoot("[MarineLife]", report);
                string[] creatures = { "PF_School_ClownFish", "PF_School_BlueTang", "PF_Wanderer_Turtle", "PF_School_ReefFish", "PF_School_Tuna", "PF_Wanderer_StingRay" };
                for (int i = 0; i < creatures.Length; i++)
                {
                    GameObject prefab = Load($"Assets/Lalim/Prefabs/Creatures/{creatures[i]}.prefab");
                    if (prefab == null) continue;
                    float z = (i / 2) * ZoneSpacing + 18f;
                    Place(prefab, life, prefab.name, new Vector3(i % 2 == 0 ? -16f : 16f, 6f, z), null);
                }
            }

            if (nameIds) report.Append(FixIds(region));
            EditorSceneManager.MarkSceneDirty(scene);
            return report.ToString();
        }

        private static void InstallFlow(Region region, string levelName, PlayerController player, Light sun, System.Text.StringBuilder report)
        {
            LevelFlowDirector director = FirstOfType<LevelFlowDirector>();
            if (director != null) { report.AppendLine($"Kept the scene's level flow ({director.name})."); return; }

            GameObject systems = Load($"{FlowFolder}/PF_LevelSystems.prefab");
            GameObject zonePrefab = Load($"{TemplateFolder}/PF_ZoneTemplate.prefab");
            GameObject finalPrefab = Load($"{TemplateFolder}/PF_ZoneTemplate_Final.prefab");
            if (systems == null || zonePrefab == null || finalPrefab == null) { report.AppendLine("Missing PF_LevelSystems or the zone templates — run step 1 first."); return; }

            GameObject flow = Place(systems, null, "[LevelFlow]", Vector3.zero, report);
            director = flow.GetComponent<LevelFlowDirector>();
            string[] ids = GameConstants.Zones.GetZonesForRegion(region);
            var zones = new List<ZoneQuest>();
            for (int i = 0; i < 3; i++)
            {
                GameObject z = Place(i == 2 ? finalPrefab : zonePrefab, flow.transform, $"Zone{i + 1}", new Vector3(0f, 0f, i * ZoneSpacing), report);
                ZoneQuest quest = z.GetComponent<ZoneQuest>();
                var so = new SerializedObject(quest);
                so.FindProperty("_zoneId").stringValue = ids != null && i < ids.Length ? ids[i] : $"zone_{i + 1}";
                so.FindProperty("_displayName").stringValue = $"Zone {i + 1}";
                so.FindProperty("_region").enumValueIndex = (int)region;
                so.ApplyModifiedProperties();
                zones.Add(quest);

                var guardian = z.GetComponentInChildren<StatueDialogueRouter>();
                if (guardian != null) SetString(guardian, "_deityName", $"Guardian {i + 1}");
                var barrier = z.GetComponentInChildren<BarrierNotification>();
                if (barrier != null && i < 2) barrier.Message = $"Complete Zone {i + 1} to pass through.";
            }
            // Each gate points the way to the next zone's guardian.
            for (int i = 0; i < 2; i++)
            {
                var gate = zones[i].GetComponentInChildren<UnlockBarrierStep>();
                var next = zones[i + 1].GetComponentInChildren<StatueDialogueRouter>();
                if (gate != null && next != null) SetRef(gate, "_nextObjective", next.transform, onlyIfEmpty: false);
            }

            var dso = new SerializedObject(director);
            dso.FindProperty("_region").enumValueIndex = (int)region;
            dso.FindProperty("_levelDisplayName").stringValue = levelName;
            SerializedProperty list = dso.FindProperty("_zones");
            list.arraySize = zones.Count;
            for (int i = 0; i < zones.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = zones[i];
            dso.FindProperty("_pinDrainToBase").boolValue = false;   // later levels scale the drain by difficulty
            dso.ApplyModifiedProperties();

            var pollution = flow.GetComponent<PollutionDirector>();
            if (pollution != null && sun != null)
            {
                var pso = new SerializedObject(pollution);
                SerializedProperty lights = pso.FindProperty("_lights");
                lights.arraySize = 1;
                lights.GetArrayElementAtIndex(0).objectReferenceValue = sun;
                pso.ApplyModifiedProperties();
            }
            report.AppendLine($"Level flow: {levelName} ({region}) with 3 template zones, {ZoneSpacing:0} m apart along +Z.");
        }

        /// <summary>Zone, step and checkpoint IDs are save keys: give this level its own (region_zN_…).</summary>
        public static string FixIds(Region region)
        {
            string prefix = region.ToString().ToLowerInvariant();
            int steps = 0, checkpoints = 0;
            LevelFlowDirector director = FirstOfType<LevelFlowDirector>();
            if (director == null) return "No level flow to fix up.\n";
            var dso = new SerializedObject(director);
            dso.FindProperty("_region").enumValueIndex = (int)region;
            dso.ApplyModifiedProperties();
            string[] zoneIds = GameConstants.Zones.GetZonesForRegion(region);
            SerializedProperty zones = dso.FindProperty("_zones");
            for (int z = 0; z < zones.arraySize; z++)
            {
                var quest = zones.GetArrayElementAtIndex(z).objectReferenceValue as ZoneQuest;
                if (quest == null) continue;
                // The zone belongs to this region, with this region's zone ID.
                var qso = new SerializedObject(quest);
                qso.FindProperty("_region").enumValueIndex = (int)region;
                SerializedProperty zoneId = qso.FindProperty("_zoneId");
                if (zoneIds != null && z < zoneIds.Length && !zoneId.stringValue.StartsWith(prefix)) zoneId.stringValue = zoneIds[z];
                qso.ApplyModifiedProperties();
                foreach (QuestStep step in quest.Steps)
                {
                    if (step == null) continue;
                    var so = new SerializedObject(step);
                    SerializedProperty id = so.FindProperty("_stepId");
                    if (!id.stringValue.StartsWith("zone_")) continue;   // already named
                    id.stringValue = $"{prefix}_z{z + 1}_{id.stringValue.Substring(5)}";
                    so.ApplyModifiedProperties();
                    steps++;
                }
                foreach (Checkpoint cp in quest.GetComponentsInChildren<Checkpoint>(true))
                {
                    var so = new SerializedObject(cp);
                    SerializedProperty id = so.FindProperty("_checkpointId");
                    if (!id.stringValue.StartsWith("cp_zone_")) continue;
                    id.stringValue = $"cp_z{z + 1}_{id.stringValue.Substring(8)}";
                    so.ApplyModifiedProperties();
                    checkpoints++;
                }
            }
            return $"IDs: region {region}, zone IDs, {steps} steps and {checkpoints} checkpoints named for {prefix}.\n";
        }

        // ═════════════════════════ Helpers ═════════════════════════

        private static Region RegionOf(string sceneName)
        {
            if (sceneName.Contains("Mindanao")) return Region.Mindanao;
            if (sceneName.Contains("Luzon")) return Region.Luzon;
            return Region.Visayas;
        }

        private static string LevelName(Region region) => region == Region.Luzon ? "Level 1" : region == Region.Visayas ? "Level 2" : "Level 3";

        private static void CopyRoot(string scenePath, string prefabPath, System.Text.StringBuilder report, System.Action<GameObject> adjust)
        {
            if (Kept(prefabPath, report)) return;
            GameObject src = FindInScene(scenePath);
            if (src == null) { report.AppendLine($"No '{scenePath}' in the scene to copy."); return; }
            CopyObject(src, prefabPath, report, adjust);
        }

        private static void CopyObject(GameObject src, string prefabPath, System.Text.StringBuilder report, System.Action<GameObject> adjust)
        {
            if (Kept(prefabPath, report)) return;
            GameObject copy = Object.Instantiate(src);
            copy.name = src.name;
            copy.SetActive(true);
            copy.transform.position = Vector3.zero;
            adjust?.Invoke(copy);
            PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
            Object.DestroyImmediate(copy);
            report.AppendLine($"Saved {Path.GetFileName(prefabPath)}");
        }

        // References to the scene a prefab can't keep (they'd be dropped anyway): cleared explicitly.
        private static void ClearSceneRefs(GameObject go)
        {
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;
                var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object o = it.objectReferenceValue;
                    GameObject target = o as GameObject ?? (o as Component)?.gameObject;
                    if (target == null || !target.scene.IsValid() || target.transform.IsChildOf(go.transform)) continue;
                    it.objectReferenceValue = null;
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject Place(GameObject prefab, Transform parent, string name, Vector3 position, System.Text.StringBuilder report)
        {
            if (prefab == null) { report?.AppendLine($"Missing prefab for {name}."); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            if (parent != null) go.transform.SetParent(parent, false);
            go.name = name;
            go.transform.position = parent != null ? parent.position + position : position;
            Undo.RegisterCreatedObjectUndo(go, "Add level piece");
            report?.AppendLine($"Added {name}.");
            return go;
        }

        private static bool SceneHas(GameObject prefab)
        {
            foreach (MonoBehaviour mb in prefab.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (Object.FindFirstObjectByType(mb.GetType(), FindObjectsInactive.Include) != null) return true;
            }
            return prefab.GetComponent<EventSystem>() != null && Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null;
        }

        private static T FirstOfType<T>(System.Predicate<T> match = null) where T : Object
        {
            foreach (T t in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (match == null || match(t)) return t;
            return null;
        }

        private static Transform RootNamed(string name)
        {
            foreach (GameObject r in SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == name) return r.transform;
            return null;
        }

        private static Transform NewRoot(string name, System.Text.StringBuilder report)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add level piece");
            report.AppendLine($"Added {name}.");
            return go.transform;
        }

        private static GameObject FindInScene(string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            Transform root = RootNamed(rootName);
            if (root == null) return null;
            if (slash < 0) return root.gameObject;
            Transform t = root.Find(path.Substring(slash + 1));
            return t != null ? t.gameObject : null;
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static bool Kept(string path, System.Text.StringBuilder report)
        {
            if (!File.Exists(path)) return false;
            report.AppendLine($"Kept existing {Path.GetFileName(path)}");
            return true;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static Transform Child(Transform parent, string name, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        private static List<DialogueLine> Lines(params string[] texts)
        {
            var list = new List<DialogueLine>();
            foreach (string t in texts) list.Add(new DialogueLine { speakerName = "Guardian", text = t });
            return list;
        }

        private static Material PlaceholderMaterial()
        {
            string path = $"{TemplateFolder}/M_GuardianPlaceholder.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(lit != null ? lit : Shader.Find("Standard")) { color = new Color(0.55f, 0.6f, 0.58f) };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetString(Object target, string field, string value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRef(Object target, string field, Object value, bool onlyIfEmpty)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null || (onlyIfEmpty && p.objectReferenceValue != null)) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
