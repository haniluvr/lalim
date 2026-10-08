using System.Collections;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Real time that stops while the game is paused (the pause menu is open). The world already
    /// freezes with timeScale 0; HUD animations run on unscaled time so they keep moving during
    /// dialogue and minigames — they use this clock instead, so a pause freezes them too (toasts,
    /// banners, the quest box, buttons, minigame cards). Menus that are used WHILE paused (the
    /// pause menu, settings) and transitions keep using Unity's unscaled time.
    /// </summary>
    public static class PauseClock
    {
        private static float _pausedAt = -1f;
        private static float _pausedTotal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _pausedAt = -1f;
            _pausedTotal = 0f;
        }

        public static bool Paused => _pausedAt >= 0f;

        /// <summary>Called by the pause menu as it opens and closes.</summary>
        public static void SetPaused(bool paused)
        {
            if (paused == Paused) return;
            if (paused) _pausedAt = UnityEngine.Time.unscaledTime;
            else
            {
                _pausedTotal += UnityEngine.Time.unscaledTime - _pausedAt;
                _pausedAt = -1f;
            }
        }

        /// <summary>Unscaled frame time, 0 while paused.</summary>
        public static float DeltaTime => Paused ? 0f : UnityEngine.Time.unscaledDeltaTime;

        /// <summary>Unscaled time that doesn't advance while paused (for pulses and bobbing).</summary>
        public static float Time => (Paused ? _pausedAt : UnityEngine.Time.unscaledTime) - _pausedTotal;

        /// <summary>Waits <paramref name="seconds"/> of unpaused real time.</summary>
        public static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += DeltaTime) yield return null;
        }
    }
}
