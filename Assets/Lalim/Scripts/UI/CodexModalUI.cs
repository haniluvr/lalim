using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.UI
{
    [System.Serializable]
    public class CodexSpeciesEntry
    {
        public string id;
        public string commonName;
        public string localName;
        public string scientificName;
        public string conservationStatus;
        [TextArea(2, 5)] public string loreDescription;
        public bool isUnlocked;
    }

    /// <summary>
    /// Philippine Marine Species Codex Modal.
    /// Displays discovered marine life, scientific classifications, cultural folklore,
    /// and conservation status.
    /// </summary>
    public class CodexModalUI : MonoBehaviour
    {
        public static CodexModalUI Instance { get; private set; }

        [Header("Modal Root")]
        [SerializeField] private GameObject _modalContent;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;

        [Header("Species Details")]
        [SerializeField] private TMP_Text _speciesTitleText;
        [SerializeField] private TMP_Text _scientificNameText;
        [SerializeField] private TMP_Text _statusBadgeText;
        [SerializeField] private TMP_Text _loreBodyText;
        [SerializeField] private Image _speciesIllustration;

        [Header("Audio")]
        [SerializeField] private AudioClip _openSound;
        [SerializeField] private AudioClip _closeSound;

        [Header("Species Database")]
        [SerializeField] private List<CodexSpeciesEntry> _entries = new List<CodexSpeciesEntry>();

        public bool IsOpen => _modalContent != null && _modalContent.activeSelf;
        private int _selectedIdx = 0;

        private void Awake()
        {
            Instance = this;
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(CloseModal);
            }
            if (_prevButton != null)
            {
                _prevButton.onClick.AddListener(PreviousEntry);
            }
            if (_nextButton != null)
            {
                _nextButton.onClick.AddListener(NextEntry);
            }

            if (_entries.Count == 0)
            {
                PopulateDefaultEntries();
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CodexEntryUnlockedEvent>(OnCodexUnlocked);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CodexEntryUnlockedEvent>(OnCodexUnlocked);
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
                }
            }
        }

        private void PopulateDefaultEntries()
        {
            _entries.Add(new CodexSpeciesEntry
            {
                id = "taklobo",
                commonName = "Giant Clam",
                localName = "Taklobo",
                scientificName = "Tridacna gigas",
                conservationStatus = "Vulnerable (IUCN)",
                loreDescription = "The colossal bivalve of Philippine coral reefs. Taklobo act as natural bio-filters, purifying lagoon currents while hosting symbiotic zooxanthellae that generate vibrant turquoise mantles.",
                isUnlocked = true
            });

            _entries.Add(new CodexSpeciesEntry
            {
                id = "butanding",
                commonName = "Whale Shark",
                localName = "Butanding",
                scientificName = "Rhincodon typus",
                conservationStatus = "Endangered (IUCN)",
                loreDescription = "Gentle ocean giants migrating through the Visayas and Donsol waters. Filter-feeding on plankton, their distinct starry spot patterns are unique as human fingerprints.",
                isUnlocked = true
            });

            _entries.Add(new CodexSpeciesEntry
            {
                id = "pawikan",
                commonName = "Green Sea Turtle",
                localName = "Pawikan",
                scientificName = "Chelonia mydas",
                conservationStatus = "Endangered (IUCN)",
                loreDescription = "Ancient navigators of the archipelago, pawikan graze on seagrass beds, maintaining underwater meadows vital for juvenile fish nurseries.",
                isUnlocked = false
            });

            _entries.Add(new CodexSpeciesEntry
            {
                id = "bahura_coral",
                commonName = "Staghorn & Brain Coral",
                localName = "Bahura",
                scientificName = "Acropora cervicornis",
                conservationStatus = "Critically Threatened (IUCN)",
                loreDescription = "The structural architects of Philippine coral reefs, providing habitat for over 2,000 marine species. Highly susceptible to warming and bleaching, requiring coral replanting.",
                isUnlocked = true
            });
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

            SelectEntry(_selectedIdx);
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

        public void SelectEntry(int index)
        {
            if (index < 0 || index >= _entries.Count) return;
            _selectedIdx = index;
            var entry = _entries[index];

            if (_speciesTitleText != null)
            {
                _speciesTitleText.text = $"{entry.commonName} ({entry.localName})";
            }
            if (_scientificNameText != null)
            {
                _scientificNameText.text = $"Scientific: <i>{entry.scientificName}</i>";
            }
            if (_statusBadgeText != null)
            {
                _statusBadgeText.text = $"Status: {entry.conservationStatus}";
            }
            if (_loreBodyText != null)
            {
                _loreBodyText.text = entry.isUnlocked
                    ? entry.loreDescription
                    : "<i>[Undiscovered Specimen - Explore regional reefs and hidden zones to unlock this entry.]</i>";
            }
        }

        public void NextEntry()
        {
            SelectEntry((_selectedIdx + 1) % _entries.Count);
        }

        public void PreviousEntry()
        {
            SelectEntry((_selectedIdx - 1 + _entries.Count) % _entries.Count);
        }

        private void OnCodexUnlocked(CodexEntryUnlockedEvent evt)
        {
            var entry = _entries.Find(x => x.id == evt.SpeciesId);
            if (entry != null)
            {
                entry.isUnlocked = true;
                Debug.Log($"[Codex] Marine species catalog updated: {entry.commonName} unlocked!");
            }
        }
    }
}
