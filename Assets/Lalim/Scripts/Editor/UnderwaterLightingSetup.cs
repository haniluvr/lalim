using UnityEngine;
using UnityEditor;

namespace Lalim.EditorScripts
{
    public class UnderwaterLightingSetup : EditorWindow
    {
        [MenuItem("Lalim/Setup Underwater Lighting")]
        public static void SetupLighting()
        {
            // 1. Remove Skybox (Set to solid color)
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            
            // A deep, dark teal/blue for the ambient light
            Color ambientColor = new Color(0.02f, 0.15f, 0.2f, 1f);
            RenderSettings.ambientLight = ambientColor;

            // 2. Setup Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.025f; // Adjust this to make it thicker/thinner
            
            // Match fog color to the deep sea background
            Color fogColor = new Color(0.01f, 0.1f, 0.15f, 1f);
            RenderSettings.fogColor = fogColor;
            
            // Make the camera background match the fog so the horizon blends perfectly
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = fogColor;
            }

            // 3. Setup Directional Light (The "Sun")
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            Light dirLight = null;
            
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    dirLight = light;
                    break;
                }
            }

            // If no directional light exists, create one
            if (dirLight == null)
            {
                GameObject lightGO = new GameObject("Directional Light");
                dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            // A bright cyan/teal for the main volumetric-style light
            dirLight.color = new Color(0.3f, 0.8f, 0.9f, 1f);
            dirLight.intensity = 1.2f; // Keep it bright enough to see the textures

            // Angle it diagonally downwards to create long shadows
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Debug.Log("<b><color=#00FFFF>[Lalim]</color></b> Underwater lighting successfully applied to the scene!");
        }
    }
}
