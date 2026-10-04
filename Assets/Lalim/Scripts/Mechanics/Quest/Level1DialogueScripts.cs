using System.Collections.Generic;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Level 1 statue scripts (LEVEL1_IMPLEMENTATION_PLAN.md §3.4), grounded in the lore research:
    ///  - Amansinaya: Tagalog patron of fishermen, inventor of fishing gear; fishermen whistled and
    ///    prayed "Kasumpâ akó" before casting nets and never ate the first catch (sinaya).
    ///  - Haik: Tagalog sea being (Boxer Codex) invoked for protection at sea and from storms.
    ///  - Neguno: "sagacious ruler of the sea" who turned greedy Kablay into the first shark.
    /// The scene builder copies these into each StatueDialogueRouter so they stay editable.
    /// {player} is replaced with the saved player name.
    /// </summary>
    public static class Level1DialogueScripts
    {
        public const string Amansinaya = "Amansinaya";
        public const string Haik = "Haik";
        public const string Neguno = "Neguno";

        public const string BarkAltarRises = "altar_rises";
        public const string BarkReadingsDone = "readings_done";
        public const string BarkRelicGift = "relic_gift";

        // Line of each gift on which the relic rises out of the statue (RelicRewardStep).
        public const int AmansinayaRelicLine = 2;
        public const int HaikRelicLine = 2;
        public const int NegunoRelicLine = 1;

        private static DialogueLine N(string speaker, string text) => new DialogueLine { speakerName = speaker, text = text };
        private static DialogueLine P(string text) => new DialogueLine { speakerName = "{player}", text = text, isPlayer = true };

        // ───────────────────────── Amansinaya (zone 1) ─────────────────────────

        public static List<DialogueLine> AmansinayaQuest() => new List<DialogueLine>
        {
            N(Amansinaya, "Hush, little diver. Long ago the fisherfolk whistled to me before they cast their nets, and I answered them with silver shoals."),
            N(Amansinaya, "\"Kasumpâ akó,\" they would say. \"I am your sworn friend.\" And they never ate the first catch. They gave it back, so the sea would give again."),
            P("The water stings… and everything here is so grey."),
            N(Amansinaya, "Now the land sends down what it no longer wants. Plastic, glass, metal. My shallows choke on it, and the water has turned to poison."),
            N(Amansinaya, "Gather every piece you find. Leave nothing behind, not one shard."),
            N(Amansinaya, "If your eyes fail you, awaken the spirit sight. What is hidden will glow, and a light will lead you to it."),
            N(Amansinaya, "Then lay each piece in its rightful bowl. What is sorted truly can be made new again. Go, and be quick. The poison does not wait."),
        };

        public static List<StatueDialogueRouter.NamedLines> AmansinayaBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines
            {
                key = BarkAltarRises,
                lines = new List<DialogueLine>
                {
                    N(Amansinaya, "Every wound lifted. Now: plastic, glass, metal. Choose with care. The sea remembers."),
                }
            },
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = AmansinayaRelicGift() },
        };

        /// <summary>After the sorting: thanks, and the Radiant Conch rises from the statue.</summary>
        public static List<DialogueLine> AmansinayaRelicGift() => new List<DialogueLine>
        {
            N(Amansinaya, "Do you feel it? My shallows taste sweet again."),
            N(Amansinaya, "Every shard lifted, every piece laid in its rightful bowl. You have a fisherfolk's heart, {player}."),
            N(Amansinaya, "Take this, the Radiant Conch. The fisherfolk blew it across the water to call one another home."),
            P("It's glowing… like there's light inside it."),
            N(Amansinaya, "Keep it close. One day, waters far from here will need its call."),
        };

        public static List<DialogueLine> AmansinayaClosing() => new List<DialogueLine>
        {
            N(Amansinaya, "Take this as well. It reads the water's hidden moods: its sourness, its salt, its warmth."),
            N(Amansinaya, "Haik, who once guided sailors through every storm, waits beyond the next gate. His waters are sick in ways the eye cannot see."),
            P("I'll find him."),
            N(Amansinaya, "Remember the fisherfolk's way, {player}. Take only what you need, and give back the first of what you are given."),
        };

        public static List<DialogueLine> AmansinayaIdle() => new List<DialogueLine>
        {
            N(Amansinaya, "The fisherfolk will whistle for me again one day. Go on, Haik is waiting."),
        };

        // ───────────────────────── Haik (zone 2) ─────────────────────────

        public static List<DialogueLine> HaikQuest() => new List<DialogueLine>
        {
            N(Haik, "A traveller… Few find their way to me now. Sailors once called my name before every voyage, and I calmed the storms in their path."),
            N(Haik, "But no storm has frightened me like this one. It has no wind, no thunder. It hides inside the water itself."),
            P("Amansinaya gave me this kit. She said your waters are sick in ways I can't see."),
            N(Haik, "Then read them the way a navigator reads the sky. Three places in my waters speak loudest. Test each one."),
            N(Haik, "Healthy reef water is only a little bitter: a pH between 7.9 and 8.4. Its salt sits between 33 and 37 parts per thousand."),
            N(Haik, "It is warm, but never hot: 24 to 29 degrees. Any warmer and the coral sickens and turns white."),
            N(Haik, "Stop each needle when it sits in the green. Only a true reading can guide a true course."),
        };

        public static List<StatueDialogueRouter.NamedLines> HaikBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines
            {
                key = BarkReadingsDone,
                lines = new List<DialogueLine>
                {
                    N(Haik, "The water steadies under your hands. Sourness, salt and warmth, all as they should be once more."),
                    N(Haik, "Now the reef can heal. Take these seeds. Clear the dead ground at each place you tested, then plant them where the rock is clean."),
                }
            },
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = HaikRelicGift() },
        };

        /// <summary>After the coral replanting: thanks, and the Compass rises from the statue.</summary>
        public static List<DialogueLine> HaikRelicGift() => new List<DialogueLine>
        {
            N(Haik, "The young coral holds. One day fish will shelter here again, and sailors will follow them home."),
            N(Haik, "You read my waters truly, traveller, and then you healed what you read."),
            N(Haik, "Take my compass. Every sailor who called my name steered by it, and it never once lied about the way home."),
            P("I'll keep it safe."),
            N(Haik, "Keep it steady, too. A day will come when you must find your way through waters no one has charted."),
        };

        public static List<DialogueLine> HaikClosing() => new List<DialogueLine>
        {
            N(Haik, "Go deeper, traveller. Neguno rules the waters past the last gate. He is not as gentle as I am."),
        };

        public static List<DialogueLine> HaikIdle() => new List<DialogueLine>
        {
            N(Haik, "Steady hands, true readings. Neguno is waiting below."),
        };

        // ───────────────────────── Neguno (zone 3) ─────────────────────────

        public static List<DialogueLine> NegunoQuest() => new List<DialogueLine>
        {
            N(Neguno, "So. A child of the land comes to my waters. Do you know what happened to the last one who came here only to take?"),
            P("…No."),
            N(Neguno, "His name was Kablay. He owned many boats, and rented them to poor fishers for more than they could ever pay."),
            N(Neguno, "When famine came, an old beggar knocked on his door. Kablay told him to go and jump into the sea."),
            N(Neguno, "The beggar was me. That night my storm took his house, and the sea took Kablay. He became the first shark."),
            N(Neguno, "Kablay is gone, but his greed is not. Look: nets cast and abandoned, still fishing with no one left to haul them in."),
            N(Neguno, "My creatures are tangled in them. Undo the knots, free every one, and pull those nets out of my sea."),
            N(Neguno, "Do this, and I will open the deep path."),
        };

        public static List<StatueDialogueRouter.NamedLines> NegunoBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = NegunoRelicGift() },
        };

        /// <summary>After the ghost nets: thanks, and the Woven Net Fragment rises from the statue.</summary>
        public static List<DialogueLine> NegunoRelicGift() => new List<DialogueLine>
        {
            N(Neguno, "Free. Every one of them. You came to give, not to take. That is rarer than pearls."),
            N(Neguno, "Here. A piece of a net woven by honest hands, made to take only what is needed and let the rest swim free."),
            N(Neguno, "Kablay never understood the difference. Carry it, and remember it."),
            P("I will."),
        };

        public static List<DialogueLine> NegunoClosing() => new List<DialogueLine>
        {
            N(Neguno, "The deep path is open. Swim down, {player}. Other waters are waiting for someone like you."),
        };

        public static List<DialogueLine> NegunoIdle() => new List<DialogueLine>
        {
            N(Neguno, "The deep path is open. Do not keep it waiting."),
        };
    }
}
