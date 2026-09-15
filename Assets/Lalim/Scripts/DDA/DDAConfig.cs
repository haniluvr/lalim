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

        [Header("Task Quantity Scaling")]
        [Tooltip("Base task quantity multiplier at minimum difficulty")]
        public float minTaskMultiplier = 0.6f;

        [Tooltip("Base task quantity multiplier at maximum difficulty")]
        public float maxTaskMultiplier = 2.0f;

        [Header("Timer Scaling")]
        [Tooltip("Timer multiplier at minimum difficulty (generous = higher)")]
        public float minTimerMultiplier = 1.5f;

        [Tooltip("Timer multiplier at maximum difficulty (strict = lower)")]
        public float maxTimerMultiplier = 0.6f;

        [Header("Hint Scaling")]
        [Tooltip("Hints available at minimum difficulty")]
        public int maxHints = 5;

        [Tooltip("Hints available at maximum difficulty")]
        public int minHints = 1;

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
            return Mathf.Lerp(minTaskMultiplier, maxTaskMultiplier, difficulty);
        }

        /// <summary>
        /// Get the timer multiplier for a given difficulty level.
        /// </summary>
        public float GetTimerMultiplier(float difficulty)
        {
            return Mathf.Lerp(minTimerMultiplier, maxTimerMultiplier, difficulty);
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
            return Mathf.FloorToInt(difficulty * complexityTiers);
        }
    }
}
