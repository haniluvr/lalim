using System.Collections.Generic;
using UnityEngine;
using Lalim.UI;

namespace Lalim.Mechanics
{
    public class DialogueTrigger : MonoBehaviour, IInteractable
    {
        [Header("Dialogue Content")]
        [SerializeField] private List<DialogueLine> _dialogueLines = new List<DialogueLine>();

        [Header("Trigger Settings")]
        [SerializeField] private bool _triggerOnStart = false;
        [SerializeField] private bool _destroyAfterDialogue = false;
        [SerializeField] private string _interactPrompt = "Talk";
        
        [Header("Events")]
        [SerializeField] private UnityEngine.Events.UnityEvent _onDialogueFinished;

        private bool _hasTriggered = false;

        private void Start()
        {
            if (_triggerOnStart)
            {
                TriggerDialogue();
            }
        }

        public void TriggerDialogue()
        {
            if (_hasTriggered) return;
            
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(_dialogueLines, OnDialogueComplete);
                _hasTriggered = true;
            }
            else
            {
                Debug.LogWarning("[DialogueTrigger] No DialogueManager found in scene!");
            }
        }

        private void OnDialogueComplete()
        {
            _onDialogueFinished?.Invoke();

            if (_destroyAfterDialogue)
            {
                Destroy(gameObject);
            }
        }

        // ─────────────────────────────────────────────
        // IInteractable Implementation
        // ─────────────────────────────────────────────
        
        public void Interact()
        {
            TriggerDialogue();
        }

        public string GetInteractionPrompt()
        {
            return _hasTriggered ? "" : _interactPrompt;
        }
    }
}
