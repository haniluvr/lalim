using UnityEngine;
using UnityEditor;
using Lalim.Mechanics;
using Lalim.Core;

namespace Lalim.EditorScripts
{
    public class HubSetupUtility : EditorWindow
    {
        [MenuItem("Lalim/Setup Hub (Player, Physics, Teleporters)")]
        public static void SetupHubScene()
        {
            // 1. Add Smooth Invisible Floor (and remove jagged MeshCollider)
            GameObject oceanFloor = GameObject.Find("OceanfloorFixedFinal");
            if (oceanFloor != null)
            {
                // Remove the buggy jagged mesh collider that gets the player stuck on seaweeds
                Transform meshChild = oceanFloor.transform.Find("default");
                if (meshChild != null)
                {
                    MeshCollider mc = meshChild.GetComponent<MeshCollider>();
                    if (mc != null) DestroyImmediate(mc);
                }
                
                // Create a perfectly smooth invisible glass floor right above the seaweed
                GameObject smoothFloor = GameObject.Find("SmoothInvisibleFloor");
                if (smoothFloor == null)
                {
                    smoothFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    smoothFloor.name = "SmoothInvisibleFloor";
                    smoothFloor.transform.position = new Vector3(-51f, -25f, -3f); // Right above the sand
                    smoothFloor.transform.localScale = new Vector3(80f, 0.5f, 80f); // Huge flat area
                    
                    // Remove rendering so it is invisible
                    DestroyImmediate(smoothFloor.GetComponent<MeshRenderer>());
                    DestroyImmediate(smoothFloor.GetComponent<MeshFilter>());
                    Debug.Log("Created SmoothInvisibleFloor and removed jagged MeshCollider.");
                }
            }

            // 2. Set up Underwater Bounds (Invisible Walls)
            UnderwaterBounds bounds = FindAnyObjectByType<UnderwaterBounds>();
            if (bounds == null)
            {
                GameObject boundsGo = new GameObject("UnderwaterBounds");
                bounds = boundsGo.AddComponent<UnderwaterBounds>();
                Debug.Log("Created UnderwaterBounds to keep player inside.");
            }

            // 3. Spawn Player & HUD
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lalim/Prefabs/Player.prefab");
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lalim/Prefabs/UI/GameHUD.prefab");

            if (playerPrefab != null && FindAnyObjectByType<PlayerController>() == null)
            {
                GameObject p = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
                p.transform.position = new Vector3(-51.5f, -23.5f, 5.2f); // Near the treasure chest
                Debug.Log("Spawned Player.");
            }

            if (hudPrefab != null && FindAnyObjectByType<Lalim.UI.HUDController>() == null)
            {
                PrefabUtility.InstantiatePrefab(hudPrefab);
                Debug.Log("Spawned Game HUD.");
            }

            // 3.5 Setup EventSystem (Required for UI/Joystick)
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Debug.Log("Created EventSystem for UI interactions.");
            }

            // 3.6 Setup DevBootstrapper for testing
            if (FindAnyObjectByType<DevTools.DevBootstrapper>() == null)
            {
                GameObject boot = new GameObject("[DevBootstrapper]");
                boot.AddComponent<DevTools.DevBootstrapper>();
                Debug.Log("Created DevBootstrapper. You can now press Play directly in this scene!");
            }

            // 4. Setup Teleporters
            GameObject teleportParent = GameObject.Find("Teleport");
            if (teleportParent != null)
            {
                // Unpack the prefab so Unity doesn't throw an exception when we add scripts and child objects to it!
                if (PrefabUtility.IsAnyPrefabInstanceRoot(teleportParent))
                {
                    PrefabUtility.UnpackPrefabInstance(teleportParent, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }

                // Assuming it has 3 children for the 3 arches
                Transform arch1 = teleportParent.transform.childCount > 0 ? teleportParent.transform.GetChild(0) : null;
                Transform arch2 = teleportParent.transform.childCount > 1 ? teleportParent.transform.GetChild(1) : null;
                Transform arch3 = teleportParent.transform.childCount > 2 ? teleportParent.transform.GetChild(2) : null;

                if (arch1 != null) SetupSingleTeleporter(arch1.gameObject, Region.Luzon, "Luzon Portal", Color.blue);
                if (arch2 != null) SetupSingleTeleporter(arch2.gameObject, Region.Visayas, "Visayas Portal", Color.green);
                if (arch3 != null) SetupSingleTeleporter(arch3.gameObject, Region.Mindanao, "Mindanao Portal", Color.red);
                
                Debug.Log("Configured Teleporters with Fog and Backgrounds!");
            }
            else
            {
                Debug.LogWarning("Could not find 'Teleport' object in scene to configure arches.");
            }
        }

        private static void SetupSingleTeleporter(GameObject archGo, Region region, string name, Color bgColor)
        {
            // Add BoxCollider trigger if not present
            BoxCollider col = archGo.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = archGo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(3f, 3f, 3f);
            }

            // Add Teleporter script
            RegionTeleporter teleporter = archGo.GetComponent<RegionTeleporter>();
            if (teleporter == null)
            {
                teleporter = archGo.AddComponent<RegionTeleporter>();
            }

            // Use SerializedObject to set private fields on RegionTeleporter
            SerializedObject serializedTeleporter = new SerializedObject(teleporter);
            serializedTeleporter.FindProperty("_targetRegion").enumValueIndex = (int)region;
            serializedTeleporter.FindProperty("_teleporterName").stringValue = name;

            // Create Background Quad
            Transform bgTrans = archGo.transform.Find("BackgroundQuad");
            GameObject bgVisual;
            if (bgTrans == null)
            {
                bgVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bgVisual.name = "BackgroundQuad";
                bgVisual.transform.SetParent(archGo.transform);
                bgVisual.transform.localPosition = new Vector3(0, 1.5f, 0.5f); // Position inside the arch
                bgVisual.transform.localScale = new Vector3(2.5f, 3f, 1f);
                DestroyImmediate(bgVisual.GetComponent<Collider>()); // Don't want player to bump into the image
                
                // Set to solid color for now (can be replaced with VideoPlayer later!)
                MeshRenderer rend = bgVisual.GetComponent<MeshRenderer>();
                rend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                rend.sharedMaterial.color = bgColor;
            }
            else
            {
                bgVisual = bgTrans.gameObject;
            }
            
            serializedTeleporter.FindProperty("_backgroundVisual").objectReferenceValue = bgVisual;

            // Create Fog Particle System
            Transform fogTrans = archGo.transform.Find("LockedFog");
            GameObject fogVisual;
            if (fogTrans == null)
            {
                fogVisual = new GameObject("LockedFog");
                fogVisual.transform.SetParent(archGo.transform);
                fogVisual.transform.localPosition = new Vector3(0, 1.5f, 0);
                
                ParticleSystem ps = fogVisual.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startColor = new Color(0.2f, 0.5f, 0.7f, 0.8f);
                main.startSize = 2f;
                main.startLifetime = 3f;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                
                var emission = ps.emission;
                emission.rateOverTime = 20;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(2f, 3f, 1f);

                // Use default particle material
                ParticleSystemRenderer psRend = fogVisual.GetComponent<ParticleSystemRenderer>();
                psRend.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                
                // Add sorting fudge to render correctly in URP
                psRend.sortingFudge = 50; 
            }
            else
            {
                fogVisual = fogTrans.gameObject;
            }
            
            serializedTeleporter.FindProperty("_lockedFogVisual").objectReferenceValue = fogVisual;

            serializedTeleporter.ApplyModifiedProperties();
        }
    }
}
