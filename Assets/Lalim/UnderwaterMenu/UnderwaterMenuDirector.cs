using UnityEngine;

namespace Lalim.UI
{
    /// <summary>
    /// Main-menu intro: the player and the dugong swim in from off-screen right (16:9)
    /// and settle on the right side of the frame, like the prototype video.
    /// The dugong keeps its swimming heading while travelling, then turns to the yaw of
    /// DugongEnd once it is in position. The menu UI fades in after the dugong arrives.
    /// The scene keeps the end pose so the edit-mode view is the final composition.
    /// </summary>
    public class UnderwaterMenuDirector : MonoBehaviour
    {
        [Header("Actors")]
        [SerializeField] Transform _player;
        [SerializeField] Transform _dugong;
        [SerializeField] Animator _playerAnim;
        [SerializeField] Animator _dugongAnim;

        [Header("Markers (position + yaw)")]
        [SerializeField] Transform _playerStart;
        [SerializeField] Transform _playerEnd;
        [SerializeField] Transform _dugongStart;
        [SerializeField] Transform _dugongEnd;

        [Header("Timing")]
        [SerializeField] float _startDelay = 0.4f;
        [SerializeField] float _playerDuration = 4.5f;
        [SerializeField] float _dugongDuration = 5.5f;
        [SerializeField] float _dugongTurnDuration = 2.2f;
        [Tooltip("The dugong starts turning once it has covered this fraction of its X path.")]
        [SerializeField, Range(0f, 1f)] float _turnStartPathFraction = 0.3333f;
        [SerializeField] float _travelBlend = 0.7f;
        [SerializeField] float _restBlend = 0.4f;
        [SerializeField] float _playerStartYaw = -15f;

        [Header("Menu UI (shown after the dugong is in position)")]
        [SerializeField] LalimTitleAnimator _title;
        [Tooltip("The title starts this many seconds before the dugong arrives.")]
        [SerializeField] float _titleLeadBeforeArrival = 0.8f;
        [SerializeField] CanvasGroup _menuUI;
        [Tooltip("The menu starts fading in this many seconds after the title starts.")]
        [SerializeField] float _menuAfterTitleStart = 1.6f;
        [SerializeField] float _menuFadeDuration = 1.0f;

        [Header("Ambient")]
        [SerializeField] Transform _cameraRig;
        [SerializeField] float _cameraDrift = 0.12f;
        [SerializeField] float _parallax = 0.15f;
        [SerializeField] float _bobAmount = 0.05f;
        [SerializeField] float _bobSpeed = 0.9f;

        static readonly int SwimBlend = Animator.StringToHash("SwimBlend");

        float _t;
        Vector3 _camBase;
        Vector3 _pEnd, _dEnd;
        float _pEndYaw, _dEndYaw, _dTravelYaw;

        float DugongArrival { get { return _startDelay + _dugongDuration; } }
        // ease-out cubic: path = 1 - (1-k)^3, so k for a given path fraction
        float TurnStart { get { return _startDelay + _dugongDuration * (1f - Mathf.Pow(1f - _turnStartPathFraction, 1f / 3f)); } }
        float TitleStart { get { return DugongArrival - _titleLeadBeforeArrival; } }

        void Start()
        {
            if (_cameraRig != null) _camBase = _cameraRig.position;
            if (_menuUI != null)
            {
                _menuUI.alpha = 0f;
                _menuUI.interactable = false;
                _menuUI.blocksRaycasts = false;
            }
            if (_player == null || _playerEnd == null) return;
            _pEnd = _playerEnd.position; _pEndYaw = _playerEnd.eulerAngles.y;
            _dEnd = _dugongEnd != null ? _dugongEnd.position : _dugong.position;
            _dEndYaw = _dugongEnd != null ? _dugongEnd.eulerAngles.y : _dugong.eulerAngles.y;
            _dTravelYaw = _dugongStart != null ? _dugongStart.eulerAngles.y : _dEndYaw;
            Apply(0f);
        }

        static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3f); }

        void Apply(float t)
        {
            float pk = Mathf.Clamp01((t - _startDelay) / _playerDuration);
            float dk = Mathf.Clamp01((t - _startDelay) / _dugongDuration);
            float pe = EaseOut(pk), de = EaseOut(dk);
            float bob = Mathf.Sin(t * _bobSpeed * 2f) * _bobAmount;

            if (_player != null && _playerStart != null)
            {
                Vector3 p = Vector3.Lerp(_playerStart.position, _pEnd, pe);
                p.y += bob * pk;
                _player.position = p;
                float yaw = Mathf.LerpAngle(_playerStartYaw, _pEndYaw, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, pe)));
                _player.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (_playerAnim != null)
                    _playerAnim.SetFloat(SwimBlend, Mathf.Lerp(_travelBlend, _restBlend, Mathf.InverseLerp(0.6f, 1f, pe)));
            }
            if (_dugong != null && _dugongStart != null)
            {
                Vector3 d = Vector3.Lerp(_dugongStart.position, _dEnd, de);
                d.y += Mathf.Sin(t * _bobSpeed * 1.6f + 1f) * _bobAmount * 1.5f * dk;
                _dugong.position = d;
                // keep the swimming heading while travelling, then turn once in position
                float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - TurnStart) / Mathf.Max(0.01f, _dugongTurnDuration)));
                _dugong.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(_dTravelYaw, _dEndYaw, turn), 0f);
            }
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_player != null && _playerEnd != null) Apply(_t);

            if (_title != null) _title.Tick(_t - TitleStart);

            if (_menuUI != null)
            {
                float a = Mathf.Clamp01((_t - TitleStart - _menuAfterTitleStart) / Mathf.Max(0.01f, _menuFadeDuration));
                _menuUI.alpha = a;
                bool on = a >= 0.99f;
                _menuUI.interactable = on;
                _menuUI.blocksRaycasts = on;
            }

            if (_cameraRig != null)
            {
                Vector2 m = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height)) - new Vector2(0.5f, 0.5f);
                Vector3 off = new Vector3(Mathf.Sin(_t * 0.15f) * _cameraDrift + m.x * _parallax, Mathf.Sin(_t * 0.11f) * _cameraDrift * 0.5f + m.y * _parallax * 0.5f, 0f);
                _cameraRig.position = Vector3.Lerp(_cameraRig.position, _camBase + off, Time.deltaTime * 2f);
            }
        }
    }
}
