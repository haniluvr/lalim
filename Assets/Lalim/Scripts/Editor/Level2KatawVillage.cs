using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Fills Ajuy's root village with Kataw families and friends (zone 2): small groups gathered
    /// in front of the huts and tents — parents with children, pairs of friends, elders,
    /// whole families — each facing the others as if talking. Groups keep clear of the teachers,
    /// the elder and the existing villagers, of each other, and of the roots and planks, and float
    /// low enough that they stay under the diver's swim ceiling.
    /// </summary>
    public static class Level2KatawVillage
    {
        private const string PrefabDir = "Assets/Lalim/Prefabs/Characters/Kataw/PF_Kataw_";
        public const string GroupPrefix = "VillageGroup_";

        private enum T { Male, Female, OldMale, OldFemale, ChildMale, ChildFemale }

        private static readonly (string label, T[] members)[] Groups =
        {
            ("FatherSon",        new[] { T.Male, T.ChildMale }),
            ("FatherDaughter",   new[] { T.Male, T.ChildFemale }),
            ("MotherSon",        new[] { T.Female, T.ChildMale }),
            ("MotherDaughter",   new[] { T.Female, T.ChildFemale }),
            ("TwoMen",           new[] { T.Male, T.Male }),
            ("TwoWomen",         new[] { T.Female, T.Female }),
            ("Couple",           new[] { T.Male, T.Female }),
            ("TwoBoys",          new[] { T.ChildMale, T.ChildMale }),
            ("TwoGirls",         new[] { T.ChildFemale, T.ChildFemale }),
            ("BoyGirl",          new[] { T.ChildMale, T.ChildFemale }),
            ("FamilyThree",      new[] { T.Male, T.Female, T.ChildFemale }),
            ("Elders",           new[] { T.OldMale, T.OldFemale }),
            ("GrandmaGrandson",  new[] { T.OldFemale, T.ChildMale }),
            ("FamilyFour",       new[] { T.Male, T.Female, T.ChildMale, T.ChildFemale }),
            ("GrandpaGirls",     new[] { T.OldMale, T.ChildFemale, T.ChildFemale }),
            ("Neighbours",       new[] { T.Female, T.OldFemale, T.ChildMale }),
        };

        private const float MaxPivotY = -4.6f;    // heads stay under the swim ceiling
        private const float AdultFloat = 1.4f;    // feet above the floor, as the existing villagers
        private const float ChildFloat = 1.6f;
        private const float GroupSpacing = 16f;
        private const float AwayFromQuest = 14f;
        private const float AwayFromVillager = 9f;

        public static string Build(int seed)
        {
            Physics.SyncTransforms();
            var report = new StringBuilder();
            Transform parent = GameObject.Find("Villagers")?.transform;
            if (parent == null) return "No Villagers object.";

            // Rebuild: remove earlier groups.
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name.StartsWith(GroupPrefix)) Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);

            var avoid = new List<(Vector3 p, float r)>();
            AudioClip hum = null;
            foreach (KatawVillager k in Object.FindObjectsByType<KatawVillager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool quest = k.transform.parent != parent;
                avoid.Add((k.transform.position, quest ? AwayFromQuest : AwayFromVillager));
                if (hum == null) hum = new SerializedObject(k).FindProperty("_hum").objectReferenceValue as AudioClip;
            }
            foreach (StatueDialogueRouter s in Object.FindObjectsByType<StatueDialogueRouter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.transform.position.x > 650f && s.transform.position.x < 1160f) avoid.Add((s.transform.position, AwayFromQuest));

            // Anchors: the huts and tents, in a shuffled order so groups spread over the village.
            var anchors = new List<Bounds>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string n = r.gameObject.name;
                if (n.StartsWith("Z2V_Z2_Hut") || n.StartsWith("Z2V_Z2_Tent")) anchors.Add(r.bounds);
            }
            // Props without colliders (crystals, coral, seaweed): keep the Kataw out of them too.
            _props.Clear();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                if (b.center.x < 650f || b.center.x > 1160f || r is ParticleSystemRenderer) continue;
                if (Mathf.Max(b.size.x, b.size.z) > 15f || r.gameObject.name.Contains("Ground") || r.gameObject.name.Contains("Terrain")) continue;
                if (r.GetComponent<KatawVillager>() != null || r.GetComponentInParent<KatawVillager>() != null) continue;
                _props.Add(b);
            }
            var rng = new System.Random(seed);
            for (int i = anchors.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (anchors[i], anchors[j]) = (anchors[j], anchors[i]); }

            int g = 0, placed = 0;
            foreach (Bounds hut in anchors)
            {
                if (g >= Groups.Length) break;
                (string label, T[] members) = Groups[g];
                if (!TryPlace(hut, members, avoid, rng, out Vector3 centre, out List<Vector3> spots)) continue;

                var group = new GameObject($"{GroupPrefix}{g:00}_{label}");
                group.transform.SetParent(parent, false);
                group.transform.position = centre;
                Undo.RegisterCreatedObjectUndo(group, "Kataw village");
                for (int m = 0; m < members.Length; m++)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + members[m] + ".prefab");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                    go.name = $"{label}_{m}_{members[m]}";
                    Vector3 face = centre - spots[m];
                    face.y = 0f;
                    float yaw = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face).eulerAngles.y : 0f;
                    yaw += (float)(rng.NextDouble() * 30.0 - 15.0);
                    go.transform.SetPositionAndRotation(spots[m], Quaternion.Euler(0f, yaw, 0f));
                    KatawVillager v = go.GetComponent<KatawVillager>();
                    if (v != null) v.EditorSetHum(m == 0 ? hum : null);   // one hum per group, not a chorus
                    placed++;
                }
                avoid.Add((centre, GroupSpacing));
                report.AppendLine($"{group.name} at {centre:F0} ({members.Length})");
                g++;
            }
            report.AppendLine($"{g}/{Groups.Length} groups, {placed} Kataw.");
            return report.ToString();
        }

        private static readonly List<Bounds> _props = new List<Bounds>();

        private static bool InProp(Vector3 feet, float height)
        {
            var body = new Bounds(feet + Vector3.up * height * 0.5f, new Vector3(0.9f, height + 0.2f, 0.9f));
            foreach (Bounds b in _props) if (b.Intersects(body)) return true;
            return false;
        }

        private static bool IsChild(T t) => t == T.ChildMale || t == T.ChildFemale;

        private static bool TryPlace(Bounds hut, T[] members, List<(Vector3 p, float r)> avoid, System.Random rng,
                                     out Vector3 centre, out List<Vector3> spots)
        {
            centre = default;
            spots = null;
            float hutRadius = Mathf.Max(hut.extents.x, hut.extents.z);
            double start = rng.NextDouble() * Mathf.PI * 2f;
            for (int ring = 0; ring < 2; ring++)
            for (int a = 0; a < 12; a++)
            {
                float angle = (float)start + a * Mathf.PI * 2f / 12f;
                float dist = hutRadius + 3.5f + ring * 2.5f;
                var c = new Vector3(hut.center.x + Mathf.Cos(angle) * dist, 0f, hut.center.z + Mathf.Sin(angle) * dist);
                bool near = false;
                foreach ((Vector3 p, float r) in avoid)
                    if (new Vector2(p.x - c.x, p.z - c.z).sqrMagnitude < r * r) { near = true; break; }
                if (near) continue;

                // Lay the members around the middle, facing in.
                var tryspots = new List<Vector3>();
                float spread = members.Length == 2 ? 0.75f : 0.95f;
                float turn = (float)(rng.NextDouble() * Mathf.PI * 2f);
                bool ok = true;
                float maxFloor = float.MinValue;
                for (int m = 0; m < members.Length && ok; m++)
                {
                    float t = turn + m * Mathf.PI * 2f / members.Length;
                    float s = spread * (IsChild(members[m]) ? 0.8f : 1f);
                    var p = new Vector3(c.x + Mathf.Cos(t) * s, 0f, c.z + Mathf.Sin(t) * s);
                    if (!Physics.Raycast(new Vector3(p.x, -2f, p.z), Vector3.down, out RaycastHit hit, 30f, ~0, QueryTriggerInteraction.Ignore)) { ok = false; break; }
                    p.y = hit.point.y + (IsChild(members[m]) ? ChildFloat : AdultFloat);
                    maxFloor = Mathf.Max(maxFloor, hit.point.y);
                    if (p.y > MaxPivotY) { ok = false; break; }
                    float h = IsChild(members[m]) ? 0.6f : 1.05f;
                    if (Physics.CheckCapsule(p + Vector3.up * 0.35f, p + Vector3.up * (h - 0.1f), 0.35f, ~0, QueryTriggerInteraction.Ignore)) { ok = false; break; }
                    if (InProp(p, h)) { ok = false; break; }
                    tryspots.Add(p);
                }
                if (!ok) continue;
                // A level group: no member far above or below the others.
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (Vector3 p in tryspots) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                if (maxY - minY > 1.2f) continue;
                Vector3 mid = Vector3.zero;
                foreach (Vector3 p in tryspots) mid += p;
                centre = mid / tryspots.Count;
                spots = tryspots;
                return true;
            }
            return false;
        }
    }
}
