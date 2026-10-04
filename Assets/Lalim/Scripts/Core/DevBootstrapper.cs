using UnityEngine;
using UnityEngine.SceneManagement;
using Lalim.Core;

namespace Lalim.DevTools
{
    /// <summary>
    /// Place this script on an empty GameObject in any Level/Hub scene.
    /// If you press Play directly in that scene, this script will automatically 
    /// load all the required managers from the BootSequence so you don't get errors!
    /// </summary>
    public class DevBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            // F9 debug readout / F10 infinite stamina / F11 difficulty presets (dev builds only).
            AbilityDebugOverlay.Ensure();

            // If the GameManager doesn't exist, we started the game from this scene directly
            if (GameManager.Instance == null)
            {
                Debug.Log("[DevBootstrapper] GameManager missing! Automatically loading BootSequence for testing...");
                
                // Store the scene we actually want to test
                string sceneToTest = SceneManager.GetActiveScene().name;
                
                // Set a temporary PlayerPref so the BootSequence knows where to send us back to
                PlayerPrefs.SetString("DevTestScene", sceneToTest);
                PlayerPrefs.Save();
                
                // Load the BootSequence so it can spawn all the singletons
                SceneManager.LoadScene(GameConstants.Scenes.BOOT);
            }
        }
    }
}
