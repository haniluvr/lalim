using UnityEngine;

namespace Lalim.Abilities
{
    /// <summary>
    /// Pure stamina math, kept free of Unity lifecycle so it can be unit-tested.
    /// Stored as one float so spending from a partly filled orb keeps that orb's progress
    /// (spending 1 orb at 2.6 leaves 1.6). Only full orbs can be spent. Regen pauses for a
    /// delay after each spend, then fills continuously — which naturally fills orbs one at a time.
    /// </summary>
    public class StaminaPool
    {
        public int MaxOrbs { get; private set; }
        public float Current { get; private set; }
        public float RegenDelayRemaining { get; private set; }
        public int FullOrbs => Mathf.FloorToInt(Current + 0.0001f);

        public StaminaPool(int maxOrbs) : this(maxOrbs, maxOrbs) { }

        /// <summary>Starts with 'startingOrbs' full orbs (clamped to 0…max).</summary>
        public StaminaPool(int maxOrbs, float startingOrbs)
        {
            MaxOrbs = Mathf.Max(1, maxOrbs);
            Current = Mathf.Clamp(startingOrbs, 0f, MaxOrbs);
        }

        public bool CanSpend(int orbs) => FullOrbs >= orbs;

        /// <summary>Spends full orbs and restarts the regen delay. Returns false and changes nothing if too few are full.</summary>
        public bool TrySpend(int orbs, float regenDelaySeconds, bool free = false)
        {
            if (!CanSpend(orbs)) return false;
            if (!free) Current -= orbs;
            RegenDelayRemaining = regenDelaySeconds;
            return true;
        }

        /// <summary>Advances regen by deltaTime. multiplier stacks mounted (1.5×) / toxic (0.5×) factors.</summary>
        public void Tick(float deltaTime, float secondsPerOrb, float multiplier)
        {
            if (RegenDelayRemaining > 0f)
            {
                // Any time left over after the delay expires this frame is still used for regen.
                float leftover = deltaTime - RegenDelayRemaining;
                RegenDelayRemaining = Mathf.Max(0f, RegenDelayRemaining - deltaTime);
                if (leftover <= 0f) return;
                deltaTime = leftover;
            }

            if (Current >= MaxOrbs) return;
            Current = Mathf.Min(MaxOrbs, Current + deltaTime / Mathf.Max(0.01f, secondsPerOrb) * multiplier);
        }

        /// <summary>Resuming a save: stamina exactly as it was.</summary>
        public void SetCurrent(float orbs)
        {
            Current = Mathf.Clamp(orbs, 0f, MaxOrbs);
            RegenDelayRemaining = 0f;
        }

        public void Refill()
        {
            Current = MaxOrbs;
            RegenDelayRemaining = 0f;
        }

        public void SetMax(int maxOrbs)
        {
            MaxOrbs = Mathf.Max(1, maxOrbs);
            Current = Mathf.Min(Current, MaxOrbs);
        }
    }
}
