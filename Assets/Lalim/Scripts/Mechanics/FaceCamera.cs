using UnityEngine;

namespace Lalim.Mechanics
{
    /// <summary>Keeps a sprite, icon or label turned to face the main camera.</summary>
    public class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
