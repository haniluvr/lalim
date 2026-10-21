using System.Collections;
using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;
using Lalim.UI;

namespace Lalim.Abilities
{
    /// <summary>
    /// Tracks the last checkpoint reached and drives the respawn sequence on death:
    /// fade out, teleport, restore full HP/orbs, end any active ability durations,
    /// fade back in. A respawn always refills HP and stamina, so a bad run never compounds
    /// into a stuck loop (resuming a save restores them as they were; see PlayerVitals).
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [SerializeField] private float _fadeOutDuration = 0.4f;
        [SerializeField] private float _fadeInDuration = 0.5f;

        public Checkpoint CurrentCheckpoint { get; private set; }
        private bool _isRespawning;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable() => EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        private void OnDisable() => EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);

        // The player spawns inside the level's spawn checkpoint, and its trigger fires on the first
        // physics step — before the level has read the save to put a resumed player back at their
        // checkpoint. Held until the level says where the player starts (SettleResume).
        private bool _resumeSettled;
        private Checkpoint _reachedBeforeSettled;

        /// <summary>
        /// The level has placed the player (<paramref name="resumedElsewhere"/>: at the saved
        /// checkpoint). A checkpoint touched before then counts only for a fresh start.
        /// </summary>
        public void SettleResume(bool resumedElsewhere)
        {
            _resumeSettled = true;
            Checkpoint held = _reachedBeforeSettled;
            _reachedBeforeSettled = null;
            if (!resumedElsewhere && held != null) OnCheckpointReached(held);
        }

        public void OnCheckpointReached(Checkpoint checkpoint)
        {
            if (!_resumeSettled && Lalim.Mechanics.Quest.LevelFlowDirector.Instance != null)
            {
                _reachedBeforeSettled = checkpoint;
                return;
            }
            if (checkpoint == CurrentCheckpoint) return;

            CurrentCheckpoint = checkpoint;
            RecordResumePoint(checkpoint);
            EventBus.Publish(new CheckpointReachedEvent
            {
                CheckpointId = checkpoint.CheckpointId,
                SectionId = checkpoint.SectionId
            });
        }

        /// <summary>Saves this checkpoint as the place to resume after quitting.</summary>
        private static void RecordResumePoint(Checkpoint checkpoint)
        {
            SaveManager save = SaveManager.Instance;
            if (save == null || save.CurrentSave == null) return;
            save.CurrentSave.lastCheckpointId = checkpoint.CheckpointId;
            save.CurrentSave.lastSceneName = checkpoint.gameObject.scene.name;
            // New games start writing from the first checkpoint (the CheckpointReachedEvent that
            // follows triggers the actual AutoSave).
            save.EnsureNewGameSlotBound();
        }

        /// <summary>
        /// Resume after loading a save: makes this the current checkpoint and moves the player
        /// there without a death fade. Returns false if no checkpoint has that ID.
        /// </summary>
        public bool PlaceAtCheckpoint(string checkpointId)
        {
            if (string.IsNullOrEmpty(checkpointId)) return false;
            foreach (Checkpoint cp in FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude))
            {
                if (cp.CheckpointId != checkpointId) continue;
                CurrentCheckpoint = cp;
                if (PlayerController.Instance != null)
                {
                    PlayerController.Instance.SetPosition(cp.SpawnPosition);
                    PlayerController.Instance.transform.rotation = cp.SpawnRotation;
                    PlayerController.Instance.SyncFacingToTransform();
                }
                // Teleporting doesn't always fire OnTriggerExit, so start toxic tracking clean.
                EventBus.Publish(new PlayerRespawnedEvent { CheckpointId = cp.CheckpointId, IsResume = true });
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            RespawnAtLastCheckpoint();
        }

        public void RespawnAtLastCheckpoint()
        {
            if (_isRespawning) return;
            StartCoroutine(RespawnSequence());
        }

        private IEnumerator RespawnSequence()
        {
            _isRespawning = true;

            yield return ScreenFader.Instance.FadeOut(_fadeOutDuration);

            // End any active ability durations before moving the player, so e.g. a
            // Riptide/Tidebond speed multiplier doesn't survive the teleport.
            if (AbilityController.Instance != null)
            {
                AbilityController.Instance.ForceEndAll();
            }

            if (PlayerController.Instance != null)
            {
                Vector3 pos = CurrentCheckpoint != null ? CurrentCheckpoint.SpawnPosition : PlayerController.Instance.transform.position;
                PlayerController.Instance.SetPosition(pos);
                if (CurrentCheckpoint != null)
                {
                    PlayerController.Instance.transform.rotation = CurrentCheckpoint.SpawnRotation;
                }
            }

            if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetToFull();
            if (PlayerStamina.Instance != null) PlayerStamina.Instance.RefillToFull();

            EventBus.Publish(new PlayerRespawnedEvent
            {
                CheckpointId = CurrentCheckpoint != null ? CurrentCheckpoint.CheckpointId : ""
            });

            yield return ScreenFader.Instance.FadeIn(_fadeInDuration);

            _isRespawning = false;
        }
    }
}
