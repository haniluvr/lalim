using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Lalim.UI
{
    /// <summary>Marks UI whose buttons are gameplay, not menus (minigame panels, the action button): no tap sound.</summary>
    [DisallowMultipleComponent]
    public class NoTapSound : MonoBehaviour { }

    /// <summary>
    /// One quiet tap for every menu button in the game (menus, pause, inventory, Equip, settings,
    /// the dialogue's own buttons), without each screen wiring it up: a press and release on the
    /// same interactable Button or Toggle plays SfxCue.ButtonTap. Skipped under a
    /// <see cref="NoTapSound"/> and on the hint bulb (it has its own sound). Lives for the whole
    /// session; DialogueManager plays the same tap when a tap moves the dialogue on.
    /// </summary>
    public class UiTapSound : MonoBehaviour
    {
        private readonly Dictionary<int, (Selectable button, Camera cam)> _pressed = new Dictionary<int, (Selectable, Camera)>();
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[UiTapSound]");
            DontDestroyOnLoad(go);
            go.AddComponent<UiTapSound>();
        }

        private void Update()
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began) Press(t.fingerId, t.position);
                    else if (t.phase == TouchPhase.Ended) Release(t.fingerId, t.position);
                    else if (t.phase == TouchPhase.Canceled) _pressed.Remove(t.fingerId);
                }
                return;
            }
            if (Input.GetMouseButtonDown(0)) Press(-1, Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) Release(-1, Input.mousePosition);
        }

        private void Press(int id, Vector2 pos)
        {
            Selectable s = TappableAt(pos, out Camera cam);
            if (s != null) _pressed[id] = (s, cam); else _pressed.Remove(id);
        }

        // A tap, not a drag off the button: released inside the button it was pressed on (checked
        // against its rectangle, so a button that closes its own panel on click still counts).
        private void Release(int id, Vector2 pos)
        {
            if (!_pressed.TryGetValue(id, out var press)) return;
            _pressed.Remove(id);
            if (press.button == null) return;
            var rt = (RectTransform)press.button.transform;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, pos, press.cam)) Core.Sfx.Play(Core.SfxCue.ButtonTap);
        }

        private Selectable TappableAt(Vector2 pos, out Camera cam)
        {
            cam = null;
            EventSystem es = EventSystem.current;
            if (es == null) return null;
            _pointer ??= new PointerEventData(es);
            _pointer.position = pos;
            _hits.Clear();
            es.RaycastAll(_pointer, _hits);
            if (_hits.Count == 0) return null;
            GameObject top = _hits[0].gameObject;
            cam = _hits[0].module != null ? _hits[0].module.eventCamera : null;
            Selectable s = top.GetComponentInParent<Selectable>();
            if (s == null || !s.IsInteractable() || !(s is Button || s is Toggle)) return null;
            if (s.GetComponentInParent<NoTapSound>() != null || s.GetComponent<HintBulb>() != null) return null;
            return s;
        }
    }
}
