using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    [RequireComponent(typeof(Collider))]
    public class RegionTeleporter : MonoBehaviour, IInteractable
    {
        [Header("Teleport Destination")]
        [Tooltip("The region this teleporter will take the player to.")]
        [SerializeField] private Region _targetRegion;
        [SerializeField] private string _teleporterName = "Teleporter";
        
        [Header("Feedback")]
        [SerializeField] private AudioClip _teleportSound;
        [SerializeField] private AudioClip _lockedSound;
        [SerializeField] private ParticleSystem _activeParticles;

        [Header("Visuals (Hub)")]
        [Tooltip("The object containing the screenshot of the region")]
        [SerializeField] private GameObject _backgroundVisual;
        [Tooltip("The fog particle system that blocks the arch when locked")]
        [SerializeField] private GameObject _lockedFogVisual;

        private void Start()
        {
            // Ensure collider is set to trigger or solid as needed by the player controller
            // Visuals can be updated here if we want locked teleporters to look different
            bool isUnlocked = GameManager.Instance != null && GameManager.Instance.IsRegionAccessible(_targetRegion);

            if (_activeParticles != null)
            {
                if (isUnlocked)
                    _activeParticles.Play();
                else
                    _activeParticles.Stop();
            }

            // Hub Visuals
            if (_backgroundVisual != null) _backgroundVisual.SetActive(isUnlocked);
            if (_lockedFogVisual != null) _lockedFogVisual.SetActive(!isUnlocked);
        }

        public void Interact()
        {
            if (GameManager.Instance == null || Lalim.Core.SceneLoader.Instance == null) return;

            if (GameManager.Instance.IsRegionAccessible(_targetRegion))
            {
                // Unlocked - Teleport!
                PlaySound(_teleportSound);
                Debug.Log($"[RegionTeleporter] Teleporting to {_targetRegion}...");
                
                // Let the GameManager handle the transition logic
                GameManager.Instance.TryEnterRegion(_targetRegion);
            }
            else
            {
                // Locked
                PlaySound(_lockedSound);
                Debug.Log($"[RegionTeleporter] {_targetRegion} is locked.");
                
                // Show HUD notification if available
                if (Lalim.UI.HUDController.Instance != null)
                {
                    Lalim.UI.HUDController.Instance.ShowInteractionPrompt($"⚠ {_targetRegion} Region is Locked. Complete prior region first.");
                }
            }
        }

        public string GetInteractionPrompt()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsRegionAccessible(_targetRegion))
            {
                return $"⚠ {_teleporterName} ({_targetRegion}) - Locked";
            }
            
            return $"Enter {_teleporterName} to travel to {_targetRegion}";
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null)
            {
                GameAudio.PlayAt(clip, transform.position);
            }
        }
    }
}
