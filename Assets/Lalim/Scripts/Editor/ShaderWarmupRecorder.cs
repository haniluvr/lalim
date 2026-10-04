using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.Mechanics.Quest;
using Lalim.Abilities;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Records the shaders (graphics states) each scene uses, for the loading screen to compile
    /// ahead of time (ShaderWarmup):
    ///   1. Tools ▸ Lalim ▸ Shader Warm-up ▸ Record While Playing (on)
    ///   2. Play the scene — or run Fly Camera Tour in Play mode for a quick sweep of the level
    ///   3. Stop / change scene: the recording is added to Resources/ShaderWarmup/&lt;scene&gt;_&lt;API&gt;
    /// Recordings are per graphics API (the Mac Editor records Metal). Development builds on a
    /// device send theirs to the connected Editor.
    /// </summary>
    public static class ShaderWarmupRecorder
    {
        private const string RecordMenu = "Tools/Lalim/Shader Warm-up/Record While Playing";

        [MenuItem(RecordMenu)]
        private static void ToggleRecording()
        {
            bool on = PlayerPrefs.GetInt(ShaderWarmup.RecordPref, 0) == 0;
            PlayerPrefs.SetInt(ShaderWarmup.RecordPref, on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log(on ? "[ShaderWarmup] Recording on: play scenes to record them (starts next time you press Play)." : "[ShaderWarmup] Recording off.");
        }

        [MenuItem(RecordMenu, true)]
        private static bool ToggleRecordingCheck()
        {
            Menu.SetChecked(RecordMenu, PlayerPrefs.GetInt(ShaderWarmup.RecordPref, 0) == 1);
            return true;
        }

        public static void SetRecording(bool on)
        {
            PlayerPrefs.SetInt(ShaderWarmup.RecordPref, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        // ───────────────────────── Camera tour ─────────────────────────

        private static readonly List<Pose> _tour = new List<Pose>();
        private static int _step;

        public static bool TourRunning => _tour.Count > 0;

        /// <summary>
        /// Play mode: flies the camera past every minigame area, statue and down the well, looking
        /// around at each, one view per frame — so a recording covers the whole level in seconds.
        /// </summary>
        [MenuItem("Tools/Lalim/Shader Warm-up/Fly Camera Tour (Play mode)")]
        public static void StartTour()
        {
            if (!Application.isPlaying || Camera.main == null) { Debug.LogWarning("[ShaderWarmup] Enter Play mode first."); return; }
            var points = new List<Vector3>();
            foreach (var st in Object.FindObjectsByType<AreaStation>(FindObjectsInactive.Include, FindObjectsSortMode.None)) points.Add(st.transform.position + Vector3.up * 2f);
            foreach (var st in Object.FindObjectsByType<StatueDialogueRouter>(FindObjectsInactive.Include, FindObjectsSortMode.None)) points.Add(st.transform.position + Vector3.up * 3f + st.transform.forward * 6f);
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)) points.Add(cp.transform.position + Vector3.up * 1.5f);
            var dark = Object.FindFirstObjectByType<ShaftDarkness>();
            if (dark != null) for (float y = -45f; y > -110f; y -= 12f) points.Add(new Vector3(dark.transform.position.x + 3f, y, dark.transform.position.z));

            _tour.Clear();
            foreach (Vector3 p in points)
                for (int yaw = 0; yaw < 360; yaw += 45)
                    foreach (float pitch in new[] { -35f, 0f, 30f })
                        _tour.Add(new Pose(p, Quaternion.Euler(pitch, yaw, 0f)));
            _step = 0;
            if (CameraController.Instance != null) CameraController.Instance.enabled = false;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
            Debug.Log($"[ShaderWarmup] Camera tour: {_tour.Count} views from {points.Count} spots.");
        }

        private static void Step()
        {
            Camera cam = Camera.main;
            if (!Application.isPlaying || cam == null || _step >= _tour.Count)
            {
                EditorApplication.update -= Step;
                _tour.Clear();
                if (Application.isPlaying && CameraController.Instance != null)
                {
                    CameraController.Instance.enabled = true;
                    CameraController.Instance.SnapToCurrentMode();
                }
                Debug.Log("[ShaderWarmup] Camera tour finished.");
                return;
            }
            cam.transform.SetPositionAndRotation(_tour[_step].position, _tour[_step].rotation);
            _step++;
        }
    }
}
