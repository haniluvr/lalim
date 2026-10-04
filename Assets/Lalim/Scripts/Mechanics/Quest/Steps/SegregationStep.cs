using UnityEngine;
using Lalim.Challenges;
using Lalim.Core;
using Lalim.DDA;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Zone 1, minigame 2 — trash sorting (1st person). Starts automatically right after the last
    /// piece of trash is collected: no return to the statue. Amansinaya says one line, the camera
    /// switches to first person, three shrines (one bowl each) rise from the sand, and the player
    /// drags exactly what they collected into the bowls.
    /// </summary>
    public class SegregationStep : QuestStep
    {
        [SerializeField] private SegregationAltar _altar;
        [SerializeField] private TrashCollectionStep _collection;
        [Tooltip("Where the player floats while sorting (faces the altar). Optional.")]
        [SerializeField] private Transform _standPoint;
        [SerializeField] private string _barkKey = Level1DialogueScripts.BarkAltarRises;
        [SerializeField, Min(1f)] private float _expectedSecondsPerItem = 5f;
        [Tooltip("Knob: labelMode (0 always, 1 after first mistake, 2 never).")]
        [SerializeField] private MinigameDifficultyProfile _profile;

        private void OnEnable() => EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        private void OnDisable() => EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);

        protected override void OnBegin()
        {
            ObjectiveTracker.Instance?.Clear();

            if (_altar == null || _collection == null || _collection.CollectedItems.Count == 0)
            {
                Debug.LogWarning($"[SegregationStep] {StepId}: missing altar/collection or nothing collected — skipping.");
                Complete();
                return;
            }

            if (_standPoint != null && PlayerController.Instance != null)
            {
                PlayerController.Instance.SetPosition(_standPoint.position);
                PlayerController.Instance.transform.rotation = _standPoint.rotation;
                PlayerController.Instance.SyncFacingToTransform();
            }

            StatueDialogueRouter statue = Zone != null ? Zone.Statue : null;
            // No talk framing: the diver stays at the altar's stand point.
            if (statue != null) statue.PlayBark(_barkKey, StartSorting, frame: false);
            else StartSorting();
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
                HintBudget = 0, // no hint button in this minigame (keeps hint reliance out of its DDA score)
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
            if (!IsRunning || evt.ChallengeId != StepId || !evt.Success) return;
            Complete();
        }

        private void DebugForceComplete()
        {
            if (ChallengeManager.Instance != null && ChallengeManager.Instance.IsChallengeActive) ChallengeManager.Instance.CompleteChallenge(true);
            else Complete();
        }

#if UNITY_EDITOR
        public void EditorSetupProfile(MinigameDifficultyProfile profile) => _profile = profile;

        public void EditorSetupSegregation(SegregationAltar altar, TrashCollectionStep collection, Transform standPoint)
        {
            _altar = altar;
            _collection = collection;
            _standPoint = standPoint;
        }
#endif
    }
}
