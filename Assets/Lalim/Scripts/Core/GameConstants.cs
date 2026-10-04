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
            public const string SPLASH = "01_Splash";
            public const string MAIN_MENU = "02_MainMenu";
            public const string LOADING = "04_Loading";
            public const string NAME_INPUT = "03_NameInput";
            public const string CUTSCENE = "01_Cutscene";   // Scenes/Cutscene/01_Cutscene.unity
            public const string TUTORIAL = "01_Tutorial";
            public const string TELEPORT = "TeleportScene";
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
            public const string TOXIC_WATER = "ToxicWater";
            public const string CHECKPOINT = "Checkpoint";
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
            public const string HAZARD = "Hazard";
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
        // Relics (ItemData.itemId) — earned from Level 1's deities, used in Level 2
        // ─────────────────────────────────────────────

        public static class Relics
        {
            public const string RADIANT_CONCH = "00_radiantconch";            // Amansinaya
            public const string COMPASS = "01_compass";                       // Haik
            public const string WOVEN_NET_FRAGMENT = "02_wovennetfragment";   // Neguno
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
            public const float REVEAL_RADIUS = 40.0f;
            public const float COOLDOWN_SECONDS = 20.0f;
            public const float COMPASS_UPDATE_INTERVAL = 0.25f;
        }

        // ─────────────────────────────────────────────
        // Ability Defaults (fallbacks when no AbilityConfig is assigned)
        // ─────────────────────────────────────────────

        public static class Abilities
        {
            // Fallback values if AbilityConfig asset is not assigned.
            public const int MAX_STAMINA_ORBS = 4;
            public const int STARTING_STAMINA_ORBS = 2; // new game / new level (respawns refill to full)
            public const float ORB_REGEN_SECONDS = 15.0f;
            public const float ORB_REGEN_DELAY_SECONDS = 1.0f;

            public const float PLAYER_MAX_HP = 100f;

            public const float SURGE_DASH_DISTANCE = 8f;
            public const float SURGE_DASH_DURATION = 0.35f;
            public const float SURGE_COOLDOWN = 7f;
            public const int SURGE_COST_ORBS = 0; // basic skill: cooldown only, never costs stamina
            public const int SURGE_COMBO_CHARGES = 3;
            public const float SURGE_COMBO_WINDOW = 1.5f;

            public const float RIPTIDE_DURATION = 60f;
            public const float RIPTIDE_COOLDOWN = 30f;
            public const int RIPTIDE_COST_ORBS = 0; // basic skill: cooldown only, never costs stamina
            public const float RIPTIDE_SPEED_MULTIPLIER = 1.75f;

            public const int SPIRIT_SENSE_COST_ORBS = 1;
            public const float SPIRIT_SENSE_DURATION = 15f;

            public const float TIDEBOND_FATIGUE_SECONDS = 45f;
            public const float TIDEBOND_COOLDOWN = 30f;
            public const int TIDEBOND_COST_ORBS = 2;
            public const float TIDEBOND_SPEED_MULTIPLIER = 2.25f;

            public const float SEAGRASS_MEND_HEAL_DURATION = 3f;
            public const float SEAGRASS_MEND_COOLDOWN = 40f;
            public const float SEAGRASS_MEND_HEAL_AMOUNT = 30f;

            public const float BUBBLE_BARRIER_DURATION = 20f;
            public const float BUBBLE_BARRIER_COOLDOWN = 0f; // charges from activity instead
            public const int BUBBLE_BARRIER_COST_ORBS = 0; // charge-based ultimate: no stamina
            public const float BUBBLE_BARRIER_RADIUS_SOLO = 3f;
            public const float BUBBLE_BARRIER_RADIUS_WITH_DUGONG = 5f;
            public const float BUBBLE_BARRIER_DUGONG_PROXIMITY = 6f;

            public const float TOXIC_LIGHT_DPS = 3f;
            public const float TOXIC_HEAVY_DPS = 6f;
            public const float CONTACT_HAZARD_DAMAGE = 10f;
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
