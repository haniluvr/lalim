using UnityEngine;

namespace Lalim.UI
{
    /// <summary>
    /// Shared HUD colours so every "charging" element matches the seashell stamina orbs:
    /// the shells themselves, the Seagrass Mend card background and the Bubble Barrier fill.
    /// Change a colour here and all three follow.
    /// </summary>
    public static class HudPalette
    {
        /// <summary>Full / ready: pearly sea-glass aqua.</summary>
        public static readonly Color ShellReady = new Color(0.42f, 0.88f, 0.9f, 1f);

        /// <summary>Filling up: the same hue, desaturated but light enough to read against dark backdrops.</summary>
        public static readonly Color ShellCharging = new Color(0.58f, 0.7f, 0.74f, 1f);

        /// <summary>Seagrass Mend card background once it's fully charged and ready to use.</summary>
        public static readonly Color ReadyGold = new Color(1f, 0.76f, 0.2f, 1f);

        /// <summary>Empty shell outline.</summary>
        public static readonly Color ShellEmpty = new Color(0.62f, 0.7f, 0.74f, 0.35f);

        /// <summary>Small pearls between the shells.</summary>
        public static readonly Color Pearl = new Color(0.85f, 0.96f, 0.98f, 0.7f);

        /// <summary>Failed press (not enough stamina).</summary>
        public static readonly Color Warning = new Color(1f, 0.3f, 0.3f, 1f);

        /// <summary>Riptide duration line under the HP bar.</summary>
        public static readonly Color Riptide = new Color(0.45f, 0.95f, 1f, 1f);
    }
}
