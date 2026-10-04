using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// The work behind the loading screen (SceneLoader), in order, and how much of the progress bar
    /// each takes:
    ///   FreeMemory  0–10%   the old scene and its assets leave RAM; collect the garbage
    ///   LoadData   10–80%   read the new scene from storage, decompress and deserialise it
    ///   WarmShaders 80–100% compile the scene's recorded graphics states (ShaderWarmup)
    /// Activation (creating the scene's objects, restoring the save, uploading to the GPU) happens
    /// after 100%, behind the loading screen's frozen last frame.
    /// </summary>
    public static class LoadingStages
    {
        public enum Stage { FreeMemory, LoadData, WarmShaders }

        /// <summary>Resources folder holding each scene's recorded graphics states (ShaderWarmup.FileName).</summary>
        public const string ShaderWarmupFolder = "ShaderWarmup/";

        private static readonly float[] Starts = { 0f, 0.1f, 0.8f, 1f };

        /// <summary>Overall 0..1 progress for <paramref name="t"/> (0..1) of the way through <paramref name="stage"/>.</summary>
        public static float Progress(Stage stage, float t)
        {
            int i = (int)stage;
            return Mathf.Lerp(Starts[i], Starts[i + 1], Mathf.Clamp01(t));
        }
    }
}
