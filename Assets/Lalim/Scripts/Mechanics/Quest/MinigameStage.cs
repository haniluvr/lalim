using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// First-person "stage" for an area minigame: takes the camera from CameraController and frames
    /// the work site from the station's view point, hides the diver (the camera is at their eyes),
    /// locks movement and hides the game HUD (the HP/stamina bars stay). Going in and coming out
    /// fade through black, like a conversation, and the view then stays fixed (no panning).
    /// Exit() hands everything back.
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

            if (_camera != null && viewPoint != null) _move = _host.StartCoroutine(FadeTo(viewPoint));
        }

        /// <summary>Seconds of each half of the fade in and out of the stage (as for conversations).</summary>
        public const float FadeSeconds = 0.3f;

        private IEnumerator FadeTo(Transform target)
        {
            ScreenFader fader = ScreenFader.Instance;
            yield return fader.FadeOut(FadeSeconds);
            _camera.transform.SetPositionAndRotation(target.position, target.rotation);
            yield return fader.FadeIn(FadeSeconds);
            _move = null;
        }

        public void Exit()
        {
            if (!_active) return;
            _active = false;

            if (_move != null && _host != null) _host.StopCoroutine(_move);
            _move = null;

            if (_host != null && _host.isActiveAndEnabled) _host.StartCoroutine(FadeBack());
            else HandBack();
        }

        // Fade to black, give the camera back behind the diver, fade in.
        private IEnumerator FadeBack()
        {
            ScreenFader fader = ScreenFader.Instance;
            yield return fader.FadeOut(FadeSeconds * 0.7f);
            HandBack();
            yield return fader.FadeIn(FadeSeconds);
        }

        private void HandBack()
        {
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
