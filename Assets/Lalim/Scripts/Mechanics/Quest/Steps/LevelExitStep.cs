using System.Collections;
using UnityEngine;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// The level's final step: swim down the opened tunnel. The exit trigger fills the lower quarter
    /// of the well (from 75% of the way down), where it has gone (nearly) pitch black: reaching it completes the level, saves,
    /// and the dark frame crossfades into the loading screen, which crossfades into the teleport
    /// scene, where the next level's teleport is now unlocked (the three cleansed zones already
    /// completed the region).
    /// </summary>
    public class LevelExitStep : QuestStep
    {
        // Swimming down the tunnel is travel: the box starts closed.
        protected override bool DefaultBoxOpen => false;

        [Tooltip("Navigation target: the tunnel entrance.")]
        [SerializeField] private Transform _tunnelEntrance;
        [Tooltip("Trigger at the end of the tunnel (any collider set to Is Trigger).")]
        [SerializeField] private Collider _exitTrigger;
        [SerializeField] private string _nextScene = GameConstants.Scenes.TELEPORT;
        [SerializeField, Min(0f)] private float _delayBeforeLoad = 0.4f;
        [Tooltip("Dissolve the last frame into the loading screen instead of fading through black.")]
        [SerializeField] private bool _crossfadeIntoLoading = true;

        private bool _exiting;

        protected override void OnBegin()
        {
            if (_tunnelEntrance != null) ObjectiveTracker.Instance?.SetObjective(_tunnelEntrance);
            if (_exitTrigger != null)
            {
                _exitTrigger.isTrigger = true;
                LevelExitTrigger relay = _exitTrigger.GetComponent<LevelExitTrigger>();
                if (relay == null) relay = _exitTrigger.gameObject.AddComponent<LevelExitTrigger>();
                relay.Entered = OnExitReached;
            }
        }

        private void OnExitReached()
        {
            if (!IsRunning || _exiting) return;
            _exiting = true;
            StartCoroutine(ExitRoutine());
        }

        private IEnumerator ExitRoutine()
        {
            Region region = LevelFlowDirector.Instance != null ? LevelFlowDirector.Instance.Region : Region.Luzon;
            EventBus.Publish(new LevelCompletedEvent { Region = region });

            // The next session should start in the teleport scene, not back in the tunnel.
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            if (save != null)
            {
                save.lastSceneName = _nextScene;
                save.lastCheckpointId = null;
            }

            PlayerController.Instance?.SetMovementEnabled(false);
            Complete(); // records the step and auto-saves

            yield return new WaitForSeconds(_delayBeforeLoad);
            SceneLoader.Ensure().LoadScene(_nextScene, true, _crossfadeIntoLoading);
        }

        private void DebugForceComplete() => OnExitReached();

#if UNITY_EDITOR
        public void EditorSetupExit(Transform entrance, Collider exitTrigger)
        {
            _tunnelEntrance = entrance;
            _exitTrigger = exitTrigger;
        }
#endif
    }
}
