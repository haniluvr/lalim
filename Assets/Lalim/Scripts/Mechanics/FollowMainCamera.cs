using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Keeps an ambient effect (bubbles, plankton) centred on the main camera, so a world-space
    /// particle box only ever fills the water around the player instead of the whole level.
    /// </summary>
    public class FollowMainCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 _offset = Vector3.zero;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.position = cam.transform.position + _offset;
        }
    }
}
