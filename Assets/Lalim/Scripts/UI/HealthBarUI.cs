using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// Top-left HP bar with a trailing "damage ghost" that lags behind the real value, and a red
    /// pulse below 25% HP.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [Tooltip("Sits behind the fill; catches up after a short delay so recent damage stays visible.")]
        [SerializeField] private Image _ghostFill;
        [SerializeField] private Image _frame;
        [SerializeField] private TMP_Text _label;

        [Header("Behaviour")]
        [SerializeField] private float _ghostDelay = 0.4f;
        [SerializeField] private float _ghostSpeed = 0.6f; // fraction of the bar per second
        [SerializeField, Range(0f, 1f)] private float _lowThreshold = 0.25f;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = new Color(0.35f, 0.95f, 0.65f);
        [SerializeField] private Color _lowColor = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color _ghostColor = new Color(1f, 0.85f, 0.5f, 0.8f);
        [SerializeField] private Color _frameColor = Color.white;

        private float _target = 1f;
        private float _ghost = 1f;
        private float _ghostHold;

        private void OnEnable() => EventBus.Subscribe<PlayerHealthChangedEvent>(OnHealthChanged);
        private void OnDisable() => EventBus.Unsubscribe<PlayerHealthChangedEvent>(OnHealthChanged);

        private void Start()
        {
            if (PlayerHealth.Instance != null) SetValue(PlayerHealth.Instance.Current, PlayerHealth.Instance.Max, snap: true);
            if (_ghostFill != null) _ghostFill.color = _ghostColor;
        }

        private void OnHealthChanged(PlayerHealthChangedEvent evt)
        {
            // Heals and respawns (Delta >= 0) move the ghost with the bar; only damage leaves a trail.
            SetValue(evt.Current, evt.Max, snap: evt.Delta >= 0f);
        }

        private void SetValue(float current, float max, bool snap)
        {
            float previous = _target;
            _target = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (snap || _target > _ghost) _ghost = _target;
            else if (_target < previous) _ghostHold = _ghostDelay;

            if (_label != null) _label.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void Update()
        {
            if (_ghostHold > 0f) _ghostHold -= Time.deltaTime;
            else if (_ghost > _target) _ghost = Mathf.MoveTowards(_ghost, _target, _ghostSpeed * Time.deltaTime);

            if (_fill != null) _fill.fillAmount = _target;
            if (_ghostFill != null) _ghostFill.fillAmount = _ghost;

            bool low = _target > 0f && _target < _lowThreshold;
            float pulse = low ? (Mathf.Sin(Lalim.Core.PauseClock.Time * 6f) + 1f) * 0.5f : 0f;
            if (_fill != null) _fill.color = low ? Color.Lerp(_lowColor, Color.white, pulse * 0.35f) : _normalColor;
            if (_frame != null) _frame.color = low ? Color.Lerp(_frameColor, _lowColor, pulse) : _frameColor;
        }
    }
}
