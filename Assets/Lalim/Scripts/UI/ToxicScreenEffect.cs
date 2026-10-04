using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// Green-tinted vignette while the player is in toxic water. The URP Volume's weight follows
    /// the severity (Light / Heavy), and is softened while Bubble Barrier is blocking the damage.
    /// </summary>
    public class ToxicScreenEffect : MonoBehaviour
    {
        [Tooltip("Optional global Volume. If empty, one is created at runtime.")]
        [SerializeField] private Volume _volume;
        [SerializeField] private Color _tint = new Color(0.55f, 0.85f, 0.35f);
        [SerializeField, Range(0f, 1f)] private float _vignetteIntensity = 0.45f;
        [SerializeField] private float _lightWeight = 0.5f;
        [SerializeField] private float _heavyWeight = 1f;
        [SerializeField] private float _shieldedWeightScale = 0.3f;
        [SerializeField] private float _fadeSpeed = 2f;

        private ToxicSeverity _severity;
        private VolumeProfile _runtimeProfile;

        private void Awake() => EnsureVolume();

        private void OnDestroy()
        {
            if (_runtimeProfile != null) Destroy(_runtimeProfile);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ToxicExposureChangedEvent>(OnToxicChanged);
            EventBus.Subscribe<PlayerRespawnedEvent>(OnRespawned);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ToxicExposureChangedEvent>(OnToxicChanged);
            EventBus.Unsubscribe<PlayerRespawnedEvent>(OnRespawned);
        }

        private void OnToxicChanged(ToxicExposureChangedEvent evt) => _severity = evt.Severity;
        private void OnRespawned(PlayerRespawnedEvent evt) => _severity = ToxicSeverity.None;

        private void Update()
        {
            if (_volume == null) return;

            float target = _severity == ToxicSeverity.Heavy ? _heavyWeight
                         : _severity == ToxicSeverity.Light ? _lightWeight : 0f;

            BubbleBarrierAbility barrier = PlayerHealth.Instance != null ? PlayerHealth.Instance.Barrier : null;
            if (barrier != null && barrier.IsActive) target *= _shieldedWeightScale;

            _volume.weight = Mathf.MoveTowards(_volume.weight, target, _fadeSpeed * Time.deltaTime);
        }

        private void EnsureVolume()
        {
            if (_volume == null)
            {
                GameObject go = new GameObject("ToxicVolume (runtime)");
                go.transform.SetParent(transform, false);
                _volume = go.AddComponent<Volume>();
                _volume.isGlobal = true;
                _volume.priority = 55f;

                _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                var vig = _runtimeProfile.Add<Vignette>(true);
                vig.color.Override(_tint);
                vig.intensity.Override(_vignetteIntensity);
                vig.smoothness.Override(0.6f);
                var color = _runtimeProfile.Add<ColorAdjustments>(true);
                color.colorFilter.Override(Color.Lerp(Color.white, _tint, 0.35f));
                _volume.profile = _runtimeProfile;
            }
            _volume.weight = 0f;
        }
    }
}
