using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Challenges;
using Lalim.Core;
using Lalim.DDA;
using Lalim.Inventory;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.UI;
using NetFit = Lalim.Challenges.GhostNet.NetPuzzle.Fit;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 1 ▸ Build Level Flow — installs the Level 1 flow into 02_Luzon.
    ///
    /// Idempotent and surgical: every object is looked up by name first and only created when
    /// missing, so anything already in the scene (including hand edits) is left alone. Re-run it
    /// safely after pulling changes.
    /// </summary>
    public static class Level1SceneBuilder
    {
        private const string ScenePath = "Assets/Lalim/Scenes/Levels/01_Luzon/02_Luzon.unity";
        private const string RootName = "[Level1Flow]";
        private const string CheckpointPrefabPath = "Assets/Lalim/Prefabs/Hazards/PF_Checkpoint.prefab";
        private const string GameHudPrefabPath = "Assets/Lalim/Prefabs/UI/GameHUD.prefab";
        private const string ItemsFolder = "Assets/Lalim/Data/Items";
        private const string ItemDatabasePath = "Assets/Lalim/Data/ItemDatabase.asset";
        private const string DdaConfigPath = "Assets/Lalim/Data/DDAConfig.asset";
        private const string WaterKitModelPath = "Assets/Lalim/Models/tools/water sampling kit.fbx";
        private const string CoralSeedModelPath = "Assets/Lalim/Models/tools/coral seed.fbx";
        private const string IconInteractPath = "Assets/Lalim/Sprites/UI/Game Mechanics/icon_interact.png";
        private const string IconChatPath = "Assets/Lalim/Sprites/UI/Game Mechanics/icon_chat.png";
        private const string MusicPath = "Assets/NekoLegends/Music/PianoSoloSingles/Fairy Water Cave.wav";
        private const string DifficultyFolder = "Assets/Lalim/Data/Difficulty";
        private const string CoralModelsFolder = "Assets/Lalim/Models/sea creatures + elements (fbx)";
        private const string OldCoralKitPath = "Assets/Lalim/Data/Items/05_CoralReplantingKit.asset";

        // Magwayen Well: the tunnel down from Neguno's hall (open to the seabed at y ≈ -115).
        private static readonly Vector3 ShaftCenter = new Vector3(-23.92f, -76f, 113.7f);
        private static readonly Vector3 ShaftSize = new Vector3(24f, 80f, 24f);

        public const string WaterKitId = "07_watersamplekit";
        public const string CoralSeedId = "09_coralseed";

        // Map extents (from MapBounds) and zone splits along +Z.
        private const float MinX = -105f, MaxX = 57f, MinY = -58f, MaxY = 45f;

        // Every coordinate in this builder was authored with LuzonScene at LevelOrigin. When the
        // environment is moved, _offset carries the gameplay pieces along with it (see At()).
        private static readonly Vector3 LevelOrigin = new Vector3(-23.92f, 0f, -46.3f);
        private static Vector3 _offset;

        /// <summary>An authored world position, shifted to wherever LuzonScene now sits.</summary>
        private static Vector3 At(float x, float y, float z) => new Vector3(x, y, z) + _offset;
        private const float Zone1MinZ = -106f, Zone12Split = 4f, Zone23Split = 72f, Zone3MaxZ = 157f;

        private static readonly string[] ReefPrefixes =
        {
            "Reef_coral", "Reef_plantcoral", "Reef_plantanemone", "Reef_miscanemone", "Reef_sponge", "Reef_plantsponge",
            "Garden_coral", "Garden_plantcoral", "Garden_plantanemone", "Garden_miscanemone", "Garden_sponge", "Garden_plantsponge"
        };

        [MenuItem("Tools/Lalim/Level 1/Build Level Flow")]
        public static void BuildFromMenu()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }
            Debug.Log(Build());
        }

        /// <summary>Builds into the open scene (must be 02_Luzon) and saves it. Returns a report.</summary>
        public static string Build()
        {
            var report = new System.Text.StringBuilder();
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) return $"Open {ScenePath} first (active: {scene.path}).";

            Transform env = Find("LuzonScene");
            if (env == null) return "LuzonScene environment not found.";
            _offset = env.position - LevelOrigin;
            if (_offset != Vector3.zero) report.AppendLine($"Level offset {_offset} (LuzonScene was moved).");

            RemoveObsolete(report);
            EnsureItems(report);
            report.Append(RelicAssetBuilder.Build(false));
            RemoveCoralReplantingKit(report);
            var profiles = EnsureDifficultyProfiles(report);
            EnsureBuildSettings(report);
            EnsureDdaManager(report);
            EnsureActionButton(report);

            GameObject root = FindOrCreate(RootName, null);
            Add<ObjectiveTracker>(root);
            EnsureMusic(Add<ProximityMusic>(root), report);
            EnsureDialogueUI(report);
            // Bubbles, post-processing + underwater sky, and marine life (prefabs shared by every level).
            report.Append(EnvironmentPrefabsBuilder.AddToLuzon());
            Add<ZoneBannerUI>(root).EditorSetFont(AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset"));
            Add<AchievementService>(root);
            var director = Add<LevelFlowDirector>(root);
            var pollution = Add<PollutionDirector>(root);

            // ── Statues ──
            StatueDialogueRouter aman = EnsureStatue(env, "Statue_Aman_Sinaya", Level1DialogueScripts.Amansinaya, report,
                Level1DialogueScripts.AmansinayaQuest(), Level1DialogueScripts.AmansinayaClosing(),
                Level1DialogueScripts.AmansinayaIdle(), Level1DialogueScripts.AmansinayaBarks());
            StatueDialogueRouter haik = EnsureStatue(env, "Statue_Haik", Level1DialogueScripts.Haik, report,
                Level1DialogueScripts.HaikQuest(), Level1DialogueScripts.HaikClosing(),
                Level1DialogueScripts.HaikIdle(), Level1DialogueScripts.HaikBarks());
            StatueDialogueRouter neguno = EnsureStatue(env, "Statue_Neguno", Level1DialogueScripts.Neguno, report,
                Level1DialogueScripts.NegunoQuest(), Level1DialogueScripts.NegunoClosing(),
                Level1DialogueScripts.NegunoIdle(), Level1DialogueScripts.NegunoBarks());
            foreach (StatueDialogueRouter statue in new[] { aman, haik, neguno }) Level1RelicRewards.EnsureGiftLines(statue, report);
            report.Append(StatueTalkFraming.SetUp(false));

            // ── Zones + toxic water ──
            ZoneQuest z1 = EnsureZone(root.transform, "Zone1_Amansinaya", GameConstants.Zones.LUZON_BLEACHED_REEF, "Amansinaya's Shallows", aman, Zone1MinZ, Zone12Split);
            ZoneQuest z2 = EnsureZone(root.transform, "Zone2_Haik", GameConstants.Zones.LUZON_TOXIN_SITES, "Haik's Canyon", haik, Zone12Split, Zone23Split);
            ZoneQuest z3 = EnsureZone(root.transform, "Zone3_Neguno", GameConstants.Zones.LUZON_CORAL_BED, "Neguno's Sunken Hall", neguno, Zone23Split, Zone3MaxZ);

            // ── Barriers ──
            // All barriers are PF_FoggyBarrier (made from Zone2_Barrier); each says what unlocks it.
            GameObject zone2Barrier = Find("Zone2_Barrier") != null ? Find("Zone2_Barrier").gameObject : null;
            SetBarrierMessage(zone2Barrier, "Complete Zone 1 to pass through.");
            GameObject zone3Barrier = EnsureBarrier(root.transform, zone2Barrier, "Zone3_Barrier",
                At(-24f, 2.5f, Zone23Split), new Vector3(92f, 85f, 3f), "Complete Zone 2 to pass through.", report);
            GameObject tunnelBarrier = EnsureBarrier(root.transform, zone2Barrier, "Tunnel_Barrier",
                At(-23.92f, -37.5f, 113.7f), new Vector3(26f, 1.5f, 26f), "Complete Zone 3 to pass through.", report);

            // ── Checkpoints ──
            Transform cps = FindOrCreate("Checkpoints", null).transform;
            EnsureCheckpoint(cps, "cp_z1_statue", "luzon_z1", At(-23.92f, -3.5f, -61f));
            EnsureCheckpoint(cps, "cp_z1_altar", "luzon_z1", At(-14.92f, -5.5f, -56f));
            EnsureCheckpoint(cps, "cp_z2_entry", "luzon_z2", At(-23.92f, -10f, 10f));
            EnsureCheckpoint(cps, "cp_z2_statue", "luzon_z2", At(-12.72f, -22f, 38f));
            EnsureCheckpoint(cps, "cp_z3_entry", "luzon_z3", At(-23.92f, -24f, 80f));
            EnsureCheckpoint(cps, "cp_z3_statue", "luzon_z3", At(-23.92f, -26f, 124f));
            EnsureCheckpoint(cps, "cp_z3_tunnel", "luzon_z3", At(-23.92f, -32f, 113.7f));

            // ── Zone 2 / zone 3 areas ──
            Vector3[] z2Areas = { GroundAt(-6.3f, 36.9f, -26.5f), GroundAt(-17f, 12f, -20f), GroundAt(-19f, 56f, -27f) };
            Vector3[] z3Areas =
            {
                Find("LuzonScene/Prop_Ghost_Net_Trap") != null ? Find("LuzonScene/Prop_Ghost_Net_Trap").position : At(-7.92f, -35.2f, 94.5f),
                Find("LuzonScene/Prop_Ghost_Net_Trap.001") != null ? Find("LuzonScene/Prop_Ghost_Net_Trap.001").position : At(-41.5f, -35f, 93.8f),
                Find("LuzonScene/Prop_Ghost_Net_Trap.002") != null ? Find("LuzonScene/Prop_Ghost_Net_Trap.002").position : At(-9.5f, -35.5f, 138.5f),
            };
            string[] keys = { "a", "b", "c" };
            for (int i = 0; i < 3; i++)
            {
                EnsureCheckpoint(cps, $"cp_z2_area_{keys[i]}", "luzon_z2", z2Areas[i] + Vector3.up * 4f);
                EnsureCheckpoint(cps, $"cp_z3_net_{keys[i]}", "luzon_z3", z3Areas[i] + Vector3.up * 5f);
            }

            // ── Steps ──
            Transform altarT = Find("Altar_AmanSinaya");
            SegregationAltar altar = altarT != null ? altarT.GetComponent<SegregationAltar>() : null;
            Transform stand = FindOrCreate("SortStandPoint", altarT != null ? altarT : root.transform).transform;
            if (stand.localPosition == Vector3.zero)
            {
                stand.position = At(-14.92f, -6.2f, -55.4f);
                stand.rotation = Quaternion.LookRotation(Vector3.forward);
            }

            // Relic gifts were inserted mid-zone; renumber older step objects before looking them up.
            Level1RelicRewards.RenameSteps(z1, Level1RelicRewards.Zones[0]);
            Level1RelicRewards.RenameSteps(z2, Level1RelicRewards.Zones[1]);
            Level1RelicRewards.RenameSteps(z3, Level1RelicRewards.Zones[2]);

            var z1Steps = new List<QuestStep>();
            z1Steps.Add(Step<TalkStep>(z1, "01_Talk", "luzon_z1_talk", "Swim to the statue of Amansinaya."));
            var trash = Step<TrashCollectionStep>(z1, "02_TrashCollection", "luzon_z1_trash", "Collect every piece of trash in the shallows.");
            trash.EditorSetupTrash(env, z1.ToxicVolume.GetComponent<Collider>());
            trash.EditorSetupProfile(profiles["TrashCollection"]);
            z1Steps.Add(trash);
            // After the trash the player swims back and talks: Amansinaya hands over the kit (1st reward).
            var trashTalk = Step<DialogueStep>(z1, "03_TrashDoneTalk", "luzon_z1_trash_talk", "Return to Amansinaya.");
            trashTalk.EditorSetupDialogue(DialogueStep.Kind.Talk, Level1DialogueScripts.BarkTrashDone);
            trashTalk.EditorSetBoxOpen(false);
            z1Steps.Add(trashTalk);
            z1Steps.Add(Grant(z1, "04_WaterKit", "luzon_z1_kit", WaterKitId, "Tool Received", "Water Sampling Kit"));   // listed in the Quest Complete toast
            var sort = Step<SegregationStep>(z1, "05_Sorting", "luzon_z1_sort", "Lay each piece in its rightful bowl.");
            sort.EditorSetupSegregation(altar, trash, stand);
            sort.EditorSetupProfile(profiles["Sorting"]);
            sort.EditorSetStation(EnsureSortStation(sort, stand));
            altar.EditorSetFonts(AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset"),
                                 AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset"));
            z1Steps.Add(sort);
            z1Steps.Add(Level1RelicRewards.EnsureStep(z1, Level1RelicRewards.Zones[0]));   // the relic: 2nd reward
            z1Steps.Add(Dialogue(z1, "07_Closing", "luzon_z1_closing", DialogueStep.Kind.Closing, null));
            z1Steps.Add(Cleanse(z1, "08_Cleanse", "luzon_z1_cleanse", "Quest Complete", "Amansinaya's shallows are clean again"));
            z1Steps.Add(Unlock(z1, "09_Gate", "luzon_z1_gate", zone2Barrier, null));

            var z2Steps = new List<QuestStep>();
            z2Steps.Add(Step<TalkStep>(z2, "01_Talk", "luzon_z2_talk", "Find Haik in the canyon."));
            var waterStep = Areas(z2, "02_WaterReadings", "luzon_z2_water", "Read the water at all three places.", z2Areas, keys,
                "Water", "Tap to use Water Sampling Kit", ChallengeType.WaterSampling, WaterKitId, "Water reading");
            InstallWaterCalibration(waterStep, profiles["WaterCalibration"], report);
            z2Steps.Add(waterStep);
            // After the readings the player swims back and talks (as with Amansinaya after the trash).
            var readingsTalk = Step<DialogueStep>(z2, "03_ReadingsDone", "luzon_z2_readings_bark", "Return to Haik.");
            readingsTalk.EditorSetupDialogue(DialogueStep.Kind.Talk, Level1DialogueScripts.BarkReadingsDone);
            readingsTalk.EditorSetBoxOpen(false);
            z2Steps.Add(readingsTalk);
            z2Steps.Add(Grant(z2, "04_CoralSeed", "luzon_z2_seed", CoralSeedId, "Tool Received", "Coral Seed"));   // listed in the Quest Complete toast
            var coralStep = Areas(z2, "05_CoralReplanting", "luzon_z2_coral", "Clear the dead ground and plant coral at all three places.", z2Areas, keys,
                "Coral", "Prepare the site", ChallengeType.CoralReplanting, CoralSeedId, "Coral replanting");
            InstallCoralSitePrep(coralStep, profiles["CoralSitePrep"], report);
            z2Steps.Add(coralStep);
            z2Steps.Add(Level1RelicRewards.EnsureStep(z2, Level1RelicRewards.Zones[1]));
            z2Steps.Add(Dialogue(z2, "07_Closing", "luzon_z2_closing", DialogueStep.Kind.Closing, null));
            z2Steps.Add(Cleanse(z2, "08_Cleanse", "luzon_z2_cleanse", "Quest Complete", "Haik's waters run true again"));
            z2Steps.Add(Unlock(z2, "09_Gate", "luzon_z2_gate", zone3Barrier, null));

            Transform tunnelEntrance = FindOrCreate("TunnelEntrance", root.transform).transform;
            tunnelEntrance.position = At(-23.92f, -36f, 113.7f);
            GameObject exit = FindOrCreate("LevelExit_Trigger", root.transform);
            // The lower quarter of the tunnel (from 75% of the way down, y -96.4, to just above the
            // floor at -116), where it has gone almost pitch black (ShaftDarkness). Reaching it
            // crossfades into the loading screen.
            exit.transform.position = At(-23.92f, -105.2f, 113.7f);
            BoxCollider exitCol = Add<BoxCollider>(exit);
            exitCol.isTrigger = true;
            exitCol.size = new Vector3(24f, 17.6f, 24f);

            var z3Steps = new List<QuestStep>();
            z3Steps.Add(Step<TalkStep>(z3, "01_Talk", "luzon_z3_talk", "Descend to Neguno's sunken hall."));
            var netStep = Areas(z3, "02_GhostNets", "luzon_z3_nets", "Untangle the ghost nets and free the trapped animals.", z3Areas, keys,
                "Net", "Untangle the net", ChallengeType.GhostNetRescue, "", "Ghost net");
            InstallGhostNets(netStep, profiles["GhostNet"], env, report);
            z3Steps.Add(netStep);
            z3Steps.Add(Level1RelicRewards.EnsureStep(z3, Level1RelicRewards.Zones[2]));
            z3Steps.Add(Dialogue(z3, "04_Closing", "luzon_z3_closing", DialogueStep.Kind.Closing, null));
            z3Steps.Add(Cleanse(z3, "05_Cleanse", "luzon_z3_cleanse", "Quest Complete", "Neguno's creatures swim free"));
            UnlockBarrierStep tunnelStep = Unlock(z3, "06_Tunnel", "luzon_z3_tunnel", tunnelBarrier, tunnelEntrance);
            tunnelStep.EditorSetupShaft(ShaftCenter + _offset, ShaftSize);
            z3Steps.Add(tunnelStep);
            var exitStep = Step<LevelExitStep>(z3, "07_Exit", "luzon_exit", "Swim down the deep path.");
            exitStep.EditorSetupExit(tunnelEntrance, exitCol);
            z3Steps.Add(exitStep);

            z1.EditorSetup(GameConstants.Zones.LUZON_BLEACHED_REEF, "Amansinaya's Shallows", Region.Luzon, aman, z1.ToxicVolume, z1Steps);
            z2.EditorSetup(GameConstants.Zones.LUZON_TOXIN_SITES, "Haik's Canyon", Region.Luzon, haik, z2.ToxicVolume, z2Steps);
            z3.EditorSetup(GameConstants.Zones.LUZON_CORAL_BED, "Neguno's Sunken Hall", Region.Luzon, neguno, z3.ToxicVolume, z3Steps);
            director.EditorSetup(Region.Luzon, "Level 1", new List<ZoneQuest> { z1, z2, z3 });

            SetupPollution(pollution, env, new[] { z1, z2, z3 }, report);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("Level 1 flow built and scene saved.");
            return report.ToString();
        }

        // ───────────────────────── Cleanup / project setup ─────────────────────────

        private static void RemoveObsolete(System.Text.StringBuilder report)
        {
            foreach (string name in new[] { "[SpiritVision]", "[LuzonReefQuestController]" })
            {
                Transform t = Find(name);
                if (t == null) continue;
                Object.DestroyImmediate(t.gameObject);
                report.AppendLine($"Removed obsolete {name}.");
            }
        }

        private static void EnsureItems(System.Text.StringBuilder report)
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            ItemData kit = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/07_WaterSampleKit.asset");
            if (kit != null)
            {
                bool changed = false;
                if (kit.worldModelPrefab == null) { kit.worldModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterKitModelPath); changed = true; }
                if (kit.displayName == "Water Sample Kit") { kit.displayName = "Water Sampling Kit"; changed = true; }
                if (string.IsNullOrEmpty(kit.description)) { kit.description = "Reads the water's pH, salinity and temperature. A gift from Amansinaya."; changed = true; }
                if (changed) EditorUtility.SetDirty(kit);
            }

            string seedPath = $"{ItemsFolder}/09_CoralSeed.asset";
            ItemData seed = AssetDatabase.LoadAssetAtPath<ItemData>(seedPath);
            if (seed == null)
            {
                seed = ScriptableObject.CreateInstance<ItemData>();
                seed.itemId = CoralSeedId;
                seed.displayName = "Coral Seed";
                seed.description = "Young coral to plant where the rock is clean. A gift from Haik.";
                seed.itemType = ItemType.Tool;
                seed.regionOrigin = Region.Luzon;
                seed.worldModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoralSeedModelPath);
                AssetDatabase.CreateAsset(seed, seedPath);
                report.AppendLine("Created item 09_CoralSeed.");
            }

            if (db != null)
            {
                var so = new SerializedObject(db);
                SerializedProperty list = so.FindProperty("_items");
                bool present = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == seed) present = true;
                if (!present)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = seed;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    report.AppendLine("Registered Coral Seed in ItemDatabase.");
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ───────────────────────── Difficulty profiles & real minigames ─────────────────────────

        private static Dictionary<string, MinigameDifficultyProfile> EnsureDifficultyProfiles(System.Text.StringBuilder report)
        {
            if (!AssetDatabase.IsValidFolder(DifficultyFolder)) AssetDatabase.CreateFolder("Assets/Lalim/Data", "Difficulty");
            var map = new Dictionary<string, MinigameDifficultyProfile>();

            MinigameDifficultyProfile Profile(string name, float expected, float firstAction)
            {
                string path = $"{DifficultyFolder}/{name}.asset";
                var p = AssetDatabase.LoadAssetAtPath<MinigameDifficultyProfile>(path);
                if (p == null)
                {
                    p = ScriptableObject.CreateInstance<MinigameDifficultyProfile>();
                    p.expectedSeconds = expected;
                    p.expectedFirstActionSeconds = firstAction;
                    AssetDatabase.CreateAsset(p, path);
                    report.AppendLine($"Created difficulty profile {name}.");
                }
                map[name] = p;
                return p;
            }

            // Easy / Normal / Hard. Existing knobs keep their tuned values.
            var trash = Profile("TrashCollection", 200f, 8f);
            trash.EnsureKnob(Level1DifficultyKnobs.TrashCount, 12f, 18f, 25f);
            trash.EnsureKnob(Level1DifficultyKnobs.TrashHiddenShare, 0f, 0.15f, 0.4f);

            var sorting = Profile("Sorting", 90f, 6f);
            sorting.EnsureKnob(Level1DifficultyKnobs.SortLabelMode, 0f, 1f, 2f);

            var water = Profile("WaterCalibration", 60f, 5f);
            water.EnsureKnob(Level1DifficultyKnobs.BandWidth, 0.35f, 0.22f, 0.12f);
            water.EnsureKnob(Level1DifficultyKnobs.BandSpeed, 0.25f, 0.45f, 0.7f);
            water.EnsureKnob(Level1DifficultyKnobs.NeedleNoise, 0.12f, 0.2f, 0.3f);
            water.EnsureKnob(Level1DifficultyKnobs.HoldSeconds, 2f, 3f, 3.5f);
            water.EnsureKnob(Level1DifficultyKnobs.LockDrain, 0.3f, 0.6f, 1f);

            var coral = Profile("CoralSitePrep", 45f, 5f);
            coral.EnsureKnob(Level1DifficultyKnobs.AlgaeTarget, 0.6f, 0.75f, 0.85f);
            coral.EnsureKnob(Level1DifficultyKnobs.Skeletons, 2f, 3f, 5f);
            coral.EnsureKnob(Level1DifficultyKnobs.TapsPerSkeleton, 3f, 4f, 6f);
            coral.EnsureKnob(Level1DifficultyKnobs.Decoys, 0f, 1f, 3f);

            var nets = Profile("GhostNet", 90f, 8f);
            nets.EnsureKnob(Level1DifficultyKnobs.Animals, 3f, 5f, 7f);
            nets.EnsureKnob(Level1DifficultyKnobs.KnotsPerTangle, 4f, 5f, 6f);
            nets.EnsureKnob(Level1DifficultyKnobs.Scramble, 0.6f, 0.8f, 1f);
            nets.EnsureKnob(Level1DifficultyKnobs.Hints, 3f, 2f, 1f);

            foreach (var p in map.Values) EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return map;
        }

        // Per-area water readings, all within the healthy range (each locks as "Normal").
        private static readonly WaterCalibrationMinigame.Readings[] WaterReadings =
        {
            new WaterCalibrationMinigame.Readings { ph = 8.1f, salinity = 35f, temperature = 27f },
            new WaterCalibrationMinigame.Readings { ph = 8.2f, salinity = 34f, temperature = 26f },
            new WaterCalibrationMinigame.Readings { ph = 8.0f, salinity = 36f, temperature = 28f },
        };

        private static void InstallWaterCalibration(AreaMinigameStep step, MinigameDifficultyProfile profile, System.Text.StringBuilder report)
        {
            GameObject kit = AssetDatabase.LoadAssetAtPath<GameObject>(WaterKitModelPath);
            int i = 0;
            foreach (Transform area in step.transform)
            {
                if (area.GetComponent<AreaStation>() == null) continue;
                bool replaced = SwapPlaceholder<WaterCalibrationMinigame>(area.gameObject, out WaterCalibrationMinigame game);
                if (!replaced && HasOutOfRangeReading(game))
                {
                    // Readings from the earlier "find the bad reading" version: switch to healthy values.
                    game.EditorSetup($"Area {(char)('A' + i)}", WaterReadings[Mathf.Min(i, WaterReadings.Length - 1)], profile, kit);
                    report.AppendLine($"Updated {area.name} readings to healthy values.");
                }
                if (replaced)
                {
                    game.EditorSetup($"Area {(char)('A' + i)}", WaterReadings[Mathf.Min(i, WaterReadings.Length - 1)], profile, kit);
                    if (FrameWaterViewPoint(area) == null) FrameViewPoint(area);
                    report.AppendLine($"Installed water calibration at {area.name}.");
                }
                SetupSeaSensorsPanel(game);
                i++;
            }
        }

        /// <summary>The Sea Sensors panel's fonts and STOP-button hand icon.</summary>
        public static void SetupSeaSensorsPanel(WaterCalibrationMinigame game)
        {
            if (game == null) return;
            game.EditorSetupPanel(
                AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset"),
                AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset"),
                AssetDatabase.LoadAssetAtPath<Sprite>(IconInteractPath));
            EditorUtility.SetDirty(game);
        }

        // Small-to-mid sized animals only (Whale, Hammerhead, Tuna, Seal and Penguin are left out).
        // Fit = the hole each one needs to slip out of the ghost net.
        private static readonly (string id, string name, string file, Lalim.Challenges.GhostNet.NetPuzzle.Fit fit)[] NetSpecies =
        {
            ("clownfish", "Clownfish", "ClownFish", NetFit.Small), ("bluetang", "Blue Tang", "DoryFish", NetFit.Small),
            ("reeffish", "Reef Fish", "BrownFish", NetFit.Small), ("crab", "Crab", "Crab", NetFit.Small),
            ("lobster", "Spiny Lobster", "Lobster", NetFit.Long), ("seastar", "Sea Star", "StarFish", NetFit.Small),
            ("octopus", "Octopus", "Octopus", NetFit.Large), ("squid", "Squid", "Squid", NetFit.Long),
            ("moray", "Moray Eel", "Eel", NetFit.Long), ("pawikan", "Sea Turtle", "Turtle", NetFit.Large),
            ("stingray", "Stingray", "StingRay", NetFit.Large),
        };

        private const string NetKnotMat = "Assets/Lalim/Materials/M_NetKnot.mat";
        private const string NetTangledMat = "Assets/Lalim/Materials/M_NetTangled.mat";

        private static List<Lalim.Challenges.GhostNet.GhostNetMinigame.Species> NetSpeciesList()
        {
            var species = new List<Lalim.Challenges.GhostNet.GhostNetMinigame.Species>();
            foreach (var s in NetSpecies)
            {
                species.Add(new Lalim.Challenges.GhostNet.GhostNetMinigame.Species
                {
                    id = s.id,
                    displayName = s.name,
                    model = AssetDatabase.LoadAssetAtPath<GameObject>($"{CoralModelsFolder}/{s.file}.fbx"),
                    fit = s.fit
                });
            }
            return species;
        }

        private static void InstallGhostNets(AreaMinigameStep step, MinigameDifficultyProfile profile, Transform env, System.Text.StringBuilder report)
        {
            foreach (Transform area in step.transform)
            {
                if (area.GetComponent<AreaStation>() == null) continue;
                if (SwapPlaceholder<Lalim.Challenges.GhostNet.GhostNetMinigame>(area.gameObject, out var game))
                {
                    // The scene's net and trapped-creature props at this spot disappear when the net is hauled away.
                    var props = new List<GameObject>();
                    foreach (Transform child in env)
                    {
                        if (!(child.name.StartsWith("Prop_Ghost_Net_Trap") || child.name.StartsWith("Creature_Trapped_In_Net"))) continue;
                        Vector3 d = child.position - area.position;
                        d.y = 0f;
                        if (d.magnitude < 9f) props.Add(child.gameObject);
                    }
                    game.EditorSetup(profile, NetSpeciesList(), props);
                    report.AppendLine($"Installed ghost net puzzle at {area.name} ({props.Count} scene props linked).");
                }
                SetupGhostNetLook(game);
            }
            report.Append(DrapeGhostNets());
        }

        /// <summary>The puzzle net's materials (the scene net's cord, tangled ropes, glowing knots), fonts and animal fits.</summary>
        public static void SetupGhostNetLook(Lalim.Challenges.GhostNet.GhostNetMinigame game)
        {
            if (game == null) return;
            Material cord = null;
            var so = new SerializedObject(game);
            SerializedProperty props = so.FindProperty("_sceneProps");
            for (int i = 0; i < props.arraySize && cord == null; i++)
            {
                var go = props.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go != null && go.name.StartsWith("Prop_Ghost_Net_Trap")) cord = go.GetComponent<Renderer>()?.sharedMaterial;
            }
            // Unlit knots glow in any light (an emissive Lit material loses its _EMISSION keyword on save).
            Material tangled = EnsureMaterial(NetTangledMat, "Universal Render Pipeline/Lit", new Color(0.85f, 0.22f, 0.14f));
            Material knot = EnsureMaterial(NetKnotMat, "Universal Render Pipeline/Unlit", new Color(0.45f, 1f, 0.93f));
            game.EditorSetupLook(cord, tangled, knot,
                AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-ExtraBold SDF Padded.asset"),
                AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset"),
                NetSpeciesList());
            EditorUtility.SetDirty(game);
        }

        private static Material EnsureMaterial(string path, string shader, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.shader.name != shader) mat.shader = Shader.Find(shader);
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Lays each ghost net area's creatures flat on the seabed and drapes the scene net low over
        /// them (level, rim on the sand, just clearing the animals) instead of a tall empty dome.
        /// Safe to run again: a draped net stays as it is.
        /// </summary>
        [MenuItem("Tools/Lalim/Level 1/Drape Ghost Nets")]
        private static void DrapeGhostNetsMenu() => Debug.Log(DrapeGhostNets());

        public static string DrapeGhostNets()
        {
            var report = new System.Text.StringBuilder();
            foreach (var game in Object.FindObjectsByType<Lalim.Challenges.GhostNet.GhostNetMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(game);
                SerializedProperty props = so.FindProperty("_sceneProps");
                var creatures = new List<Transform>();
                var nets = new List<Transform>();
                for (int i = 0; i < props.arraySize; i++)
                {
                    var go = props.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (go == null) continue;
                    if (go.name.StartsWith("Creature_")) creatures.Add(go.transform);
                    else if (go.name.StartsWith("Prop_Ghost_Net_Trap")) nets.Add(go.transform);
                }
                foreach (Transform c in creatures)
                {
                    Undo.RecordObject(c, "Drape ghost nets");
                    LayFlat(c);
                    RestOnGround(c, 0.02f);
                }
                foreach (Transform n in nets)
                {
                    Undo.RecordObject(n, "Drape ghost nets");
                    float height = DrapeNet(n, creatures);
                    report.AppendLine($"Draped {n.name} at {game.name}: {height:F2} m high over {creatures.Count} animal(s).");
                }
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return report.ToString();
        }

        private const string UnderwaterSkyMat = "Assets/Lalim/Materials/M_UnderwaterSky.mat";

        /// <summary>The underwater sky dome's material (Lalim/Underwater Sky), kept as an asset so the shader ships.</summary>
        public static void EnsureUnderwaterSky(UnderwaterSky sky)
        {
            if (sky == null) return;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(UnderwaterSkyMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Lalim/Underwater Sky"));
                AssetDatabase.CreateAsset(mat, UnderwaterSkyMat);
            }
            sky.EditorSetup(mat);
            EditorUtility.SetDirty(sky);
        }

        // ───────────────────────── Zone 3 well (the tunnel down) ─────────────────────────

        private static readonly Vector3 WellAxis = new Vector3(-153f, 0f, 113.7f);

        /// <summary>Pitch black at the bottom of the Magwayen Well: ShaftDarkness on the tunnel entrance.</summary>
        public static string EnsureShaftDarkness()
        {
            Transform root = GameObject.Find(RootName)?.transform;
            Transform entrance = root != null ? root.Find("TunnelEntrance") : null;
            if (entrance == null) return "No TunnelEntrance found.\n";
            if (entrance.GetComponent<ShaftDarkness>() != null) return "Shaft darkness already set up.\n";
            var dark = Undo.AddComponent<ShaftDarkness>(entrance.gameObject);
            dark.EditorSetup(18f, -40f, -105f);
            EditorUtility.SetDirty(dark);
            return "Added ShaftDarkness to TunnelEntrance (dark from y -40, pitch black by y -105).\n";
        }

        [MenuItem("Tools/Lalim/Level 1/Line Well With Rocks")]
        private static void LineWellMenu() => Debug.Log(LineWellWithRocks());

        /// <summary>
        /// The well's rocks (LuzonScene/Well_*) sat behind the shaft's terrain wall, so from inside you
        /// only saw the bare wall. Each rock is pushed in toward the shaft's centre until it stands
        /// 1.2–2.4 m out of the wall (and gets a collider so the diver can't swim through it).
        /// Rocks that already stick out far enough are left alone, so it's safe to run again.
        /// </summary>
        public static string LineWellWithRocks()
        {
            var report = new System.Text.StringBuilder();
            GameObject scene = GameObject.Find("LuzonScene");
            if (scene == null) return "No LuzonScene.\n";
            int moved = 0;
            foreach (Transform t in scene.transform)
            {
                if (!t.name.StartsWith("Well_") || t.GetComponent<MeshFilter>() == null) continue;
                List<Vector3> verts = WorldVertices(t);
                if (verts.Count == 0) continue;
                Vector3 centre = Centre(verts);
                if (centre.y < -114f) continue;   // the floor of the well
                Vector2 radial = new Vector2(centre.x - WellAxis.x, centre.z - WellAxis.z);
                if (radial.magnitude < 1f) continue;
                Vector2 dir = radial.normalized;

                float y = Mathf.Clamp(centre.y, -110f, -38f);
                float wall = float.MaxValue;
                foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(WellAxis.x, y, WellAxis.z), new Vector3(dir.x, 0f, dir.y), 40f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.name.StartsWith("Terrain")) wall = Mathf.Min(wall, hit.distance);
                }
                if (wall == float.MaxValue) continue;

                float inner = float.MaxValue;
                foreach (Vector3 v in verts) inner = Mathf.Min(inner, Vector2.Dot(new Vector2(v.x - WellAxis.x, v.z - WellAxis.z), dir));
                float stickOut = 1.2f + 1.2f * Mathf.Abs(t.name.GetHashCode() % 1000) / 1000f;
                float target = wall - stickOut;
                if (inner <= target + 0.05f) continue;

                Undo.RecordObject(t, "Line well with rocks");
                t.position += new Vector3(dir.x, 0f, dir.y) * (target - inner);
                if (t.GetComponent<Collider>() == null) Undo.AddComponent<MeshCollider>(t.gameObject);
                moved++;
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            report.AppendLine($"Moved {moved} well rocks out of the shaft wall.");
            return report.ToString();
        }

        private static List<Vector3> WorldVertices(Transform t)
        {
            var list = new List<Vector3>();
            foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices) list.Add(mf.transform.TransformPoint(v));
            }
            foreach (SkinnedMeshRenderer smr in t.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null) continue;
                foreach (Vector3 v in smr.sharedMesh.vertices) list.Add(smr.transform.TransformPoint(v));
            }
            return list;
        }

        // Turns t about its centre so its thinnest side faces up (a turtle on its belly, a shark on its side).
        private static void LayFlat(Transform t)
        {
            List<Vector3> verts = WorldVertices(t);
            if (verts.Count == 0) return;
            Vector3[] axes = { t.right, t.up, t.forward };
            int thin = 0;
            float thinSize = float.MaxValue;
            for (int a = 0; a < 3; a++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (Vector3 v in verts) { float d = Vector3.Dot(v, axes[a]); lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d); }
                if (hi - lo < thinSize) { thinSize = hi - lo; thin = a; }
            }
            Vector3 dir = axes[thin].y < 0f ? -axes[thin] : axes[thin];
            RotateAbout(t, Quaternion.FromToRotation(dir, Vector3.up), Centre(verts));
        }

        private static Vector3 Centre(List<Vector3> verts)
        {
            Bounds b = new Bounds(verts[0], Vector3.zero);
            foreach (Vector3 v in verts) b.Encapsulate(v);
            return b.center;
        }

        private static void RotateAbout(Transform t, Quaternion delta, Vector3 pivot)
        {
            t.position = pivot + delta * (t.position - pivot);
            t.rotation = delta * t.rotation;
        }

        private static float SeabedUnder(Vector3 centre, float reach)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < 9; i++)
            {
                Vector3 o = centre + (i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * reach);
                o.y = centre.y + 3f;
                float best = float.MinValue;
                foreach (RaycastHit hit in Physics.RaycastAll(o, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
                    best = Mathf.Max(best, hit.point.y);
                }
                if (best > float.MinValue) { sum += best; n++; }
            }
            return n > 0 ? sum / n : centre.y - 1f;
        }

        private static void RestOnGround(Transform t, float gap)
        {
            List<Vector3> verts = WorldVertices(t);
            if (verts.Count == 0) return;
            float lowest = float.MaxValue;
            foreach (Vector3 v in verts) lowest = Mathf.Min(lowest, v.y);
            Vector3 c = Centre(verts);
            t.position += Vector3.up * (SeabedUnder(c, 0.4f) + gap - lowest);
        }

        // Levels the net, squashes its dome down until it just clears the animals under it, and sets its rim on the sand.
        private static float DrapeNet(Transform net, List<Transform> creatures)
        {
            Vector3[] axes = { net.right, net.up, net.forward };
            int upAxis = 0;
            for (int a = 1; a < 3; a++) if (Mathf.Abs(axes[a].y) > Mathf.Abs(axes[upAxis].y)) upAxis = a;
            Vector3 dir = axes[upAxis].y < 0f ? -axes[upAxis] : axes[upAxis];
            List<Vector3> verts = WorldVertices(net);
            if (verts.Count == 0) return 0f;
            RotateAbout(net, Quaternion.FromToRotation(dir, Vector3.up), Centre(verts));

            verts = WorldVertices(net);
            Vector3 centre = Centre(verts);
            float baseY = float.MaxValue, peakY = float.MinValue, reach = 0f;
            foreach (Vector3 v in verts)
            {
                baseY = Mathf.Min(baseY, v.y);
                peakY = Mathf.Max(peakY, v.y);
                reach = Mathf.Max(reach, new Vector2(v.x - centre.x, v.z - centre.z).magnitude);
            }
            float rimY = SeabedUnder(centre, reach * 0.6f) - 0.05f;

            // Height needed over each animal, against the dome's height at that spot.
            float factor = 0.45f / Mathf.Max(0.01f, peakY - baseY);
            float tallest = 0f;
            foreach (Transform c in creatures)
            {
                List<Vector3> body = WorldVertices(c);
                if (body.Count == 0) continue;
                Vector3 cc = Centre(body);
                if (new Vector2(cc.x - centre.x, cc.z - centre.z).magnitude > reach) continue;
                float top = float.MinValue;
                foreach (Vector3 v in body) top = Mathf.Max(top, v.y);
                float dome = float.MinValue;
                foreach (Vector3 v in verts)
                {
                    if (new Vector2(v.x - cc.x, v.z - cc.z).sqrMagnitude < 0.36f) dome = Mathf.Max(dome, v.y);
                }
                tallest = Mathf.Max(tallest, top - rimY);
                if (dome - baseY < 0.05f) continue;
                factor = Mathf.Max(factor, (top + 0.06f - rimY) / (dome - baseY));
            }
            // A crumpled net's highest fold shouldn't stand far above the tallest animal.
            if (tallest > 0f) factor = Mathf.Min(factor, (tallest + 0.25f) / Mathf.Max(0.01f, peakY - baseY));
            factor = Mathf.Min(1f, factor);

            Vector3 scale = net.localScale;
            scale[upAxis] *= factor;
            net.localScale = scale;

            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (Vector3 v in WorldVertices(net)) { lowest = Mathf.Min(lowest, v.y); highest = Mathf.Max(highest, v.y); }
            net.position += Vector3.up * (rimY - lowest);
            return highest - lowest;
        }

        private static bool HasOutOfRangeReading(WaterCalibrationMinigame game)
        {
            var so = new SerializedObject(game);
            SerializedProperty r = so.FindProperty("_readings");
            return !WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.PH, r.FindPropertyRelative("ph").floatValue)
                || !WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.Salinity, r.FindPropertyRelative("salinity").floatValue)
                || !WaterCalibrationMinigame.IsNormal(WaterCalibrationMinigame.Param.Temperature, r.FindPropertyRelative("temperature").floatValue);
        }

        private static void InstallCoralSitePrep(AreaMinigameStep step, MinigameDifficultyProfile profile, System.Text.StringBuilder report)
        {
            var models = new List<GameObject>();
            foreach (string name in new[] { "Coral", "Coral1", "Coral2", "Coral3", "Coral4", "Coral5", "Coral6" })
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>($"{CoralModelsFolder}/{name}.fbx");
                if (m != null) models.Add(m);
            }
            GameObject seed = AssetDatabase.LoadAssetAtPath<GameObject>(CoralSeedModelPath);
            foreach (Transform area in step.transform)
            {
                if (area.GetComponent<AreaStation>() == null) continue;
                if (SwapPlaceholder<CoralSitePrepMinigame>(area.gameObject, out CoralSitePrepMinigame game))
                {
                    game.EditorSetup(profile, models.ToArray(), seed);
                    FrameViewPoint(area);
                    report.AppendLine($"Installed coral site prep at {area.name}.");
                }
                SetupCoralSiteLook(game);
            }
        }

        private const string CoralAlgaeMat = "Assets/Lalim/Materials/M_CoralAlgae.mat";
        private const string CoralPlatformRock = "Assets/Lalim/Models/sea creatures + elements (fbx)/Rock8.fbx";
        private const string ReefCoralFolder = "Assets/Lalim/Prefabs/Corals";
        private static readonly string[] ReefKinds = { "plantcoral", "coral", "plantsponge", "sponge", "plantanemone", "miscanemone", "planturchins", "plantfunnel", "misccluster" };

        /// <summary>
        /// Prefabs of the level's own living reef pieces (LuzonScene's Garden_* coral, sponges,
        /// anemones, urchins, funnels, clusters), one per mesh + material, each standing upright with
        /// its base at the pivot. Existing prefabs in Prefabs/Corals are reused.
        /// </summary>
        public static List<GameObject> EnsureReefCoralPrefabs()
        {
            var existing = AssetDatabase.FindAssets("t:Prefab", new[] { ReefCoralFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(g => g != null).ToList();
            if (existing.Count > 0) return existing;

            if (!AssetDatabase.IsValidFolder(ReefCoralFolder)) AssetDatabase.CreateFolder("Assets/Lalim/Prefabs", "Corals");
            var made = new List<GameObject>();
            var seen = new HashSet<string>();
            var perKind = new Dictionary<string, int>();
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("Garden_")) continue;
                string kind = ReefKinds.FirstOrDefault(k => r.name.StartsWith("Garden_" + k + "_"));
                var mf = r.GetComponent<MeshFilter>();
                if (kind == null || mf == null || mf.sharedMesh == null || r.sharedMaterial == null) continue;
                float size = r.bounds.size.magnitude;
                if (size < 0.8f || size > 7f) continue;                       // skip specks and walls of sponge
                string key = mf.sharedMesh.name + "|" + r.sharedMaterial.name;
                if (!seen.Add(key)) continue;
                perKind.TryGetValue(kind, out int n);
                if (n >= 3) continue;
                perKind[kind] = n + 1;

                var root = new GameObject($"PF_Reef_{kind}_{n + 1}");
                var child = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root.transform, false);
                child.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = child.GetComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials;
                // Same orientation as in the level (the Blender imports are rotated), unit-ish scale.
                child.transform.rotation = r.transform.rotation;
                Vector3 lossy = r.transform.lossyScale;
                child.transform.localScale = lossy / Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));
                // Base at the pivot, centred.
                Bounds b = mr.bounds;
                child.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z) - root.transform.position;
                string path = $"{ReefCoralFolder}/{root.name}.prefab";
                made.Add(PrefabUtility.SaveAsPrefabAsset(root, path));
                Object.DestroyImmediate(root);
            }
            return made;
        }

        /// <summary>
        /// Moves each coral replanting station to the nearest flat, open patch of seabed (within 12 m):
        /// level under the work slab, nothing growing or lying on it, open water above for the camera.
        /// </summary>
        public static string PlaceCoralSitesOnFlatGround()
        {
            var report = new System.Text.StringBuilder();
            var blockers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)
                            && r.bounds.size.magnitude < 18f && !r.name.StartsWith("Terrain") && r.GetComponentInParent<AreaStation>() == null)
                .Select(r => r.bounds).ToList();

            foreach (CoralSitePrepMinigame game in Object.FindObjectsByType<CoralSitePrepMinigame>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform area = game.transform;
                Vector3 start = area.position;
                Vector3 best = Vector3.zero;
                float bestScore = float.MaxValue, bestRange = 0f;
                for (float dx = -12f; dx <= 12f; dx += 1f)
                for (float dz = -12f; dz <= 12f; dz += 1f)
                {
                    Vector3 c = new Vector3(start.x + dx, start.y + 8f, start.z + dz);
                    if (!Physics.Raycast(c, Vector3.down, out RaycastHit centre, 30f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!centre.collider.name.StartsWith("Terrain")) continue;
                    float lo = centre.point.y, hi = centre.point.y;
                    bool ok = true;
                    for (int i = 0; i < 12 && ok; i++)
                    {
                        float a = i * Mathf.PI / 6f;
                        Vector3 p = centre.point + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.7f;
                        if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out RaycastHit h, 12f, ~0, QueryTriggerInteraction.Ignore)
                            || !h.collider.name.StartsWith("Terrain")) { ok = false; break; }
                        lo = Mathf.Min(lo, h.point.y);
                        hi = Mathf.Max(hi, h.point.y);
                    }
                    float range = hi - lo;
                    if (!ok || range > 0.2f) continue;
                    // Open water above (top-down camera) and nothing on the slab's footprint.
                    if (Physics.Raycast(centre.point + Vector3.up * 0.5f, Vector3.up, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var foot = new Bounds(centre.point + Vector3.up * 1f, new Vector3(3.6f, 2f, 3.6f));
                    if (blockers.Any(b => b.Intersects(foot))) continue;
                    float score = range * 20f + new Vector2(dx, dz).magnitude * 0.1f;
                    if (score < bestScore) { bestScore = score; best = centre.point; bestRange = range; }
                }
                if (bestScore == float.MaxValue) { report.AppendLine($"{area.name}: no flat open spot within 12 m, left where it is"); continue; }

                Undo.RecordObject(area, "Coral site on flat ground");
                Vector3 old = area.position;
                area.position = best + Vector3.up * 1.5f;   // stations float 1.5 m over their spot
                // Keep the view point the same distance and direction from the station.
                Transform view = area.Find("ViewPoint");
                if (view != null) Undo.RecordObject(view, "Coral site on flat ground");
                report.AppendLine($"{area.name}: moved {Vector3.Distance(new Vector3(old.x, 0f, old.z), new Vector3(best.x, 0f, best.z)):F1} m to flat ground (height range {bestRange * 100f:F0} cm)");
            }
            return report.ToString();
        }

        /// <summary>
        /// The algae layer's material (URP Unlit with alpha clipping, so brushed patches vanish and the
        /// variant ships in builds) and the hint font of the glass card.
        /// </summary>
        public static void SetupCoralSiteLook(CoralSitePrepMinigame game)
        {
            if (game == null) return;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CoralAlgaeMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, CoralAlgaeMat);
            }
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f);
            mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);
            game.EditorSetupLook(mat, AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Lalim/Fonts/PlusJakartaSans-Medium SDF Padded.asset"),
                                 AssetDatabase.LoadAssetAtPath<GameObject>(CoralPlatformRock));
            // The reef's own coral, as prefabs made from the level's Garden_* pieces (their own colours).
            List<GameObject> reef = EnsureReefCoralPrefabs();
            if (reef.Count > 0)
                game.EditorSetupCorals(reef.Where(g => g.name.Contains("coral")).ToArray(), reef.ToArray());
            EditorUtility.SetDirty(game);
        }

        /// <summary>Replaces the placeholder minigame with T (only once — an existing T is kept as tuned).</summary>
        private static bool SwapPlaceholder<T>(GameObject go, out T game) where T : Component
        {
            game = go.GetComponent<T>();
            if (game != null) return false;
            var placeholder = go.GetComponent<PlaceholderAreaMinigame>();
            if (placeholder != null) Object.DestroyImmediate(placeholder);
            game = go.AddComponent<T>();
            return true;
        }

        /// <summary>Puts the station's view point above and behind the work spot, looking down at it.</summary>
        private static void FrameViewPoint(Transform area)
        {
            Transform view = area.Find("ViewPoint");
            if (view == null) return;
            Vector3 ground = area.position - Vector3.up * 1.5f;
            if (Physics.Raycast(area.position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point;
            view.position = ground + new Vector3(0f, 2.4f, -2.6f);
            view.rotation = Quaternion.LookRotation((ground + Vector3.up * 0.3f) - view.position);
        }

        private static readonly string[] CoralWords = { "coral", "reef", "sponge", "garden", "anemone", "kelp", "seaweed", "plant" };

        /// <summary>
        /// Water readings view: the diver's eyes a little back from the spot, looking slightly down
        /// at the densest coral around it (searching wider if the spot is bare), with a clear line of sight.
        /// Returns a short report, or null if no coral was found (the view is left as it was).
        /// </summary>
        public static string FrameWaterViewPoint(Transform area)
        {
            Transform view = area.Find("ViewPoint");
            if (view == null) return null;
            Vector3 ground = area.position - Vector3.up * 1.5f;
            if (Physics.Raycast(area.position + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point;

            var corals = new List<Renderer>();
            foreach (float radius in new[] { 12f, 20f })
            {
                corals.Clear();
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    string n = r.name.ToLowerInvariant();
                    if (!CoralWords.Any(n.Contains) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    Vector3 d = r.bounds.center - ground;
                    d.y = 0f;
                    if (d.magnitude >= 1.5f && d.magnitude <= radius) corals.Add(r);
                }
                if (corals.Count >= 4) break;
            }
            if (corals.Count == 0) return null;

            // Try twelve headings. Each is scored by the coral in front of it (real coral counts more
            // than kelp or seaweed; bigger and closer counts more) times how much of the view is clear.
            // The eye may move up to 8 m toward the coral so it sits ~6 m away, never inside rock.
            Vector3 bestDir = Vector3.forward, bestEye = ground + Vector3.up * 2.2f, bestTarget = ground;
            float bestScore = -1f;
            int bestCount = 0;
            for (int a = 0; a < 360; a += 30)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                float score = 0f, distSum = 0f;
                int count = 0;
                foreach (Renderer r in corals)
                {
                    Vector3 d = r.bounds.center - ground;
                    Vector3 flat = new Vector3(d.x, 0f, d.z);
                    if (Vector3.Angle(flat, dir) > 35f || d.y > 6f) continue;   // skip coral up on the cliffs
                    string n = r.name.ToLowerInvariant();
                    float kind = n.Contains("coral") ? 3f : n.Contains("sponge") || n.Contains("anemone") ? 1.5f : 0.7f;
                    float w = kind * Mathf.Min(r.bounds.size.magnitude, 5f) / Mathf.Max(2f, flat.magnitude);
                    score += w;
                    distSum += flat.magnitude * w;
                    count++;
                }
                if (count == 0) continue;
                float clusterDist = distSum / score;
                float advance = Mathf.Clamp(clusterDist - 6f, -2.5f, 8f);
                Vector3 eye = ground + Vector3.up * 2.2f + dir * advance;
                for (int k = 0; k < 6 && Physics.CheckSphere(eye, 0.5f, ~0, QueryTriggerInteraction.Ignore); k++) eye -= dir * 0.6f;
                if (Physics.CheckSphere(eye, 0.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                float lookDist = Mathf.Clamp(clusterDist - advance, 3f, 9f);
                Vector3 target = ground + dir * (advance + lookDist) + Vector3.up * 0.6f;

                // How much of the view is open: five rays across it, each must get most of the way there.
                int clear = 0;
                foreach (float yaw in new[] { -25f, -12f, 0f, 12f, 25f })
                {
                    Vector3 ray = Quaternion.Euler(0f, yaw, 0f) * (target - eye);
                    if (!Physics.Raycast(eye, ray.normalized, out RaycastHit h, ray.magnitude * 0.75f, ~0, QueryTriggerInteraction.Ignore)
                        || (h.collider.GetComponentInParent<Renderer>() is Renderer hr && corals.Contains(hr))) clear++;
                }
                if (clear < 3) continue;
                score *= clear / 5f;
                if (score > bestScore) { bestScore = score; bestDir = dir; bestEye = eye; bestTarget = target; bestCount = count; }
            }
            if (bestScore < 0f) return null;

            Undo.RecordObject(view, "Frame water view");
            view.position = bestEye;
            view.rotation = Quaternion.LookRotation(bestTarget - bestEye, Vector3.up);
            return $"{area.name}: facing {bestCount} coral pieces";
        }

        private static void RemoveCoralReplantingKit(System.Text.StringBuilder report)
        {
            var kit = AssetDatabase.LoadAssetAtPath<ItemData>(OldCoralKitPath);
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            if (db != null)
            {
                var so = new SerializedObject(db);
                SerializedProperty list = so.FindProperty("_items");
                // Rebuild the list without the kit (and without empty slots).
                var keep = new List<ItemData>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    var item = list.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
                    if (item != null && item.itemId != "05_coralreplantingkit") keep.Add(item);
                }
                if (keep.Count != list.arraySize)
                {
                    list.arraySize = keep.Count;
                    for (int i = 0; i < keep.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // The scene's starter-item list (editor testing) must not hand out coral gear for free.
            InventoryManager inv = Object.FindAnyObjectByType<InventoryManager>(FindObjectsInactive.Include);
            if (inv != null)
            {
                var so = new SerializedObject(inv);
                SerializedProperty starters = so.FindProperty("_starterItemIds");
                if (starters != null)
                {
                    for (int i = starters.arraySize - 1; i >= 0; i--)
                    {
                        string id = starters.GetArrayElementAtIndex(i).stringValue;
                        if (id == "05_coralreplantingkit" || id == CoralSeedId) starters.DeleteArrayElementAtIndex(i);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (kit != null)
            {
                AssetDatabase.DeleteAsset(OldCoralKitPath);
                report.AppendLine("Deleted the Coral Replanting Kit item.");
            }
        }

        private static void EnsureBuildSettings(System.Text.StringBuilder report)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            report.AppendLine("Build Settings: added 02_Luzon.");
        }

        // DDAManager normally comes from 00_BootSequence. This scene already contains GameManager,
        // so DevBootstrapper would never load the boot scene — give the scene its own DDAManager
        // (a duplicate from the boot sequence destroys itself).
        private static void EnsureDdaManager(System.Text.StringBuilder report)
        {
            if (Object.FindAnyObjectByType<DDAManager>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("[DDAManager]");
            var dda = go.AddComponent<DDAManager>();
            var so = new SerializedObject(dda);
            so.FindProperty("_config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DDAConfig>(DdaConfigPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine("Added [DDAManager] with DDAConfig.");
        }

        /// <summary>Adds the context action button to GameHUD.prefab once; never touches existing children.</summary>
        private static void EnsureActionButton(System.Text.StringBuilder report)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(GameHudPrefabPath);
            try
            {
                if (prefabRoot.transform.Find("ActionButton") != null) return;

                var go = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
                go.transform.SetParent(prefabRoot.transform, false);
                Transform pause = prefabRoot.transform.Find("PauseModal");
                if (pause != null) go.transform.SetSiblingIndex(pause.GetSiblingIndex());

                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-720f, 330f);
                rt.sizeDelta = new Vector2(150f, 150f);

                var bg = go.GetComponent<UnityEngine.UI.Image>();
                bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconInteractPath);
                bg.preserveAspect = true;

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                labelGo.transform.SetParent(go.transform, false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.anchorMin = new Vector2(0.5f, 0f);
                lrt.anchorMax = new Vector2(0.5f, 0f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -6f);
                lrt.sizeDelta = new Vector2(420f, 60f);
                var label = labelGo.GetComponent<TMPro.TextMeshProUGUI>();
                label.alignment = TMPro.TextAlignmentOptions.Top;
                label.fontSize = 30f;
                label.color = Color.white;
                label.raycastTarget = false;

                var button = go.AddComponent<InteractActionButton>();
                var so = new SerializedObject(button);
                so.FindProperty("_group").objectReferenceValue = go.GetComponent<CanvasGroup>();
                so.FindProperty("_button").objectReferenceValue = go.GetComponent<UnityEngine.UI.Button>();
                so.FindProperty("_icon").objectReferenceValue = bg;
                so.FindProperty("_label").objectReferenceValue = label;
                so.FindProperty("_interactSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(IconInteractPath);
                so.FindProperty("_chatSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(IconChatPath);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, GameHudPrefabPath);
                report.AppendLine("Added ActionButton (chat/interact icon) to GameHUD.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        // ───────────────────────── Scene pieces ─────────────────────────

        private static StatueDialogueRouter EnsureStatue(Transform env, string name, string deity, System.Text.StringBuilder report,
            List<DialogueLine> quest, List<DialogueLine> closing, List<DialogueLine> idle, List<StatueDialogueRouter.NamedLines> barks)
        {
            Transform t = env.Find(name);
            if (t == null) { report.AppendLine($"Statue {name} not found."); return null; }

            GameObject go = t.gameObject;
            go.layer = LayerMask.NameToLayer(GameConstants.Layers.INTERACTABLE);
            if (go.GetComponent<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                }
            }

            StatueDialogueRouter router = go.GetComponent<StatueDialogueRouter>();
            bool isNew = router == null;
            if (isNew) router = go.AddComponent<StatueDialogueRouter>();

            // The old Amansinaya placeholder lines (fixed "twenty-five", turn-in flow) are replaced once;
            // after that, hand edits in the Inspector are kept.
            var so = new SerializedObject(router);
            bool migrated = so.FindProperty("_questLines").arraySize > 0 && so.FindProperty("_closingLines").arraySize == 0
                            && so.FindProperty("_barks").arraySize == 0;
            if (isNew || migrated || so.FindProperty("_questLines").arraySize == 0)
            {
                router.EditorReplaceScripts(deity, quest, closing, idle, barks);
                report.AppendLine($"Installed {deity}'s dialogue.");
            }
            else router.EditorFillIfEmpty(deity, quest, closing, idle, barks);

            // Lines rewritten since the first install: replace exact old text, keep any other hand edits.
            int updated = ReplaceOldLines(router, quest, barks);
            if (updated > 0) report.AppendLine($"Updated {updated} {deity} line(s).");

            // Barks added since the first install (e.g. Amansinaya's "trash_done" talk).
            foreach (StatueDialogueRouter.NamedLines bark in barks)
            {
                if (router.GetBark(bark.key) != null) continue;
                router.EditorSetBark(bark.key, bark.lines);
                report.AppendLine($"Added {deity}'s '{bark.key}' lines.");
            }

            EditorUtility.SetDirty(router);
            return router;
        }

        // Old line text → index of the replacement in the current script (quest lines, then bark lines).
        private static readonly string[] RetiredLines =
        {
            "Hold the needles steady, then tell me what is wrong. Only a true reading can guide a true course.",
            "Now you see the storm. Sour water, wrong salt, fevered heat. No coral could live in this.",
            "Take these seeds. Clear the dead ground at each place you tested, then plant them where the rock is clean.",
            "Then lay each piece in its rightful bowl. What is sorted truly can be made new again. Go, and be quick. The poison does not wait.",
        };

        private static int ReplaceOldLines(StatueDialogueRouter router, List<DialogueLine> quest, List<StatueDialogueRouter.NamedLines> barks)
        {
            var so = new SerializedObject(router);
            int changed = 0;

            void Sync(SerializedProperty list, List<DialogueLine> current)
            {
                for (int i = 0; i < list.arraySize && i < current.Count; i++)
                {
                    SerializedProperty text = list.GetArrayElementAtIndex(i).FindPropertyRelative("text");
                    if (System.Array.IndexOf(RetiredLines, text.stringValue) >= 0 && text.stringValue != current[i].text)
                    {
                        text.stringValue = current[i].text;
                        changed++;
                    }
                }
            }

            Sync(so.FindProperty("_questLines"), quest);
            SerializedProperty barkProp = so.FindProperty("_barks");
            for (int b = 0; b < barkProp.arraySize; b++)
            {
                SerializedProperty entry = barkProp.GetArrayElementAtIndex(b);
                string key = entry.FindPropertyRelative("key").stringValue;
                var match = barks.Find(n => n.key == key);
                if (match != null) Sync(entry.FindPropertyRelative("lines"), match.lines);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static ZoneQuest EnsureZone(Transform root, string name, string zoneId, string display, StatueDialogueRouter statue, float minZ, float maxZ)
        {
            GameObject go = FindOrCreate(name, root);
            ZoneQuest zone = Add<ZoneQuest>(go);

            GameObject toxicGo = FindOrCreate("ToxicWater", go.transform);
            float minY = name.StartsWith("Zone3") ? -120f : MinY;
            toxicGo.transform.position = At((MinX + MaxX) * 0.5f, (minY + MaxY) * 0.5f, (minZ + maxZ) * 0.5f);
            toxicGo.tag = GameConstants.Tags.TOXIC_WATER;
            BoxCollider box = Add<BoxCollider>(toxicGo);
            box.isTrigger = true;
            box.size = new Vector3(MaxX - MinX, MaxY - minY, maxZ - minZ);
            ToxicWaterVolume toxic = Add<ToxicWaterVolume>(toxicGo);
            var so = new SerializedObject(toxic);
            so.FindProperty("_severity").enumValueIndex = (int)ToxicSeverity.Light;
            so.FindProperty("_useLevelRate").boolValue = true;
            so.FindProperty("_slowsStaminaRegen").boolValue = false; // zone-wide pollution: HP drain only
            so.ApplyModifiedPropertiesWithoutUndo();

            zone.EditorSetup(zoneId, display, Region.Luzon, statue, toxic, new List<QuestStep>(zone.Steps));
            return zone;
        }

        private const string FoggyBarrierPrefab = "Assets/Lalim/Prefabs/LevelFlow/PF_FoggyBarrier.prefab";

        /// <summary>Places a PF_FoggyBarrier (zone 2's barrier as a prefab). Replaces a plain collider-only barrier.</summary>
        private static GameObject EnsureBarrier(Transform root, GameObject template, string name, Vector3 center, Vector3 scale,
                                                string message, System.Text.StringBuilder report)
        {
            Transform existing = Find(name);
            if (existing != null && existing.GetComponent<MeshRenderer>() != null)
            {
                SetBarrierMessage(existing.gameObject, message);
                return existing.gameObject;
            }
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject go;
            var barrierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FoggyBarrierPrefab);
            if (barrierPrefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab, root);
                go.name = name;
            }
            else if (template != null)
            {
                go = Object.Instantiate(template, root);
                go.name = name;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(root, false);
            }
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.SetActive(true);
            SetBarrierMessage(go, message);
            report.AppendLine($"Built {name} (foggy water barrier).");
            return go;
        }

        private static void SetBarrierMessage(GameObject barrier, string message)
        {
            if (barrier == null) return;
            BarrierNotification note = Add<BarrierNotification>(barrier);
            var so = new SerializedObject(note);
            so.FindProperty("_warningMessage").stringValue = message;
            so.FindProperty("_checkRegion").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureMusic(ProximityMusic music, System.Text.StringBuilder report)
        {
            // A long looping track: stream it instead of decompressing ~24 MB into memory on phones.
            if (AssetImporter.GetAtPath(MusicPath) is AudioImporter importer && importer.defaultSampleSettings.loadType != AudioClipLoadType.Streaming)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                report.AppendLine("Music import set to Streaming.");
            }

            var so = new SerializedObject(music);
            SerializedProperty track = so.FindProperty("_defaultTrack");
            if (track.objectReferenceValue != null) return;
            track.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            report.AppendLine("Music: Fairy Water Cave.");
        }

        // Dialogue, quest box and toast come from their prefabs (edit the prefab, not the scene).
        private static void EnsureDialogueUI(System.Text.StringBuilder report)
        {
            string r = LevelUIPrefabs.UseInOpenScene();
            if (r != "Scene already uses the UI prefabs.") report.Append(r);
        }

        private static void EnsureCheckpoint(Transform parent, string id, string section, Vector3 position)
        {
            if (parent.Find(id) != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
            GameObject go = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : new GameObject(id);
            go.name = id;
            go.transform.position = position;
            Checkpoint cp = go.GetComponent<Checkpoint>();
            if (cp == null) return;
            var so = new SerializedObject(cp);
            so.FindProperty("_checkpointId").stringValue = id;
            so.FindProperty("_sectionId").stringValue = section;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Step<T>(ZoneQuest zone, string name, string stepId, string objective) where T : QuestStep
        {
            GameObject go = FindOrCreate(name, zone.transform);
            T step = Add<T>(go);
            if (string.IsNullOrEmpty(step.StepId)) step.EditorSetup(stepId, objective);
            return step;
        }

        private static DialogueStep Dialogue(ZoneQuest zone, string name, string stepId, DialogueStep.Kind kind, string barkKey)
        {
            var step = Step<DialogueStep>(zone, name, stepId, "");
            step.EditorSetupDialogue(kind, barkKey);
            return step;
        }

        private static GrantItemStep Grant(ZoneQuest zone, string name, string stepId, string itemId, string title, string body, bool announceWithQuestComplete = true)
        {
            var step = Step<GrantItemStep>(zone, name, stepId, "");
            step.EditorSetupItem(itemId, title, body, announceWithQuestComplete);
            return step;
        }

        private static CleanseZoneStep Cleanse(ZoneQuest zone, string name, string stepId, string title, string body)
        {
            var step = Step<CleanseZoneStep>(zone, name, stepId, "");
            step.EditorSetupToast(title, body);
            return step;
        }

        private static UnlockBarrierStep Unlock(ZoneQuest zone, string name, string stepId, GameObject barrier, Transform next)
        {
            var step = Step<UnlockBarrierStep>(zone, name, stepId, "");
            step.EditorSetupBarrier(barrier, next);
            return step;
        }

        private static AreaMinigameStep Areas(ZoneQuest zone, string name, string stepId, string objective, Vector3[] positions, string[] keys,
            string prefix, string prompt, ChallengeType type, string requiredItem, string placeholderName)
        {
            var step = Step<AreaMinigameStep>(zone, name, stepId, objective);
            var stations = new List<AreaStation>();
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject go = FindOrCreate($"{prefix}_Area_{keys[i].ToUpperInvariant()}", step.transform);
                bool fresh = go.GetComponent<AreaStation>() == null;
                if (fresh) go.transform.position = positions[i] + Vector3.up * 1.5f;
                go.layer = LayerMask.NameToLayer(GameConstants.Layers.INTERACTABLE);

                SphereCollider col = Add<SphereCollider>(go);
                col.isTrigger = true;
                col.radius = StationReach;   // generous: the button shows anywhere near the area

                GameObject view = FindOrCreate("ViewPoint", go.transform);
                if (fresh)
                {
                    view.transform.localPosition = new Vector3(0f, 1.5f, -3f);
                    view.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
                }

                GameObject marker = EnsureMarker(go.transform);

                AreaStation station = Add<AreaStation>(go);
                if (fresh) station.EditorSetup(keys[i], prompt, type, requiredItem, view.transform, marker);

                if (go.GetComponent<IAreaMinigame>() == null)
                {
                    var placeholder = go.AddComponent<PlaceholderAreaMinigame>();
                    placeholder.EditorSetup(placeholderName);
                }
                stations.Add(station);
            }
            step.EditorSetupStations(stations);
            return step;
        }

        // The altar's interact point: the sorting starts when the player taps it (glow ring on the sand).
        private static SortingStation EnsureSortStation(SegregationStep sort, Transform stand)
        {
            GameObject go = FindOrCreate("SortStation", sort.transform);
            // Low over the sand in front of the bowls, so its button sits at the diver's height.
            if (go.GetComponent<SortingStation>() == null && stand != null)
            {
                Vector3 p = stand.position;
                if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit sand, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) p.y = sand.point.y + 1.2f;
                go.transform.position = p;
            }
            go.layer = LayerMask.NameToLayer(GameConstants.Layers.INTERACTABLE);
            SphereCollider col = Add<SphereCollider>(go);
            col.isTrigger = true;
            col.radius = StationReach;
            GameObject marker = EnsureMarker(go.transform);
            Transform ring = marker.transform.Find("Ring");
            if (ring != null && Physics.Raycast(go.transform.position + Vector3.up * 20f, Vector3.down, out RaycastHit ground, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                ring.position = new Vector3(ring.position.x, ground.point.y + 0.08f, ring.position.z);
            SortingStation station = Add<SortingStation>(go);
            station.EditorSetup("Sort the trash", marker);
            return station;
        }

        private const float StationReach = 4f;
        private const string AreaMarkerMat = "Assets/Lalim/Materials/AreaMarker.mat";
        private const string AreaGlowTex = "Assets/Lalim/Textures/T_AreaGlow.png";

        // A subtle glow ring on the ground + a faint light, breathing slowly (AreaMarkerPulse).
        // An older solid-disc marker is upgraded in place.
        public static GameObject EnsureMarker(Transform parent)
        {
            Transform existing = parent.Find("Marker");
            GameObject marker = existing != null ? existing.gameObject : new GameObject("Marker");
            if (existing == null) marker.transform.SetParent(parent, false);

            Transform ringT = marker.transform.Find("Ring");
            if (ringT == null)
            {
                ringT = new GameObject("Ring", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                ringT.SetParent(marker.transform, false);
                ringT.localPosition = new Vector3(0f, -1.4f, 0f);
            }
            // A flat quad facing up (instead of a solid cylinder disc), drawing the soft ring texture.
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            ringT.GetComponent<MeshFilter>().sharedMesh = quad;
            ringT.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ringT.localScale = new Vector3(4f, 4f, 1f);
            var renderer = ringT.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AreaGlowMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Transform glowT = marker.transform.Find("Glow");
            Light light = glowT != null ? glowT.GetComponent<Light>() : null;
            if (light == null)
            {
                light = new GameObject("Glow").AddComponent<Light>();
                light.transform.SetParent(marker.transform, false);
            }
            light.type = LightType.Point;
            light.color = new Color(0.3f, 1f, 0.9f);
            light.range = 6f;
            light.intensity = 1f;
            light.shadows = LightShadows.None;

            if (marker.GetComponent<AreaMarkerPulse>() == null) marker.AddComponent<AreaMarkerPulse>();
            return marker;
        }

        private static Material AreaGlowMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AreaMarkerMat);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, AreaMarkerMat);
            }
            if (mat.shader.name != "Universal Render Pipeline/Unlit") mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat.SetFloat("_Surface", 1f);                 // transparent
            mat.SetFloat("_Blend", 2f);                   // additive: only ever a soft brightening
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(0.3f, 1f, 0.9f, 0.4f));
            mat.SetTexture("_BaseMap", AreaGlowTexture());
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A soft ring: brightest just inside the edge, fading out, with a faint centre.</summary>
        private static Texture2D AreaGlowTexture()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AreaGlowTex);
            if (tex != null) return tex;
            const int N = 256;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float r = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f).magnitude;
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.12f, 2f));
                float fill = Mathf.Clamp01(1f - r / 0.75f) * 0.18f;
                float a = Mathf.Clamp01(ring + fill) * Mathf.Clamp01((1f - r) / 0.12f);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
            t.SetPixels32(px);
            t.Apply();
            System.IO.File.WriteAllBytes(AreaGlowTex, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(AreaGlowTex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AreaGlowTex);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AreaGlowTex);
        }

        private static void SetupPollution(PollutionDirector pollution, Transform env, ZoneQuest[] zones, System.Text.StringBuilder report)
        {
            var so = new SerializedObject(pollution);
            SerializedProperty zonesProp = so.FindProperty("_zones");
            if (zonesProp.arraySize > 0) return; // already configured — keep hand edits

            zonesProp.arraySize = zones.Length;
            for (int i = 0; i < zones.Length; i++)
            {
                SerializedProperty z = zonesProp.GetArrayElementAtIndex(i);
                z.FindPropertyRelative("zoneId").stringValue = zones[i].ZoneId;
                Collider bounds = zones[i].ToxicVolume.GetComponent<Collider>();
                z.FindPropertyRelative("bounds").objectReferenceValue = bounds;

                var reef = new List<Renderer>();
                var life = new List<(GameObject go, float at)>();
                foreach (Transform child in env)
                {
                    if (!bounds.bounds.Contains(child.position)) continue;
                    if (ReefPrefixes.Any(p => child.name.StartsWith(p)))
                    {
                        reef.AddRange(child.GetComponentsInChildren<Renderer>());
                    }
                    else if (child.name.StartsWith("Creature_") && !child.name.StartsWith("Creature_Trapped"))
                    {
                        float at = 0f;
                        if (child.name.Contains("Dolphin") || child.name.Contains("Turtle") || child.name.Contains("Whale") || child.name.Contains("StingRay")) at = 0.8f;
                        else if (child.name.Contains("Octopus") || child.name.Contains("Squid") || child.name.Contains("Hammerhead")) at = 0.5f;
                        if (at > 0f) life.Add((child.gameObject, at));
                    }
                }

                SerializedProperty reefProp = z.FindPropertyRelative("reefRenderers");
                reefProp.arraySize = reef.Count;
                for (int r = 0; r < reef.Count; r++) reefProp.GetArrayElementAtIndex(r).objectReferenceValue = reef[r];

                SerializedProperty lifeProp = z.FindPropertyRelative("life");
                lifeProp.arraySize = life.Count;
                for (int l = 0; l < life.Count; l++)
                {
                    lifeProp.GetArrayElementAtIndex(l).FindPropertyRelative("target").objectReferenceValue = life[l].go;
                    lifeProp.GetArrayElementAtIndex(l).FindPropertyRelative("appearsAtLook").floatValue = life[l].at;
                }

                ParticleSystem murk = EnsureMurk(zones[i].transform, bounds);
                SerializedProperty murkProp = z.FindPropertyRelative("murk");
                murkProp.arraySize = 1;
                murkProp.GetArrayElementAtIndex(0).objectReferenceValue = murk;

                report.AppendLine($"Pollution {zones[i].ZoneId}: {reef.Count} reef renderers, {life.Count} returning creatures.");
            }

            var lights = new List<Light>();
            Transform sun = Find("Directional Light");
            if (sun != null && sun.GetComponent<Light>() != null) lights.Add(sun.GetComponent<Light>());
            foreach (Transform child in env)
            {
                if (!child.name.StartsWith("LIGHT_")) continue;
                if (child.name.Contains("Sunlight") || child.name.Contains("GodRay") || child.name.Contains("Skylight") || child.name.Contains("Shaft"))
                {
                    lights.AddRange(child.GetComponentsInChildren<Light>());
                }
            }
            SerializedProperty lightsProp = so.FindProperty("_lights");
            lightsProp.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++) lightsProp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Brown silt + plastic flecks hanging in the polluted water.
        private static ParticleSystem EnsureMurk(Transform zone, Collider bounds)
        {
            Transform existing = zone.Find("Murk");
            if (existing != null) return existing.GetComponent<ParticleSystem>();

            var go = new GameObject("Murk");
            go.transform.SetParent(zone, false);
            go.transform.position = bounds.bounds.center;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.4f, 0.25f, 0.55f), new Color(0.6f, 0.65f, 0.55f, 0.4f));
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 220f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.bounds.size.x * 0.8f, Mathf.Min(bounds.bounds.size.y, 60f), bounds.bounds.size.z * 0.9f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
            if (mat != null) renderer.sharedMaterial = mat;
            go.AddComponent<AmbientEffect>();
            return ps;
        }

        // ───────────────────────── Helpers ─────────────────────────

        private static Vector3 GroundAt(float x, float z, float fallbackY)
        {
            Vector3 p = At(x, 0f, z);
            if (Physics.Raycast(new Vector3(p.x, 20f + _offset.y, p.z), Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }
            return new Vector3(p.x, fallbackY + _offset.y, p.z);
        }

        private static Transform Find(string path)
        {
            GameObject go = GameObject.Find(path);
            if (go != null) return go.transform;
            // GameObject.Find skips inactive objects; search roots too.
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == path) return root.transform;
                Transform t = root.transform.Find(path);
                if (t != null) return t;
            }
            return null;
        }

        private static GameObject FindOrCreate(string name, Transform parent)
        {
            Transform t = parent != null ? parent.Find(name) : Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static T Add<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
