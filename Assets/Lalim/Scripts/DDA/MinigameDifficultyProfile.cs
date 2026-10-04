using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lalim.DDA
{
    /// <summary>
    /// Per-minigame difficulty knobs with Easy / Normal / Hard values. The current DDA difficulty
    /// (0 easiest … 0.5 normal … 1 hardest) blends between them, so designers tune in the Inspector.
    /// Also holds the minigame's DDA baselines (expected completion time, expected first action).
    /// </summary>
    [CreateAssetMenu(fileName = "MinigameDifficulty", menuName = "Lalim/Minigame Difficulty Profile")]
    public class MinigameDifficultyProfile : ScriptableObject
    {
        [Serializable]
        public class Knob
        {
            public string key;
            public float easy;
            public float normal;
            public float hard;

            public Knob() { }
            public Knob(string key, float easy, float normal, float hard)
            {
                this.key = key; this.easy = easy; this.normal = normal; this.hard = hard;
            }
        }

        [Header("DDA baselines (Normal difficulty)")]
        [Min(1f)] public float expectedSeconds = 60f;
        [Min(0.5f)] public float expectedFirstActionSeconds = 6f;

        [Header("Knobs")]
        public List<Knob> knobs = new List<Knob>();

        /// <summary>Knob value at the current DDA difficulty.</summary>
        public float Get(string key)
        {
            float d = DDAManager.Instance != null ? DDAManager.Instance.DifficultyLevel : 0.5f;
            return Get(key, d);
        }

        /// <summary>Knob value at a given difficulty (piecewise linear through Normal at 0.5).</summary>
        public float Get(string key, float difficulty)
        {
            foreach (Knob k in knobs)
            {
                if (k.key != key) continue;
                float t = Mathf.Clamp01(difficulty);
                return t <= 0.5f ? Mathf.Lerp(k.easy, k.normal, t / 0.5f) : Mathf.Lerp(k.normal, k.hard, (t - 0.5f) / 0.5f);
            }
            Debug.LogWarning($"[MinigameDifficulty] {name}: no knob '{key}'");
            return 0f;
        }

        public int GetInt(string key) => Mathf.RoundToInt(Get(key));
        public bool GetBool(string key) => Get(key) >= 0.5f;

        public bool Has(string key) => knobs.Exists(k => k.key == key);

        /// <summary>Adds a knob only if it's missing (editor setup keeps tuned values).</summary>
        public void EnsureKnob(string key, float easy, float normal, float hard)
        {
            if (!Has(key)) knobs.Add(new Knob(key, easy, normal, hard));
        }
    }
}
