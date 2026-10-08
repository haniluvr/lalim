using System;
using UnityEngine;
using Lalim.Abilities;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Zone 1's altar as a place to start the sorting: once the sorting step is active the player
    /// swims over, the interact button appears ("Sort the trash"), and tapping it starts the
    /// minigame. Same look and feel as the zone 2/3 area stations (glow ring on the ground).
    /// Needs a trigger collider on the Interactable layer.
    /// </summary>
    [DisallowMultipleComponent]
    public class SortingStation : MonoBehaviour, IInteractable, IInteractionIcon, IInteractionAnchor, ISpiritSenseRelevant
    {
        [SerializeField] private string _prompt = "Sort the trash";
        [Tooltip("Glow ring shown while the station is waiting.")]
        [SerializeField] private GameObject _marker;
        [SerializeField] private float _promptHeight = 0.4f;

        private Action _onStart;

        public bool IsArmed => _onStart != null;
        public InteractionIcon Icon => InteractionIcon.Interact;
        public Vector3 PromptAnchor => transform.position + Vector3.up * _promptHeight;
        public bool IsSpiritRelevant => IsArmed;

        private void Awake() => SetMarker(false);

        /// <summary>Waits for the player to tap the station, then calls <paramref name="onStart"/> once.</summary>
        public void Arm(Action onStart)
        {
            _onStart = onStart;
            SetMarker(true);
        }

        public void Disarm()
        {
            _onStart = null;
            SetMarker(false);
        }

        public void Interact()
        {
            if (!IsArmed) return;
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) return;
            if (Lalim.UI.DialogueManager.Instance != null && Lalim.UI.DialogueManager.Instance.IsOpen) return;
            Action start = _onStart;
            Disarm();
            start();
        }

        public string GetInteractionPrompt() => IsArmed ? _prompt : "";

        private void SetMarker(bool on)
        {
            if (_marker != null) _marker.SetActive(on);
        }

#if UNITY_EDITOR
        public void EditorSetup(string prompt, GameObject marker)
        {
            _prompt = prompt;
            _marker = marker;
        }
#endif
    }
}
