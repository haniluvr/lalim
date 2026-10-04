using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.EditorTools
{
    /// <summary>
    /// Tools ▸ Lalim ▸ Level 1 ▸ Set Up Statue Talk Framing — for each deity statue, a TalkPoint
    /// (where the diver floats, facing the statue's face), a TalkDugong (the dugong beside the
    /// diver) and a TalkCamera (a fixed 3/4 shot: the statue's face on the left, diver and dugong
    /// on the right), used when the player taps Talk.
    ///
    /// They sit next to the statue (not under it: the imported statues carry a huge scale), so
    /// they're easy to move by hand. Existing points are kept; delete one to have it re-made, or
    /// use Reframe to re-place the dugong spots and cameras around the current TalkPoints.
    /// </summary>
    public static class StatueTalkFraming
    {
        private const float DiverDistance = 2.6f;    // diver's distance in front of the statue's front surface
        private const float FaceHeight = 0.83f;      // of the statue's height, from the bottom
        private const float DugongBack = 0.9f;       // further from the statue than the diver...
        private const float DugongSide = 1.9f;       // ...and beside them, on the far side from the camera
        private const float DugongDown = 0.4f;
        private const float CameraAngle = 30f;       // from side-on toward the statue's front: the 3/4 view
        private const float CameraDistance = 1.6f;   // × the statue-face ↔ diver span
        private const float CameraUp = 0.1f;         // × the span
        private const float CameraAim = 0.55f;       // aim between the face (0) and the diver + dugong (1)

        [MenuItem("Tools/Lalim/Level 1/Set Up Statue Talk Framing")]
        public static void SetUpMenu() => Debug.Log(SetUp(false));

        [MenuItem("Tools/Lalim/Level 1/Reframe Statue Talk Cameras")]
        public static void ReframeMenu() => Debug.Log(SetUp(true));

        /// <param name="reframe">Re-place the dugong spots and cameras around the (kept) TalkPoints.</param>
        public static string SetUp(bool reframe)
        {
            var report = new System.Text.StringBuilder();
            foreach (StatueDialogueRouter statue in Object.FindObjectsByType<StatueDialogueRouter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Bounds b;
                if (!TryBounds(statue.transform, out b)) continue;

                // The statues are Blender imports: the face points along -up (flattened).
                Vector3 faceDir = -statue.transform.up;
                faceDir.y = 0f;
                if (faceDir.sqrMagnitude < 0.01f) faceDir = statue.transform.forward;
                faceDir.Normalize();
                // The camera sits on this side, so the statue lands on the left of the frame.
                Vector3 side = Vector3.Cross(Vector3.up, faceDir).normalized;

                float faceY = b.min.y + b.size.y * FaceHeight;
                float halfDepth = Mathf.Abs(faceDir.x) * b.extents.x + Mathf.Abs(faceDir.z) * b.extents.z;
                Vector3 face = new Vector3(b.center.x, faceY, b.center.z) + faceDir * halfDepth * 0.5f;

                Transform parent = statue.transform.parent;
                string deity = statue.DeityName;
                Transform point = Ensure(parent, $"TalkPoint_{deity}", out bool newPoint);
                if (newPoint)
                {
                    point.position = new Vector3(b.center.x, faceY - 0.8f, b.center.z) + faceDir * (halfDepth + DiverDistance);
                    point.rotation = Quaternion.LookRotation(-faceDir, Vector3.up);
                }
                Transform dugong = Ensure(parent, $"TalkDugong_{deity}", out bool newDugong);
                if (newDugong || reframe)
                {
                    Undo.RecordObject(dugong, "Statue talk framing");
                    dugong.position = point.position + faceDir * DugongBack - side * DugongSide - Vector3.up * DugongDown;
                    Vector3 look = face - dugong.position;
                    look.y = 0f;
                    dugong.rotation = Quaternion.LookRotation(look, Vector3.up);
                }
                Transform cam = Ensure(parent, $"TalkCamera_{deity}", out bool newCam);
                if (newCam || reframe)
                {
                    Undo.RecordObject(cam, "Statue talk framing");
                    Vector3 group = (point.position + dugong.position) * 0.5f;
                    Vector3 flatSpan = group - face;
                    flatSpan.y = 0f;
                    float span = Mathf.Max(3f, flatSpan.magnitude);
                    Vector3 target = Vector3.Lerp(face, group, CameraAim);
                    float a = CameraAngle * Mathf.Deg2Rad;
                    Vector3 dir = side * Mathf.Cos(a) + faceDir * Mathf.Sin(a);
                    cam.position = target + dir * span * CameraDistance + Vector3.up * span * CameraUp;
                    cam.rotation = Quaternion.LookRotation(target - cam.position, Vector3.up);
                }

                Undo.RecordObject(statue, "Statue talk framing");
                statue.EditorSetTalkFraming(point, dugong, cam);
                EditorUtility.SetDirty(statue);

                bool blocked = Physics.CheckCapsule(point.position + Vector3.up * 0.5f, point.position - Vector3.up * 0.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore);
                report.AppendLine($"{deity}: talk point {(newPoint ? "made" : "kept")} at {point.position:F1}, dugong spot {(newDugong ? "made" : reframe ? "re-placed" : "kept")}, camera {(newCam ? "made" : reframe ? "re-placed" : "kept")}" +
                                  (blocked ? " — WARNING: the diver would overlap scenery there, move TalkPoint" : ""));
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return report.Length > 0 ? report.ToString() : "No statues in the open scene.";
        }

        private static Transform Ensure(Transform parent, string name, out bool created)
        {
            Transform t = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
            created = t == null;
            if (!created) return t;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Statue talk framing");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static bool TryBounds(Transform t, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
