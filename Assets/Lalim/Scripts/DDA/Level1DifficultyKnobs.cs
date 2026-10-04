namespace Lalim.DDA
{
    /// <summary>Knob keys used by the Level 1 MinigameDifficultyProfile assets (Data/Difficulty).</summary>
    public static class Level1DifficultyKnobs
    {
        // Trash collection
        public const string TrashCount = "count";
        public const string TrashHiddenShare = "hiddenShare";

        // Sorting: 0/1 = a wrong drop names the bowl, 2 = it doesn't (the bowls have no text labels)
        public const string SortLabelMode = "labelMode";

        // Water readings (Sea Sensors gauges)
        public const string BandWidth = "bandWidth";       // green band, as a share of the gauge
        public const string BandSpeed = "bandSpeed";       // needle sweep speed
        public const string NeedleNoise = "needleNoise";   // how much the sweep speed wobbles
        public const string HoldSeconds = "holdSeconds";   // unused since the stop-the-needle gauges
        public const string LockDrain = "lockDrain";       // seconds a missed STOP is locked out

        // Coral site prep
        public const string AlgaeTarget = "algaeTarget";
        public const string Skeletons = "skeletons";
        public const string TapsPerSkeleton = "tapsPerSkeleton";
        public const string Decoys = "decoys";

        // Ghost nets
        public const string Animals = "animals";
        public const string KnotsPerTangle = "knotsPerTangle"; // unused since the one-net puzzle (scramble sets how many knots start out of place)
        public const string Scramble = "scramble";
        public const string Hints = "hints";
    }
}
