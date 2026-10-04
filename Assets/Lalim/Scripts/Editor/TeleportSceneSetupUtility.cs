#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Lalim.UI;
using Lalim.Mechanics;
using Lalim.Core;

namespace Lalim.Editor
{
    public class TeleportSceneSetupUtility
    {
        [MenuItem("Lalim/Setup Teleport Scene")]
        public static void SetupTeleportScene()
        {
            CreateDialogueSystem();
            CreateTeleporters();
            
            Debug.Log("[Setup] Teleport Scene generated! Save your scene as 'TeleportScene' and add it to Build Settings.");
        }

        private static void CreateDialogueSystem()
        {
            if (GameObject.FindAnyObjectByType<DialogueManager>() != null)
            {
                Debug.Log("[Setup] DialogueManager already exists.");
                return;
            }

            // Same dialogue box, buttons and review log as the level scenes: a linked PF_DialogueSystem.
            var report = new System.Text.StringBuilder();
            Lalim.EditorTools.LevelUIPrefabs.EnsureInstance(Lalim.EditorTools.LevelUIPrefabs.DialoguePrefab,
                typeof(DialogueManager), new[] { "DialogueCanvas" }, report);
            Debug.Log("[Setup] " + report);
        }

        private static void CreateTeleporters()
        {
            GameObject container = new GameObject("--- TELEPORTERS ---");

            CreateTeleporter("Teleporter_Luzon", Region.Luzon, new Vector3(-5, 0, 0), container.transform);
            CreateTeleporter("Teleporter_Visayas", Region.Visayas, new Vector3(0, 0, 5), container.transform);
            CreateTeleporter("Teleporter_Mindanao", Region.Mindanao, new Vector3(5, 0, 0), container.transform);
        }

        private static void CreateTeleporter(string name, Region region, Vector3 position, Transform parent)
        {
            GameObject tp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tp.name = name;
            tp.transform.position = position;
            tp.transform.localScale = new Vector3(2, 0.2f, 2);
            tp.transform.SetParent(parent);

            // Setup interactable
            Collider col = tp.GetComponent<Collider>();
            col.isTrigger = false;
            tp.layer = LayerMask.NameToLayer("Interactable");

            RegionTeleporter logic = tp.AddComponent<RegionTeleporter>();
            
            SerializedObject so = new SerializedObject(logic);
            so.FindProperty("_targetRegion").enumValueIndex = (int)region;
            so.FindProperty("_teleporterName").stringValue = name;
            so.ApplyModifiedProperties();
        }
    }
}
#endif
