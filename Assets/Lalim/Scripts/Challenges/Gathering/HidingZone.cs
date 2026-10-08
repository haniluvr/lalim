using System.Collections.Generic;
using UnityEngine;
using Lalim.Abilities;
using Lalim.Core;

namespace Lalim.Challenges.Gathering
{
    /// <summary>
    /// Cover from predators: a trigger volume over a seaweed thicket or inside a rock crevice.
    /// While the diver is inside one — or riding the dugong with Tidebond, which camouflages them
    /// as one of Manunubo's herd — predators can't see them and give up the chase.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HidingZone : MonoBehaviour
    {
        public enum Kind { Thicket, Crevice }

        [SerializeField] private Kind _kind = Kind.Thicket;

        private static readonly HashSet<HidingZone> _occupied = new HashSet<HidingZone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _occupied.Clear();

        public Kind ZoneKind => _kind;

        /// <summary>The diver is inside a thicket or crevice.</summary>
        public static bool PlayerInCover => _occupied.Count > 0;

        /// <summary>Tidebond: riding alongside the dugong.</summary>
        public static bool PlayerRiding =>
            DugongCompanion.Instance != null && DugongCompanion.Instance.State == DugongCompanion.DugongState.Mounted;

        /// <summary>Predators can't see the diver.</summary>
        public static bool PlayerHidden => PlayerInCover || PlayerRiding;

        /// <summary>The diver swam into cover (not fired when moving between overlapping zones).</summary>
        public static event System.Action<HidingZone> PlayerEntered;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;
            bool wasInCover = _occupied.Count > 0;
            _occupied.Add(this);
            if (!wasInCover) PlayerEntered?.Invoke(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(GameConstants.Tags.PLAYER)) _occupied.Remove(this);
        }

        private void OnDisable() => _occupied.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = _kind == Kind.Thicket ? new Color(0.3f, 1f, 0.4f, 0.25f) : new Color(0.6f, 0.5f, 1f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (TryGetComponent(out BoxCollider box)) Gizmos.DrawCube(box.center, box.size);
            else if (TryGetComponent(out SphereCollider sphere)) Gizmos.DrawSphere(sphere.center, sphere.radius);
        }

#if UNITY_EDITOR
        public void EditorSetup(Kind kind) => _kind = kind;
#endif
    }
}
