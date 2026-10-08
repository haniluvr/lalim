using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Add Solid Scenery Colliders — gives the open scene's solid scenery (arches,
    /// cliff walls, floor rocks, bones, wrecks, statue alcoves…) mesh colliders shaped like what you
    /// see, so the diver can't swim through them. Plants, anemones, coral fronds and creatures stay
    /// swim-through; editor helper volumes (Layout_*, Baseline_*, *_Zone) are skipped. Objects that
    /// already have a collider are left as they are. Undo-able; re-running adds only what's missing.
    /// </summary>
    public static class SolidSceneryColliders
    {
        // Name starts that are solid (checked against each object under the scene's environment roots).
        private static readonly string[] Solid =
        {
            "Arch_", "Terrain_Fissure_Arch", "Terrain_Cavern_Arch", "Terrain_Throat_Arch", "Wall_", "Well_",
            "Reef_flatrock", "Reef_rockflat", "Reef_encrust", "Prop_Whale_Bone", "Prop_WWII_Shipwreck",
            "Prop_Kataw_Spire", "Prop_Tribal_Statue", "Rock", "rock", "Cliff", "Boulder", "Pillar", "Column",
        };

        private static readonly string[] Skip = { "Layout_", "Baseline_", "VFX_", "Creature_", "Minigame", "Portal", "Blocker", "Fog" };

        [MenuItem("Tools/Lalim/Add Solid Scenery Colliders")]
        public static void Menu() => Debug.Log(AddToOpenScene());

        public static string AddToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int added = 0, objects = 0;
            var kinds = new SortedDictionary<string, int>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Renderer r = mf.GetComponent<Renderer>();
                    if (r == null || !r.enabled) continue;
                    Transform owner = SolidOwner(mf.transform);
                    if (owner == null) continue;
                    if (owner.GetComponentInChildren<Collider>(true) != null && mf.GetComponent<Collider>() == null && owner != mf.transform)
                    {
                        // The object already has its own collider somewhere (e.g. a hand-made box): keep it.
                        if (!HasOnlyOurColliders(owner)) continue;
                    }
                    if (mf.GetComponent<Collider>() != null) continue;

                    var col = Undo.AddComponent<MeshCollider>(mf.gameObject);
                    col.sharedMesh = mf.sharedMesh;
                    added++;
                    string key = System.Text.RegularExpressions.Regex.Replace(owner.name, @"[\s_\.]*\(?\d+\)?$", "");
                    kinds.TryGetValue(key, out int n);
                    kinds[key] = n + 1;
                }
            }
            foreach (var kv in kinds) objects++;
            if (added > 0) EditorSceneManager.MarkSceneDirty(scene);
            var report = new System.Text.StringBuilder($"{scene.name}: added {added} mesh colliders");
            if (added > 0)
            {
                report.Append(" (");
                bool first = true;
                foreach (var kv in kinds) { report.Append(first ? "" : ", ").Append(kv.Key).Append(" ×").Append(kv.Value); first = false; }
                report.Append(")");
            }
            return report.ToString();
        }

        // The nearest object up the hierarchy whose name marks it solid (and no skipped name on the way).
        private static Transform SolidOwner(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                foreach (string s in Skip) if (p.name.StartsWith(s)) return null;
                foreach (string s in Solid) if (p.name.StartsWith(s)) return p;
            }
            return null;
        }

        private static bool HasOnlyOurColliders(Transform owner)
        {
            foreach (Collider c in owner.GetComponentsInChildren<Collider>(true)) if (!(c is MeshCollider)) return false;
            return true;
        }
    }
}
