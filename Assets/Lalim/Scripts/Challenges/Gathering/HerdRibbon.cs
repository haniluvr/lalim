using System.Collections.Generic;
using UnityEngine;
using Lalim.Mechanics;

namespace Lalim.Challenges.Gathering
{
    /// <summary>
    /// The ribbon of called fish behind the diver. It records the diver's swim path, and every
    /// following fish takes a slot further back along it, so the herds trail behind in one long
    /// line that bends the way the diver swam. Herds join at the end and leave when delivered or
    /// scattered; the slots close up behind them.
    /// </summary>
    public class HerdRibbon : MonoBehaviour
    {
        private const float SampleSpacing = 0.35f;
        private const int MaxSamples = 700;
        private const float FirstSlot = 1.3f;     // metres behind the diver
        private const float SlotSpacing = 0.45f;
        private const float Lift = 0.35f;         // a little above the diver's line, so the camera sees over them

        private static HerdRibbon _instance;
        private readonly List<Vector3> _path = new List<Vector3>();   // newest last
        private readonly List<Herd> _herds = new List<Herd>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public static HerdRibbon Instance
        {
            get
            {
                if (_instance == null) _instance = new GameObject("[HerdRibbon]").AddComponent<HerdRibbon>();
                return _instance;
            }
        }

        /// <summary>Herds following the diver, in ribbon order.</summary>
        public IReadOnlyList<Herd> Herds => _herds;
        public bool HasFollowers => _herds.Count > 0;

        public void Join(Herd herd) { if (!_herds.Contains(herd)) _herds.Add(herd); }
        public void Leave(Herd herd) => _herds.Remove(herd);

        /// <summary>The ribbon slot of a herd's fish (0 = right behind the diver).</summary>
        public int SlotOf(Herd herd, int fishIndex)
        {
            int slot = 0;
            foreach (Herd h in _herds)
            {
                if (h == herd) return slot + fishIndex;
                slot += h.FishCount;
            }
            return slot + fishIndex;
        }

        /// <summary>Where a slot sits now: on the diver's recorded path, that far behind.</summary>
        public Vector3 SlotPosition(int slot)
        {
            Transform diver = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (diver == null) return Vector3.zero;
            return PointBehind(diver, FirstSlot + slot * SlotSpacing) + Vector3.up * Lift;
        }

        private void Update()
        {
            Transform diver = PlayerController.Instance != null ? PlayerController.Instance.transform : null;
            if (diver == null) return;
            Vector3 p = diver.position;
            if (_path.Count == 0 || (p - _path[_path.Count - 1]).sqrMagnitude >= SampleSpacing * SampleSpacing)
            {
                _path.Add(p);
                if (_path.Count > MaxSamples) _path.RemoveAt(0);
            }
        }

        private Vector3 PointBehind(Transform diver, float distance)
        {
            Vector3 prev = diver.position;
            float left = distance;
            for (int i = _path.Count - 1; i >= 0; i--)
            {
                Vector3 p = _path[i];
                float seg = Vector3.Distance(prev, p);
                if (seg >= left) return Vector3.Lerp(prev, p, seg > 0.0001f ? left / seg : 0f);
                left -= seg;
                prev = p;
            }
            // The path is shorter than the ribbon: carry on straight back from the oldest point.
            Vector3 back = _path.Count >= 2 ? (_path[0] - _path[1]).normalized : -diver.forward;
            if (back.sqrMagnitude < 0.01f) back = -diver.forward;
            return prev + back * left;
        }
    }
}
