using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lalim.Core
{
    /// <summary>
    /// The loading (or splash) scene's screen — its root overlay canvases — handled as one layer
    /// that fades over another scene while both are live. That's how SceneLoader crossfades:
    ///
    ///  - Into the loading screen: the loading scene is loaded additively and "parked" (its
    ///    cameras, lights, audio listener, event system, volumes and 3D renderers are switched off
    ///    so the scene we're leaving keeps rendering untouched), and its canvases fade in on top.
    ///  - Out of it: the canvases are detached (DontDestroyOnLoad) so they survive the swap to the
    ///    next scene, then fade away over the new scene and are destroyed.
    ///
    /// No screenshots are involved, so nothing can be captured at the wrong size or alpha.
    /// </summary>
    public sealed class TransitionScreen
    {
        /// <summary>Above every gameplay/menu canvas, below ScreenFader's black (32767).</summary>
        public const int SortingOrder = 32000;

        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
        private readonly List<GameObject> _roots = new List<GameObject>();
        private readonly List<Behaviour> _parkedBehaviours = new List<Behaviour>();
        private readonly List<Renderer> _parkedRenderers = new List<Renderer>();

        public bool IsEmpty => _groups.Count == 0;

        /// <summary>The screen made of <paramref name="scene"/>'s root canvases.</summary>
        public static TransitionScreen Of(Scene scene)
        {
            var screen = new TransitionScreen();
            if (!scene.IsValid()) return screen;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (!canvas.isRootCanvas) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = SortingOrder;
                    CanvasGroup group = canvas.GetComponent<CanvasGroup>();
                    if (group == null) group = canvas.gameObject.AddComponent<CanvasGroup>();
                    screen._groups.Add(group);
                    if (!screen._roots.Contains(canvas.transform.root.gameObject))
                        screen._roots.Add(canvas.transform.root.gameObject);
                }
            }
            return screen;
        }

        /// <summary>Hides everything in <paramref name="scene"/> except its canvases.</summary>
        public void Park(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Behaviour b in root.GetComponentsInChildren<Behaviour>(true))
                {
                    bool park = b is Camera || b is Light || b is AudioListener
                        || b is UnityEngine.EventSystems.EventSystem
                        || b is UnityEngine.Rendering.Volume;
                    if (park && b.enabled) { b.enabled = false; _parkedBehaviours.Add(b); }
                }
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.enabled) { r.enabled = false; _parkedRenderers.Add(r); }
            }
        }

        /// <summary>Switches back on what Park switched off (once the scene underneath is gone).</summary>
        public void Unpark()
        {
            foreach (Behaviour b in _parkedBehaviours) if (b != null) b.enabled = true;
            foreach (Renderer r in _parkedRenderers) if (r != null) r.enabled = true;
            _parkedBehaviours.Clear();
            _parkedRenderers.Clear();
        }

        public void SetAlpha(float alpha)
        {
            foreach (CanvasGroup g in _groups)
            {
                if (g == null) continue;
                g.alpha = alpha;
                g.blocksRaycasts = alpha > 0.01f;   // nothing underneath can be tapped mid-transition
            }
        }

        /// <summary>Fades between two alphas (smoothstep). A long frame never skips the fade.</summary>
        public IEnumerator Fade(float from, float to, float seconds)
        {
            SetAlpha(from);
            for (float t = 0f; t < 1f;)
            {
                yield return null;
                t += SceneLoader.SteadyDeltaTime / Mathf.Max(0.01f, seconds);
                float k = Mathf.Clamp01(t);
                SetAlpha(Mathf.Lerp(from, to, k * k * (3f - 2f * k)));
            }
        }

        /// <summary>Keeps the canvases through the next single-mode scene load.</summary>
        public void Detach()
        {
            foreach (GameObject root in _roots)
                if (root != null) Object.DontDestroyOnLoad(root);
        }

        public void Destroy()
        {
            foreach (GameObject root in _roots)
                if (root != null) Object.Destroy(root);
            _roots.Clear();
            _groups.Clear();
        }
    }
}
