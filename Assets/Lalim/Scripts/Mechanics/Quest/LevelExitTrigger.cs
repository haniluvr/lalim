using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics.Quest
{
    /// <summary>Relays the player entering the level-exit trigger to LevelExitStep.</summary>
    public class LevelExitTrigger : MonoBehaviour
    {
        public System.Action Entered;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(GameConstants.Tags.PLAYER)) Entered?.Invoke();
        }

        // Also when the diver is already inside as the step begins (the step ignores repeats).
        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag(GameConstants.Tags.PLAYER)) Entered?.Invoke();
        }
    }
}
