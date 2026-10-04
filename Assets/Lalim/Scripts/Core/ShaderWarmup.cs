using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.Jobs;

namespace Lalim.Core
{
    /// <summary>
    /// Shader warm-up for the loading screen. The first time the GPU meets a material in a new
    /// light/fog/shadow setup it has to compile a "graphics state" (shader variant + pipeline
    /// state); doing that mid-swim causes a hitch. Each scene's states are recorded while playing
    /// (see <see cref="Tracer"/>) into Resources/ShaderWarmup/&lt;scene&gt;_&lt;graphics API&gt;, and the
    /// loading screen compiles them before the scene appears.
    /// </summary>
    public static class ShaderWarmup
    {
        public static string FileName(string scene) => $"{scene}_{SystemInfo.graphicsDeviceType}";

        /// <summary>
        /// Starts loading the scene's recording. Do this BEFORE the scene's own async load: Unity
        /// holds other async loads back while a scene waits for activation.
        /// </summary>
        public static ResourceRequest Request(string scene) =>
            Resources.LoadAsync<GraphicsStateCollection>(LoadingStages.ShaderWarmupFolder + FileName(scene));

        /// <summary>Compiles the recorded graphics states a few per frame; reports 0..1. No recording = nothing to do.</summary>
        public static IEnumerator WarmUp(GraphicsStateCollection states, Action<float> progress)
        {
            if (states == null || states.isWarmedUp || states.totalGraphicsStateCount == 0) yield break;

            int total = states.totalGraphicsStateCount;
            int stalled = 0, last = -1;
            // A state this device can't build is skipped: stop once nothing more completes.
            while (!states.isWarmedUp && stalled < 3)
            {
                JobHandle job = states.WarmUpProgressively(6);
                while (!job.IsCompleted) yield return null;
                job.Complete();
                int done = states.completedWarmupCount;
                stalled = done > last ? 0 : stalled + 1;
                last = done;
                progress?.Invoke(done / (float)total);
                yield return null;
            }
        }

        // ───────────────────────── Recording ─────────────────────────

        /// <summary>PlayerPrefs switch for recording in the Editor (Tools ▸ Lalim ▸ Shader Warm-up).</summary>
        public const string RecordPref = "lalim.recordShaderWarmup";

        /// <summary>
        /// Records the graphics states each gameplay scene uses, from when it opens until you leave
        /// it. In the Editor (when switched on) it's added to Resources/ShaderWarmup; in a
        /// development build on a device it's sent to the connected Editor (Unity saves it in the
        /// project; move it into Resources/ShaderWarmup).
        /// </summary>
        public static class Tracer
        {
            private static GraphicsStateCollection _trace;
            private static string _scene;
            private static bool _hooked;

            private static bool Enabled =>
#if UNITY_EDITOR
                PlayerPrefs.GetInt(RecordPref, 0) == 1;
#else
                Debug.isDebugBuild;
#endif

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void Init()
            {
                if (!Enabled) return;
                if (!_hooked)
                {
                    _hooked = true;   // once, even when play mode keeps statics between runs
                    SceneManager.activeSceneChanged += (_, next) => { Finish(); Begin(next.name); };
                    Application.quitting += Finish;
                }
                Begin(SceneManager.GetActiveScene().name);
            }

            private static void Begin(string scene)
            {
                if (scene == GameConstants.Scenes.LOADING || scene == GameConstants.Scenes.BOOT || scene == GameConstants.Scenes.SPLASH) return;
                _trace = new GraphicsStateCollection();
                _scene = scene;
                if (!_trace.BeginTrace()) _trace = null;
            }

            private static void Finish()
            {
                if (_trace == null) return;
                _trace.EndTrace();
                string name = FileName(_scene);
#if UNITY_EDITOR
                // Add to what earlier sessions recorded.
                string path = $"Assets/Lalim/Resources/{LoadingStages.ShaderWarmupFolder}{name}.graphicsstate";
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                var all = new GraphicsStateCollection();
                if (System.IO.File.Exists(path)) all.LoadFromFile(path);
                all.Append(_trace);
                all.SaveToFile(path);
                UnityEditor.AssetDatabase.ImportAsset(path);
                Debug.Log($"[ShaderWarmup] Recorded {_scene}: {all.totalGraphicsStateCount} graphics states → {path}");
#else
                _trace.SendToEditor(name);
#endif
                _trace = null;
            }
        }
    }
}
