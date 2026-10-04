using UnityEngine;

namespace Lalim.Mechanics
{
    public class CausticsAnimator : MonoBehaviour
    {
        [SerializeField] private Vector2 scrollSpeed = new Vector2(0.05f, 0.03f);
        private Light causticsLight;

        private void Start()
        {
            causticsLight = GetComponent<Light>();
        }

        private void Update()
        {
            if (causticsLight != null)
            {
                // Scroll the cookie texture by adjusting the light's cookie offset
                // Note: Standard Unity Light component does not expose cookie offset directly.
                // We must use a Projector, Decal, or manipulate a custom shader.
                // However, moving the Directional Light's position combined with a world-space cookie projection 
                // in URP can simulate scrolling if it's tied to camera or we just slowly move the light itself!
                
                // For a Directional Light in URP, translating the light moves the cookie projection!
                transform.Translate(new Vector3(scrollSpeed.x, 0, scrollSpeed.y) * Time.deltaTime, Space.World);
                
                // Reset position if it strays too far to avoid precision issues
                if (transform.position.magnitude > 1000f)
                {
                    transform.position = Vector3.zero;
                }
            }
        }
    }
}