using UnityEngine;

namespace Lalim.Core
{
    /// <summary>The shared sound cues every level reuses, so each one is sourced once.</summary>
    public enum SfxCue
    {
        Correct,           // a right answer: the right bowl, a gauge locked, an animal freed, a clean catch
        Wrong,             // a wrong answer: the wrong bowl, a missed gauge, living coral tapped, a protected catch
        ItemPickup,        // picking something up: trash, a coral fragment, an item
        MinigameStart,
        MinigameComplete,
        MinigameFailed,    // a minigame lost or restarted
        AltarRise,         // the segregation shrines rising (Level 1)
        Reward,            // a gift handed over: the relic appearing, a tool received mid-quest
        Hint,              // a hint used (any minigame's hint bulb)
        ButtonTap,         // any menu button, and the tap that moves a dialogue on (UiTapSound)
        ActTitle,          // the ACT banner as a level opens
        ZoneCleansed,      // a zone's water clears after its quest
        Teleport,          // swimming through an arch in the teleport scene
    }

    /// <summary>
    /// One place for the shared cues (<see cref="SfxCue"/>): a SfxLibrary asset in Resources
    /// (Resources/Audio/SfxLibrary). Minigames call <see cref="Play"/> / <see cref="PlayAt"/> instead
    /// of each holding their own clips; a missing clip is simply silent.
    ///
    /// Every minigame also gets its complete and fail stings for free: this listens to the
    /// ChallengeManager's completed / failed events, whatever the level.
    /// </summary>
    public static class Sfx
    {
        private static SfxLibrary _library;
        private static bool _loaded;

        private static SfxLibrary Library
        {
            get
            {
                if (!_loaded) { _library = Resources.Load<SfxLibrary>("Audio/SfxLibrary"); _loaded = true; }
                return _library;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _library = null;
            _loaded = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ListenToChallenges()
        {
            // Sound marks the moments that matter (a minigame won or lost), not every action:
            // and a hint used; no start sting, and nothing for cooldowns, checkpoints or routine mechanics.
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnFailed);
            EventBus.Unsubscribe<HintRequestedEvent>(OnHint);
            EventBus.Subscribe<ChallengeCompletedEvent>(OnCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnFailed);
            EventBus.Subscribe<HintRequestedEvent>(OnHint);
        }

        private static void OnCompleted(ChallengeCompletedEvent e) => Play(e.Success ? SfxCue.MinigameComplete : SfxCue.MinigameFailed);
        private static void OnFailed(ChallengeFailedEvent e) => Play(SfxCue.MinigameFailed);
        private static void OnHint(HintRequestedEvent e) => Play(SfxCue.Hint);

        /// <summary>Heard "in the head" (UI-style answers, stingers).</summary>
        public static void Play(SfxCue cue, float volume = 1f, bool survivesSceneLoad = false)
        {
            SfxLibrary lib = Library;
            if (lib == null) return;
            GameAudio.PlayFlat(lib.Clip(cue), volume * lib.Volume(cue), AudioChannel.Sfx, survivesSceneLoad);
        }

        /// <summary>From a place in the world (a pickup, something rising).</summary>
        public static void PlayAt(SfxCue cue, Vector3 position, float volume = 1f)
        {
            SfxLibrary lib = Library;
            if (lib == null) return;
            GameAudio.PlayAt(lib.Clip(cue), position, volume * lib.Volume(cue));
        }
    }
}
