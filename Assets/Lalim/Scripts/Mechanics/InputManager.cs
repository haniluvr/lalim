using UnityEngine;
using UnityEngine.InputSystem;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Centralized input manager that wraps Unity Input System for touch controls.
    /// Provides MoveInput (from joystick), LookDelta (from swipe), and action buttons.
    /// Multiple systems can read input without conflict.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        // ─────────────────────────────────────────────
        // Input Values (read by PlayerController, CameraController, etc.)
        // ─────────────────────────────────────────────

        /// <summary>Normalized movement direction from virtual joystick (-1 to 1).</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Vertical swim input (-1 to 1): Space = +1 (ascend), Control/C = -1 (descend).</summary>
        public float VerticalMoveInput { get; private set; }

        /// <summary>Look delta from swipe gesture (raw pixel delta per frame).</summary>
        public Vector2 LookDelta { get; private set; }

        /// <summary>Whether sprint / fast swim is held (LeftShift).</summary>
        public bool SwimSprintHeld { get; private set; }

        /// <summary>Whether free look is active (holding Mouse Button 4 or Alt).</summary>
        public bool IsFreeLooking { get; private set; }

        /// <summary>Whether the SpiritSense button was pressed this frame.</summary>
        public bool SpiritSensePressed { get; private set; }

        /// <summary>Whether the interact button was pressed this frame.</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>Whether the interact button is currently held down (for hold-to-complete mechanics).</summary>
        public bool InteractHeld { get; private set; }

        /// <summary>Whether the pause button was pressed this frame.</summary>
        public bool PausePressed { get; private set; }

        /// <summary>Position of last tap in screen coordinates.</summary>
        public Vector2 LastTapPosition { get; private set; }

        /// <summary>Whether any touch input is currently active.</summary>
        public bool HasTouchInput { get; private set; }

        // ─────────────────────────────────────────────
        // Settings
        // ─────────────────────────────────────────────

        [Header("Look Sensitivity")]
        [SerializeField] private float _lookSensitivity = 0.15f;
        [SerializeField] private float _lookDeadZone = 2.0f; // Minimum pixel movement to register

        // ─────────────────────────────────────────────
        // Internal State
        // ─────────────────────────────────────────────

        private bool _inputEnabled = true;
        private Vector2 _lastMousePosition;
        private bool _isRmbDragging;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─────────────────────────────────────────────
        // Editor / Desktop Fallback (WASD + Mouse)
        // ─────────────────────────────────────────────

        private void Update()
        {
            // Clear look delta at the start of each frame so stale values
            // don't carry over, but CameraController (in LateUpdate) can
            // still read whatever gets set during this Update.
            LookDelta = Vector2.zero;

            // Only use keyboard/mouse fallback on non-touch platforms (Editor, Standalone)
            if (Application.isMobilePlatform) return;
            if (!_inputEnabled) return;

            // --- WASD Movement ---
            Vector2 keyboardMove = Vector2.zero;
            if (Input.GetKey(KeyCode.W)) keyboardMove.y += 1f;
            if (Input.GetKey(KeyCode.S)) keyboardMove.y -= 1f;
            if (Input.GetKey(KeyCode.A)) keyboardMove.x -= 1f;
            if (Input.GetKey(KeyCode.D)) keyboardMove.x += 1f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) keyboardMove.y += 1f;
                if (Keyboard.current.sKey.isPressed) keyboardMove.y -= 1f;
                if (Keyboard.current.aKey.isPressed) keyboardMove.x -= 1f;
                if (Keyboard.current.dKey.isPressed) keyboardMove.x += 1f;
            }

            if (keyboardMove.sqrMagnitude > 0f)
            {
                MoveInput = keyboardMove.normalized;
            }
            else if (!HasTouchInput)
            {
                // Only clear if no touch joystick is active
                MoveInput = Vector2.zero;
            }

            // --- Vertical Swim (Space = Up, Control / C = Down) ---
            float verticalSwim = 0f;
            if (Input.GetKey(KeyCode.Space)) verticalSwim += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.C)) verticalSwim -= 1f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.isPressed) verticalSwim += 1f;
                if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed || Keyboard.current.cKey.isPressed) verticalSwim -= 1f;
            }
            VerticalMoveInput = Mathf.Clamp(verticalSwim, -1f, 1f);

            // --- Swim Sprint (LeftShift) ---
            SwimSprintHeld = Input.GetKey(KeyCode.LeftShift) || (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);

            // --- Free Look (Mouse Button 4 / KeyCode.Mouse3 / Alt) ---
            bool mouse4Held = Input.GetMouseButton(3) || Input.GetMouseButton(4) ||
                              Input.GetKey(KeyCode.Mouse3) || Input.GetKey(KeyCode.Mouse4) ||
                              Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (Mouse.current != null && (Mouse.current.backButton.isPressed || Mouse.current.forwardButton.isPressed))
            {
                mouse4Held = true;
            }
            IsFreeLooking = mouse4Held;

            // --- Camera Look: Free Look (Mouse 4) or Swipe Simulation (RMB Drag) ---
            if (mouse4Held)
            {
                // Free look mode: mouse movement directly controls orbit camera
                float mouseX = Input.GetAxis("Mouse X") * 10f;
                float mouseY = Input.GetAxis("Mouse Y") * 10f;
                if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
                {
                    LookDelta = new Vector2(mouseX, mouseY);
                }
            }
            else
            {
                // RMB Drag simulating mobile swipe
                if (Input.GetMouseButtonDown(1))
                {
                    _isRmbDragging = true;
                    _lastMousePosition = Input.mousePosition;
                    SetTouchActive(true);
                }
                else if (Input.GetMouseButton(1) && _isRmbDragging)
                {
                    Vector2 currentMousePos = Input.mousePosition;
                    Vector2 mouseDelta = currentMousePos - _lastMousePosition;
                    _lastMousePosition = currentMousePos;

                    // Pass delta through SetLookDelta so it uses the exact same sensitivity & deadzone as touch swipe
                    SetLookDelta(mouseDelta);
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    _isRmbDragging = false;
                    SetLookDelta(Vector2.zero);
                    SetTouchActive(false);
                }
            }

            // --- Keyboard Triggers ---
            InteractHeld = Input.GetKey(KeyCode.E);

            if (Input.GetKeyDown(KeyCode.E))
            {
                TriggerInteract();
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                TriggerSpiritSense();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TriggerPause();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void LateUpdate()
        {
            // Reset per-frame triggers
            SpiritSensePressed = false;
            InteractPressed = false;
            PausePressed = false;
        }

        // ─────────────────────────────────────────────
        // Input Setters (called by UI components)
        // ─────────────────────────────────────────────

        /// <summary>
        /// Called by VirtualJoystick to set movement input.
        /// </summary>
        public void SetMoveInput(Vector2 input)
        {
            MoveInput = _inputEnabled ? input : Vector2.zero;
        }

        /// <summary>
        /// Called by TouchLookHandler to set look delta.
        /// </summary>
        public void SetLookDelta(Vector2 delta)
        {
            if (!_inputEnabled)
            {
                LookDelta = Vector2.zero;
                return;
            }

            // Apply dead zone
            if (delta.magnitude < _lookDeadZone)
            {
                LookDelta = Vector2.zero;
                return;
            }

            LookDelta = delta * _lookSensitivity;
        }

        /// <summary>
        /// Called by SpiritSense button UI.
        /// </summary>
        public void TriggerSpiritSense()
        {
            if (_inputEnabled)
            {
                SpiritSensePressed = true;
            }
        }

        /// <summary>
        /// Called by interact button UI (single press).
        /// </summary>
        public void TriggerInteract()
        {
            if (_inputEnabled)
            {
                InteractPressed = true;
            }
        }

        /// <summary>
        /// Called by interact button UI when pressed down (hold start).
        /// </summary>
        public void TriggerInteractDown()
        {
            if (_inputEnabled)
            {
                InteractHeld = true;
                InteractPressed = true;
            }
        }

        /// <summary>
        /// Called by interact button UI when released (hold end).
        /// </summary>
        public void TriggerInteractUp()
        {
            InteractHeld = false;
        }

        /// <summary>
        /// Called by pause button UI.
        /// </summary>
        public void TriggerPause()
        {
            // Pause is always available (even when other input is disabled)
            PausePressed = true;
        }

        /// <summary>
        /// Set tap position from touch input.
        /// </summary>
        public void SetTapPosition(Vector2 screenPos)
        {
            LastTapPosition = screenPos;
        }

        /// <summary>
        /// Called by UI buttons or virtual controls to set vertical swim input (-1 to 1).
        /// </summary>
        public void SetVerticalMoveInput(float value)
        {
            VerticalMoveInput = _inputEnabled ? Mathf.Clamp(value, -1f, 1f) : 0f;
        }

        /// <summary>
        /// Set whether touch input is active.
        /// </summary>
        public void SetTouchActive(bool active)
        {
            HasTouchInput = active;
        }

        // ─────────────────────────────────────────────
        // Enable/Disable
        // ─────────────────────────────────────────────

        /// <summary>
        /// Enable or disable all gameplay input. UI input (pause) remains active.
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled;

            if (!enabled)
            {
                MoveInput = Vector2.zero;
                VerticalMoveInput = 0f;
                LookDelta = Vector2.zero;
                SwimSprintHeld = false;
                IsFreeLooking = false;
                _isRmbDragging = false;
            }
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            switch (evt.NewState)
            {
                case GameState.Exploring:
                case GameState.InChallenge:
                    SetInputEnabled(true);
                    break;

                case GameState.Paused:
                case GameState.Menu:
                case GameState.Results:
                    SetInputEnabled(false);
                    break;
            }
        }
    }
}
