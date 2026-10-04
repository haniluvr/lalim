using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Turns on the depth-texture underwater fog (Lalim/Underwater Depth Fog, a Full Screen Pass on
    /// the URP renderers) for this scene and feeds it the water's look each frame: how fast red,
    /// green and blue are absorbed with distance, where it starts, and the water colour (the live
    /// fog colour, so pollution and the dark well carry through). Off in scenes without it.
    /// </summary>
    public class UnderwaterDepthFog : MonoBehaviour
    {
        private static readonly int FogId = Shader.PropertyToID("_LalimDepthFog");
        private static readonly int AbsorbId = Shader.PropertyToID("_LalimDepthFogAbsorb");
        private static readonly int ColorId = Shader.PropertyToID("_LalimDepthFogColor");

        [SerializeField, Range(0f, 1f)] private float _strength = 1f;
        [Tooltip("Clear water right around the camera (metres) before absorption starts.")]
        [SerializeField, Min(0f)] private float _startDistance = 2f;
        [Tooltip("Light lost per metre for red, green and blue (red goes first in real water).")]
        [SerializeField] private Vector3 _absorption = new Vector3(0.045f, 0.018f, 0.008f);
        [SerializeField, Min(0f)] private float _density = 1f;
        [Tooltip("World height of the water surface; surfaces deeper than this sit in darker water.")]
        [SerializeField] private float _surfaceY;
        [Tooltip("How much darker the water gets per metre of depth.")]
        [SerializeField, Min(0f)] private float _darkeningPerMetre = 0.006f;

        private void LateUpdate()
        {
            Shader.SetGlobalVector(FogId, new Vector4(_strength, _startDistance, _density, _darkeningPerMetre));
            Shader.SetGlobalVector(AbsorbId, new Vector4(_absorption.x, _absorption.y, _absorption.z, _surfaceY));
            Shader.SetGlobalColor(ColorId, RenderSettings.fogColor);
        }

        private void OnDisable() => Shader.SetGlobalVector(FogId, Vector4.zero);
    }
}
