using UnityEngine;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.Abilities;

namespace Lalim.UI
{
    /// <summary>
    /// The thin line under the HP bar. Only visible while Riptide is active, draining as its
    /// duration runs out — so the Surge/Riptide button never needs a duration timer of its own.
    /// </summary>
    public class RiptideTimerUI : MonoBehaviour
    {
        [SerializeField] private Image _fill;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 6f;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            if (_fill != null) _fill.color = HudPalette.Riptide;
        }

        private void Update()
        {
            AbilityBase riptide = AbilityController.Instance != null ? AbilityController.Instance.Get(AbilityId.Riptide) : null;
            bool active = riptide != null && riptide.IsActive;

            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, active ? 1f : 0f, _fadeSpeed * Time.unscaledDeltaTime);
            if (active && _fill != null) _fill.fillAmount = riptide.DurationNormalized;
        }
    }
}
