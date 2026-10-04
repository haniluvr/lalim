using System;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// The single "where do I go next?" target for the level. Quest steps set it; Spirit Sense,
    /// the escalating audio hints and the proximity music all read it, so they never disagree.
    ///
    /// Either one fixed target, or a set of candidates where the nearest unfinished one is tracked
    /// (remaining trash, unfinished minigame areas).
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveTracker : MonoBehaviour
    {
        public static ObjectiveTracker Instance { get; private set; }

        [Tooltip("How often the nearest candidate of a set is re-evaluated (seconds).")]
        [SerializeField, Min(0.05f)] private float _reevaluateInterval = 0.5f;

        [Tooltip("Getting this fraction closer to the target counts as progress for the audio hints.")]
        [SerializeField, Range(0.05f, 0.9f)] private float _progressFraction = 0.25f;

        private readonly List<Transform> _candidates = new List<Transform>();
        private Func<Transform, bool> _isDone;
        private float _nextEval;
        private float _bestDistanceSinceProgress = float.MaxValue;

        public Transform CurrentTarget { get; private set; }

        /// <summary>Distance from the player to the current target, or +infinity with no target/player.</summary>
        public float DistanceToTarget
        {
            get
            {
                if (CurrentTarget == null || PlayerController.Instance == null) return float.PositiveInfinity;
                return Vector3.Distance(PlayerController.Instance.transform.position, CurrentTarget.position);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Track one fixed target (a statue, a barrier gap, the tunnel).</summary>
        public void SetObjective(Transform target)
        {
            _candidates.Clear();
            _isDone = null;
            Apply(target);
        }

        /// <summary>Track the nearest candidate for which isDone returns false.</summary>
        public void SetObjectiveSet(IEnumerable<Transform> candidates, Func<Transform, bool> isDone)
        {
            _candidates.Clear();
            if (candidates != null)
            {
                foreach (Transform t in candidates) if (t != null) _candidates.Add(t);
            }
            _isDone = isDone;
            _nextEval = 0f;
            Apply(FindNearestOpen());
        }

        public void Clear()
        {
            _candidates.Clear();
            _isDone = null;
            Apply(null);
        }

        private void Update()
        {
            if (_candidates.Count > 0 && Time.time >= _nextEval)
            {
                _nextEval = Time.time + _reevaluateInterval;
                Transform nearest = FindNearestOpen();
                if (nearest != CurrentTarget) Apply(nearest);
            }

            TrackApproachProgress();
        }

        private Transform FindNearestOpen()
        {
            Vector3 from = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
            Transform best = null;
            float bestSqr = float.MaxValue;
            foreach (Transform t in _candidates)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                if (_isDone != null && _isDone(t)) continue;
                float d = (t.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = t; }
            }
            return best;
        }

        // Swimming the right way counts as progress, so a player on track is never nagged.
        private void TrackApproachProgress()
        {
            float d = DistanceToTarget;
            if (float.IsInfinity(d)) return;
            if (d < _bestDistanceSinceProgress * (1f - _progressFraction))
            {
                _bestDistanceSinceProgress = d;
                AudioNavigationManager.Instance?.RecordProgress();
            }
        }

        private void Apply(Transform target)
        {
            if (target == CurrentTarget) return;
            CurrentTarget = target;
            _bestDistanceSinceProgress = float.MaxValue;

            if (SpiritSense.Instance != null)
            {
                if (target != null) SpiritSense.Instance.SetObjective(target);
                else SpiritSense.Instance.ClearObjective();
            }

            if (AudioNavigationManager.Instance != null)
            {
                if (target != null) AudioNavigationManager.Instance.SetNavigationTarget(target);
                else AudioNavigationManager.Instance.ClearTarget();
            }

            EventBus.Publish(new ObjectiveChangedEvent { Target = target });
        }
    }
}
