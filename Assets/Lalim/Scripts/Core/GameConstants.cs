namespace Lalim.Core
{
    /// <summary>
    /// Centralized constants to eliminate magic strings throughout the codebase.
    /// </summary>
    public static class GameConstants
    {
        // ─────────────────────────────────────────────
        // Scene Names (must match Unity Build Settings)
        // ─────────────────────────────────────────────

        public static class Scenes
        {
            public const string BOOT = "00_BootSequence";
            public const string SPLASH = "01_SplashScene";
            public const string MAIN_MENU = "02_MainMenu";
            public const string LOADING = "04_Loading";
            public const string NAME_INPUT = "03_NameInput";
            public const string CUTSCENE = "Cutscene";
            public const string TUTORIAL = "Tutorial";
            public const string LUZON = "02_Luzon";
            public const string VISAYAS = "03_Visayas";
            public const string MINDANAO = "04_Mindanao";

            /// <summary>
            /// Returns the scene name for a given region.
            /// </summary>
            public static string GetSceneForRegion(Region region)
            {
                switch (region)
                {
                    case Region.Luzon: return LUZON;
                    case Region.Visayas: return VISAYAS;
                    case Region.Mindanao: return MINDANAO;
                    default: return MAIN_MENU;
                }
            }

            /// <summary>
            /// Returns the region for a given scene name, or null if not a region scene.
            /// </summary>
            public static Region? GetRegionForScene(string sceneName)
            {
                switch (sceneName)
                {
                    case LUZON: return Region.Luzon;
                    case VISAYAS: return Region.Visayas;
                    case MINDANAO: return Region.Mindanao;
                    default: return null;
                }
            }
        }

        // ─────────────────────────────────────────────
        // Tags
        // ─────────────────────────────────────────────

        public static class Tags
        {
            public const string PLAYER = "Player";
            public const string INTERACTABLE = "Interactable";
            public const string SPIRIT_HIDDEN = "SpiritHidden";
            public const string CHALLENGE_TRIGGER = "ChallengeTrigger";
            public const string OBJECTIVE = "Objective";
        }

        // ─────────────────────────────────────────────
        // Layers
        // ─────────────────────────────────────────────

        public static class Layers
        {
            public const string DEFAULT = "Default";
            public const string UI = "UI";
            public const string PLAYER = "Player";
            public const string INTERACTABLE = "Interactable";
            public const string HIDDEN = "Hidden";
        }

        // ─────────────────────────────────────────────
        // Save System
        // ─────────────────────────────────────────────

        public static class Save
        {
            public const string SAVE_FILE_PREFIX = "lalim_save_slot_";
            public const string SAVE_FILE_EXT = ".json";
            
            public static string GetSaveFileName(int slot)
            {
                return $"{SAVE_FILE_PREFIX}{slot}{SAVE_FILE_EXT}";
            }
            
            public static string GetBackupFileName(int slot)
            {
                return $"{SAVE_FILE_PREFIX}{slot}_backup{SAVE_FILE_EXT}";
            }
        }

        // ─────────────────────────────────────────────
        // DDA Defaults
        // ─────────────────────────────────────────────

        public static class DDA
        {
            public const float DEFAULT_DIFFICULTY = 0.5f;
            public const float MIN_DIFFICULTY = 0.0f;
            public const float MAX_DIFFICULTY = 1.0f;
            public const float DEFAULT_TASK_MULTIPLIER = 1.0f;
            public const int DEFAULT_HINT_BUDGET = 3;
            public const float DEFAULT_TIMER_STRICTNESS = 0.5f;
        }

        // ─────────────────────────────────────────────
        // Zone Identifiers
        // ─────────────────────────────────────────────

        public static class Zones
        {
            // Luzon zones
            public const string LUZON_BLEACHED_REEF = "luzon_zone_a";
            public const string LUZON_TOXIN_SITES = "luzon_zone_b";
            public const string LUZON_CORAL_BED = "luzon_zone_c";

            // Visayas zones
            public const string VISAYAS_FISHING_GROUNDS = "visayas_zone_a";
            public const string VISAYAS_GHOST_NET_FIELD = "visayas_zone_b";
            public const string VISAYAS_RESCUE_SITE = "visayas_zone_c";

            // Mindanao zones
            public const string MINDANAO_TRENCH_ENTRY = "mindanao_zone_a";
            public const string MINDANAO_BOMB_FIELD = "mindanao_zone_b";
            public const string MINDANAO_WHALE_SANCTUARY = "mindanao_zone_c";

            public const int ZONES_PER_REGION = 3;

            /// <summary>
            /// Get all zone IDs for a given region.
            /// </summary>
            public static string[] GetZonesForRegion(Region region)
            {
                switch (region)
                {
                    case Region.Luzon:
                        return new[] { LUZON_BLEACHED_REEF, LUZON_TOXIN_SITES, LUZON_CORAL_BED };
                    case Region.Visayas:
                        return new[] { VISAYAS_FISHING_GROUNDS, VISAYAS_GHOST_NET_FIELD, VISAYAS_RESCUE_SITE };
                    case Region.Mindanao:
                        return new[] { MINDANAO_TRENCH_ENTRY, MINDANAO_BOMB_FIELD, MINDANAO_WHALE_SANCTUARY };
                    default:
                        return new string[0];
                }
            }
        }

        // ─────────────────────────────────────────────
        // Player Defaults
        // ─────────────────────────────────────────────

        public static class Player
        {
            public const float MOVE_SPEED = 5.0f;
            public const float LOOK_SENSITIVITY = 2.0f;
            public const float GRAVITY = -9.81f;
            public const float INTERACTION_RANGE = 3.0f;
        }

        // ─────────────────────────────────────────────
        // SpiritSense Defaults
        // ─────────────────────────────────────────────

        public static class SpiritSense
        {
            public const float REVEAL_RADIUS = 15.0f;
            public const float COOLDOWN_SECONDS = 10.0f;
            public const float COMPASS_UPDATE_INTERVAL = 0.25f;
        }

        // ─────────────────────────────────────────────
        // Audio Escalation Defaults
        // ─────────────────────────────────────────────

        public static class Audio
        {
            public const float ESCALATION_TIER1_DELAY = 30.0f;  // seconds before subtle hint
            public const float ESCALATION_TIER2_DELAY = 60.0f;  // seconds before distinct ping
            public const float ESCALATION_TIER3_DELAY = 90.0f;  // seconds before voice hint
        }
    }
}
