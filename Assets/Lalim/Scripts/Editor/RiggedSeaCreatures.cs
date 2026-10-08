using System.Collections.Generic;
using System.IO;
using Lalim.Mechanics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Use Rigged Sea Creatures
    ///
    ///  1. Builds Resources/SeaCreatureClips (each rigged model's swim and idle clip).
    ///  2. The wanderer prefabs play their swim clip (schools and ghost-net animals look theirs up at runtime).
    ///  3. In the open Luzon scene, swaps the scene model's static creatures (LuzonScene/Creature_*,
    ///     baked meshes with no rig) for the rigged models from "sea creatures + elements (fbx)",
    ///     same spot, size and heading; the old meshes are only switched off. Kinds with no rigged
    ///     model (dolphin, shark) and the animals caught in the ghost nets are left as they are.
    ///
    /// Safe to run again: groups already swapped are skipped.
    /// </summary>
    public static class RiggedSeaCreatures
    {
        private const string ModelsFolder = "Assets/Lalim/Models/sea creatures + elements (fbx)";
        private const string ClipsAsset = "Assets/Lalim/Resources/" + SeaCreatureClips.ResourcePath + ".asset";
        private const string CreatureFolder = "Assets/Lalim/Prefabs/Creatures";
        private const string RiggedSuffix = " (rigged)";

        // model → swim clip, idle clip (null: use the swim)
        private static readonly string[,] Clips =
        {
            { "BrownFish", "BrownFishArmature|BrownFish", null },
            { "ClownFish", "ClownFishArmature|ClownFish", null },
            { "DoryFish", "DoryArmature|DorySwim", null },
            { "TunaFish", "TunaArmature|TunaSwim", null },
            { "Turtle", "TurtleArmature|Swim", "TurtleArmature|Idle" },
            { "StingRay", "Armature|ArmatureAction", null },
            { "Hammerhead", "Armature|ArmatureAction", null },
            { "Whale", "Armature|Swim", null },
            { "Eel", "Armature|Swim", "Armature|Idle" },
            { "Squid", "SquidArmature|SquidPropulsion", "SquidArmature|SquidIdle" },
            { "Octopus", "Armature|Walk", "Armature|Idle" },
            { "Crab", "Armature|Walk", "Armature|Idle" },
            { "Lobster", "LobsterArmature|Walk", "LobsterArmature|Idle" },
            { "StarFish", "Armature|ArmatureAction", null },
            { "Seal", "SealArmature|Swim", "SealArmature|Idle" },
            { "Penguin", "Armature|ArmatureAction", null },
        };

        // Scene creature kind (LuzonScene/Creature_<Kind>_…) → rigged model. Fish groups pick per fish.
        private static readonly Dictionary<string, string> KindModels = new Dictionary<string, string>
        {
            { "Hammerhead", "Hammerhead" }, { "Octopus", "Octopus" }, { "Squid", "Squid" },
            { "StingRay", "StingRay" }, { "Turtle", "Turtle" }, { "Whale", "Whale" }, { "Eel", "Eel" },
        };

        [MenuItem("Tools/Lalim/Use Rigged Sea Creatures")]
        public static void Run()
        {
            string report = Apply();
            Debug.Log("[RiggedSeaCreatures]\n" + report);
        }

        public static string Apply()
        {
            var report = new System.Text.StringBuilder();
            SeaCreatureClips library = BuildLibrary(report);
            AnimateWandererPrefabs(library, report);
            SwapSceneCreatures(library, report);
            return report.ToString();
        }

        // ───────────────────────── Clip library ─────────────────────────

        private static SeaCreatureClips BuildLibrary(System.Text.StringBuilder report)
        {
            var library = AssetDatabase.LoadAssetAtPath<SeaCreatureClips>(ClipsAsset);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ClipsAsset));
                library = ScriptableObject.CreateInstance<SeaCreatureClips>();
                AssetDatabase.CreateAsset(library, ClipsAsset);
            }
            List<SeaCreatureClips.Entry> entries = library.EditorEntries;
            entries.Clear();
            for (int i = 0; i < Clips.GetLength(0); i++)
            {
                string model = Clips[i, 0];
                var entry = new SeaCreatureClips.Entry { model = model, swim = LoadClip(model, Clips[i, 1]), idle = LoadClip(model, Clips[i, 2]) };
                if (entry.swim == null) report.AppendLine($"No clip '{Clips[i, 1]}' in {model}.fbx.");
                entries.Add(entry);
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            report.AppendLine($"Clip library: {entries.Count} creatures ({ClipsAsset}).");
            return library;
        }

        private static AnimationClip LoadClip(string model, string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath($"{ModelsFolder}/{model}.fbx"))
                if (o is AnimationClip c && c.name == clipName) return c;
            return null;
        }

        private static AnimationClip SwimClip(SeaCreatureClips library, string model)
        {
            foreach (SeaCreatureClips.Entry e in library.EditorEntries) if (e.model == model) return e.swim != null ? e.swim : e.idle;
            return null;
        }

        private static GameObject LoadModel(string model) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelsFolder}/{model}.fbx");

        // ───────────────────────── Prefabs ─────────────────────────

        private static void AnimateWandererPrefabs(SeaCreatureClips library, System.Text.StringBuilder report)
        {
            foreach (string guid in AssetDatabase.FindAssets("PF_Wanderer_ t:Prefab", new[] { CreatureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.transform.childCount == 0) continue;
                    GameObject body = root.transform.GetChild(0).gameObject;
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(body);
                    AnimationClip clip = source != null ? SwimClip(library, source.name) : null;
                    if (clip == null) { report.AppendLine($"{Path.GetFileName(path)}: no clip for its model."); continue; }
                    CreatureRig rig = body.GetComponent<CreatureRig>();
                    if (rig == null) rig = body.AddComponent<CreatureRig>();
                    rig.EditorSetup(clip, 1f);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine($"{Path.GetFileName(path)} plays {clip.name}.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        // ───────────────────────── Luzon's own creatures ─────────────────────────

        private static void SwapSceneCreatures(SeaCreatureClips library, System.Text.StringBuilder report)
        {
            GameObject scene = GameObject.Find("LuzonScene");
            if (scene == null) { report.AppendLine("Open the Luzon scene to swap its creatures."); return; }

            int groups = 0, animals = 0;
            var kept = new SortedSet<string>();
            foreach (Transform t in scene.transform)
            {
                if (!t.name.StartsWith("Creature_")) continue;
                string kind = t.name.Split('_')[1];
                if (t.name.Contains("Trapped")) { kept.Add("caught in nets"); continue; }
                bool fish = kind == "Fish";
                if (!fish && !KindModels.ContainsKey(kind)) { kept.Add(kind.ToLower()); continue; }
                if (AlreadySwapped(t)) continue;

                var wanderer = t.GetComponent<WanderingMarineAnimal>();
                Vector3? newHead = null;
                foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    string model = fish ? FishModel(mf.sharedMesh.name) : KindModels[kind];
                    GameObject source = LoadModel(model);
                    if (source == null) { report.AppendLine($"Missing {model}.fbx."); continue; }

                    bool upright = wanderer != null && YawOnly(wanderer);
                    GameObject rigged = PlaceLike(source, mf, t, upright);
                    CreatureRig rig = Undo.AddComponent<CreatureRig>(rigged);
                    rig.EditorSetup(SwimClip(library, model), Random.Range(0.9f, 1.1f));
                    newHead = t.InverseTransformDirection(rigged.transform.TransformDirection(Vector3.back)).normalized;

                    Undo.RecordObject(mf.gameObject, "Hide static creature");
                    mf.gameObject.SetActive(false);
                    animals++;
                }
                // A lone swimmer turns its new head toward where it goes.
                if (wanderer != null && newHead.HasValue)
                {
                    var so = new SerializedObject(wanderer);
                    so.FindProperty("headAxis").vector3Value = newHead.Value;
                    so.ApplyModifiedProperties();
                }
                groups++;
            }
            if (groups > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene.scene);
                EditorSceneManager.SaveScene(scene.scene);
            }
            report.AppendLine($"Luzon: {animals} static creatures in {groups} groups swapped for rigged, animated ones (old meshes switched off).");
            if (kept.Count > 0) report.AppendLine("Left as they were: " + string.Join(", ", kept) + " (no rigged model / stay put).");
        }

        private static bool AlreadySwapped(Transform group)
        {
            foreach (Transform c in group) if (c.name.EndsWith(RiggedSuffix)) return true;
            return false;
        }

        private static bool YawOnly(WanderingMarineAnimal w) => new SerializedObject(w).FindProperty("yawOnly").boolValue;

        // The baked fish meshes are named after the Blender model they were duplicated from.
        private static string FishModel(string meshName)
        {
            if (meshName.Contains("ClownFish")) return "ClownFish";
            if (meshName.Contains("Tuna")) return "TunaFish";
            if (meshName.Contains("BrownFish")) return "BrownFish";
            return "DoryFish";
        }

        /// <summary>
        /// A rigged instance where the static mesh was: same centre and length, its head (-Z in the
        /// rigged models) along the static one's longest side, back up. Upright creatures (the
        /// octopus) just keep standing, turned the way the old one faced.
        /// </summary>
        private static GameObject PlaceLike(GameObject source, MeshFilter old, Transform parent, bool upright)
        {
            Bounds mb = old.sharedMesh.bounds;
            Vector3 sx = old.transform.TransformVector(Vector3.right * mb.size.x);
            Vector3 sy = old.transform.TransformVector(Vector3.up * mb.size.y);
            Vector3 sz = old.transform.TransformVector(Vector3.forward * mb.size.z);
            Vector3 longest = sx.magnitude >= sy.magnitude && sx.magnitude >= sz.magnitude ? sx : sy.magnitude >= sz.magnitude ? sy : sz;
            Renderer oldRenderer = old.GetComponent<Renderer>();
            Vector3 centre = oldRenderer != null ? oldRenderer.bounds.center : old.transform.position;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            Undo.RegisterCreatedObjectUndo(go, "Rigged creature");
            go.name = source.name + RiggedSuffix;
            foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            // Measure the model as authored, then size it to the old one.
            go.transform.rotation = Quaternion.identity;
            Vector3 authored = Bounds(go).size;
            float modelLength = Mathf.Max(authored.x, Mathf.Max(authored.y, authored.z));
            go.transform.localScale *= longest.magnitude / Mathf.Max(0.001f, modelLength);

            Quaternion rotation;
            if (upright)
            {
                Vector3 facing = Vector3.ProjectOnPlane(old.transform.forward, Vector3.up);
                if (facing.sqrMagnitude < 0.001f) facing = Vector3.ProjectOnPlane(parent.forward, Vector3.up);
                rotation = Quaternion.LookRotation(-facing.normalized, Vector3.up);
            }
            else
            {
                Vector3 head = longest.normalized;
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, head);
                rotation = Quaternion.LookRotation(-head, up.sqrMagnitude > 0.001f ? up.normalized : Vector3.forward);
            }
            go.transform.rotation = rotation;
            go.transform.position += centre - Bounds(go).center;
            return go;
        }

        private static Bounds Bounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }
}
