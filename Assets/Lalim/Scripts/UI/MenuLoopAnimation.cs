using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lalim.UI
{
    /// <summary>
    /// Lightweight component for looping animations on Main Menu elements.
    /// Can be attached to UI RectTransforms (for breathing, floating, glowing)
    /// or 3D Camera/Background objects (for slow camera panning/orbiting).
    /// </summary>
    public class MenuLoopAnimation : MonoBehaviour
    {
        public enum AnimationType
        {
            UI_BreathAndFloat,  // Scale pulse + vertical bobbing (Great for Title / Logos / Buttons)
            UI_ScalePulseOnly,   // Pulsing scale only
            UI_FloatOnly,        // Floating vertical bobbing only
            UI_FadeGlow,         // Smooth CanvasGroup / Image alpha fade loop
            Camera_SlowOrbit,    // Continuous slow 3D rotation around Y-axis
            Camera_CinematicSway // Subtle sine wave camera sway (Position & Rotation)
        }

        [Header("Animation Settings")]
        [SerializeField] private AnimationType _type = AnimationType.UI_BreathAndFloat;
        [SerializeField] private bool _animateOnEnable = true;
        [SerializeField] private float _speedMultiplier = 1f;

        [Header("UI Breathing / Pulsing")]
        [SerializeField] private bool _enableScalePulse = true;
        [SerializeField] private Vector3 _minScale = new Vector3(0.96f, 0.96f, 1f);
        [SerializeField] private Vector3 _maxScale = new Vector3(1.04f, 1.04f, 1f);
        [SerializeField] private float _pulseFrequency = 1.5f;

        [Header("UI Floating / Bobbing")]
        [SerializeField] private bool _enableFloating = true;
        [SerializeField] private float _floatAmplitude = 10f; // Pixels
        [SerializeField] private float _floatFrequency = 1.2f;

        [Header("UI Alpha Glow")]
        [SerializeField] private CanvasGroup _targetCanvasGroup;
        [SerializeField] private float _minAlpha = 0.6f;
        [SerializeField] private float _maxAlpha = 1.0f;
        [SerializeField] private float _alphaFrequency = 2.0f;

        [Header("Camera Orbit / Sway")]
        [SerializeField] private Transform _orbitPivotTarget;
        [SerializeField] private float _orbitSpeedY = 5.0f; // Degrees per second
        [SerializeField] private float _swayPosAmount = 0.5f;
        [SerializeField] private float _swayRotAmount = 1.5f;

        // Internal State
        private RectTransform _rectTransform;
        private Vector3 _initialScale;
        private Vector2 _initialAnchoredPos;
        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private float _timeOffset;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            
            if (_rectTransform != null)
            {
                _initialScale = _rectTransform.localScale;
                _initialAnchoredPos = _rectTransform.anchoredPosition;
            }

            _initialLocalPos = transform.localPosition;
            _initialLocalRot = transform.localRotation;

            // Randomize offset so multiple menu items don't pulse synchronously
            _timeOffset = Random.Range(0f, 100f);

            if (_targetCanvasGroup == null)
            {
                _targetCanvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            if (_rectTransform != null && _initialScale != Vector3.zero)
            {
                _rectTransform.localScale = _initialScale;
                _rectTransform.anchoredPosition = _initialAnchoredPos;
            }
        }

        private void Update()
        {
            if (!_animateOnEnable) return;

            float time = (Time.time + _timeOffset) * _speedMultiplier;

            switch (_type)
            {
                case AnimationType.UI_BreathAndFloat:
                    AnimateScale(time);
                    AnimateFloat(time);
                    break;

                case AnimationType.UI_ScalePulseOnly:
                    AnimateScale(time);
                    break;

                case AnimationType.UI_FloatOnly:
                    AnimateFloat(time);
                    break;

                case AnimationType.UI_FadeGlow:
                    AnimateAlpha(time);
                    break;

                case AnimationType.Camera_SlowOrbit:
                    AnimateCameraOrbit();
                    break;

                case AnimationType.Camera_CinematicSway:
                    AnimateCameraSway(time);
                    break;
            }
        }

        private void AnimateScale(float time)
        {
            if (!_enableScalePulse || _rectTransform == null) return;

            // Sine wave normalized between 0 and 1
            float t = (Mathf.Sin(time * _pulseFrequency) + 1f) * 0.5f;
            _rectTransform.localScale = Vector3.Lerp(_minScale, _maxScale, t);
        }

        private void AnimateFloat(float time)
        {
            if (!_enableFloating || _rectTransform == null) return;

            float yOffset = Mathf.Sin(time * _floatFrequency) * _floatAmplitude;
            _rectTransform.anchoredPosition = _initialAnchoredPos + new Vector2(0f, yOffset);
        }

        private void AnimateAlpha(float time)
        {
            if (_targetCanvasGroup == null) return;

            float t = (Mathf.Sin(time * _alphaFrequency) + 1f) * 0.5f;
            _targetCanvasGroup.alpha = Mathf.Lerp(_minAlpha, _maxAlpha, t);
        }

        private void AnimateCameraOrbit()
        {
            if (_orbitPivotTarget != null)
            {
                transform.RotateAround(_orbitPivotTarget.position, Vector3.up, _orbitSpeedY * Time.deltaTime);
            }
            else
            {
                transform.Rotate(Vector3.up, _orbitSpeedY * Time.deltaTime, Space.World);
            }
        }

        private void AnimateCameraSway(float time)
        {
            float posX = Mathf.Sin(time * 0.8f) * _swayPosAmount;
            float posY = Mathf.Cos(time * 0.6f) * (_swayPosAmount * 0.5f);
            transform.localPosition = _initialLocalPos + new Vector3(posX, posY, 0f);

            float rotZ = Mathf.Sin(time * 0.5f) * _swayRotAmount;
            float rotY = Mathf.Cos(time * 0.7f) * _swayRotAmount;
            transform.localRotation = _initialLocalRot * Quaternion.Euler(0f, rotY, rotZ);
        }
    }
}
