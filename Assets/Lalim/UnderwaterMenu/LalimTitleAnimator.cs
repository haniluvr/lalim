using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Lalim.UI
{
    /// <summary>
    /// Animates the "LALIM" title: letters sit on a semi arc, fade in one after another like
    /// bubbles drifting up, then keep floating gently. Driven by UnderwaterMenuDirector.Tick(t).
    /// </summary>
    public class LalimTitleAnimator : MonoBehaviour
    {
        [SerializeField] RectTransform[] _letters;
        [SerializeField] float _stagger = 0.28f;
        [SerializeField] float _fadeDuration = 1.4f;
        [SerializeField] float _letterAlpha = 0.3f;
        [SerializeField] float _rimAlpha = 0.9f;
        [SerializeField] float _riseDistance = 30f;
        [SerializeField] float _floatAmplitude = 9f;
        [SerializeField] float _floatSpeed = 0.9f;
        [SerializeField] float _driftX = 3f;

        Vector2[] _base;
        TMP_Text[] _text;
        Material[] _mat;
        bool _init;

        public float TotalDuration
        {
            get { return (_letters != null ? Mathf.Max(0, _letters.Length - 1) : 0) * _stagger + _fadeDuration; }
        }

        [Header("Standalone use (splash screen)")]
        [Tooltip("Play the entrance and float by itself. Turn off when something else calls Tick (main menu director).")]
        [SerializeField] bool _autoPlay = true;
        [SerializeField] float _autoPlayDelay = 0.2f;
        float _clock;

        void OnEnable() { _clock = 0f; Init(); }

        void Update()
        {
            if (!_autoPlay) return;
            _clock += Time.unscaledDeltaTime;
            Tick(_clock - _autoPlayDelay);
        }

        void Init()
        {
            if (_init || _letters == null) return;
            int n = _letters.Length;
            _base = new Vector2[n];
            _text = new TMP_Text[n];
            _mat = new Material[n];
            for (int i = 0; i < n; i++)
            {
                if (_letters[i] == null) continue;
                _base[i] = _letters[i].anchoredPosition;
                _text[i] = _letters[i].GetComponentInChildren<TMP_Text>();
                if (_text[i] != null) _mat[i] = _text[i].fontMaterial; // per-letter instance so the rim can fade
            }
            _init = true;
        }

        /// <summary>t = seconds since the title started. Negative = not shown yet.</summary>
        public void Tick(float t)
        {
            Init();
            if (!_init) return;
            for (int i = 0; i < _letters.Length; i++)
            {
                if (_letters[i] == null) continue;
                float k = Mathf.Clamp01((t - i * _stagger) / Mathf.Max(0.01f, _fadeDuration));
                float e = Mathf.SmoothStep(0f, 1f, k);
                float ph = Time.unscaledTime * _floatSpeed + i * 0.9f;
                float shimmer = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 1.7f + i * 1.3f);

                Vector2 p = _base[i];
                p.y += (1f - e) * -_riseDistance + Mathf.Sin(ph) * _floatAmplitude * e;
                p.x += Mathf.Sin(ph * 0.7f + 1.1f) * _driftX * e;
                _letters[i].anchoredPosition = p;
                _letters[i].localScale = Vector3.one * (0.85f + 0.15f * e);

                if (_text[i] != null)
                {
                    Color c = _text[i].color; c.a = e * _letterAlpha * shimmer; _text[i].color = c;
                }
                if (_mat[i] != null && _mat[i].HasProperty("_OutlineColor"))
                {
                    Color c = _mat[i].GetColor("_OutlineColor"); c.a = e * _rimAlpha * shimmer; _mat[i].SetColor("_OutlineColor", c);
                }
            }
        }
    }
}
