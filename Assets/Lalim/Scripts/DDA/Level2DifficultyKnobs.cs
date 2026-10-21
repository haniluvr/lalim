namespace Lalim.DDA
{
    /// <summary>Knob keys used by the Level 2 MinigameDifficultyProfile assets (Data/Difficulty).</summary>
    public static class Level2DifficultyKnobs
    {
        // Caluya: fish gathering
        public const string Herds = "herds";                     // herds to bring home
        public const string FishPerHerd = "fishPerHerd";
        public const string ConchRadius = "conchRadius";         // metres the call reaches
        public const string ConchCooldown = "conchCooldown";     // seconds between calls
        public const string PredatorSpeed = "predatorSpeed";     // chase speed, m/s (the diver swims 5)
        public const string PredatorSight = "predatorSight";     // metres a predator spots the diver from
        public const string BiteDamage = "biteDamage";

        // Ajuy: selective catching
        public const string NetRadius = "netRadius";             // metres the net opens to
        public const string ThrowCooldown = "throwCooldown";     // seconds between throws
        public const string TargetSpeed = "targetSpeed";         // m/s the catchable schools dart at
        public const string ProtectedPerPool = "protectedPerPool"; // protected animals sharing each pool in the test
        public const string ProtectedSpeed = "protectedSpeed";   // m/s the protected animals drift at
        public const string SchoolSize = "schoolSize";           // fish in each school
        public const string SchoolsToNet = "schoolsToNet";       // schools to net in the Ajuy test (8–25)

        // Padre Burgos: relocation under Bakunawa's chase
        public const string BakunawaSpeed = "bakunawaSpeed";     // chase speed, m/s (the diver swims 5)
        public const string BakunawaSense = "bakunawaSense";     // metres it senses the diver from while resting
        public const string StrikeDamage = "strikeDamage";
        public const string CalmSeconds = "calmSeconds";         // how long the conch calms it
        public const string ReturnSeconds = "returnSeconds";     // seconds of chasing outside Padre Burgos before it goes home

        // Padre Burgos: the moon ritual (appeasing Bakunawa)
        public const string RitualLabels = "ritualLabels";       // 1 = phase names shown over the circles
        public const string RitualReshuffle = "ritualReshuffle"; // 1 = the phases move to new circles after every restart
        public const string RitualFreeHintAfter = "ritualFreeHintAfter"; // restarts in a row before the next circle pulses on its own (0 = never)
    }
}
