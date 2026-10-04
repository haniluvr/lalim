using UnityEngine;

namespace Lalim.Abilities
{
    /// <summary>
    /// The clock for the player's vitals and skills. It stands still while a dialogue is open, so
    /// talking to a deity never costs health and never refills stamina, cooldowns or skill charge.
    /// (The dialogue's own pause button freezes the whole game with timeScale; this only affects
    /// gameplay timers, so the world keeps moving behind the conversation.)
    /// </summary>
    public static class GameplayClock
    {
        public static bool Frozen => Lalim.UI.DialogueManager.Instance != null && Lalim.UI.DialogueManager.Instance.IsOpen;

        /// <summary>Time.deltaTime, or 0 while frozen.</summary>
        public static float DeltaTime => Frozen ? 0f : Time.deltaTime;
    }
}
