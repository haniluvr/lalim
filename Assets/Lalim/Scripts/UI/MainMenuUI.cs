using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Simple main menu with a Play button that transitions to Region Select,
    /// and a quick "Go to Luzon" button for testing.
    /// Place this on the MainMenu scene's Canvas.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _quickLuzonButton;  // Dev shortcut
        [SerializeField] private Button _quitButton;

        [Header("Title")]
        [SerializeField] private TMP_Text _titleText;

        private void Start()
        {
            if (_playButton != null)
            {
                _playButton.onClick.AddListener(OnPlayPressed);
            }

            if (_quickLuzonButton != null)
            {
                _quickLuzonButton.onClick.AddListener(OnQuickLuzonPressed);
            }

            if (_quitButton != null)
            {
                _quitButton.onClick.AddListener(OnQuitPressed);
            }

            // Set game state to Menu
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Menu);
            }
        }

        private void OnPlayPressed()
        {
            Debug.Log("[MainMenu] Play pressed — showing region select");

            // Option A: If UIManager has RegionSelectPanel, show it
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowRegionSelect();
            }
            else
            {
                // Option B: Go directly to Luzon as fallback
                OnQuickLuzonPressed();
            }
        }

        private void OnQuickLuzonPressed()
        {
            Debug.Log("[MainMenu] Quick load → Luzon");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TryEnterRegion(Region.Luzon);
            }
            else
            {
                // Direct scene load fallback
                if (SceneLoader.Instance != null)
                {
                    SceneLoader.Instance.LoadScene(GameConstants.Scenes.LUZON);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        GameConstants.Scenes.LUZON
                    );
                }
            }
        }

        private void OnQuitPressed()
        {
            Debug.Log("[MainMenu] Quit pressed");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
