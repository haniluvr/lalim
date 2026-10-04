using System.Collections;
using UnityEngine;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Stand-in for a station's real minigame until it's built (calibration scanner, coral site prep,
    /// ghost net). Lets the whole level be played end to end: it announces itself, waits briefly and
    /// completes the area's challenge.
    /// </summary>
    public class PlaceholderAreaMinigame : MonoBehaviour, IAreaMinigame
    {
        [SerializeField] private string _minigameName = "Minigame";
        [SerializeField, Min(0f)] private float _seconds = 2.5f;

        public int TaskCount => 1;
        public float ExpectedSeconds => 30f;
        public float ExpectedFirstActionSeconds => 0f;
        public int HintBudget => 0;

        public void ShowCompletedState() { }

        private Coroutine _routine;

        public void Play(AreaStation station)
        {
            ZoneBannerUI.Post($"{_minigameName} (placeholder)");
            _routine = StartCoroutine(Finish());
        }

        public void Stop()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        private IEnumerator Finish()
        {
            yield return new WaitForSeconds(_seconds);
            _routine = null;
            ChallengeManager.Instance?.ReportTaskCompleted();
        }

#if UNITY_EDITOR
        public void EditorSetup(string name) => _minigameName = name;
#endif
    }
}
