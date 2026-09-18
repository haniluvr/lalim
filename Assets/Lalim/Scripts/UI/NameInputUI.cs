using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Handles the standalone Name Input Scene flow.
    /// Updates the SaveManager and transitions to the Cutscene.
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
            
            // Auto-focus the input field when the scene starts
            if (_nameInputField != null)
            {
                _nameInputField.Select();
            }
        }

        private void OnSubmit()
        {
            string playerName = "Player";
            if (_nameInputField != null && !string.IsNullOrWhiteSpace(_nameInputField.text))
            {
                playerName = _nameInputField.text.Trim();
            }

            // Save the player name into the newly created save slot
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSave != null)
            {
                SaveManager.Instance.CurrentSave.playerName = playerName;
                SaveManager.Instance.SaveGame();
                Debug.Log($"[NameInput] Player name saved as: {playerName}");
            }

            // Move to the cutscene
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(GameConstants.Scenes.CUTSCENE);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameConstants.Scenes.CUTSCENE);
            }
        }
    }
}
