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
    }
}
#endif
