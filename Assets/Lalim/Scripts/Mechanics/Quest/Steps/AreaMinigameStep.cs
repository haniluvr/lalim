using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// A minigame played at several areas (zone 2: 3 water readings, then 3 coral sites; zone 3:
    /// 3 ghost nets). Each area is its own challenge and saves its own ID, so finished areas stay
    /// finished after a reload. Navigation points at the nearest unfinished area.
    /// </summary>
    public class AreaMinigameStep : QuestStep
    {
        [SerializeField] private List<AreaStation> _stations = new List<AreaStation>();
        [SerializeField] private string _counterFormat = "{0} / {1} areas";

        private static SaveData Save => SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;

        protected override void OnBegin()
        {
            foreach (AreaStation s in _stations)
            {
                if (s == null) continue;
                s.Finished -= OnStationFinished;
                s.Finished += OnStationFinished;
                if (Save != null && Save.HasChallengeId(s.ChallengeIdFor(this))) s.MarkDone(this);
                else s.Activate(this);
            }

            if (AllDone()) { Complete(); return; }

            UpdateCounter();
            var targets = new List<Transform>();
            foreach (AreaStation s in _stations) if (s != null) targets.Add(s.transform);
            ObjectiveTracker.Instance?.SetObjectiveSet(targets, t => t.GetComponent<AreaStation>().IsDone);
        }

        public override void RestoreCompleted()
        {
            foreach (AreaStation s in _stations) if (s != null) s.MarkDone(this);
        }

        private void OnStationFinished(AreaStation station)
        {
            Save?.AddChallengeId(station.ChallengeId);
            SaveManager.Instance?.AutoSave();
            UpdateCounter();
            if (AllDone()) Complete();
        }

        private bool AllDone()
        {
            foreach (AreaStation s in _stations) if (s != null && !s.IsDone) return false;
            return true;
        }

        private void UpdateCounter()
        {
            var ui = Lalim.UI.QuestObjectiveUI.Instance;
            if (ui == null) return;
            int done = 0;
            foreach (AreaStation s in _stations) if (s != null && s.IsDone) done++;
            ui.Show();
            ui.SetCounter(done, _stations.Count, _counterFormat);
        }

        private void DebugForceComplete()
        {
            foreach (AreaStation s in _stations)
            {
                if (s == null || s.IsDone) continue;
                s.MarkDone(this);
                Save?.AddChallengeId(s.ChallengeId);
            }
            Complete();
        }

#if UNITY_EDITOR
        public void EditorSetupStations(List<AreaStation> stations) => _stations = stations;
#endif
    }
}
