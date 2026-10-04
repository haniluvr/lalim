using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// The context action prompt: the chat or interact icon plus a verb ("Talk", "Pickup") whenever
    /// the player is targeting an interactable. Tapping it fires the same interact path as the E key.
    ///
    /// By default the prompt floats in the world beside the target (WorldInteractPrompt, spawned
    /// from here with this button's sprites and font) and this HUD button stays hidden. Untick
    /// _floatInWorld to go back to the fixed HUD button.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractActionButton : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Sprite _interactSprite;
        [SerializeField] private Sprite _chatSprite;
        [SerializeField, Min(0.01f)] private float _fadeSpeed = 8f;
        [Tooltip("Show the prompt as a floating 3D icon beside the target instead of this HUD button.")]
        [SerializeField] private bool _floatInWorld = true;

        private float _alpha;

        private void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_button != null) _button.onClick.AddListener(OnPressed);
            SetAlpha(0f);
            if (_floatInWorld) WorldInteractPrompt.Ensure(_interactSprite, _chatSprite, _label != null ? _label.font : null);
        }

        private void OnEnable() => PlayerController.RegisterHudPrompt(true);
        private void OnDisable() => PlayerController.RegisterHudPrompt(false);

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            IInteractable target = player != null ? player.CurrentInteractable : null;
            bool visible = target != null && !(_floatInWorld && WorldInteractPrompt.Instance != null);

            if (visible)
            {
                InteractionIcon icon = target is IInteractionIcon withIcon ? withIcon.Icon : InteractionIcon.Interact;
                Sprite sprite = icon == InteractionIcon.Chat ? _chatSprite : _interactSprite;
                if (_icon != null && sprite != null && _icon.sprite != sprite) _icon.sprite = sprite;
                if (_label != null) _label.text = player.CurrentPromptText;
            }

            float targetAlpha = visible ? 1f : 0f;
            if (!Mathf.Approximately(_alpha, targetAlpha))
            {
                SetAlpha(Mathf.MoveTowards(_alpha, targetAlpha, Time.unscaledDeltaTime * _fadeSpeed));
            }
        }

        private void SetAlpha(float a)
        {
            _alpha = a;
            if (_group == null) return;
            _group.alpha = a;
            _group.interactable = a > 0.5f;
            _group.blocksRaycasts = a > 0.5f;
        }

        // Calls the target directly: a UI click can land after PlayerController.Update, and
        // InputManager clears InteractPressed in LateUpdate, so the flag could be lost.
        private void OnPressed()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || (InputManager.Instance != null && !InputManager.Instance.InputEnabled)) return;
            player.CurrentInteractable?.Interact();
        }
    }
}
