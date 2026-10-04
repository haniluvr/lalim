using System.Collections;
using UnityEngine;
using Lalim.UI;

namespace Lalim.Mechanics.Quest
{
    /// <summary>
    /// Opens the way onward: the barrier dissolves (no banner: the finished zone already makes it
    /// obvious). The next zone's TalkStep then points navigation at its statue.
    /// </summary>
    public class UnlockBarrierStep : QuestStep
    {
        [SerializeField] private GameObject _barrier;
        [SerializeField, Min(0f)] private float _dissolveSeconds = 1.5f;
        [Tooltip("Optional: where navigation points right after (e.g. the tunnel). Empty = leave it to the next step.")]
        [SerializeField] private Transform _nextObjective;

        [Header("Optional: open a shaft through the map floor (the tunnel down the well)")]
        [SerializeField] private bool _opensFloorShaft;
        [SerializeField] private Vector3 _shaftCenter;
        [SerializeField] private Vector3 _shaftSize;

        protected override void OnBegin()
        {
            if (_nextObjective != null) ObjectiveTracker.Instance?.SetObjective(_nextObjective);
            if (_barrier != null && _barrier.activeSelf) StartCoroutine(Dissolve());
            OpenShaft();
            Complete();
        }

        public override void RestoreCompleted()
        {
            if (_barrier != null) _barrier.SetActive(false);
            OpenShaft();
        }

        private void OpenShaft()
        {
            if (_opensFloorShaft && UnderwaterBounds.Instance != null)
            {
                UnderwaterBounds.Instance.OpenShaft(new Bounds(_shaftCenter, _shaftSize));
            }
        }

        private IEnumerator Dissolve()
        {
            // Block-out barriers are invisible colliders; shrink whatever renders, then disable.
            Transform t = _barrier.transform;
            foreach (Collider c in _barrier.GetComponentsInChildren<Collider>()) c.enabled = false;
            Vector3 start = t.localScale;
            float el = 0f;
            while (el < _dissolveSeconds)
            {
                el += Time.deltaTime;
                t.localScale = Vector3.Lerp(start, new Vector3(start.x, 0f, start.z), el / _dissolveSeconds);
                yield return null;
            }
            _barrier.SetActive(false);
            t.localScale = start;
        }

#if UNITY_EDITOR
        public void EditorSetupShaft(Vector3 center, Vector3 size)
        {
            _opensFloorShaft = true;
            _shaftCenter = center;
            _shaftSize = size;
        }

        public void EditorSetupBarrier(GameObject barrier, Transform nextObjective)
        {
            _barrier = barrier;
            _nextObjective = nextObjective;
        }
#endif
    }
}
