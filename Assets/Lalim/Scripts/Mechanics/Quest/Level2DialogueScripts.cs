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
        public const string TeacherThrow = "Lakan";        // lesson 1, the throw (a man)
        public const string TeacherSense = "Amihan";       // lesson 2, identify (a woman)
        public const string TeacherPatience = "Inang Sela"; // lesson 3, patience
        public const string TeacherWatch = "Dalisay";      // watches the fourth pool in the test

        public const string BarkReport = "report";
        /// <summary>The second talk, after the relic is equipped: the spirit gives the quest itself.</summary>
        public const string BarkQuestGive = "quest_give";
        public const string BarkRelicGift = RelicRewardStep.GiftBarkKey;

        /// <summary>Line of each gift on which the reward rises into view (RelicRewardStep).</summary>
        public const int RewardLine = 1;

        private static DialogueLine N(string speaker, string text) => new DialogueLine { speakerName = speaker, text = text };
        private static DialogueLine P(string text) => new DialogueLine { speakerName = "{player}", text = text, isPlayer = true };

        // ───────────────────────── Manunubo (zone 1) ─────────────────────────

        /// <summary>Talk 1: who Manunubo is, how the sea fell, the scattered herds, and the Radiant
        /// Conch. Ends by sending the diver to equip it (then talk 2, <see cref="ManunuboQuestGive"/>).
        /// The thing in the deep is not named yet.</summary>
        public static List<DialogueLine> ManunuboQuest() => new List<DialogueLine>
        {
            N(Manunubo, "A diver… and a dugong at your side. Come closer, child of the surface. My light is weak tonight."),
            N(Manunubo, "I am Manunubo. I watch over these waters: the reefs, the seagrass, and every life that swims in them."),
            N(Manunubo, "Centuries ago, this sea was clear as moonlight. The reefs shone, and the fish ran in shoals so great they shadowed the sand."),
            N(Manunubo, "Then the surface began to give us its waste. Nets that never rot. Poison carried down the rivers. Fire that shatters the coral."),
            N(Manunubo, "Year after year the water turned. And something old in the deep, something that loved this sea before any of us, grew angry."),
            N(Manunubo, "The sea has not known peace since. Now that anger has grown so great that when it stirred in its sleep, the whole sea shook."),
            N(Manunubo, "My shoals broke apart in terror. They are scattered across the open sand, lost, where the sharks hunt."),
            P("What is down there?"),
            N(Manunubo, "Not yet. But the oldest songs of the sea remember: an anger of the deep is not fought. It is soothed with a ritual, a song sung back to it one face of the moon at a time."),
            N(Manunubo, "You carry the Radiant Conch, Amansinaya's gift. I can feel its song from here."),
            N(Manunubo, "Its call means home. Blow it near my lost fish, and they will hear it, calm, and follow you."),
            N(Manunubo, "You will need it for what I ask of you. Equip it from your inventory, then come back to me."),
        };

        /// <summary>Talk 2, once the conch is equipped: the herding quest.</summary>
        public static List<DialogueLine> ManunuboQuestGive() => new List<DialogueLine>
        {
            N(Manunubo, "Good. I can hear it humming in your hands."),
            N(Manunubo, "My shoals are scattered across the sand. Find them, blow the conch, and lead them back to my light."),
            N(Manunubo, "Mind the sharks. When they come, slip into the seaweed or the narrow rocks, wherever you see the closed eye, and keep still."),
            N(Manunubo, "Or swim beside your dugong. The hunters will take you both for one of my shoal."),
            P("I'll bring them home."),
            N(Manunubo, "Go, {player}. And keep the conch close. Its song will be needed again, deeper than you think."),
        };

        public static List<DialogueLine> ManunuboReport() => new List<DialogueLine>
        {
            N(Manunubo, "They are home. Can you see? My light grows stronger with every school."),
        };

        public static List<DialogueLine> ManunuboGift() => new List<DialogueLine>
        {
            N(Manunubo, "You swam where the hunters swim and never once left a fish behind."),
            N(Manunubo, "Take this gift: Moontide. The tides that carried my shoals will show themselves to you, and carry you as they carried them."),
            P("I can see the currents… the water is moving with me."),
        };

        public static List<DialogueLine> ManunuboClosing() => new List<DialogueLine>
        {
            N(Manunubo, "East, through the channel, the Kataw keep their village in the roots. They will teach you what my shoals cannot."),
            N(Manunubo, "Look: the tide is already moving east, silver in the dark. Swim into it, and it will carry you."),
        };

        public static List<DialogueLine> ManunuboIdle() => new List<DialogueLine>
        {
            N(Manunubo, "My shoals rest easy now. Go safely."),
        };

        public static List<StatueDialogueRouter.NamedLines> ManunuboBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines { key = BarkQuestGive, lines = ManunuboQuestGive() },
            new StatueDialogueRouter.NamedLines { key = BarkReport, lines = ManunuboReport() },
            new StatueDialogueRouter.NamedLines { key = BarkRelicGift, lines = ManunuboGift() },
        };

        // ───────────────────────── Kataw elder (zone 2) ─────────────────────────

        /// <summary>Talk 1: the village, careless fishing, and the Woven Net Fragment. Ends by sending
        /// the diver to equip it (then talk 2, <see cref="KatawQuestGive"/>).</summary>
        public static List<DialogueLine> KatawQuest() => new List<DialogueLine>
        {
            N(KatawElder, "Welcome to our village, little diver. Few from the dry world swim this far into the roots."),
            N(KatawElder, "Manunubo sent word of you. You brought the scattered shoals home."),
            N(KatawElder, "The fishers above take everything their nets touch: the young, the rare, the ones the sea cannot spare."),
            P("Then how should anyone fish?"),
            N(KatawElder, "With care. Take only what is needed, and let the rest swim free."),
            N(KatawElder, "You carry the Woven Net Fragment, Neguno's gift. Thrown well, it opens only where you aim, and closes around nothing else."),
            N(KatawElder, "You will need it for what we ask. Equip it from your inventory, then come back to me."),
        };

        /// <summary>Talk 2, once the net is equipped: the eight schools and the lessons.</summary>
        public static List<DialogueLine> KatawQuestGive() => new List<DialogueLine>
        {
            N(KatawElder, "Good. Now listen."),
            N(KatawElder, "Eight schools in our pools are not safe here. They must go to safer waters, but only them, and only with care."),
            N(KatawElder, "Our fishers will teach you first: Lakan the throw, Amihan the seeing, Inang Sela the waiting."),
            N(KatawElder, "Find Lakan at the western Root Pool. The tide will carry you there."),
            P("I'm ready to learn."),
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

        /// <summary>The end of Ajuy: equip the Compass for the relocation, and a warning about the serpent.</summary>
        public static List<DialogueLine> KatawClosing() => new List<DialogueLine>
        {
            N(KatawElder, "The schools in your net-bag cannot stay in Ajuy. Each belongs in a home of its own, somewhere in these waters."),
            N(KatawElder, "Equip the Compass. It will lead you to the sanctuaries, so you can bring each school safely to where it belongs."),
            N(KatawElder, "But hear me. Beyond the trench the sea falls into the deep, and a great serpent rests there."),
            N(KatawElder, "It is the anger Manunubo spoke of. Swim softly, and do not disturb it."),
        };

        public static List<DialogueLine> KatawIdle() => new List<DialogueLine>
        {
            N(KatawElder, "The roots are quiet. That is how it should be."),
        };

        public static List<StatueDialogueRouter.NamedLines> KatawBarks() => new List<StatueDialogueRouter.NamedLines>
        {
            new StatueDialogueRouter.NamedLines { key = BarkQuestGive, lines = KatawQuestGive() },
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
            N(KatawElder, "Eight schools must leave Ajuy for safer waters: the tuna, the clownfish, the rabbitfish, the blue tang, the squid, the mud crabs, the lobsters and the eels. They wait in our pools."),
            N(KatawElder, "Bring them all into your net-bag, and harm none of the protected ones."),
            N(KatawElder, "Net three of them and we will start again. Go."),
        };

        // ── After each lesson: what it meant, and where to go next (talk to the teacher again) ──

        /// <summary>
        /// The teacher's word once the lesson's catch is done. <paramref name="nextWho"/> /
        /// <paramref name="nextWhere"/> name the next teacher and pool; empty after the last lesson
        /// (then the diver goes back to the Elder).
        /// </summary>
        public static List<DialogueLine> LessonDebrief(int lesson, string teacher, string nextWho, string nextWhere)
        {
            var lines = new List<DialogueLine>();
            switch (lesson)
            {
                case 0:
                    lines.Add(N(teacher, "You see? The net closed only around the fish inside its ring. Nothing else was touched."));
                    lines.Add(N(teacher, "A careful throw takes only what you mean to take. A careless one takes everything near it."));
                    break;
                case 1:
                    lines.Add(N(teacher, "Green, and only green. The pagi, the taklobo and the pawikan are few, and the sea needs every one of them."));
                    lines.Add(N(teacher, "Before you throw, look. Your Spirit Sense will always tell you what may be taken."));
                    break;
                default:
                    lines.Add(N(teacher, "You waited for the water to clear. That is how a protected life stays safe beside your catch."));
                    lines.Add(N(teacher, "No catch is worth a life the sea cannot spare. Patience is the last thing a fisher learns, and the most important."));
                    break;
            }
            if (!string.IsNullOrEmpty(nextWho))
                lines.Add(N(teacher, $"Now go to {nextWho} at {nextWhere}. The tide will carry you there."));
            else
                lines.Add(N(teacher, "You have learned all three. Return to the Elder in the village square."));
            lines.Add(P("Thank you. I'll remember."));
            return lines;
        }

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
        public const string CalloutLastSchool = "That's all eight. Come back to the Elder.";

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
            N(Bakunawa, "You woke me, little one."),
            N(Bakunawa, "Swim, {player}. Swim as fast as you can."),
            N(Bakunawa, "Before I catch you."),
            N(Bakunawa, "Swim."),
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
