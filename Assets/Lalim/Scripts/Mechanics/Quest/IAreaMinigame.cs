namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// The actual puzzle played at an AreaStation (calibration scanner, coral site prep, ghost net…).
    /// The station has already published ChallengeStartedEvent (first person) when Play is called;
    /// the minigame reports tasks/errors to ChallengeManager and finishes by completing or failing
    /// that challenge.
    /// </summary>
    public interface IAreaMinigame
    {
        /// <summary>Number of ChallengeManager tasks this run will report.</summary>
        int TaskCount { get; }

        /// <summary>Expected seconds to finish at Normal difficulty (DDA baseline).</summary>
        float ExpectedSeconds { get; }

        /// <summary>Expected seconds to the first correct action (DDA "pickup rate"; 0 = unknown).</summary>
        float ExpectedFirstActionSeconds { get; }

        /// <summary>Hints offered this run (0 when the minigame has no hint button).</summary>
        int HintBudget { get; }

        /// <summary>The area was finished in an earlier session: show its end state (e.g. the planted coral).</summary>
        void ShowCompletedState();

        void Play(AreaStation station);

        /// <summary>Tear down any UI/visuals (challenge ended, failed, or the player died).</summary>
        void Stop();
    }
}
