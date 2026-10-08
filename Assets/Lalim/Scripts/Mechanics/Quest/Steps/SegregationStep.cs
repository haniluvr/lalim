using UnityEngine;
using Lalim.Challenges;
using Lalim.Core;
using Lalim.DDA;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Zone 1, minigame 2 — trash sorting (1st person). After the talk with Amansinaya that follows
    /// the trash collection (she hands over the Water Sampling Kit and explains the bowls), the
    /// player swims to the altar: its SortingStation shows the interact button, and tapping it
    /// starts the sorting — the diver is placed at the altar, the camera switches to first person,
    /// three shrines (one bowl each) rise from the sand, and the player drags exactly what they
    /// collected into the bowls. Without a station it starts on its own once the talk's shot is
    /// released. Optionally a bark plays first (none in Level 1: the talk before it explains).
    /// </summary>
    public class SegregationStep : QuestStep
    {
        [SerializeField] private SegregationAltar _altar;
        [SerializeField] private TrashCollectionStep _collection;
        [Tooltip("Where the player floats while sorting (faces the altar). Optional.")]
        [SerializeField] private Transform _standPoint;
        [Tooltip("The altar's interact point: sorting starts when the player taps it. Empty = starts on its own.")]
        [SerializeField] private SortingStation _station;
        [Tooltip("Optional line the statue says before sorting starts (empty = none).")]
        [SerializeField] private string _barkKey = "";
        [SerializeField, Min(1f)] private float _expectedSecondsPerItem = 5f;
        [Tooltip("Knob: labelMode (0 always, 1 after first mistake, 2 never).")]
        [SerializeField] private MinigameDifficultyProfile _profile;

        /// <summary>Hints on the sorting card's bulb (DDA knob "hints", else 2).</summary>
        private int HintBudget => _profile != null && _profile.Has(Level1DifficultyKnobs.Hints) ? Mathf.Max(0, _profile.GetInt(Level1DifficultyKnobs.Hints)) : 2;

        private void OnEnable()
        {
            EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Subscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
            EventBus.Unsubscribe<ChallengeFailedEvent>(OnChallengeFailed);
        }

        protected override void OnBegin()
        {
            ObjectiveTracker.Instance?.Clear();

            if (_altar == null || _collection == null || _collection.CollectedItems.Count == 0)
            {
                Debug.LogWarning($"[SegregationStep] {StepId}: missing altar/collection or nothing collected — skipping.");
                Complete();
                return;
            }

            if (_station != null)
            {
                // The player swims over and taps the altar (the music and Spirit Sense lead there).
                ObjectiveTracker.Instance?.SetObjective(_station.transform);
                _station.Arm(() => StartCoroutine(BeginWhenTalkReleased()));
            }
            else StartCoroutine(BeginWhenTalkReleased());
        }

        // Right after a conversation the statue's shot is still up and fades back to the diver;
        // move to the altar only once it has (while the screen is still black), so the two don't fight.
        private System.Collections.IEnumerator BeginWhenTalkReleased()
        {
            float until = Time.unscaledTime + 3f;
            while (Time.unscaledTime < until &&
                   ((Lalim.UI.DialogueManager.Instance != null && Lalim.UI.DialogueManager.Instance.IsOpen) ||
                    (CameraController.Instance != null && CameraController.Instance.Shot != null)))
                yield return null;

            ObjectiveTracker.Instance?.Clear();

            // Into first person through black (as for a conversation), not a camera swoop.
            Lalim.UI.ScreenFader fader = Lalim.UI.ScreenFader.Instance;
            yield return fader.FadeOut(MinigameStage.FadeSeconds);

            if (_standPoint != null && PlayerController.Instance != null)
            {
                PlayerController.Instance.SetPosition(_standPoint.position);
                PlayerController.Instance.transform.rotation = _standPoint.rotation;
                PlayerController.Instance.SyncFacingToTransform();
            }

            StatueDialogueRouter statue = Zone != null ? Zone.Statue : null;
            // No talk framing: the diver stays at the altar's stand point.
            if (statue != null && !string.IsNullOrEmpty(_barkKey)) statue.PlayBark(_barkKey, StartSorting, frame: false);
            else StartSorting();

            CameraController.Instance?.SnapToFirstPerson();
            yield return fader.FadeIn(MinigameStage.FadeSeconds);
        }

        private void StartSorting()
        {
            int count = _collection.CollectedItems.Count;
            EventBus.Publish(new ChallengeStartedEvent
            {
                Type = ChallengeType.WasteSegregation,
                ChallengeId = StepId,
                TaskQuantity = count,
                TimeLimit = 0f,
                HintBudget = HintBudget,
                Perspective = ChallengePerspective.FirstPerson
            });
            ChallengeManager.Instance?.SetExpectedCompletionTime(count * _expectedSecondsPerItem);
            if (_profile != null)
            {
                ChallengeManager.Instance?.SetExpectedFirstActionTime(_profile.expectedFirstActionSeconds);
                if (_profile.Has(Level1DifficultyKnobs.SortLabelMode)) _altar.SetLabelMode(_profile.GetInt(Level1DifficultyKnobs.SortLabelMode));
            }
            _altar.Begin(_collection.CollectedItems, StepId);
            CameraController.Instance?.AimFirstPersonAt(_altar.ViewTarget);   // the bowls up top, the trash row underneath
        }

        /// <summary>Resuming past this step: the shrines are already up.</summary>
        public override void RestoreCompleted()
        {
            if (_altar != null) _altar.ShowRisen();
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (!IsRunning || evt.ChallengeId != StepId) return;
            if (evt.Success) Complete();
            else Rearm();
        }

        // Interrupted (e.g. the player died): the altar waits to be tapped again.
        private void OnChallengeFailed(ChallengeFailedEvent evt)
        {
            if (IsRunning && evt.ChallengeId == StepId) Rearm();
        }

        private void Rearm()
        {
            if (_station == null) return;
            ObjectiveTracker.Instance?.SetObjective(_station.transform);
            _station.Arm(() => StartCoroutine(BeginWhenTalkReleased()));
        }

        private void DebugForceComplete()
        {
            _station?.Disarm();
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.CompleteChallenge(true);
            else Complete();
        }

#if UNITY_EDITOR
        public void EditorSetupProfile(MinigameDifficultyProfile profile) => _profile = profile;

        public void EditorSetStation(SortingStation station) => _station = station;

        public void EditorSetupSegregation(SegregationAltar altar, TrashCollectionStep collection, Transform standPoint)
        {
            _altar = altar;
            _collection = collection;
            _standPoint = standPoint;
            _barkKey = "";   // the talk before sorting already explains it
        }
#endif
    }
}
