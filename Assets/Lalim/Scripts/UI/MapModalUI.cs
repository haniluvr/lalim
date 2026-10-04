using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.UI
{
    /// <summary>
    /// Underwater Navigation Map Modal.
    /// Displays regional zones, player depth/coordinates, and restoration progress.
    /// </summary>
    public class MapModalUI : MonoBehaviour
    {
        public static MapModalUI Instance { get; private set; }

        [Header("Modal Root")]
        [SerializeField] private GameObject _modalContent;
        [SerializeField] private Button _closeButton;

        [Header("Text Displays")]
        [SerializeField] private TMP_Text _regionTitleText;
        [SerializeField] private TMP_Text _coordinatesText;
        [SerializeField] private TMP_Text _zoneProgressText;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _closeSound;

        public bool IsOpen => _modalContent != null && _modalContent.activeSelf;

        private void Awake()
        {
            Instance = this;
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(CloseModal);
            }
        }

        private void Start()
        {
            CloseModal();
        }

        private void Update()
        {
            if (IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseModal();
                    return;
                }

                UpdateTelemetry();
            }
        }

        public void OpenModal()
        {
            if (_modalContent != null)
            {
                _modalContent.SetActive(true);
            }

            if (_openSound != null)
            {
                GameAudio.PlayAt(_openSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.SetMovementEnabled(false);
            }
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetInputEnabled(false);
            }

            UpdateTelemetry();
        }

        public void CloseModal()
        {
            if (_modalContent != null)
            {
                _modalContent.SetActive(false);
            }

            if (_closeSound != null)
            {
                GameAudio.PlayAt(_closeSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.SetMovementEnabled(true);
            }
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SetInputEnabled(true);
            }
        }

        public void ToggleModal()
        {
            if (IsOpen)
            {
                CloseModal();
            }
            else
            {
                OpenModal();
            }
        }

        private void UpdateTelemetry()
        {
            if (PlayerController.Instance != null)
            {
                Vector3 pos = PlayerController.Instance.transform.position;
                if (_coordinatesText != null)
                {
                    _coordinatesText.text = $"DIVER DEPTH: {Mathf.Abs(pos.y):F1}m  |  COORDINATES: X: {pos.x:F0}, Z: {pos.z:F0}";
                }
            }

            if (RegionProgressTracker.Instance != null && _zoneProgressText != null)
            {
                int completed = RegionProgressTracker.Instance.GetCompletedZoneCount(Region.Luzon);
                _zoneProgressText.text = $"Restoration Sanctuary: {completed} / {GameConstants.Zones.ZONES_PER_REGION} Zones Restored";
            }
        }
    }
}
