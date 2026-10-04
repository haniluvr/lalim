using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// First-person "stage" for an area minigame: takes the camera from CameraController and frames
    /// the work site from the station's view point, hides the diver (the camera is at their eyes),
    /// locks movement and hides the game HUD. Exit() hands everything back.
    /// </summary>
    public class MinigameStage
    {
        private readonly MonoBehaviour _host;
        private Camera _camera;
        private CameraController _controller;
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private Coroutine _move;
        private bool _active;

        public Camera Camera => _camera;

        public MinigameStage(MonoBehaviour host) { _host = host; }

        public void Enter(Transform viewPoint, float blendSeconds = 0.6f)
        {
            if (_active) return;
            _active = true;

            _camera = Camera.main;
            _controller = CameraController.Instance;
            if (_controller != null) _controller.enabled = false;

            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                player.SetMovementEnabled(false);
                foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                {
                    if (r.enabled) { r.enabled = false; _hiddenRenderers.Add(r); }
                }
            }

            HUDController.Instance?.SetSuppressed(this, true);

            if (_camera != null && viewPoint != null) _move = _host.StartCoroutine(Blend(viewPoint, blendSeconds));
        }

        public void Exit()
        {
            if (!_active) return;
            _active = false;

            if (_move != null && _host != null) _host.StopCoroutine(_move);
            _move = null;

            foreach (Renderer r in _hiddenRenderers) if (r != null) r.enabled = true;
            _hiddenRenderers.Clear();

            HUDController.Instance?.SetSuppressed(this, false);

            if (_controller != null)
            {
                _controller.enabled = true;
                _controller.SnapToCurrentMode();
            }
            PlayerController.Instance?.SetMovementEnabled(true);
        }

        private IEnumerator Blend(Transform target, float seconds)
        {
            Transform cam = _camera.transform;
            Vector3 p0 = cam.position;
            Quaternion r0 = cam.rotation;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                cam.SetPositionAndRotation(Vector3.Lerp(p0, target.position, k), Quaternion.Slerp(r0, target.rotation, k));
                yield return null;
            }
            cam.SetPositionAndRotation(target.position, target.rotation);
            _move = null;
        }

        /// <summary>World ray under the pointer (mouse / first touch).</summary>
        public Ray PointerRay() => _camera != null ? _camera.ScreenPointToRay(Input.mousePosition) : new Ray();

        /// <summary>True when the pointer is over a UI button (so world taps underneath are ignored).</summary>
        public static bool PointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            var data = new UnityEngine.EventSystems.PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(data, hits);
            foreach (var h in hits) if (h.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
            return false;
        }
    }
}
