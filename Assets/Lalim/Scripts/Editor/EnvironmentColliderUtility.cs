#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Lalim.Editor
{
    /// <summary>
    /// Editor utility to quickly add or configure Colliders on environment groups
    /// (e.g. Rocks, Clams, Funnels, Ground meshes) to prevent player clipping.
    /// </summary>
    public static class EnvironmentColliderUtility
    {
        [MenuItem("Tools/Lalim/Environment/Add Mesh Colliders to Selected & Children", false, 10)]
        public static void AddMeshCollidersToSelected()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Add Colliders", "Please select one or more GameObjects in the Hierarchy first.", "OK");
                return;
            }

            int addedCount = 0;
            int existingCount = 0;

            foreach (GameObject root in selectedObjects)
            {
                MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf.sharedMesh == null) continue;

                    Collider existingCol = mf.GetComponent<Collider>();
                    if (existingCol == null)
                    {
                        Undo.AddComponent<MeshCollider>(mf.gameObject);
                        MeshCollider mc = mf.GetComponent<MeshCollider>();
                        if (mc != null)
                        {
                            mc.sharedMesh = mf.sharedMesh;
                            mc.convex = false; // Static environment works best with exact concave meshes
                            mc.isTrigger = false;
                        }
                        addedCount++;
                    }
                    else
                    {
                        existingCount++;
                    }
                }
            }

            EditorUtility.DisplayDialog(
                "Environment Colliders Added",
                $"Successfully processed selection:\n\n" +
                $"• Added MeshColliders: {addedCount}\n" +
                $"• Already had Colliders: {existingCount}\n\n" +
                $"Your character will now collide with these surfaces.",
                "OK"
            );
        }

        [MenuItem("Tools/Lalim/Environment/Add Mesh Colliders to Selected & Children", true)]
        private static bool ValidateAddMeshColliders()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        [MenuItem("Tools/Lalim/Environment/Remove Colliders from Selected & Children", false, 11)]
        public static void RemoveCollidersFromSelected()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Remove Colliders", "Please select one or more GameObjects in the Hierarchy first.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Confirm Removal",
                "Are you sure you want to remove all Colliders from the selected object(s) and their children?",
                "Yes, Remove", "Cancel"))
            {
                return;
            }

            int removedCount = 0;

            foreach (GameObject root in selectedObjects)
            {
                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                foreach (Collider col in colliders)
                {
                    Undo.DestroyObjectImmediate(col);
                    removedCount++;
                }
            }

            EditorUtility.DisplayDialog("Colliders Removed", $"Removed {removedCount} collider(s).", "OK");
        }

        [MenuItem("Tools/Lalim/Environment/Remove Colliders from Selected & Children", true)]
        private static bool ValidateRemoveColliders()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }
    }
}
#endif
