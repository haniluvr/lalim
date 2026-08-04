using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;

namespace Lalim.UI
{
    /// <summary>
    /// Region selection UI showing three regions (Luzon, Visayas, Mindanao)
    /// with lock/unlock/completed visual states.
    /// Only allows selection of unlocked or completed regions.
    /// </summary>
    public class RegionSelectUI : MonoBehaviour
    {
        [Header("Region Buttons")]
        [SerializeField] private Button _luzonButton;
        [SerializeField] private Button _visayasButton;
        [SerializeField] private Button _mindanaoButton;

        [Header("Region Labels")]
        [SerializeField] private TMP_Text _luzonLabel;
        [SerializeField] private TMP_Text _visayasLabel;
        [SerializeField] private TMP_Text _mindanaoLabel;

        [Header("Status Icons")]
        [SerializeField] private GameObject _luzonLockIcon;
        [SerializeField] private GameObject _visayasLockIcon;
        [SerializeField] private GameObject _mindanaoLockIcon;
        [SerializeField] private GameObject _luzonCompleteIcon;
        [SerializeField] private GameObject _visayasCompleteIcon;
        [SerializeField] private GameObject _mindanaoCompleteIcon;

        [Header("Visual Settings")]
        [SerializeField] private Color _lockedColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        [SerializeField] private Color _unlockedColor = Color.white;
        [SerializeField] private Color _completedColor = new Color(0.3f, 1f, 0.5f, 1f);

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void OnEnable()
        {
            SetupButtons();
            RefreshRegionStates();

            EventBus.Subscribe<RegionUnlockedEvent>(OnRegionUnlocked);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RegionUnlockedEvent>(OnRegionUnlocked);
        }

        // ─────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────

        private void SetupButtons()
        {
            if (_luzonButton != null)
            {
                _luzonButton.onClick.RemoveAllListeners();
                _luzonButton.onClick.AddListener(() => OnRegionSelected(Region.Luzon));
            }

            if (_visayasButton != null)
            {
                _visayasButton.onClick.RemoveAllListeners();
                _visayasButton.onClick.AddListener(() => OnRegionSelected(Region.Visayas));
            }

            if (_mindanaoButton != null)
            {
                _mindanaoButton.onClick.RemoveAllListeners();
                _mindanaoButton.onClick.AddListener(() => OnRegionSelected(Region.Mindanao));
            }
        }

        // ─────────────────────────────────────────────
        // Region State Display
        // ─────────────────────────────────────────────

        private void RefreshRegionStates()
        {
            if (GameManager.Instance == null) return;

            UpdateRegionDisplay(
                Region.Luzon,
                _luzonButton, _luzonLabel,
                _luzonLockIcon, _luzonCompleteIcon
            );

            UpdateRegionDisplay(
                Region.Visayas,
                _visayasButton, _visayasLabel,
                _visayasLockIcon, _visayasCompleteIcon
            );

            UpdateRegionDisplay(
                Region.Mindanao,
                _mindanaoButton, _mindanaoLabel,
                _mindanaoLockIcon, _mindanaoCompleteIcon
            );
        }

        private void UpdateRegionDisplay(
            Region region,
            Button button, TMP_Text label,
            GameObject lockIcon, GameObject completeIcon)
        {
            RegionStatus status = GameManager.Instance.GetRegionStatus(region);

            bool isAccessible = status != RegionStatus.Locked;
            bool isCompleted = status == RegionStatus.Completed;

            // Button interactability
            if (button != null)
            {
                button.interactable = isAccessible;

                // Update button color
                ColorBlock colors = button.colors;
                if (!isAccessible)
                {
                    colors.normalColor = _lockedColor;
                }
                else if (isCompleted)
                {
                    colors.normalColor = _completedColor;
                }
                else
                {
                    colors.normalColor = _unlockedColor;
                }
                button.colors = colors;
            }

            // Status label
            if (label != null)
            {
                string statusSuffix = "";
                if (!isAccessible) statusSuffix = " 🔒";
                else if (isCompleted) statusSuffix = " ✓";

                label.text = $"{region}{statusSuffix}";
            }

            // Lock/complete icons
            if (lockIcon != null) lockIcon.SetActive(!isAccessible);
            if (completeIcon != null) completeIcon.SetActive(isCompleted);
        }

        // ─────────────────────────────────────────────
        // Selection
        // ─────────────────────────────────────────────

        private void OnRegionSelected(Region region)
        {
            if (GameManager.Instance == null) return;

            bool entered = GameManager.Instance.TryEnterRegion(region);

            if (entered)
            {
                Debug.Log($"[RegionSelect] Entering: {region}");
            }
            else
            {
                Debug.Log($"[RegionSelect] Cannot enter locked region: {region}");
                // TODO: Play locked feedback animation/sound
            }
        }

        // ─────────────────────────────────────────────
        // Event Handlers
        // ─────────────────────────────────────────────

        private void OnRegionUnlocked(RegionUnlockedEvent evt)
        {
            RefreshRegionStates();
            // TODO: Play unlock animation for the newly unlocked region
        }
    }
}
