using UnityEngine;
using UnityEngine.Rendering.Universal;

// Ambient motion for the underwater main-menu background.
public class UnderwaterMenuMotion : MonoBehaviour
{
    [Header("Camera")]
    public Transform cameraRig;
    public float driftAmount = 0.25f;
    public float driftSpeed = 0.15f;
    public float mouseParallax = 0.35f;
    [Header("Characters")]
    public Transform player;
    public Transform dugong;
    public float bobAmount = 0.12f;
    public float bobSpeed = 0.8f;
    [Header("Light")]
    public Light sun;
    public Vector2 causticsSpeed = new Vector2(0.03f, 0.018f);
    public Transform lightShafts;

    Vector3 camStart, playerStart, dugongStart;
    UniversalAdditionalLightData sunData;
    Vector3[] shaftStart;

    void Start()
    {
        if (cameraRig == null && Camera.main != null) cameraRig = Camera.main.transform;
        if (cameraRig) camStart = cameraRig.position;
        if (player) playerStart = player.position;
        if (dugong) dugongStart = dugong.position;
        if (sun) sunData = sun.GetUniversalAdditionalLightData();
        if (lightShafts)
        {
            shaftStart = new Vector3[lightShafts.childCount];
            for (int i = 0; i < shaftStart.Length; i++) shaftStart[i] = lightShafts.GetChild(i).position;
        }
    }

    void Update()
    {
        float t = Time.time;
        if (cameraRig)
        {
            Vector2 m = new Vector2(Input.mousePosition.x / Screen.width - 0.5f, Input.mousePosition.y / Screen.height - 0.5f);
            Vector3 target = camStart
                + new Vector3(Mathf.Sin(t * driftSpeed) * driftAmount + m.x * mouseParallax,
                              Mathf.Sin(t * driftSpeed * 1.3f) * driftAmount * 0.5f + m.y * mouseParallax * 0.5f, 0f);
            cameraRig.position = Vector3.Lerp(cameraRig.position, target, Time.deltaTime * 2f);
        }
        if (player) player.position = playerStart + Vector3.up * Mathf.Sin(t * bobSpeed) * bobAmount;
        if (dugong) dugong.position = dugongStart + Vector3.up * Mathf.Sin(t * bobSpeed + 1.2f) * bobAmount;
        if (sunData) sunData.lightCookieOffset = new Vector2(t * causticsSpeed.x, t * causticsSpeed.y);
        if (shaftStart != null)
            for (int i = 0; i < shaftStart.Length; i++)
                lightShafts.GetChild(i).position = shaftStart[i] + Vector3.right * Mathf.Sin(t * 0.12f + i) * 1.2f;
    }
}
