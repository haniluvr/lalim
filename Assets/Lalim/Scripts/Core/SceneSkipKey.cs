using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Lalim.Core
{
    /// <summary>
    /// Press P to skip this scene (name input, tutorial) and go on to the next one in the flow,
    /// the same way finishing it would. Ignored while the player is typing in a text field.
    /// </summary>
    public class SceneSkipKey : MonoBehaviour
    {
        [SerializeField] private string _nextScene = GameConstants.Scenes.TELEPORT;
        [SerializeField] private bool _showLoadingScreen = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key _key = Key.P;
#endif
        [SerializeField] private KeyCode _legacyKey = KeyCode.P;

        private bool _skipping;

        private void Update()
        {
            if (_skipping || !Pressed() || IsTyping()) return;
            _skipping = true;
            Debug.Log($"[SceneSkipKey] Skipping to {_nextScene}.");
            SceneLoader.Ensure().LoadScene(_nextScene, _showLoadingScreen);
        }

        private bool Pressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) return Keyboard.current[_key].wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(_legacyKey);
#else
            return false;
#endif
        }

        private static bool IsTyping()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var tmp = selected.GetComponent<TMPro.TMP_InputField>();
            if (tmp != null && tmp.isFocused) return true;
            var legacy = selected.GetComponent<UnityEngine.UI.InputField>();
            return legacy != null && legacy.isFocused;
        }

#if UNITY_EDITOR
        public void EditorSetup(string nextScene, bool showLoadingScreen)
        {
            _nextScene = nextScene;
            _showLoadingScreen = showLoadingScreen;
        }
#endif
    }
}
