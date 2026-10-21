using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Relics ▸ Build Kataw Gift Placeholder — until the real Kataw Gift is decided,
    /// a necklace of coral, shell and sea-glass beads like the ones the Kataw wear ("made by many
    /// hands"), so the handover looks like Level 1's relics instead of a primitive. It replaces the
    /// mesh and materials of PF_Relic_KatawGift_PH in place (the prefab keeps its GUID).
    /// </summary>
    public static class KatawGiftPlaceholderBuilder
    {
        private const string PrefabPath = "Assets/Lalim/Prefabs/Relics/PF_Relic_KatawGift_PH.prefab";
        private const string MeshPath = "Assets/Lalim/Models/relics/Optimized/KatawGift_Necklace_Mesh.asset";
        private const string MaterialFolder = "Assets/Lalim/Materials/Relics";

        private const int Beads = 20;
        private const float RingRadius = 0.11f;
        private const float RingTilt = 55f;     // degrees from flat: it reads as a ring while it turns

        [MenuItem("Tools/Lalim/Relics/Build Kataw Gift Placeholder")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var coral = new List<CombineInstance>();
            var shell = new List<CombineInstance>();
            var glass = new List<CombineInstance>();
            Mesh sphere = Sphere(12, 8);

            Quaternion tilt = Quaternion.Euler(RingTilt, 0f, 0f);
            for (int i = 0; i < Beads; i++)
            {
                float a = i * Mathf.PI * 2f / Beads;
                Vector3 p = tilt * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RingRadius;
                // Coral, coral, shell, coral, sea glass … as on the Kataw necklaces.
                int kind = i % 5 == 4 ? 2 : i % 5 == 2 ? 1 : 0;
                float size = kind == 2 ? 0.026f : kind == 1 ? 0.018f : 0.022f;
                Add(kind == 0 ? coral : kind == 1 ? shell : glass, sphere, p, Vector3.one * size);
            }
            // The pendant: a sea-glass drop hanging from the lowest bead.
            Vector3 low = tilt * new Vector3(0f, 0f, -1f) * RingRadius;
            Add(shell, sphere, low + Vector3.down * 0.022f, Vector3.one * 0.014f);
            Add(glass, sphere, low + Vector3.down * 0.05f, new Vector3(0.026f, 0.036f, 0.018f));

            var mesh = new Mesh { name = "KatawGift_Necklace" };
            var parts = new List<CombineInstance>();
            foreach (List<CombineInstance> set in new[] { coral, shell, glass })
            {
                var m = new Mesh();
                m.CombineMeshes(set.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
            }
            mesh.CombineMeshes(parts.ToArray(), false, true);   // one submesh per material
            mesh.RecalculateBounds();
            // Pivot at the centre, as the other relic meshes are.
            Vector3 c = mesh.bounds.center;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] -= c;
            mesh.vertices = verts;
            mesh.RecalculateBounds();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, MeshPath);

            Material[] mats =
            {
                Lit("M_Relic_KatawGift_Coral", new Color(0.92f, 0.45f, 0.36f), 0.55f, new Color(0.35f, 0.12f, 0.08f)),
                Lit("M_Relic_KatawGift_Shell", new Color(0.95f, 0.9f, 0.8f), 0.7f, new Color(0.2f, 0.18f, 0.14f)),
                Lit("M_Relic_KatawGift_SeaGlass", new Color(0.55f, 0.84f, 0.76f), 0.85f, new Color(0.1f, 0.3f, 0.26f)),
            };

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            MeshFilter mf = root.GetComponentInChildren<MeshFilter>();
            MeshRenderer mr = root.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null) { PrefabUtility.UnloadPrefabContents(root); return "PF_Relic_KatawGift_PH has no MeshFilter/MeshRenderer."; }
            mf.sharedMesh = mesh;
            mr.sharedMaterials = mats;
            mf.transform.localScale = Vector3.one;
            mf.transform.localRotation = Quaternion.identity;
            foreach (Collider col in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            return $"Kataw Gift placeholder: a {Beads}-bead necklace with a sea-glass drop ({mesh.vertexCount} vertices) on {PrefabPath}.";
        }

        private static void Add(List<CombineInstance> set, Mesh sphere, Vector3 at, Vector3 scale) =>
            set.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(at, Quaternion.identity, scale) });

        private static Material Lit(string name, Color color, float smoothness, Color emission)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A unit-diameter UV sphere.</summary>
        private static Mesh Sphere(int segments, int rings)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = v * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float theta = u * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    verts.Add(n * 0.5f);
                    normals.Add(n);
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }
            int row = segments + 1;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    int a = r * row + s, b = a + row;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b);
                    tris.Add(a + 1); tris.Add(b + 1); tris.Add(b);
                }
            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }
    }
}
