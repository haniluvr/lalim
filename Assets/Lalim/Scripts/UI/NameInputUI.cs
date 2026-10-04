using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Handles the standalone Name Input Scene flow.
    /// Updates the SaveManager and goes straight on to the tutorial (a fade, no loading screen).
    /// P skips the scene (SceneSkipKey) unless the player is typing in the name box.
    /// </summary>
    public class NameInputUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField _nameInputField;
        [SerializeField] private Button _submitButton;

        private void Start()
        {
            if (_submitButton != null)
            {
                _submitButton.onClick.AddListener(OnSubmit);
            }
            // The name box isn't auto-focused, so P (skip) works until the player taps the box to type.
        }

        private void OnSubmit()
        {
            string playerName = "Player";
            if (_nameInputField != null && !string.IsNullOrWhiteSpace(_nameInputField.text))
            {
                playerName = _nameInputField.text.Trim();
            }

            // Keep the name on the current save; it's written when the player first saves
            // (a new game only claims a slot once the player picks one in the pause menu).
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null)
            {
                SaveManager.Instance.CurrentSave.playerName = playerName;
                SaveManager.Instance.AutoSave();
                Debug.Log($"[NameInput] Player name saved as: {playerName}");
            }

            // Straight into the tutorial: a fade, no loading screen.
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(GameConstants.Scenes.TUTORIAL, showLoadingScreen: false);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scenes.TUTORIAL);
            }
        }
    }
}
