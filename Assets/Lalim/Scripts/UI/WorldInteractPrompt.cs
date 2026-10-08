using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Lalim.Mechanics;
using InputSystemPointer = UnityEngine.InputSystem;

namespace Lalim.UI
{
    /// <summary>
    /// The floating 3D interaction prompt: the chat or interact icon with its verb ("Talk",
    /// "Interact", "Pickup") centred underneath, hovering beside whatever the player is targeting.
    /// It always faces the camera square-on, whatever angle the diver or camera is at, and draws
    /// over scenery so a statue never hides its own prompt.
    ///
    /// Tapping (or clicking) the icon interacts, same as the E key. Hidden whenever the game HUD
    /// is (dialogue, menus, full-screen minigames).
    ///
    /// Created at runtime by InteractActionButton, so every scene with the GameHUD gets it.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldInteractPrompt : MonoBehaviour
    {
        public static WorldInteractPrompt Instance { get; private set; }

        [Header("Look")]
        [SerializeField] private Sprite _interactSprite;
        [SerializeField] private Sprite _chatSprite;
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("Icon height in metres at the reference distance.")]
        [SerializeField, Min(0.1f)] private float _iconHeight = 0.7f;
        [Tooltip("Camera distance at which the icon is exactly _iconHeight tall. Nearer or farther, " +
                 "it scales part-way so it stays readable without looking glued to the screen.")]
        [SerializeField, Min(0.5f)] private float _referenceDistance = 6f;
        [SerializeField, Range(0f, 1f)] private float _keepScreenSize = 0.7f;
        [Tooltip("HDR brightness so the prompt still reads through the murky polluted-water grade.")]
        [SerializeField, Min(1f)] private float _glow = 1.6f;

        [Header("Placement")]
        [Tooltip("Gap between the top of a small target and the bottom of the label.")]
        [SerializeField, Min(0f)] private float _gapAboveTarget = 0.35f;
        [Tooltip("Targets taller than this (statues) get the prompt at the diver's eye level instead of on top.")]
        [SerializeField, Min(0.5f)] private float _tallTargetHeight = 4f;
        [SerializeField, Min(0f)] private float _bobHeight = 0.06f;
        [SerializeField, Min(0f)] private float _bobSpeed = 2f;
        [SerializeField, Min(0.01f)] private float _fadeSpeed = 7f;
        [SerializeField, Min(0.01f)] private float _followSharpness = 12f;

        [Header("Tap")]
        [SerializeField, Min(0f)] private float _tapSlopPixels = 30f;
        [SerializeField, Min(0f)] private float _tapMaxSeconds = 0.6f;

        // Canvas units: 1 unit = _iconHeight / IconUnits metres.
        private const float IconUnits = 150f;
        private const float LabelUnits = 56f;

        private RectTransform _root;
        private CanvasGroup _group;
        private Image _icon;
        private TextMeshProUGUI _label;
        private RectTransform _labelRect;
        private RectTransform _iconRect;
        private string _rawPrompt;

        private IInteractable _target;
        private Component _targetComponent;
        private Vector3 _anchor;
        private float _alpha;
        private float _pop = 1f;
        private bool _placed;

        // Tap tracking (pointer id → press info).
        private readonly Dictionary<int, (Vector2 pos, float time)> _presses = new Dictionary<int, (Vector2, float)>();
        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        public static WorldInteractPrompt Ensure(Sprite interact, Sprite chat, TMP_FontAsset font)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[WorldInteractPrompt]");
            var prompt = go.AddComponent<WorldInteractPrompt>();
            prompt._interactSprite = interact;
            prompt._chatSprite = chat;
            prompt._font = font;
            prompt.Build();
            return prompt;
        }

        /// <summary>True while the prompt is on screen (the HUD action button stays hidden then).</summary>
        public bool IsShowing => _alpha > 0.01f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => PlayerController.RegisterHudPrompt(true);
        private void OnDisable() => PlayerController.RegisterHudPrompt(false);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ───────────────────────── Label ─────────────────────────

        private void SetPrompt(string raw)
        {
            if (raw == _rawPrompt) return;
            _rawPrompt = raw;
            string text = FormatPrompt(raw);
            _label.text = text;
            // Two lines: the label grows upward and lifts the icon with it.
            int lines = text.IndexOf('\n') >= 0 ? 2 : 1;
            _labelRect.sizeDelta = new Vector2(0f, LabelUnits * lines);
            _root.sizeDelta = new Vector2(_root.sizeDelta.x, IconUnits + LabelUnits * lines + 10f);
        }

        /// <summary>
        /// At most two lines. "Tap to use Water Sampling Kit" → "Tap to use" / "Water Sampling Kit";
        /// other long prompts break at the space that best balances the two lines; short ones stay on one.
        /// </summary>
        public static string FormatPrompt(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int newline = text.IndexOf('\n');
            if (newline >= 0)
            {
                int second = text.IndexOf('\n', newline + 1);
                return second < 0 ? text : text.Substring(0, second).Trim() + " " + text.Substring(second + 1).Replace('\n', ' ').Trim();
            }

            string[] words = text.Split(' ');
            if (words.Length > 3 && words[0] == "Tap" && words[1] == "to")
                return string.Join(" ", words, 0, 3) + "\n" + string.Join(" ", words, 3, words.Length - 3);
            if (text.Length <= 18 || words.Length < 2) return text;

            int best = -1, bestLongest = int.MaxValue;
            for (int i = text.IndexOf(' '); i >= 0; i = text.IndexOf(' ', i + 1))
            {
                int longest = Mathf.Max(i, text.Length - i - 1);
                if (longest < bestLongest) { bestLongest = longest; best = i; }
            }
            return text.Substring(0, best) + "\n" + text.Substring(best + 1);
        }

        // ───────────────────────── Build ─────────────────────────

        private void Build()
        {
            if (_root != null) return;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            _root = (RectTransform)canvasGo.transform;
            _root.sizeDelta = new Vector2(420f, IconUnits + LabelUnits + 10f);
            _root.pivot = new Vector2(0.5f, 0f);   // bottom of the label sits on the anchor

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;
            _group = canvasGo.GetComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;   // taps are hit-tested by hand (see HandleTaps)

            // Icon on top.
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(_root, false);
            _iconRect = (RectTransform)iconGo.transform;
            _iconRect.anchorMin = _iconRect.anchorMax = new Vector2(0.5f, 1f);
            _iconRect.pivot = new Vector2(0.5f, 1f);
            _iconRect.sizeDelta = new Vector2(IconUnits, IconUnits);
            _iconRect.anchoredPosition = Vector2.zero;
            _icon = iconGo.GetComponent<Image>();
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;
            _icon.sprite = _interactSprite;
            _icon.material = IconMaterial();

            // Verb centred underneath.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(_root, false);
            var labelRect = (RectTransform)labelGo.transform;
            _labelRect = labelRect;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, LabelUnits);
            labelRect.anchoredPosition = Vector2.zero;
            _label = labelGo.GetComponent<TextMeshProUGUI>();
            if (_font != null) _label.font = _font;
            _label.fontSize = 40f;
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 22f;
            _label.fontSizeMax = 40f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.fontMaterial = TextMaterial(_label.fontSharedMaterial);

            SetAlpha(0f);
        }

        // World-space canvases depth-test against the scene, so the prompt would slip behind the
        // diver or a statue. Both materials skip the depth test. The templates live in Resources
        // so their shaders ship in builds.
        private Material IconMaterial()
        {
            var template = Resources.Load<Material>("WorldPrompt/WorldPromptIcon");
            Shader shader = template != null ? template.shader : Shader.Find("Lalim/UI/AlwaysOnTop");
            var m = new Material(shader != null ? shader : Shader.Find("UI/Default")) { name = "WorldPromptIcon (Instance)" };
            m.SetColor("_Color", new Color(_glow, _glow, _glow, 1f));
            return m;
        }

        private Material TextMaterial(Material fontMaterial)
        {
            var template = Resources.Load<Material>("WorldPrompt/WorldPromptText");
            Shader shader = template != null ? template.shader : Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            var m = new Material(fontMaterial) { name = "WorldPromptText (Instance)" };
            if (shader != null) m.shader = shader;
            m.SetColor(ShaderUtilities.ID_FaceColor, new Color(_glow, _glow, _glow, 1f));
            m.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(0, 20, 40, 230));
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            return m;
        }

        // ───────────────────────── Per frame ─────────────────────────

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            PlayerController player = PlayerController.Instance;
            IInteractable target = player != null ? player.CurrentInteractable : null;
            bool hudUp = HUDController.Instance == null || HUDController.Instance.IsShowing;
            bool blocked = !hudUp
                           || (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
                           || (ChallengeManager.Instance != null && ChallengeManager.Instance.IsFirstPersonChallengeActive)
                           || (InputManager.Instance != null && !InputManager.Instance.InputEnabled);
            bool want = cam != null && target != null && !blocked;

            if (want && !ReferenceEquals(target, _target))
            {
                // New target: jump there and pop in, rather than sliding across the reef.
                _target = target;
                _targetComponent = target as Component;
                _placed = false;
                _pop = 0f;
                _presses.Clear();
                Sprite sprite = target is IInteractionIcon withIcon && withIcon.Icon == InteractionIcon.Chat ? _chatSprite : _interactSprite;
                if (sprite != null) _icon.sprite = sprite;
            }
            if (want) SetPrompt(player.CurrentPromptText);

            float targetAlpha = want ? 1f : 0f;
            if (!Mathf.Approximately(_alpha, targetAlpha))
                SetAlpha(Mathf.MoveTowards(_alpha, targetAlpha, Lalim.Core.PauseClock.DeltaTime * _fadeSpeed));
            if (_alpha <= 0f)
            {
                if (!want) _target = null;
                return;
            }
            if (cam == null || _targetComponent == null) return;

            if (want) _anchor = AnchorFor(_target, _targetComponent, player);
            Vector3 pos = _anchor + Vector3.up * (Mathf.Sin(Time.time * _bobSpeed) * _bobHeight);
            transform.position = _placed ? Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-_followSharpness * Lalim.Core.PauseClock.DeltaTime)) : pos;
            _placed = true;

            // Face the camera square-on: same orientation as the camera, so it never skews.
            transform.rotation = cam.transform.rotation;

            float distance = Vector3.Distance(cam.transform.position, transform.position);
            float distanceScale = Mathf.Lerp(1f, distance / _referenceDistance, _keepScreenSize);
            _pop = Mathf.MoveTowards(_pop, 1f, Lalim.Core.PauseClock.DeltaTime * 4f);
            float metresPerUnit = _iconHeight / IconUnits * Mathf.Clamp(distanceScale, 0.45f, 2.5f) * EaseOutBack(_pop);
            transform.localScale = Vector3.one * metresPerUnit;

            if (want) HandleTaps(cam);
        }

        private Vector3 AnchorFor(IInteractable target, Component c, PlayerController player)
        {
            if (target is IInteractionAnchor anchored) return anchored.PromptAnchor;

            Bounds b = BoundsOf(c);
            if (b.size.y <= _tallTargetHeight)
                return new Vector3(b.center.x, b.max.y + _gapAboveTarget, b.center.z);

            // Statue-sized: hover just in front of it, toward the diver, at their eye level.
            Vector3 eye = player.transform.position + Vector3.up * 1.6f;
            Vector3 onSurface = b.ClosestPoint(eye);
            Vector3 toPlayer = eye - onSurface;
            toPlayer.y = 0f;
            Vector3 outward = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector3.zero;
            float y = Mathf.Clamp(eye.y, b.min.y + 1f, b.max.y);
            return new Vector3(onSurface.x, y, onSurface.z) + outward * 0.8f;
        }

        private static Bounds BoundsOf(Component c)
        {
            bool has = false;
            Bounds b = new Bounds(c.transform.position, Vector3.zero);
            foreach (Renderer r in c.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (has) return b;
            foreach (Collider col in c.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled) continue;
                if (!has) { b = col.bounds; has = true; } else b.Encapsulate(col.bounds);
            }
            return b;
        }

        // ───────────────────────── Tap to interact ─────────────────────────

        private void HandleTaps(Camera cam)
        {
            var touchscreen = InputSystemPointer.Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    Vector2 p = touch.position.ReadValue();
                    if (touch.press.wasPressedThisFrame) OnPress(id, p, cam);
                    else if (touch.press.wasReleasedThisFrame) OnRelease(id, p, cam);
                }
            }

            var mouse = InputSystemPointer.Mouse.current;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame) OnPress(-1, p, cam);
                else if (mouse.leftButton.wasReleasedThisFrame) OnRelease(-1, p, cam);
            }
        }

        private void OnPress(int id, Vector2 screen, Camera cam)
        {
            if (_alpha < 0.6f || !HitsIcon(screen, cam) || OverHudControl(screen)) return;
            _presses[id] = (screen, Time.unscaledTime);
        }

        private void OnRelease(int id, Vector2 screen, Camera cam)
        {
            if (!_presses.TryGetValue(id, out var press)) return;
            _presses.Remove(id);
            bool isTap = (screen - press.pos).sqrMagnitude <= _tapSlopPixels * _tapSlopPixels
                         && Time.unscaledTime - press.time <= _tapMaxSeconds;
            if (!isTap || !HitsIcon(screen, cam)) return;

            PlayerController player = PlayerController.Instance;
            if (player == null || (InputManager.Instance != null && !InputManager.Instance.InputEnabled)) return;
            player.CurrentInteractable?.Interact();
        }

        // The whole prompt (icon + label) is the hit area, padded a little for fingers.
        private bool HitsIcon(Vector2 screen, Camera cam)
        {
            var corners = new Vector3[4];
            _root.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 w in corners)
            {
                Vector3 s = cam.WorldToScreenPoint(w);
                if (s.z <= 0f) return false;
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
            float pad = Screen.dpi > 0f ? Screen.dpi * 0.08f : 12f;
            return screen.x >= min.x - pad && screen.x <= max.x + pad && screen.y >= min.y - pad && screen.y <= max.y + pad;
        }

        // A real HUD control (joystick, ability button) under the finger wins over the prompt.
        private bool OverHudControl(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, _uiHits);
            foreach (RaycastResult hit in _uiHits)
            {
                GameObject go = hit.gameObject;
                if (go.GetComponentInParent<Selectable>() != null || go.GetComponentInParent<VirtualJoystick>() != null) return true;
            }
            return false;
        }

        // ───────────────────────── Helpers ─────────────────────────

        private void SetAlpha(float a)
        {
            _alpha = a;
            if (_group != null) _group.alpha = a;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
