using System.Collections;
using UnityEngine;
using Lalim.Core;

namespace Lalim.Mechanics
{
    /// <summary>
    /// One arch of the teleport scene, tied to a region:
    ///
    ///   locked   — the portal video is grey and dim, fog shrouds the arch and a solid blocker
    ///              (with a "complete … first" message) stops the player.
    ///   unlocked — the video plays in full colour; swimming through the arch loads the region.
    ///
    /// Unlock state comes from GameManager (Luzon is open from the start, Visayas after Level 1,
    /// Mindanao after Level 2). On the first visit after an unlock (the same "seen_unlock_{region}"
    /// flag TeleportUnlockReveal uses), the fog drifts away and the colour fades back in.
    ///
    /// Sits on the Portal_N object (with ArchPortalSurface); the trigger and blocker are sized from
    /// the arch shape, so moving or resizing the portal by hand keeps them in place.
    /// </summary>
    [RequireComponent(typeof(ArchPortalSurface), typeof(BoxCollider))]
    public class TeleportArchGate : MonoBehaviour
    {
        [SerializeField] private Region _region = Region.Luzon;

        [Header("Locked look")]
        [SerializeField] private GameObject _lockedFog;
        [SerializeField] private BoxCollider _blocker;
        [SerializeField, Range(0f, 1f)] private float _lockedSaturation = 0f;
        [SerializeField, Range(0f, 2f)] private float _lockedBrightness = 0.55f;

        [Header("Unlock reveal (first visit after unlocking)")]
        [SerializeField, Min(0f)] private float _revealDelay = 1.2f;
        [SerializeField, Min(0.1f)] private float _revealSeconds = 2.5f;

        [Header("Entering")]
        [Tooltip("How far (metres) in front of and behind the video counts as entering. The diver's " +
                 "collision capsule is wider than the small arches, so it stops short of the video itself.")]
        [SerializeField, Min(0.1f)] private float _enterReach = 1f;
        [Tooltip("Pause between swimming in and the load starting (the flash plays meanwhile).")]
        [SerializeField, Min(0f)] private float _enterDelay = 0.5f;

        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        private MaterialPropertyBlock _block;
        private Renderer _renderer;
        private BoxCollider _trigger;
        private bool _open;
        private bool _entering;

        public Region Region => _region;
        public bool IsOpen => _open;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _trigger = GetComponent<BoxCollider>();
            _trigger.isTrigger = true;
            SyncColliders();
        }

        private IEnumerator Start()
        {
            bool accessible = GameManager.Instance != null
                ? GameManager.Instance.IsRegionAccessible(_region)
                : _region == Region.Luzon;
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.CurrentSave : null;
            bool seen = _region == Region.Luzon || save == null || save.tutorialFlags.Contains($"seen_unlock_{_region}");

            if (!accessible) { ApplyLocked(true); yield break; }
            if (seen) { ApplyLocked(false); yield break; }

            // Newly unlocked: start shrouded, then lift the fog.
            ApplyLocked(true);
            yield return new WaitForSeconds(_revealDelay);
            if (_lockedFog != null)
            {
                foreach (ParticleSystem ps in _lockedFog.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // puffs fade out on their own
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / _revealSeconds)
            {
                float e = t * t * (3f - 2f * t);
                SetLook(Mathf.Lerp(_lockedSaturation, 1f, e), Mathf.Lerp(_lockedBrightness, 1f, e));
                yield return null;
            }
            ApplyLocked(false);
        }

        private void ApplyLocked(bool locked)
        {
            _open = !locked;
            SetLook(locked ? _lockedSaturation : 1f, locked ? _lockedBrightness : 1f);
            if (_blocker != null) _blocker.gameObject.SetActive(locked);
            if (_trigger != null) _trigger.enabled = !locked;
            if (_lockedFog != null && !locked)
            {
                // After the reveal the fog has stopped emitting: let the last puffs fade, then hide it.
                ParticleSystem ps = _lockedFog.GetComponentInChildren<ParticleSystem>();
                if (ps == null || ps.isEmitting || !ps.IsAlive(true)) _lockedFog.SetActive(false);
                else StartCoroutine(HideFogWhenClear(ps));
            }
            else if (_lockedFog != null) _lockedFog.SetActive(true);
        }

        private IEnumerator HideFogWhenClear(ParticleSystem ps)
        {
            while (ps != null && ps.IsAlive(true)) yield return null;
            if (_lockedFog != null) _lockedFog.SetActive(false);
        }

        private void SetLook(float saturation, float brightness)
        {
            if (_renderer == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(SaturationId, saturation);
            _block.SetFloat(BrightnessId, brightness);
            _renderer.SetPropertyBlock(_block);
        }

        // ───────────────────────── Entering ─────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!_open || _entering || !other.CompareTag(GameConstants.Tags.PLAYER)) return;
            StartCoroutine(Enter());
        }

        // The diver is a CharacterController, which doesn't reliably raise trigger events, so also
        // check whether they are inside the arch (same approach as BarrierNotification).
        private void Update()
        {
            if (!_open || _entering || _trigger == null || !_trigger.enabled) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            Vector3 p = player.transform.position;
            if ((_trigger.ClosestPoint(p) - p).sqrMagnitude < 0.0001f) StartCoroutine(Enter());
        }

        private IEnumerator Enter()
        {
            _entering = true;
            Flash();
            Sfx.Play(SfxCue.Teleport, survivesSceneLoad: true);   // rings on into the loading screen
            yield return new WaitForSeconds(_enterDelay);
            Debug.Log($"[TeleportArchGate] Entering {_region}.");
            if (GameManager.Instance != null) GameManager.Instance.TryEnterRegion(_region);
            else EventBus.Publish(new SceneLoadRequestedEvent { SceneName = GameConstants.Scenes.GetSceneForRegion(_region), ShowLoadingScreen = true });
        }

        private void Flash()
        {
            var go = new GameObject("[PortalFlash]");
            go.transform.position = transform.TransformPoint(new Vector3(0f, GetComponent<ArchPortalSurface>().Height * 0.5f, 0f));
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.7f, 0.8f, 1f);
            light.range = 10f;
            light.intensity = 8f;
            Destroy(go, _enterDelay + 1f);
        }

        // ───────────────────────── Colliders ─────────────────────────

        /// <summary>Fits the enter trigger and the blocker to the arch shape (pivot bottom centre, XY plane).</summary>
        public void SyncColliders()
        {
            var shape = GetComponent<ArchPortalSurface>();
            if (shape == null) return;
            float w = shape.Width, h = shape.Height;

            BoxCollider trigger = _trigger != null ? _trigger : GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, h * 0.5f, 0f);
                float depth = 2f * _enterReach / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.z));
                trigger.size = new Vector3(w * 0.85f, h * 0.9f, depth);
            }
            if (_blocker != null)
            {
                _blocker.transform.localPosition = Vector3.zero;
                _blocker.transform.localRotation = Quaternion.identity;
                _blocker.transform.localScale = Vector3.one;
                _blocker.isTrigger = false;
                _blocker.center = new Vector3(0f, h * 0.5f, 0f);
                _blocker.size = new Vector3(w, h, w * 0.4f);
            }
            if (_lockedFog != null)
            {
                _lockedFog.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
                _lockedFog.transform.localRotation = Quaternion.identity;
                foreach (ParticleSystem ps in _lockedFog.GetComponentsInChildren<ParticleSystem>(true))
                {
                    // Sizes and speeds in the portal's own units (the system scales with the hierarchy).
                    var main = ps.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(w * 0.55f, w * 1.1f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0f, w * 0.04f);
                    var shapeModule = ps.shape;
                    shapeModule.scale = new Vector3(w * 0.9f, h * 0.9f, w * 0.5f);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) SyncColliders(); };

        public void EditorSetup(Region region, GameObject lockedFog, BoxCollider blocker)
        {
            _region = region;
            _lockedFog = lockedFog;
            _blocker = blocker;
            SyncColliders();
        }
#endif
    }
}
