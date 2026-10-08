using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Analytic two-bone IK (upper arm → forearm → hand) for rigs without a humanoid avatar, so
    /// Animator IK isn't available. Run it in LateUpdate, after the Animator has posed the bones;
    /// the Animator rewrites them every frame, so nothing accumulates.
    /// </summary>
    public static class TwoBoneIK
    {
        /// <summary>
        /// Bends <paramref name="root"/>/<paramref name="mid"/> so <paramref name="end"/> reaches
        /// <paramref name="target"/> (as close as the limb allows), blended by
        /// <paramref name="weight"/>. <paramref name="bendHint"/> is the direction the elbow should
        /// point when the limb is straight and the bend plane is undefined.
        /// </summary>
        public static void Solve(Transform root, Transform mid, Transform end, Vector3 target, Vector3 bendHint, float weight)
        {
            if (root == null || mid == null || end == null || weight <= 0f) return;
            weight = Mathf.Clamp01(weight);
            Quaternion rootStart = root.rotation, midStart = mid.rotation;

            Vector3 a = root.position, b = mid.position, c = end.position;
            float lab = Vector3.Distance(a, b), lcb = Vector3.Distance(b, c);
            if (lab < 1e-5f || lcb < 1e-5f) return;
            float lat = Mathf.Clamp(Vector3.Distance(a, target), 1e-4f, (lab + lcb) * 0.999f);

            // Interior angles now, and the ones that put the hand at distance lat.
            float acAb0 = Angle(c - a, b - a);
            float baBc0 = Angle(a - b, c - b);
            float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            Vector3 axis = Vector3.Cross(c - a, b - a);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(c - a, bendHint);
            if (axis.sqrMagnitude < 1e-8f) return;
            axis.Normalize();

            root.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, axis) * root.rotation;
            mid.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, axis) * mid.rotation;

            // Swing the whole limb so the hand points at the target.
            c = end.position;
            root.rotation = Quaternion.FromToRotation(c - a, target - a) * root.rotation;

            if (weight < 1f)
            {
                root.rotation = Quaternion.Slerp(rootStart, root.rotation, weight);
                mid.rotation = Quaternion.Slerp(midStart, mid.rotation, weight);
            }
        }

        private static float Angle(Vector3 u, Vector3 v)
        {
            float d = Vector3.Dot(u.normalized, v.normalized);
            return Mathf.Acos(Mathf.Clamp(d, -1f, 1f));
        }
    }
}
