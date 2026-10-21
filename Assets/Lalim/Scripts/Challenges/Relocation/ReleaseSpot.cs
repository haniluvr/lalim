using System;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Relocation
{
    /// <summary>
    /// The "Release the fish" button for Minigame 3: a small trigger that rides with the diver, so
    /// the HUD's interact button offers it whenever the relocation step says a school can be let
    /// out here (the diver is in the home of the school whose turn it is).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class ReleaseSpot : MonoBehaviour, IInteractable
    {
        /// <summary>The prompt to show now ("" hides the button).</summary>
        public Func<string> Prompt;
        public Action Released;

        private SphereCollider _trigger;

        public static ReleaseSpot Create(Transform parent)
        {
            var go = new GameObject("[ReleaseSpot]");
            go.transform.SetParent(parent, false);
            var spot = go.AddComponent<ReleaseSpot>();
            spot._trigger = go.GetComponent<SphereCollider>();
            spot._trigger.isTrigger = true;
            spot._trigger.radius = 0.8f;
            spot._trigger.enabled = false;
            return spot;
        }

        /// <summary>Follows the diver; only there (and found by the interact search) while a release is possible.</summary>
        public void Follow(Vector3 diver, bool available)
        {
            transform.position = diver + Vector3.up * 0.5f;
            if (_trigger != null && _trigger.enabled != available) _trigger.enabled = available;
        }

        public string GetInteractionPrompt() => _trigger != null && _trigger.enabled && Prompt != null ? Prompt() : "";

        public void Interact()
        {
            if (!string.IsNullOrEmpty(GetInteractionPrompt())) Released?.Invoke();
        }
    }
}
