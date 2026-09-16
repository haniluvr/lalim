#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.Inventory;

namespace Lalim.Editor
{
    /// <summary>
    /// Editor utility to automatically create and configure required Scene Managers in 1 click.
    /// </summary>
    public static class SceneManagersUtility
    {
        [MenuItem("Tools/Lalim/Setup Scene Managers (1-Click)", false, 1)]
        public static void SetupSceneManagers()
        {
            int createdCount = 0;

            // 1. [GameManager]
            if (Object.FindObjectOfType<GameManager>() == null)
            {
                GameObject gmObj = new GameObject("[GameManager]");
                Undo.RegisterCreatedObjectUndo(gmObj, "Create [GameManager]");
                gmObj.AddComponent<GameManager>();
                createdCount++;
            }

            // 2. [ChallengeManager]
            if (Object.FindObjectOfType<ChallengeManager>() == null)
            {
                GameObject cmObj = new GameObject("[ChallengeManager]");
                Undo.RegisterCreatedObjectUndo(cmObj, "Create [ChallengeManager]");
                cmObj.AddComponent<ChallengeManager>();
                createdCount++;
            }

            // 3. [InputManager]
            if (Object.FindObjectOfType<InputManager>() == null)
            {
                GameObject imObj = new GameObject("[InputManager]");
                Undo.RegisterCreatedObjectUndo(imObj, "Create [InputManager]");
                imObj.AddComponent<InputManager>();
                createdCount++;
            }

            // 4. [InventoryManager]
            if (Object.FindObjectOfType<InventoryManager>() == null)
            {
                GameObject invObj = new GameObject("[InventoryManager]");
                Undo.RegisterCreatedObjectUndo(invObj, "Create [InventoryManager]");
                InventoryManager inv = invObj.AddComponent<InventoryManager>();

                // Try to find ItemDatabase asset
                string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    ItemDatabase db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
                    if (db != null)
                    {
                        SerializedObject so = new SerializedObject(inv);
                        SerializedProperty prop = so.FindProperty("_itemDatabase");
                        if (prop != null)
                        {
                            prop.objectReferenceValue = db;
                            so.ApplyModifiedProperties();
                        }
                    }
                }
                createdCount++;
            }

            // 5. [ToolEquipSystem]
            if (Object.FindObjectOfType<ToolEquipSystem>() == null)
            {
                GameObject toolObj = new GameObject("[ToolEquipSystem]");
                Undo.RegisterCreatedObjectUndo(toolObj, "Create [ToolEquipSystem]");
                toolObj.AddComponent<ToolEquipSystem>();
                createdCount++;
            }

            // 6. [InventoryUI]
            if (Object.FindObjectOfType<Lalim.UI.InventoryUI>() == null)
            {
                GameObject uiObj = new GameObject("[InventoryUI]");
                Undo.RegisterCreatedObjectUndo(uiObj, "Create [InventoryUI]");
                uiObj.AddComponent<Lalim.UI.InventoryUI>();
                createdCount++;
            }

            if (createdCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog(
                    "Scene Managers Setup",
                    $"Successfully created {createdCount} missing manager(s) at the root of your scene:\n\n" +
                    "• [GameManager]\n• [ChallengeManager]\n• [InputManager]\n• [InventoryManager]\n• [ToolEquipSystem]\n• [InventoryUI]\n\n" +
                    "Press Ctrl + S to save your scene.",
                    "OK"
                );
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Scene Managers Setup",
                    "All required Scene Managers are already present in this scene!",
                    "OK"
                );
            }
        }

        [MenuItem("Tools/Lalim/Create Water Sampling Zone (1-Click)", false, 20)]
        public static void CreateWaterSamplingZone()
        {
            // Determine zone index and unique zone ID
            var existingChallenges = Object.FindObjectsOfType<Challenges.WaterSamplingChallenge>();
            int zoneIndex = existingChallenges.Length + 1;
            string zoneName = $"WaterSampling_Zone_{zoneIndex:D2}";
            string zoneId = zoneIndex == 1 ? "luzon_zone_b" : $"luzon_zone_b_{zoneIndex}";

            // Determine spawn position (near SceneView camera or diver)
            Vector3 center = Vector3.zero;
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            if (player != null)
            {
                center = player.transform.position + player.transform.forward * 12f;
            }
            else if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                center = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * 15f;
            }

            // Offset position slightly if creating consecutive zones
            if (zoneIndex > 1)
            {
                center += (zoneIndex - 1) * new Vector3(22f, 0f, 22f);
            }

            // 1. Root Zone
            GameObject zoneObj = new GameObject(zoneName);
            zoneObj.transform.position = center;
            Undo.RegisterCreatedObjectUndo(zoneObj, $"Create {zoneName}");

            var challenge = zoneObj.AddComponent<Challenges.WaterSamplingChallenge>();
            SerializedObject chalSO = new SerializedObject(challenge);
            chalSO.FindProperty("_zoneId").stringValue = zoneId;
            chalSO.ApplyModifiedProperties();

            // 2. Zone Trigger Entry
            GameObject triggerObj = new GameObject("ZoneTrigger_Entry");
            triggerObj.transform.SetParent(zoneObj.transform, false);
            triggerObj.transform.localPosition = Vector3.zero;

            BoxCollider triggerBox = triggerObj.AddComponent<BoxCollider>();
            triggerBox.isTrigger = true;
            triggerBox.size = new Vector3(10f, 10f, 10f);

            ZoneTrigger zt = triggerObj.AddComponent<ZoneTrigger>();
            SerializedObject ztSO = new SerializedObject(zt);
            ztSO.FindProperty("_zoneId").stringValue = zoneId;
            ztSO.FindProperty("_region").enumValueIndex = (int)Region.Luzon;
            ztSO.FindProperty("_challengeType").enumValueIndex = (int)ChallengeType.WaterSampling;
            ztSO.FindProperty("_baseTaskQuantity").intValue = 3;
            ztSO.FindProperty("_baseTimeLimit").floatValue = 120f;
            ztSO.FindProperty("_requiredToolId").stringValue = "07_watersamplekit";
            ztSO.ApplyModifiedProperties();

            // 3. Create 3 Sample Points (pH, Salinity, Temperature)
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            Vector3[] offsets = {
                new Vector3(-5f, 1.5f, 6f),
                new Vector3(6f, -1.5f, 9f),
                new Vector3(0f, -3.5f, 14f)
            };
            Challenges.SampleParameter[] parameters = {
                Challenges.SampleParameter.pH,
                Challenges.SampleParameter.Salinity,
                Challenges.SampleParameter.Temperature
            };
            string[] names = {
                $"Surface Water pH Point ({zoneName})",
                $"Deep Water Salinity Point ({zoneName})",
                $"Thermal Vent Temp Point ({zoneName})"
            };

            System.Collections.Generic.List<Challenges.SamplePoint> pointsList = new System.Collections.Generic.List<Challenges.SamplePoint>();

            for (int i = 0; i < 3; i++)
            {
                GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                buoy.name = $"SamplePoint_{parameters[i]}_0{i + 1}";
                buoy.transform.SetParent(zoneObj.transform, false);
                buoy.transform.localPosition = offsets[i];
                buoy.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                if (interactableLayer != -1) buoy.layer = interactableLayer;

                SphereCollider sc = buoy.GetComponent<SphereCollider>();
                sc.isTrigger = false;
                sc.radius = 0.8f;

                Challenges.SamplePoint sp = buoy.AddComponent<Challenges.SamplePoint>();
                SerializedObject spSO = new SerializedObject(sp);
                spSO.FindProperty("_parameter").enumValueIndex = (int)parameters[i];
                spSO.FindProperty("_pointName").stringValue = names[i];
                spSO.FindProperty("_requiredToolId").stringValue = "07_watersamplekit";
                spSO.FindProperty("_samplingDuration").floatValue = 2.5f;
                spSO.FindProperty("_enableBubbleSeepage").boolValue = true;
                spSO.FindProperty("_enableMooringCable").boolValue = true;
                spSO.FindProperty("_enableIdleBob").boolValue = true;
                spSO.FindProperty("_enableBeaconLight").boolValue = true;
                spSO.FindProperty("_enableFloatingHUDTag").boolValue = true;
                spSO.ApplyModifiedProperties();

                pointsList.Add(sp);
            }

            // Wire into WaterSamplingChallenge
            chalSO.Update();
            SerializedProperty pointsProp = chalSO.FindProperty("_preplacedPoints");
            pointsProp.ClearArray();
            for (int i = 0; i < pointsList.Count; i++)
            {
                pointsProp.InsertArrayElementAtIndex(i);
                pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = pointsList[i];
            }
            chalSO.ApplyModifiedProperties();

            Selection.activeGameObject = zoneObj;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Water Sampling Challenge Created",
                $"Successfully created [{zoneName}] with unique Zone ID: '{zoneId}':\n\n" +
                "• Zone Trigger Entry (Requires Water Sample Kit [1])\n" +
                "• 3x Sample Points (pH, Salinity, Temperature)\n" +
                "• Built-in Bubble Seepage, Mooring Cables & Beacon Lights\n" +
                "• Scientific Digital Readouts & Floating HUD Badges\n" +
                "• Post-Sampling Water Safety Replanting Pop-up\n\n" +
                "Press Ctrl + S to save your scene.",
                "OK"
            );
        }

        [MenuItem("Tools/Lalim/Create Coral Replanting Zone (1-Click)", false, 25)]
        public static void CreateCoralReplantingZone()
        {
            // Determine zone index and unique zone ID
            var existingChallenges = Object.FindObjectsOfType<Challenges.CoralReplantingChallenge>();
            int zoneIndex = existingChallenges.Length + 1;
            string zoneName = $"CoralReplanting_Zone_{zoneIndex:D2}";
            string zoneId = zoneIndex == 1 ? "luzon_zone_c" : $"luzon_zone_c_{zoneIndex}";

            // Coral foliage prefabs from lowpolyoceanpack
            string[] coralPrefabPaths = new string[]
            {
                "Assets/Davis3D/LowpolyOceanPack/Prefabs/Foliage/SM_Plant_Coral_1 Variant.prefab",
                "Assets/Davis3D/LowpolyOceanPack/Prefabs/Foliage/SM_Plant_Coral_2 Variant.prefab",
                "Assets/Davis3D/LowpolyOceanPack/Prefabs/Foliage/SM_Plant_Coral_1B Variant.prefab",
                "Assets/Davis3D/LowpolyOceanPack/Prefabs/Foliage/SM_Plant_Coral_2B Variant.prefab"
            };
            int coralIdx = (zoneIndex - 1) % coralPrefabPaths.Length;
            GameObject coralPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(coralPrefabPaths[coralIdx]);

            // Determine spawn position (near SceneView camera or diver)
            Vector3 center = Vector3.zero;
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            if (player != null)
            {
                center = player.transform.position + player.transform.forward * 12f;
            }
            else if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                center = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * 15f;
            }

            if (zoneIndex > 1)
            {
                center += (zoneIndex - 1) * new Vector3(25f, 0f, 25f);
            }

            // 1. Root Zone
            GameObject zoneObj = new GameObject(zoneName);
            zoneObj.transform.position = center;
            Undo.RegisterCreatedObjectUndo(zoneObj, $"Create {zoneName}");

            var challenge = zoneObj.AddComponent<Challenges.CoralReplantingChallenge>();
            SerializedObject chalSO = new SerializedObject(challenge);
            chalSO.FindProperty("_zoneId").stringValue = zoneId;
            chalSO.ApplyModifiedProperties();

            // 2. Zone Trigger Entry
            GameObject triggerObj = new GameObject("ZoneTrigger_Entry");
            triggerObj.transform.SetParent(zoneObj.transform, false);
            triggerObj.transform.localPosition = Vector3.zero;

            BoxCollider triggerBox = triggerObj.AddComponent<BoxCollider>();
            triggerBox.isTrigger = true;
            triggerBox.size = new Vector3(12f, 10f, 12f);

            ZoneTrigger zt = triggerObj.AddComponent<ZoneTrigger>();
            SerializedObject ztSO = new SerializedObject(zt);
            ztSO.FindProperty("_zoneId").stringValue = zoneId;
            ztSO.FindProperty("_region").enumValueIndex = (int)Region.Luzon;
            ztSO.FindProperty("_challengeType").enumValueIndex = (int)ChallengeType.CoralReplanting;
            ztSO.FindProperty("_baseTaskQuantity").intValue = 4;
            ztSO.FindProperty("_baseTimeLimit").floatValue = 150f;
            ztSO.FindProperty("_requiredToolId").stringValue = "05_coralreplantingkit";
            ztSO.ApplyModifiedProperties();

            int interactableLayer = LayerMask.NameToLayer("Interactable");

            // 3. Create 4 Substrate Slots
            Vector3[] slotOffsets = {
                new Vector3(-4f, -1.0f, 5f),
                new Vector3(4f, -1.2f, 6f),
                new Vector3(-3f, -1.5f, 10f),
                new Vector3(3f, -1.3f, 11f)
            };

            System.Collections.Generic.List<Challenges.CoralSlot> slotsList = new System.Collections.Generic.List<Challenges.CoralSlot>();

            for (int i = 0; i < slotOffsets.Length; i++)
            {
                GameObject slotObj = new GameObject($"CoralSlot_0{i + 1}");
                slotObj.transform.SetParent(zoneObj.transform, false);
                slotObj.transform.localPosition = slotOffsets[i];
                if (interactableLayer != -1) slotObj.layer = interactableLayer;

                SphereCollider slotCol = slotObj.AddComponent<SphereCollider>();
                slotCol.isTrigger = false;
                slotCol.radius = 0.85f;

                // Substrate disc visual
                GameObject emptyVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                emptyVisual.name = "SubstratePlug";
                emptyVisual.transform.SetParent(slotObj.transform, false);
                emptyVisual.transform.localPosition = new Vector3(0, -0.05f, 0);
                emptyVisual.transform.localScale = new Vector3(0.65f, 0.06f, 0.65f);
                Object.DestroyImmediate(emptyVisual.GetComponent<Collider>());

                Renderer emptyRenderer = emptyVisual.GetComponent<Renderer>();
                if (emptyRenderer != null)
                {
                    emptyRenderer.material.color = new Color(0.48f, 0.44f, 0.38f);
                }

                // Planted Coral Model (from davis3d foliage prefabs)
                GameObject plantedCoral = null;
                if (coralPrefab != null)
                {
                    plantedCoral = (GameObject)PrefabUtility.InstantiatePrefab(coralPrefab, slotObj.transform);
                    plantedCoral.name = $"Planted_{coralPrefab.name}";
                    plantedCoral.transform.localPosition = Vector3.zero;
                    plantedCoral.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    plantedCoral.transform.localScale = Vector3.one * 0.9f;

                    // Disable mesh collider on planted child so slot collider catches raycasts
                    Collider pCol = plantedCoral.GetComponentInChildren<Collider>();
                    if (pCol != null) pCol.enabled = false;

                    plantedCoral.SetActive(false);
                }

                Challenges.CoralSlot cs = slotObj.AddComponent<Challenges.CoralSlot>();
                SerializedObject csSO = new SerializedObject(cs);
                csSO.FindProperty("_slotName").stringValue = $"Reef Anchor Slot 0{i + 1}";
                csSO.FindProperty("_requiredToolId").stringValue = "05_coralreplantingkit";
                csSO.FindProperty("_plantingDuration").floatValue = 3.0f;
                csSO.FindProperty("_emptySlotVisual").objectReferenceValue = emptyVisual;
                csSO.FindProperty("_plantedCoralVisual").objectReferenceValue = plantedCoral;
                csSO.FindProperty("_slotRenderer").objectReferenceValue = emptyRenderer;
                csSO.ApplyModifiedProperties();

                slotsList.Add(cs);
            }

            // 4. Create 6 Coral Fragments (Collectibles)
            Vector3[] fragOffsets = {
                new Vector3(-5f, -0.9f, 3f),
                new Vector3(5f, -1.0f, 4f),
                new Vector3(-2f, -1.2f, 7f),
                new Vector3(2f, -1.1f, 8f),
                new Vector3(-6f, -1.4f, 12f),
                new Vector3(6f, -1.3f, 13f)
            };

            System.Collections.Generic.List<Challenges.CoralFragment> fragsList = new System.Collections.Generic.List<Challenges.CoralFragment>();

            for (int i = 0; i < fragOffsets.Length; i++)
            {
                GameObject fragObj = new GameObject($"CoralFragment_0{i + 1}");
                fragObj.transform.SetParent(zoneObj.transform, false);
                fragObj.transform.localPosition = fragOffsets[i];
                if (interactableLayer != -1) fragObj.layer = interactableLayer;

                SphereCollider fragCol = fragObj.AddComponent<SphereCollider>();
                fragCol.isTrigger = false;
                fragCol.radius = 0.5f;

                // Miniature coral model
                if (coralPrefab != null)
                {
                    GameObject fragVisual = (GameObject)PrefabUtility.InstantiatePrefab(coralPrefab, fragObj.transform);
                    fragVisual.name = "FragmentModel";
                    fragVisual.transform.localPosition = Vector3.zero;
                    fragVisual.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    fragVisual.transform.localScale = Vector3.one * 0.35f;

                    Collider fCol = fragVisual.GetComponentInChildren<Collider>();
                    if (fCol != null) fCol.enabled = false;
                }

                Challenges.CoralFragment cf = fragObj.AddComponent<Challenges.CoralFragment>();
                SerializedObject cfSO = new SerializedObject(cf);
                cfSO.FindProperty("_fragmentName").stringValue = $"Coral Branch Fragment 0{i + 1}";
                cfSO.ApplyModifiedProperties();

                fragsList.Add(cf);
            }

            // Wire into CoralReplantingChallenge
            chalSO.Update();
            SerializedProperty slotsProp = chalSO.FindProperty("_preplacedSlots");
            slotsProp.ClearArray();
            for (int i = 0; i < slotsList.Count; i++)
            {
                slotsProp.InsertArrayElementAtIndex(i);
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotsList[i];
            }

            SerializedProperty fragsProp = chalSO.FindProperty("_preplacedFragments");
            fragsProp.ClearArray();
            for (int i = 0; i < fragsList.Count; i++)
            {
                fragsProp.InsertArrayElementAtIndex(i);
                fragsProp.GetArrayElementAtIndex(i).objectReferenceValue = fragsList[i];
            }
            chalSO.ApplyModifiedProperties();

            Selection.activeGameObject = zoneObj;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string prefabName = coralPrefab != null ? coralPrefab.name : "SM_Plant_Coral";
            EditorUtility.DisplayDialog(
                "Coral Replanting Challenge Created",
                $"Successfully created [{zoneName}] using '{prefabName}':\n\n" +
                $"• Unique Zone ID: '{zoneId}'\n" +
                "• Zone Trigger Entry (Requires Coral Replanting Kit [2])\n" +
                $"• 4x Substrate Planting Slots (featuring {prefabName})\n" +
                $"• 6x Scattered Coral Fragments to collect\n" +
                "• Real-time hold-to-plant HUD progress meter\n\n" +
                "Press Ctrl + S to save your scene.",
                "OK"
            );
        }

        [MenuItem("Tools/Lalim/Validate & Fix Zone IDs", false, 40)]
        public static void ValidateAndFixZoneIds()
        {
            int fixedCount = 0;
            var waterChallenges = Object.FindObjectsOfType<Challenges.WaterSamplingChallenge>();
            var coralChallenges = Object.FindObjectsOfType<Challenges.CoralReplantingChallenge>();

            // 1. Water Sampling Challenges & Triggers
            for (int i = 0; i < waterChallenges.Length; i++)
            {
                var wc = waterChallenges[i];
                string expectedId = i == 0 ? "luzon_zone_b" : $"luzon_zone_b_{i + 1}";
                string expectedName = $"WaterSampling_Zone_{i + 1:D2}";

                SerializedObject wcSO = new SerializedObject(wc);
                wcSO.FindProperty("_zoneId").stringValue = expectedId;
                wcSO.ApplyModifiedProperties();

                ZoneTrigger zt = wc.GetComponentInChildren<ZoneTrigger>(true);
                if (zt != null)
                {
                    SerializedObject ztSO = new SerializedObject(zt);
                    ztSO.FindProperty("_zoneId").stringValue = expectedId;
                    ztSO.ApplyModifiedProperties();
                }

                wc.gameObject.name = expectedName;
                fixedCount++;
            }

            // 2. Coral Replanting Challenges & Triggers
            for (int i = 0; i < coralChallenges.Length; i++)
            {
                var cc = coralChallenges[i];
                string expectedId = i == 0 ? "luzon_zone_c" : $"luzon_zone_c_{i + 1}";
                string expectedName = $"CoralReplanting_Zone_{i + 1:D2}";

                SerializedObject ccSO = new SerializedObject(cc);
                ccSO.FindProperty("_zoneId").stringValue = expectedId;
                ccSO.ApplyModifiedProperties();

                ZoneTrigger zt = cc.GetComponentInChildren<ZoneTrigger>(true);
                if (zt != null)
                {
                    SerializedObject ztSO = new SerializedObject(zt);
                    ztSO.FindProperty("_zoneId").stringValue = expectedId;
                    ztSO.ApplyModifiedProperties();
                }

                cc.gameObject.name = expectedName;
                fixedCount++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog(
                "Zone IDs Validated & Repaired",
                $"Successfully synchronized and verified {fixedCount} zones across the scene:\n\n" +
                $"• Water Sampling Zones: {waterChallenges.Length} (IDs: luzon_zone_b, luzon_zone_b_2...)\n" +
                $"• Coral Replanting Zones: {coralChallenges.Length} (IDs: luzon_zone_c, luzon_zone_c_2...)\n" +
                "• All parent challenge components and child ZoneTriggers now match.\n" +
                "• Multi-zone event cross-talk is eliminated.\n\n" +
                "Press Ctrl + S to save your scene.",
                "OK"
            );
        }
    }
}
#endif
