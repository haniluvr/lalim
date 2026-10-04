using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lalim.UI
{
    /// <summary>
    /// Slide switch like the settings mock-ups: a capsule track with a pearl knob that slides
    /// right (on, teal track) or left (off, grey track). Tap anywhere on it to flip.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PillSwitch : MonoBehaviour
    {
        [SerializeField] private Image _track;
        [SerializeField] private RectTransform _knob;
        [SerializeField] private Color _onColor = new Color(0.25f, 0.56f, 0.6f, 1f);
        [SerializeField] private Color _offColor = new Color(0.62f, 0.66f, 0.68f, 1f);
        [SerializeField] private float _slideSeconds = 0.12f;

        /// <summary>Raised when the player taps the switch (with the new value).</summary>
        public event Action<bool> Toggled;

        public bool IsOn { get; private set; } = true;

        private float _t = 1f; // 0 = off position, 1 = on position

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                SetOn(!IsOn, true);
                Toggled?.Invoke(IsOn);
            });
        }

        /// <summary>Sets the state without raising Toggled.</summary>
        public void SetOn(bool on, bool animate = false)
        {
            IsOn = on;
            if (!animate || !isActiveAndEnabled) { _t = on ? 1f : 0f; Layout(); }
        }

        private void Update()
        {
            float target = IsOn ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, _slideSeconds));
            Layout();
        }

        private void OnEnable() => Layout();

        private void Layout()
        {
            if (_track != null) _track.color = Color.Lerp(_offColor, _onColor, _t);
            if (_knob == null || _track == null) return;
            float travel = Mathf.Max(0f, (_track.rectTransform.rect.width - _knob.rect.width) * 0.5f);
            _knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, Mathf.SmoothStep(0f, 1f, _t)), 0f);
        }
    }
}
