using UnityEngine;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// A trigger placed at zone entrances / before toxic crossings. When the player
    /// passes through, it becomes the respawn point and marks the current exploration
    /// section as clean-so-far for DDA evaluation.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private string _checkpointId;
        [SerializeField] private string _sectionId;
        [Tooltip("Where the player spawns when respawning here. Defaults to this transform if unset.")]
        [SerializeField] private Transform _spawnPoint;

        public string CheckpointId => _checkpointId;
        public string SectionId => _sectionId;
        public Vector3 SpawnPosition => _spawnPoint != null ? _spawnPoint.position : transform.position;
        public Quaternion SpawnRotation => _spawnPoint != null ? _spawnPoint.rotation : transform.rotation;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
            if (string.IsNullOrEmpty(_checkpointId)) _checkpointId = gameObject.name;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;
            if (CheckpointManager.Instance != null)
            {
                CheckpointManager.Instance.OnCheckpointReached(this);
            }
        }
    }
}
