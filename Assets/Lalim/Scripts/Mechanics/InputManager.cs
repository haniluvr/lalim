using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Centralized input manager that wraps Unity Input System for touch controls.
    /// Provides MoveInput (from joystick), LookDelta (from swipe), and action buttons.
    /// Multiple systems can read input without conflict.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputManager : MonoBehaviour
    {
        private static InputManager _instance;
        public static InputManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<InputManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[InputManager]");
                        _instance = go.AddComponent<InputManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        // ─────────────────────────────────────────────
        // Input Values (read by PlayerController, CameraController, etc.)
        // ─────────────────────────────────────────────

        /// <summary>Normalized movement direction from virtual joystick (-1 to 1).</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>Vertical swim input (-1 to 1): Space = +1 (ascend), Control/C = -1 (descend).</summary>
        public float VerticalMoveInput { get; private set; }

        /// <summary>Look delta from swipe gesture (raw pixel delta per frame).</summary>
        public Vector2 LookDelta { get; private set; }

        // Fix: the free LeftShift sprint flag is gone — Shift is now the
        // tap(Surge)/hold(Riptide) input, exposed below via SurgeRiptideKeyDown/Held/Up.

        /// <summary>Whether the Surge/Riptide key (LeftShift) is currently held down.</summary>
        public bool SurgeRiptideKeyHeld { get; private set; }
        /// <summary>True on the frame the Surge/Riptide key was pressed.</summary>
        public bool SurgeRiptideKeyDown { get; private set; }
        /// <summary>True on the frame the Surge/Riptide key was released.</summary>
        public bool SurgeRiptideKeyUp { get; private set; }

        /// <summary>True on the frame the Tidebond key (F) was pressed.</summary>
        public bool TidebondPressed { get; private set; }
        /// <summary>True on the frame the Seagrass Mend key (R) was pressed.</summary>
        public bool SeagrassMendPressed { get; private set; }
        /// <summary>True on the frame the Bubble Barrier key (G) was pressed.</summary>
        public bool BubbleBarrierPressed { get; private set; }

        /// <summary>Whether free look is active (holding Mouse Button 4 or Alt).</summary>
        public bool IsFreeLooking { get; private set; }

        /// <summary>Whether the SpiritSense button was pressed this frame.</summary>
        public bool SpiritSensePressed { get; private set; }

        /// <summary>Whether the interact button was pressed this frame.</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>Whether the interact button is currently held down (for hold-to-complete mechanics).</summary>
        public bool InteractHeld { get; private set; }

        /// <summary>Whether the inventory button was pressed this frame.</summary>
        public bool InventoryPressed { get; private set; }

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

        [Header("Gamepad")]
        [Tooltip("Right-stick camera speed at full tilt, in degrees per second.")]
        [SerializeField] private float _gamepadLookSpeed = 180f;
        [Tooltip("Stick deflection ignored around the centre (0–1).")]
        [SerializeField, Range(0f, 0.5f)] private float _gamepadDeadZone = 0.15f;

        // ─────────────────────────────────────────────
        // Internal State
        // ─────────────────────────────────────────────

        private bool _inputEnabled = true;

        /// <summary>False while dialogue, cutscenes or menus have taken input away.</summary>
        public bool InputEnabled => _inputEnabled;
        private bool _touchSurgeHeld;
        private Vector2 _lastMousePosition;
        private bool _isRmbDragging;
        private bool _isLmbDragging;
        private Vector2 _lmbStartPosition;
        private bool _lmbDragActivated; // Only true once drag exceeds threshold
        private const float LMB_DRAG_THRESHOLD = 5f; // Pixels before LMB drag activates
        private bool _wasKeyboardMoving;
        private bool _wasKeyboardVertical;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            var inst = Instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
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

            // Allow keyboard/mouse fallback even on mobile platforms for easier testing in the Unity Simulator
            // if (Application.isMobilePlatform) return;

            // --- Always-Active UI Shortcuts (Available even when movement/gameplay is disabled) ---
            bool iDown = Input.GetKeyDown(KeyCode.I);
            bool escDown = Input.GetKeyDown(KeyCode.Escape);

            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame) iDown = true;
                if (Keyboard.current.escapeKey.wasPressedThisFrame) escDown = true;
            }

            if (iDown)
            {
                TriggerInventory();
            }
            if (escDown)
            {
                TriggerPause();
            }

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

            // --- Gamepad left stick (movement), used when no movement keys are held ---
            Gamepad pad = Gamepad.current;
            if (keyboardMove.sqrMagnitude <= 0f && pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.magnitude > _gamepadDeadZone) keyboardMove = Vector2.ClampMagnitude(stick, 1f);
            }

            // --- Gamepad right stick (look): degrees per second, bypasses the pixel dead zone ---
            if (pad != null)
            {
                Vector2 look = pad.rightStick.ReadValue();
                if (look.magnitude > _gamepadDeadZone)
                    LookDelta += look * (_gamepadLookSpeed / GameConstants.Player.LOOK_SENSITIVITY) * Time.deltaTime;
            }

            if (keyboardMove.sqrMagnitude > 0f)
            {
                MoveInput = keyboardMove.sqrMagnitude > 1f ? keyboardMove.normalized : keyboardMove;
                _wasKeyboardMoving = true;
            }
            else if (_wasKeyboardMoving || (!HasTouchInput && MoveInput.magnitude > 0 && !Application.isMobilePlatform))
            {
                // Only reset on desktop when no keyboard movement keys are pressed AND no touch/joystick input is active
                MoveInput = Vector2.zero;
                _wasKeyboardMoving = false;
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
            if (verticalSwim != 0f)
            {
                VerticalMoveInput = Mathf.Clamp(verticalSwim, -1f, 1f);
                _wasKeyboardVertical = true;
            }
            else if (_wasKeyboardVertical || (!HasTouchInput && VerticalMoveInput != 0f && !Application.isMobilePlatform))
            {
                VerticalMoveInput = 0f;
                _wasKeyboardVertical = false;
            }

            // --- Surge / Riptide key (LeftShift): tap vs hold is resolved by AbilityController ---
            // Keyboard and the touch button are OR'd so polling Shift never overrides a held touch.
            bool shiftHeldNow = _touchSurgeHeld || Input.GetKey(KeyCode.LeftShift) || (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
            SurgeRiptideKeyDown = shiftHeldNow && !SurgeRiptideKeyHeld;
            SurgeRiptideKeyUp = !shiftHeldNow && SurgeRiptideKeyHeld;
            SurgeRiptideKeyHeld = shiftHeldNow;

            // --- Free Look (Mouse Button 4 / KeyCode.Mouse3 / Alt) ---
            bool mouse4Held = Input.GetMouseButton(3) || Input.GetMouseButton(4) ||
                              Input.GetKey(KeyCode.Mouse3) || Input.GetKey(KeyCode.Mouse4) ||
                              Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (Mouse.current != null && (Mouse.current.backButton.isPressed || Mouse.current.forwardButton.isPressed))
            {
                mouse4Held = true;
            }
            IsFreeLooking = mouse4Held;

            // --- Camera Look: Free Look (Mouse 4), LMB Hold+Drag (Mac-friendly), or RMB Drag ---
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
                // --- LMB Hold+Drag (primary method for Mac) ---
                // Only blocks if the click is on an actual interactive UI control
                // (not transparent overlay panels like TouchLookHandler)
                if (Input.GetMouseButtonDown(0))
                {
                    bool overInteractiveUI = false;
                    if (EventSystem.current != null)
                    {
                        var pointerData = new PointerEventData(EventSystem.current)
                        {
                            position = Input.mousePosition
                        };
                        var raycastResults = new System.Collections.Generic.List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointerData, raycastResults);

                        foreach (var result in raycastResults)
                        {
                            // Check if hit object has an interactive UI component
                            var go = result.gameObject;
                            if (go.GetComponent<UnityEngine.UI.Button>() != null ||
                                go.GetComponent<UnityEngine.UI.Toggle>() != null ||
                                go.GetComponent<UnityEngine.UI.Slider>() != null ||
                                go.GetComponent<UnityEngine.UI.Scrollbar>() != null ||
                                go.GetComponent<UnityEngine.UI.InputField>() != null ||
                                go.GetComponent<VirtualJoystick>() != null)
                            {
                                overInteractiveUI = true;
                                break;
                            }
                        }
                    }

                    if (!overInteractiveUI)
                    {
                        _isLmbDragging = true;
                        _lmbDragActivated = false;
                        _lmbStartPosition = Input.mousePosition;
                        _lastMousePosition = Input.mousePosition;
                    }
                }

                if (Input.GetMouseButton(0) && _isLmbDragging)
                {
                    Vector2 currentMousePos = Input.mousePosition;

                    // Activate drag only after exceeding the threshold to avoid
                    // stealing regular interaction clicks
                    if (!_lmbDragActivated)
                    {
                        float dragDistance = Vector2.Distance(currentMousePos, _lmbStartPosition);
                        if (dragDistance >= LMB_DRAG_THRESHOLD)
                        {
                            _lmbDragActivated = true;
                            _lastMousePosition = currentMousePos; // Reset so first delta isn't a jump
                        }
                    }

                    if (_lmbDragActivated)
                    {
                        Vector2 mouseDelta = currentMousePos - _lastMousePosition;
                        _lastMousePosition = currentMousePos;
                        SetLookDelta(mouseDelta);
                    }
                }

                if (Input.GetMouseButtonUp(0))
                {
                    if (_isLmbDragging && _lmbDragActivated)
                    {
                        SetLookDelta(Vector2.zero);
                    }
                    _isLmbDragging = false;
                    _lmbDragActivated = false;
                }

                // --- RMB Drag (still works as alternative) ---
                if (Input.GetMouseButtonDown(1))
                {
                    _isRmbDragging = true;
                    _lastMousePosition = Input.mousePosition;
                }
                else if (Input.GetMouseButton(1) && _isRmbDragging)
                {
                    Vector2 currentMousePos = Input.mousePosition;
                    Vector2 mouseDelta = currentMousePos - _lastMousePosition;
                    _lastMousePosition = currentMousePos;
                    SetLookDelta(mouseDelta);
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    _isRmbDragging = false;
                    SetLookDelta(Vector2.zero);
                }
            }

            // --- Gameplay Keyboard Triggers ---
            bool eHeld = Input.GetKey(KeyCode.E);
            bool eDown = Input.GetKeyDown(KeyCode.E);
            bool qDown = Input.GetKeyDown(KeyCode.Q);

            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.isPressed) eHeld = true;
                if (Keyboard.current.eKey.wasPressedThisFrame) eDown = true;
                if (Keyboard.current.qKey.wasPressedThisFrame) qDown = true;
            }

            InteractHeld = eHeld;

            if (eDown)
            {
                TriggerInteract();
            }
            if (qDown)
            {
                TriggerSpiritSense();
            }

            // --- New ability keys: Tidebond (F), Seagrass Mend (R), Bubble Barrier (G) ---
            bool fDown = Input.GetKeyDown(KeyCode.F);
            bool rDown = Input.GetKeyDown(KeyCode.R);
            bool gDown = Input.GetKeyDown(KeyCode.G);

            if (Keyboard.current != null)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame) fDown = true;
                if (Keyboard.current.rKey.wasPressedThisFrame) rDown = true;
                if (Keyboard.current.gKey.wasPressedThisFrame) gDown = true;
            }

            if (fDown) TriggerTidebond();
            if (rDown) TriggerSeagrassMend();
            if (gDown) TriggerBubbleBarrier();
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
            // Instead of resetting triggers here where they might be missed by early Updates next frame,
            // we will let components consume them. 
            // However, to keep it safe for existing code, we reset them. 
            // SpiritSense logic will be updated directly to bypass this flag.
            SpiritSensePressed = false;
            InteractPressed = false;
            InventoryPressed = false;
            PausePressed = false;
            TidebondPressed = false;
            SeagrassMendPressed = false;
            BubbleBarrierPressed = false;
            SurgeRiptideKeyDown = false;
            SurgeRiptideKeyUp = false;
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

        /// <summary>Called by the Tidebond button UI.</summary>
        public void TriggerTidebond()
        {
            if (_inputEnabled) TidebondPressed = true;
        }

        /// <summary>Called by the Seagrass Mend button UI.</summary>
        public void TriggerSeagrassMend()
        {
            if (_inputEnabled) SeagrassMendPressed = true;
        }

        /// <summary>Called by the Bubble Barrier button UI.</summary>
        public void TriggerBubbleBarrier()
        {
            if (_inputEnabled) BubbleBarrierPressed = true;
        }

        /// <summary>Called by the Surge/Riptide button UI on pointer down (touch equivalent of Shift-down).</summary>
        public void TriggerSurgeRiptideDown()
        {
            if (!_inputEnabled) return;
            _touchSurgeHeld = true;
        }

        /// <summary>Called by the Surge/Riptide button UI on pointer up (touch equivalent of Shift-up).</summary>
        public void TriggerSurgeRiptideUp()
        {
            _touchSurgeHeld = false;
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
        /// Called when the inventory key is pressed or inventory UI button is tapped.
        /// </summary>
        public void TriggerInventory()
        {
            InventoryPressed = true;
        }

        /// <summary>
        /// Called by pause button UI.
        /// </summary>
        public void TriggerPause()
        {
            // Pause is always available (even when other input is disabled)
            PausePressed = true;

            // Escape / Android back: the pause modal steps back out of sub-panels or toggles pause.
            if (PauseModalUI.Instance != null) PauseModalUI.Instance.OnPauseKey();
            else if (GameManager.Instance != null)
            {
                EventBus.Publish(new PauseRequestedEvent { IsPaused = GameManager.Instance.CurrentState != GameState.Paused });
            }
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
                SurgeRiptideKeyHeld = false;
                _touchSurgeHeld = false;
                IsFreeLooking = false;
                _isRmbDragging = false;
                _isLmbDragging = false;
                _lmbDragActivated = false;
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
