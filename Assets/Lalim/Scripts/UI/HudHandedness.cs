using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Settings ▸ Controls ▸ Right Handed Mode. On (default) keeps the HUD exactly as authored:
    /// joystick on the left, skills on the right. Off mirrors it left↔right for left-handed play.
    ///
    /// Only positions move — icons, text and bars are never flipped. Containers (objects with no
    /// graphic of their own, like BottomRight_Cluster) are mirrored together with their children;
    /// anything with its own graphic or button moves as one piece. Centred pieces (HP bar, stamina)
    /// and nested canvases (the pause and settings modals) are left alone.
    /// </summary>
    public class HudHandedness : MonoBehaviour
    {
        private struct Layout
        {
            public RectTransform Rect;
            public Vector2 AnchorMin, AnchorMax, Pivot, Position;
        }

        private readonly List<Layout> _authored = new List<Layout>();
        private bool _mirrored;

        private void Awake()
        {
            foreach (Transform child in transform) Collect(child as RectTransform);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<SettingsChangedEvent>(OnSettingsChanged);
            Apply(!SettingsManager.Active.rightHandedMode);
        }

        private void OnDisable() => EventBus.Unsubscribe<SettingsChangedEvent>(OnSettingsChanged);

        private void OnSettingsChanged(SettingsChangedEvent evt) => Apply(!evt.Settings.rightHandedMode);

        private void Collect(RectTransform rt)
        {
            if (rt == null || rt.GetComponent<Canvas>() != null) return;

            // Centred pieces (HP bar, stamina shells, timers) already sit on the mirror line —
            // leave them and their insides alone so bars still fill left-to-right.
            bool centred = Mathf.Approximately(rt.anchorMin.x, 0.5f) && Mathf.Approximately(rt.anchorMax.x, 0.5f)
                        && Mathf.Approximately(rt.pivot.x, 0.5f) && Mathf.Abs(rt.anchoredPosition.x) < 1f;
            if (centred) return;
            _authored.Add(new Layout
            {
                Rect = rt,
                AnchorMin = rt.anchorMin,
                AnchorMax = rt.anchorMax,
                Pivot = rt.pivot,
                Position = rt.anchoredPosition,
            });

            bool isContainer = rt.GetComponent<Graphic>() == null && rt.GetComponent<Selectable>() == null;
            if (!isContainer) return;
            foreach (Transform child in rt) Collect(child as RectTransform);
        }

        private void Apply(bool mirrored)
        {
            if (mirrored == _mirrored) return;
            _mirrored = mirrored;
            foreach (Layout l in _authored)
            {
                if (l.Rect == null) continue;
                if (!mirrored)
                {
                    l.Rect.anchorMin = l.AnchorMin;
                    l.Rect.anchorMax = l.AnchorMax;
                    l.Rect.pivot = l.Pivot;
                    l.Rect.anchoredPosition = l.Position;
                    continue;
                }
                l.Rect.anchorMin = new Vector2(1f - l.AnchorMax.x, l.AnchorMin.y);
                l.Rect.anchorMax = new Vector2(1f - l.AnchorMin.x, l.AnchorMax.y);
                l.Rect.pivot = new Vector2(1f - l.Pivot.x, l.Pivot.y);
                l.Rect.anchoredPosition = new Vector2(-l.Position.x, l.Position.y);
            }
        }
    }
}
