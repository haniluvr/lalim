using UnityEngine;

namespace Lalim.Abilities
{
    /// <summary>
    /// The swim stamina that Surge and Riptide share (the bar under the HP bar). Pure math, kept
    /// free of Unity lifecycle so it can be unit-tested. Separate from the shells (PlayerStamina),
    /// which pay for Spirit Sense and Tidebond.
    ///
    ///  - The bar is 6 segments and starts full. It never refills on its own.
    ///  - Surges CHARGE it: 1 surge = ⅓ of a segment, a 2-surge combo = ½, a full 3-surge combo
    ///    = 1 whole segment (each surge adds its step as it happens: ⅓, then ⅙, then ½).
    ///  - Riptide burns only WHOLE segments, 10 s of Riptide each: with 4 full segments and a
    ///    third of the 5th it runs 40 s, and that leftover third stays as the new first segment.
    ///    The bar drains as it runs; ending it early keeps whatever it hadn't burnt yet.
    /// </summary>
    public class SwimStaminaPool
    {
        /// <summary>What the n-th surge of a combo adds, so combos total ⅓, ½ and 1 segment.</summary>
        public static readonly float[] ComboCharge = { 1f / 3f, 1f / 6f, 1f / 2f };
        private const float Epsilon = 0.0001f;

        public int Segments { get; private set; }
        public float Current { get; private set; }
        public bool RiptideRunning { get; private set; }

        /// <summary>Whole segments — what Riptide can burn.</summary>
        public int FullSegments => Mathf.FloorToInt(Current + Epsilon);
        public float Normalized => Segments > 0 ? Mathf.Clamp01(Current / Segments) : 0f;

        private float _riptideFloor;

        public SwimStaminaPool(int segments)
        {
            Segments = Mathf.Max(1, segments);
            Current = Segments;
        }

        /// <summary>The charge a surge adds as the <paramref name="comboIndex"/>-th (1-based) of its combo.</summary>
        public static float ChargeForSurge(int comboIndex) =>
            ComboCharge[Mathf.Clamp(comboIndex, 1, ComboCharge.Length) - 1];

        /// <summary>Charges the bar for a surge (capped at full). Nothing while Riptide runs.</summary>
        public void AddSurgeCharge(int comboIndex)
        {
            if (RiptideRunning) return;
            Current = Mathf.Min(Segments, Current + ChargeForSurge(comboIndex));
        }

        /// <summary>Riptide's length for the stamina there is now: whole segments × seconds each.</summary>
        public float RiptideSecondsAvailable(float secondsPerSegment) => FullSegments * secondsPerSegment;

        /// <summary>Starts burning the whole segments; the partly filled one is left alone.</summary>
        public bool BeginRiptide()
        {
            if (RiptideRunning || FullSegments < 1) return false;
            RiptideRunning = true;
            _riptideFloor = Mathf.Max(0f, Current - FullSegments);
            return true;
        }

        public void EndRiptide() => RiptideRunning = false;

        /// <summary>
        /// While Riptide runs, drains one segment per <paramref name="riptideSecondsPerSegment"/>,
        /// never below the leftover partial segment. Otherwise nothing changes.
        /// </summary>
        public void Tick(float deltaTime, float riptideSecondsPerSegment)
        {
            if (deltaTime <= 0f || !RiptideRunning) return;
            Current = Mathf.Max(_riptideFloor, Current - deltaTime / Mathf.Max(0.01f, riptideSecondsPerSegment));
        }

        /// <summary>Resuming a save / respawning.</summary>
        public void SetCurrent(float segments)
        {
            Current = Mathf.Clamp(segments, 0f, Segments);
            RiptideRunning = false;
        }

        public void Refill() => SetCurrent(Segments);
    }
}
