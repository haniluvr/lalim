using UnityEngine;
using Lalim.Core;

namespace Lalim.DDA
{
    /// <summary>
    /// ScriptableObject holding all DDA tuning parameters.
    /// Allows designers to tweak difficulty behavior without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "DDAConfig", menuName = "Lalim/DDA Config")]
    public class DDAConfig : ScriptableObject
    {
        [Header("Difficulty Range")]
        [Tooltip("Starting difficulty level (0-1)")]
        [Range(0f, 1f)]
        public float startingDifficulty = 0.5f;

        [Tooltip("Minimum allowed difficulty")]
        [Range(0f, 1f)]
        public float minDifficulty = 0.0f;

        [Tooltip("Maximum allowed difficulty")]
        [Range(0f, 1f)]
        public float maxDifficulty = 1.0f;

        [Header("Metric Thresholds")]
        [Tooltip("Seconds of inactivity before counting as hesitation")]
        public float hesitationThreshold = 3.0f;

        [Tooltip("Weight multiplier for hesitation in scoring")]
        [Range(0f, 2f)]
        public float hesitationWeight = 1.0f;

        [Tooltip("Weight multiplier for error frequency in scoring")]
        [Range(0f, 2f)]
        public float errorWeight = 1.5f;

        [Tooltip("Weight multiplier for completion time deviation in scoring")]
        [Range(0f, 2f)]
        public float completionTimeWeight = 1.0f;

        [Tooltip("Weight for the 'pickup rate': how fast the first correct action came vs. the minigame's baseline")]
        [Range(0f, 2f)]
        public float firstActionWeight = 1.0f;

        [Tooltip("Weight for hint reliance (share of the hint budget used)")]
        [Range(0f, 2f)]
        public float hintWeight = 0.5f;

        [Header("History (this and all prior minigames)")]
        [Tooltip("How much the average of all earlier minigame scores pulls each new score (0 = only the latest minigame counts)")]
        [Range(0f, 0.9f)]
        public float historyWeight = 0.35f;

        [Header("Difficulty Adjustment")]
        [Tooltip("How much difficulty changes per adjustment (0.01 - 0.2)")]
        [Range(0.01f, 0.2f)]
        public float difficultyStepSize = 0.05f;

        [Tooltip("Smoothing factor for exponential moving average (0 = no smoothing, 1 = full smoothing)")]
        [Range(0f, 0.95f)]
        public float smoothingFactor = 0.3f;

        [Tooltip("Performance score above this threshold = 'too easy', difficulty increases")]
        [Range(0f, 1f)]
        public float tooEasyThreshold = 0.75f;

        [Tooltip("Performance score below this threshold = 'struggling', difficulty decreases")]
        [Range(0f, 1f)]
        public float strugglingThreshold = 0.35f;

        [Header("Task Quantity Scaling (blends through 1.0 at difficulty 0.5)")]
        [Tooltip("Task quantity multiplier at minimum difficulty")]
        public float minTaskMultiplier = 0.8f;

        [Tooltip("Task quantity multiplier at maximum difficulty. Kept modest because the hard end also has slower stamina and stronger toxic water.")]
        public float maxTaskMultiplier = 1.4f;

        [Header("Timer Scaling (blends through 1.0 at difficulty 0.5)")]
        [Tooltip("Timer multiplier at minimum difficulty (generous = higher)")]
        public float minTimerMultiplier = 1.3f;

        [Tooltip("Timer multiplier at maximum difficulty (strict = lower)")]
        public float maxTimerMultiplier = 0.8f;

        [Header("Hint Scaling")]
        [Tooltip("Hints available at minimum difficulty")]
        public int maxHints = 5;

        [Tooltip("Hints available at maximum difficulty")]
        public int minHints = 1;

        [Header("Exploration & Failure Signals (performance scores, 0 = struggling, 1 = excelling)")]
        [Tooltip("Score sent when a minigame is failed or times out")]
        [Range(0f, 1f)] public float failedChallengeScore = 0.2f;

        [Tooltip("Score sent on each death")]
        [Range(0f, 1f)] public float deathScore = 0.0f;

        [Tooltip("Step multiplier for a death (2 = counts as a double step down)")]
        public float deathStepScale = 2f;

        [Tooltip("Deaths in one section after which every further respawn forces an unsmoothed step down")]
        public int deathStreakForceThreshold = 3;

        [Tooltip("Step multiplier for the forced step down once the death streak threshold is reached")]
        public float deathStreakStepScale = 3f;

        [Tooltip("HP fraction below which a dip counts as a near-death")]
        [Range(0f, 1f)] public float nearDeathHpFraction = 0.25f;

        [Tooltip("HP fraction the player must climb back above before another near-death can be counted")]
        [Range(0f, 1f)] public float nearDeathRearmHpFraction = 0.4f;

        [Tooltip("Near-deaths in one section that count as struggling at the next checkpoint")]
        public int nearDeathCountThreshold = 3;

        [Tooltip("Score sent at the next checkpoint for a section with too many near-deaths")]
        [Range(0f, 1f)] public float nearDeathSectionScore = 0.3f;

        [Tooltip("Average HP fraction (time-weighted) above which a deathless section counts as clean")]
        [Range(0f, 1f)] public float cleanSectionAvgHpFraction = 0.6f;

        [Tooltip("Score sent at the next checkpoint for a clean section")]
        [Range(0f, 1f)] public float cleanSectionScore = 0.85f;

        [Tooltip("Sections shorter than this (seconds) aren't evaluated — avoids spam from closely spaced checkpoints")]
        public float minSectionSecondsForEvaluation = 20f;

        [Header("Puzzle Complexity")]
        [Tooltip("Number of puzzle complexity tiers")]
        public int complexityTiers = 3; // 0 = simple, 1 = medium, 2 = complex

        // ─────────────────────────────────────────────
        // Computed Values
        // ─────────────────────────────────────────────

        /// <summary>
        /// Get the task quantity multiplier for a given difficulty level.
        /// </summary>
        public float GetTaskMultiplier(float difficulty)
        {
            return BlendThroughOne(minTaskMultiplier, maxTaskMultiplier, difficulty);
        }

        /// <summary>
        /// Get the timer multiplier for a given difficulty level.
        /// </summary>
        public float GetTimerMultiplier(float difficulty)
        {
            return BlendThroughOne(minTimerMultiplier, maxTimerMultiplier, difficulty);
        }

        /// <summary>
        /// Get the number of hints for a given difficulty level.
        /// </summary>
        public int GetHintBudget(float difficulty)
        {
            return Mathf.RoundToInt(Mathf.Lerp(maxHints, minHints, difficulty));
        }

        /// <summary>
        /// Get the puzzle complexity tier for a given difficulty level.
        /// </summary>
        public int GetComplexityTier(float difficulty)
        {
            // Clamp so difficulty == 1.0 doesn't overflow into a non-existent tier
            int tier = Mathf.FloorToInt(difficulty * complexityTiers);
            return Mathf.Clamp(tier, 0, complexityTiers - 1);
        }

        /// <summary>Piecewise-linear so difficulty 0.5 always maps to exactly 1.0 (Normal).</summary>
        private static float BlendThroughOne(float atZero, float atOne, float difficulty)
        {
            difficulty = Mathf.Clamp01(difficulty);
            return difficulty <= 0.5f
                ? Mathf.Lerp(atZero, 1f, difficulty / 0.5f)
                : Mathf.Lerp(1f, atOne, (difficulty - 0.5f) / 0.5f);
        }
    }
}
