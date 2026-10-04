using UnityEngine;
using UnityEditor;
using System.IO;

namespace Lalim.EditorScripts
{
    public class OceanfloorMaterialFixer : EditorWindow
    {
        [MenuItem("Lalim/Fix Oceanfloor Materials Instantly")]
        public static void FixMaterials()
        {
            string folderPath = "Assets/Lalim/Models/OceanfloorHome";
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { folderPath });

            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/textures/basecolor.png");
            Texture2D emTex = AssetDatabase.LoadAssetAtPath<Texture2D>(folderPath + "/textures/christ_emission.png");

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (mat.name == "Sunlight")
                {
                    // Fix Sunlight
                    mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
                    mat.SetFloat("_Surface", 1); // Transparent
                    mat.SetFloat("_Blend", 2); // Additive
                    mat.SetColor("_BaseColor", new Color(0, 1f, 1f, 0.1f)); // Soft Cyan
                    mat.SetTexture("_BaseMap", null);
                    mat.DisableKeyword("_EMISSION");
                }
                else
                {
                    // Fix everything else
                    mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                    mat.SetFloat("_Surface", 0); // Opaque
                    mat.SetFloat("_Blend", 0); // Opaque

                    // Set Textures
                    mat.SetTexture("_BaseMap", baseTex);
                    mat.SetTexture("_EmissionMap", emTex);

                    // Enable Emission
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    
                    // Set Emission Color to White * 2 (Intensity)
                    mat.SetColor("_EmissionColor", Color.white * 2.5f);
                }

                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("<b><color=#00FF00>[Lalim]</color></b> All materials fixed instantly!");
        }
    }
}
