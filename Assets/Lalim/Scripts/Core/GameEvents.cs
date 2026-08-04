using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// All game event structs used by the EventBus.
    /// Events are value types (structs) to avoid GC allocations.
    /// </summary>

    // ─────────────────────────────────────────────
    // Game State
    // ─────────────────────────────────────────────

    public enum GameState
    {
        Boot,
        Menu,
        Exploring,
        InChallenge,
        Paused,
        Results
    }

    public struct GameStateChangedEvent
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    // ─────────────────────────────────────────────
    // Scene Management
    // ─────────────────────────────────────────────

    public struct SceneLoadRequestedEvent
    {
        public string SceneName;
        public bool ShowLoadingScreen;
    }

    public struct SceneLoadProgressEvent
    {
        public float Progress; // 0.0 to 1.0
    }

    public struct SceneLoadedEvent
    {
        public string SceneName;
    }

    // ─────────────────────────────────────────────
    // Challenge System (Perspective Switching)
    // ─────────────────────────────────────────────

    public enum ChallengeType
    {
        TrashCleanup,
        RelicRestoration,
        PuzzleSolving,
        ConservationTask
    }

    public struct ChallengeStartedEvent
    {
        public ChallengeType Type;
        public string ChallengeId;
        public int TaskQuantity;       // Adjusted by DDA (e.g., number of trash items)
        public float TimeLimit;         // Adjusted by DDA (seconds, 0 = no timer)
        public int HintBudget;          // Adjusted by DDA
    }

    public struct ChallengeCompletedEvent
    {
        public string ChallengeId;
        public ChallengeType Type;
        public bool Success;

        // Performance metrics for DDA
        public float HesitationTime;
        public int ErrorCount;
        public float CompletionTime;
        public float ExpectedTime;      // Baseline for deviation calculation
    }

    public struct ChallengeFailedEvent
    {
        public string ChallengeId;
        public ChallengeType Type;
        public string Reason;           // "timeout", "too_many_errors", etc.
    }

    // ─────────────────────────────────────────────
    // Inventory
    // ─────────────────────────────────────────────

    public enum ItemType
    {
        Relic,
        ConservationTool
    }

    public struct ItemCollectedEvent
    {
        public string ItemId;
        public ItemType Type;
        public string RegionOrigin;
    }

    public struct ItemUsedEvent
    {
        public string ItemId;
        public ItemType Type;
    }

    public struct InventoryChangedEvent
    {
        public int TotalRelics;
        public int TotalTools;
    }

    // ─────────────────────────────────────────────
    // DDA (Dynamic Difficulty Adjustment)
    // ─────────────────────────────────────────────

    public struct DifficultyChangedEvent
    {
        public float DifficultyLevel;           // 0.0 (easiest) to 1.0 (hardest)
        public float TaskQuantityMultiplier;     // e.g., 1.3 = 30% more items
        public float TimerStrictness;            // 0.0 (generous) to 1.0 (tight)
        public int HintBudget;                   // Number of hints available
        public int PuzzleComplexityTier;         // 0 = simple, 1 = medium, 2 = complex
    }

    public struct PlayerMetricRecordedEvent
    {
        public string MetricName;
        public float Value;
    }

    // ─────────────────────────────────────────────
    // SpiritSense
    // ─────────────────────────────────────────────

    public struct SpiritSenseActivatedEvent
    {
        public Vector3 PlayerPosition;
        public float RevealRadius;
    }

    public struct SpiritSenseRevealEvent
    {
        public GameObject RevealedObject;
        public string ObjectId;
    }

    public struct SpiritSenseCooldownEvent
    {
        public float RemainingTime;
        public float TotalCooldown;
    }

    // ─────────────────────────────────────────────
    // Audio Navigation
    // ─────────────────────────────────────────────

    public enum HintEscalationLevel
    {
        None,
        SubtleAmbient,      // Level 1: soft directional cue
        DistinctPing,        // Level 2: clear audio ping
        VoiceHint            // Level 3: narrated guidance
    }

    public struct AudioHintEscalatedEvent
    {
        public HintEscalationLevel Level;
        public Vector3 TargetPosition;
    }

    // ─────────────────────────────────────────────
    // Region / Progression
    // ─────────────────────────────────────────────

    public enum Region
    {
        Luzon,
        Visayas,
        Mindanao
    }

    public struct RegionUnlockedEvent
    {
        public Region UnlockedRegion;
    }

    public struct RegionCompletedEvent
    {
        public Region CompletedRegion;
    }

    // ─────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────

    public struct PauseRequestedEvent
    {
        public bool IsPaused;
    }

    public struct HintRequestedEvent
    {
        public int RemainingHints;
    }
}
