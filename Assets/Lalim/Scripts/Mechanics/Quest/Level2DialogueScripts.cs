using System.Collections.Generic;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Level 2 (Visayas) spirit scripts. PLACEHOLDER lines: the real script is not written yet.
    ///  - Manunubo (Caluya): guardian spirit of the sea-herds, a wisp of light; gathering minigame.
    ///  - Kataw elder (Ajuy): the merfolk of the root village; teaches selective catching.
    ///  - Bakunawa (Padre Burgos): the moon-eating serpent, angry at being woken; the relocation chase.
    /// Copied into each spirit's StatueDialogueRouter so they stay editable in the scene.
    /// {player} is replaced with the saved player name.
    /// </summary>
    public static class Level2DialogueScripts
    {
        public const string Manunubo = "Manunubo";
        public const string KatawElder = "Kataw Elder";
        public const string Bakunawa = "Bakunawa";

        // The Kataw who teach at the Root Pools (Minigame 2).
        public const string TeacherThrow = "Amihan";       // lesson 1, the throw
        public const string TeacherSense = "Lakan";        // lesson 2, identify
        public const string TeacherPatience = "Inang Sela"; // lesson 3, patience
        public const string TeacherWatch = "Dalisay";      // watches the fourth pool in the test

        public const string BarkReport = "report";
        public const string BarkRelicGift = RelicRewardStep.GiftBarkKey;

        /// <summary>Line of each gift on which the reward rises into view (RelicRewardStep).</summary>
        public const int RewardLine = 1;

        private static DialogueLine N(string speaker, string text) => new DialogueLine { speakerName = speaker, text = text };
        private static DialogueLine P(string text) => new DialogueLine { speakerName = "{player}", text = text, isPlayer = true };

        // ───────────────────────── Manunubo (zone 1) ─────────────────────────

        public static List<DialogueLine> ManunuboQuest() => new List<DialogueLine>
        {
            N(Manunubo, "A diver, and a dugong beside you… Come closer. My light is weak tonight."),
            N(Manunubo, "I am Manunubo. I keep the herds of these waters. But the sharks prowl the open sand, and my fish have scattered in fear."),
            P("How can I help them?"),
            N(Manunubo, "You carry a conch that sings of home. Blow it, and the lost ones will hear you and follow."),
            N(Manunubo, "Stay in the seaweed and the narrow rocks when the hunters pass. Ride with your dugong and they will take you for one of my herd."),
            N(Manunubo, "Bring my herds back to me, {player}."),
        };

        public static List<DialogueLine> ManunuboReport() => new List<DialogueLine>
        {
            N(Manunubo, "They are home. Can you see? My light grows stronger with every herd."),
        };

        public static List<DialogueLine> ManunuboGift() => new List<DialogueLine>
        {
            N(Manunubo, "You swam where the hunters swim and never once left a fish behind."),
            N(Manunubo, "Take this gift: Moontide. The tides that carried my herds will show themselves to you, and carry you as they carried them."),
            P("I can see the currents… the water is moving with me."),
        };

        public static List<DialogueLine> ManunuboClosing() => new List<DialogueLine>
        {
            N(Manunubo, "East, through the channel, the Kataw keep their village in the roots. They will teach you what my herds cannot."),
            N(Manunubo, "Look: the tide is already moving east, silver in the dark. Swim into it, and it will carry you."),
        };

        public static List<DialogueLine> ManunuboIdle() => new List<DialogueLine>
        {
            N(Manunubo, "My herds rest easy now. Go safely."),
        };

        public static List<StatueDialogueRouter.NamedLines> ManunuboBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines { key = BarkReport, lines = ManunuboReport() },
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = ManunuboGift() },
        };

        // ───────────────────────── Kataw elder (zone 2) ─────────────────────────

        public static List<DialogueLine> KatawQuest() => new List<DialogueLine>
        {
            N(KatawElder, "Welcome to our village, little diver. Few from the dry world swim this far into the roots."),
            N(KatawElder, "The fishers above take everything their nets touch. The young, the rare, the ones the sea cannot spare."),
            P("Then how should anyone fish?"),
            N(KatawElder, "With care. Take only what is needed, and let the rest swim free. Your woven net knows the way, if you do."),
            N(KatawElder, "Our fishers will teach you. Learn to see which fish may be taken, and which must be left in peace."),
        };

        public static List<DialogueLine> KatawReport() => new List<DialogueLine>
        {
            N(KatawElder, "Every school taken with care, and not one protected life harmed. You have learned well."),
        };

        public static List<DialogueLine> KatawGift() => new List<DialogueLine>
        {
            N(KatawElder, "The village has something for you."),
            N(KatawElder, "Keep it close. It was made by many hands."),
            P("Thank you. I'll keep it safe."),
        };

        public static List<DialogueLine> KatawClosing() => new List<DialogueLine>
        {
            N(KatawElder, "The fish you carry need new homes. But beyond the trench lies the deep, and something sleeps there that does not like to be woken."),
        };

        public static List<DialogueLine> KatawIdle() => new List<DialogueLine>
        {
            N(KatawElder, "The roots are quiet. That is how it should be."),
        };

        public static List<StatueDialogueRouter.NamedLines> KatawBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines { key = BarkReport, lines = KatawReport() },
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = KatawGift() },
        };

        // ───────────────────────── Kataw lessons (Minigame 2) ─────────────────────────

        public static List<DialogueLine> LessonThrow() => new List<DialogueLine>
        {
            N(TeacherThrow, "So the Elder sent you to me. Good. Show me that net of yours."),
            N(TeacherThrow, "Hold it to aim. The light shows the path it will fly, and the ring is where it opens."),
            N(TeacherThrow, "Let go, and it closes around whatever is inside that ring. Nothing more."),
            N(TeacherThrow, "There, one fish alone by the roots. Take it."),
        };

        public static List<DialogueLine> LessonSense() => new List<DialogueLine>
        {
            N(TeacherSense, "Not every fish is ours to take. Some the sea cannot spare."),
            N(TeacherSense, "Open your Spirit Sense. What glows green may be taken. What glows red must be left in peace."),
            P("And if I net one by mistake?"),
            N(TeacherSense, "Then you let it go, gently, and you try again. Find the green school."),
        };

        public static List<DialogueLine> LessonPatience() => new List<DialogueLine>
        {
            N(TeacherPatience, "Patience, child. The pawikan feeds in these roots, and it hurries for no one."),
            N(TeacherPatience, "It will drift between you and your school. When it does, wait."),
            N(TeacherPatience, "Throw only when the water between you is clear."),
        };

        public static List<DialogueLine> CatchingTest() => new List<DialogueLine>
        {
            N(KatawElder, "The throw, the seeing and the waiting. You have all three now."),
            N(KatawElder, "Four schools must leave Ajuy for safer waters: the tuna, the clownfish, the reef fish and the squid. One waits in each pool."),
            N(KatawElder, "Bring them all into your net-bag, and harm none of the protected ones."),
            N(KatawElder, "Net three of them and we will start again. Go."),
        };

        public static List<DialogueLine> TeacherIdle(string teacher) => new List<DialogueLine>
        {
            N(teacher, "Take only what is needed. The rest of the sea will thank you."),
        };

        // Short comments during the throws (CalloutUI; play does not stop).
        public const string CalloutLessonThrowDone = "Clean. The net closes only around what it touches.";
        public const string CalloutLessonSenseDone = "Green, and only green. You see it now.";
        public const string CalloutLessonPatienceDone = "You waited. That is the whole lesson.";
        public const string CalloutMiss = "Nothing. Watch where the ring falls.";
        public const string CalloutUseSense = "Use your Spirit Sense first. Green may be taken, red may not.";
        public const string CalloutSpoiled = "A protected one was in there too. The whole net goes free.";
        public const string CalloutTestRestart = "Three protected lives in your net. Let them all go. We start again.";
        public const string CalloutSchoolTaken = "Into the bag. {0} to go.";
        public const string CalloutLastSchool = "That's all four. Come back to the Elder.";

        /// <summary>What the teachers say when a protected animal is netted (and released).</summary>
        public static string CalloutProtected(string species)
        {
            switch (species)
            {
                case "turtle": return "That's a pawikan! Let it go, gently.";
                case "stingray": return "A pagi. Release it, it is not ours to take.";
                case "clam": return "The taklobo stays where it lives. Let it be.";
                default: return "That one is protected. Let it go.";
            }
        }

        // ───────────────────────── Bakunawa (zone 3) ─────────────────────────

        public static List<DialogueLine> BakunawaQuest() => new List<DialogueLine>
        {
            N(Bakunawa, "Who dares swim into my sleep?"),
            N(Bakunawa, "For centuries your kind has poured its filth into my sea. You blast the reefs, you strip the shores, you take and take."),
            P("We're trying to put things right. These fish need a safe home."),
            N(Bakunawa, "A safe home? There is no safe home while your kind still breathes."),
            N(Bakunawa, "You woke me, little one. Now swim."),
        };

        public static List<DialogueLine> BakunawaReport() => new List<DialogueLine>
        {
            N(Bakunawa, "…You came back. And the shoals are home."),
        };

        public static List<DialogueLine> BakunawaGift() => new List<DialogueLine>
        {
            N(Bakunawa, "That song… it is the sound of a sea at rest. I have not heard it in a long time."),
            N(Bakunawa, "Take this. A moon I swallowed long ago, the Pearl of Luminescence. Let it light the dark for you."),
            P("It's glowing… like moonlight underwater."),
        };

        public static List<DialogueLine> BakunawaClosing() => new List<DialogueLine>
        {
            N(Bakunawa, "Go, before I change my mind. The way east is open."),
        };

        public static List<DialogueLine> BakunawaIdle() => new List<DialogueLine>
        {
            N(Bakunawa, "Let me sleep."),
        };

        public static List<StatueDialogueRouter.NamedLines> BakunawaBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines { key = BarkReport, lines = BakunawaReport() },
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = BakunawaGift() },
        };
    }
}
