using System.Collections.Generic;

namespace Lalim.UI
{
    /// <summary>
    /// The loading screen's starting set of fun facts. The builder copies these into
    /// PF_LoadingScreen, where they stay editable (add, remove or reword them in the Inspector).
    /// Lore follows Level1DialogueScripts; ability numbers follow AbilityConfig.
    /// </summary>
    public static class LoadingFacts
    {
        public const string Deity = "Deity";
        public const string Relic = "Relic";
        public const string Tool = "Tool";
        public const string Mechanic = "Mechanic";
        public const string Ocean = "Ocean fact";
        public const string Tip = "Tip";

        private static LoadingScreen.Fact F(string category, string text) => new LoadingScreen.Fact { category = category, text = text };

        public static List<LoadingScreen.Fact> Default() => new List<LoadingScreen.Fact>
        {
            // Deities
            F(Deity, "Amansinaya is the Tagalog patron of fishermen. Before casting their nets, fisherfolk whistled to her and said \"Kasumpâ akó\": I am your sworn friend."),
            F(Deity, "Fisherfolk never ate their first catch. They gave it back to Amansinaya, so the sea would give again."),
            F(Deity, "Haik is a Tagalog sea spirit that sailors called on for protection from storms at sea."),
            F(Deity, "Neguno, the sagacious ruler of the sea, turned the greedy boat owner Kablay into the very first shark."),

            // Relics
            F(Relic, "The Radiant Conch: fisherfolk blew conches across the water to call one another home. Amansinaya gives it to whoever heals her shallows."),
            F(Relic, "Haik's Compass guided every sailor who called his name, and it never once lied about the way home."),
            F(Relic, "The Woven Net Fragment was woven by honest hands, made to take only what is needed and let the rest swim free."),
            F(Relic, "Relics you earn from Luzon's deities will be needed in the waters of the Visayas."),

            // Tools
            F(Tool, "The Water Sampling Kit reads the water's pH, salinity and temperature. Healthy reef water sits between pH 7.9 and 8.4."),
            F(Tool, "Healthy reef water holds 33 to 37 parts per thousand of salt, and stays between 24 and 29 °C."),
            F(Tool, "Coral Seeds only take root on clean rock. Clear the dead ground before you plant."),
            F(Tool, "Sort trash into plastic, glass and metal. What is sorted properly can be made new again."),

            // Mechanics
            F(Mechanic, "Spirit Sense makes hidden things glow, and a spirit wisp leads you to whatever you need next."),
            F(Mechanic, "Tap Surge for a quick dash. You can chain up to three in a row."),
            F(Mechanic, "Hold the Surge button to ride a Riptide: 1.75× swim speed for a whole minute."),
            F(Mechanic, "Call your dugong with Tidebond and ride it to cross open water faster."),
            F(Mechanic, "Your health never comes back on its own. Seagrass Mend heals you over three seconds."),
            F(Mechanic, "Bubble Barrier charges while you swim and use other skills. While it's up, toxic water can't hurt you."),
            F(Mechanic, "Your dugong has its own health. Keep it out of toxic water, or shelter it inside your Bubble Barrier."),
            F(Mechanic, "Polluted water slowly drains your health, but talking with the deities and playing minigames pauses it."),
            F(Mechanic, "Respawning at a checkpoint restores your health and stamina, so one bad run never follows you."),

            // Ocean facts
            F(Ocean, "Dugongs are the only plant-eating mammals that live entirely in the sea. They graze on seagrass meadows."),
            F(Ocean, "Ghost nets are fishing nets lost or abandoned at sea. They keep trapping animals for years."),
            F(Ocean, "The Philippines lies in the Coral Triangle, home to more kinds of coral and reef fish than anywhere else on Earth."),
            F(Ocean, "When water gets too warm, coral spits out the tiny algae that feed it, turns white, and starts to starve. This is called bleaching."),

            // Tips
            F(Tip, "Lost? The quest box in the top-left corner always shows what to do next. Tap the key to open or close it."),
            F(Tip, "Talk to the statues. Each deity has a task, and a gift for whoever completes it."),
            F(Tip, "Trash can hide behind coral and rocks. Use Spirit Sense to make every piece glow."),
        };
    }
}
