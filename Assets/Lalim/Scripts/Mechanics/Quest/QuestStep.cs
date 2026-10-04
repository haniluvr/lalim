using System;
using UnityEngine;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// One step of a zone's quest (talk to the statue, a minigame, grant a tool, cleanse the zone…).
    /// The LevelFlowDirector runs steps in order. A finished step's StepId is saved, so on reload the
    /// director calls RestoreCompleted() on finished steps and Begin() on the first unfinished one.
    /// </summary>
    public abstract class QuestStep : MonoBehaviour
    {
        [Tooltip("Unique, stable ID saved in completedChallengeIds (e.g. luzon_z1_trash).")]
        [SerializeField] private string _stepId;

        [Tooltip("Shown in the quest objective box while this step runs (empty = keep the previous text).")]
        [SerializeField, TextArea(1, 3)] private string _objectiveText;

        [Tooltip("Open the quest box when this step's text appears. Off for travel/return steps (find the " +
                 "statue, return to it, swim down the tunnel): their text is there, but the box starts " +
                 "closed and the player can open it with the quest button.")]
        [SerializeField] private bool _boxOpen = true;

        public string StepId => _stepId;
        public string ObjectiveText => _objectiveText;

        /// <summary>The zone this step belongs to (set by the director).</summary>
        public ZoneQuest Zone { get; set; }

        public bool IsRunning { get; private set; }

        private Action _onComplete;

        /// <summary>The quest box's starting state for a newly added step (actual quests open; travel closed).</summary>
        protected virtual bool DefaultBoxOpen => true;

        protected virtual void Reset() => _boxOpen = DefaultBoxOpen;

        /// <summary>Called by the director. Implementations call Complete() when done.</summary>
        public void Begin(Action onComplete)
        {
            _onComplete = onComplete;
            IsRunning = true;
            var questUi = Lalim.UI.QuestObjectiveUI.Instance;
            if (questUi != null && !string.IsNullOrEmpty(_objectiveText))
            {
                questUi.Show();
                questUi.SetObjective(Lalim.UI.DialogueManager.ResolveTokens(_objectiveText), _boxOpen);
                questUi.ClearCounter();
            }
            OnBegin();
        }

        /// <summary>Instantly applies this step's end state (loading a save where it was already done).</summary>
        public virtual void RestoreCompleted() { }

        protected abstract void OnBegin();

        protected void Complete()
        {
            if (!IsRunning) return;
            IsRunning = false;
            Action callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

#if UNITY_EDITOR
        public void EditorSetup(string stepId, string objectiveText)
        {
            _stepId = stepId;
            _objectiveText = objectiveText;
        }

        public void EditorSetBoxOpen(bool open) => _boxOpen = open;
        public bool EditorBoxOpen => _boxOpen;
#endif
    }
}
