using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Lalim.UI;

namespace Lalim.Challenges.Gathering
{
    /// <summary>
    /// Danger feedback for Caluya's predators: the screen edges tint red while one is chasing,
    /// stronger and pulsing faster as it closes in, and a small "Hidden" tag shows while the diver
    /// is in cover (or riding the dugong) with a predator nearby. A predator within warning range
    /// that is off screen gets an arrow at the screen edge pointing at it (amber while it patrols,
    /// red while it hunts you), and a bite flashes the screen red. Sits under the HUD; built at
    /// runtime, so it needs no art.
    /// </summary>
    public class PredatorAlertUI : MonoBehaviour
    {
        private static PredatorAlertUI _instance;

        private Image _edges;
        private CanvasGroup _hidden;
        private float _shown;

        private const float WarnRange = 30f;       // metres: a patrolling predator this close gets an arrow
        private const int MaxArrows = 3;
        private static readonly Color Patrol = new Color(1f, 0.72f, 0.25f);
        private static readonly Color Hunting = new Color(1f, 0.22f, 0.18f);
        private readonly Image[] _arrows = new Image[MaxArrows];
        private Image _flash;
        private float _flashLevel;

        // Sound: the chase-tension loop follows the red edges; a heartbeat loop follows the HIDDEN tag.
        private const float TensionVolume = 0.8f;
        private const float HeartbeatVolume = 0.9f;
        private AudioSource _tension;
        private AudioSource _heartbeat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        public static void Ensure(AudioClip tensionLoop = null, AudioClip hiddenHeartbeat = null)
        {
            if (_instance != null) return;
            var go = new GameObject("[PredatorAlert]", typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5;   // under the HUD's buttons
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _instance = go.AddComponent<PredatorAlertUI>();
            _instance.Build();
            _instance.BuildWarnings();
            _instance.BuildAudio(tensionLoop, hiddenHeartbeat);
        }

        public static void Remove()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
        }

        private void Build()
        {
            var edges = new GameObject("Edges", typeof(RectTransform));
            edges.transform.SetParent(transform, false);
            var rt = (RectTransform)edges.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _edges = edges.AddComponent<Image>();
            _edges.sprite = VignetteSprite();
            _edges.color = new Color(0.95f, 0.08f, 0.06f, 0f);
            _edges.raycastTarget = false;

            var tag = new GameObject("Hidden", typeof(RectTransform));
            tag.transform.SetParent(transform, false);
            var trt = (RectTransform)tag.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -150f);
            trt.sizeDelta = new Vector2(220f, 58f);
            var pill = tag.AddComponent<Image>();
            pill.sprite = GlassSprites.Rounded;
            pill.type = Image.Type.Sliced;
            pill.color = new Color(0.05f, 0.25f, 0.2f, 0.7f);
            pill.raycastTarget = false;
            _hidden = tag.AddComponent<CanvasGroup>();
            _hidden.alpha = 0f;
            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(tag.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var text = label.AddComponent<TextMeshProUGUI>();
            if (HUDController.Instance != null && HUDController.Instance.TitleFont != null) text.font = HUDController.Instance.TitleFont;
            text.text = "HIDDEN";
            text.fontSize = 30f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.62f, 1f, 0.75f);
            text.raycastTarget = false;
        }

        private void OnEnable() => Predator.Bit += OnBit;
        private void OnDisable() => Predator.Bit -= OnBit;

        private void OnBit(Predator p, bool harmed) => _flashLevel = harmed ? 1f : 0.4f;

        private void BuildWarnings()
        {
            var flash = new GameObject("BiteFlash", typeof(RectTransform));
            flash.transform.SetParent(transform, false);
            var frt = (RectTransform)flash.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            _flash = flash.AddComponent<Image>();
            _flash.sprite = VignetteSprite();
            _flash.color = new Color(1f, 0.05f, 0.03f, 0f);
            _flash.raycastTarget = false;

            for (int i = 0; i < MaxArrows; i++)
            {
                var go = new GameObject("PredatorArrow_" + i, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(58f, 58f);
                var img = go.AddComponent<Image>();
                img.sprite = GlassSprites.Triangle;
                img.raycastTarget = false;
                img.enabled = false;
                _arrows[i] = img;
            }
        }

        /// <summary>Arrows at the screen edge toward each nearby predator that is off screen.</summary>
        private void UpdateArrows(bool hudUp)
        {
            Camera cam = Camera.main;
            var player = Mechanics.PlayerController.Instance;
            int used = 0;
            if (hudUp && cam != null && player != null)
            {
                // Inside the screen edge, clear of the quest box and pause button (top) and the
                // health bar and orbs (bottom).
                Vector2 half = ((RectTransform)transform).rect.size * 0.5f;
                Rect bounds = Rect.MinMaxRect(-half.x + 90f, -half.y + 200f, half.x - 90f, half.y - 160f);
                foreach (Predator pr in Predator.Active)
                {
                    if (used >= MaxArrows) break;
                    bool hunting = pr.CurrentMode == Predator.Mode.Chase;
                    float d = Vector3.Distance(pr.transform.position, player.transform.position);
                    if (!hunting && d > WarnRange) continue;
                    Vector3 vp = cam.WorldToViewportPoint(pr.transform.position);
                    bool onScreen = vp.z > 0f && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.94f;
                    if (onScreen) continue;
                    Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                    if (vp.z < 0f) dir = -dir;                     // behind the camera: flip
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                    dir.Normalize();
                    float kx = dir.x > 0f ? bounds.xMax / dir.x : dir.x < 0f ? bounds.xMin / dir.x : float.MaxValue;
                    float ky = dir.y > 0f ? bounds.yMax / dir.y : dir.y < 0f ? bounds.yMin / dir.y : float.MaxValue;
                    float k = Mathf.Max(0f, Mathf.Min(kx, ky));
                    Image a = _arrows[used++];
                    a.enabled = true;
                    a.rectTransform.anchoredPosition = dir * k;
                    a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                    float near = Mathf.InverseLerp(WarnRange, 6f, d);
                    float pulse = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * (hunting ? 12f : 5f));
                    Color c = hunting ? Hunting : Patrol;
                    c.a = Mathf.Lerp(0.45f, 1f, near) * pulse;
                    a.color = c;
                    a.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.25f, near) * (pr.IsAttacking ? 1.2f : 1f);
                }
            }
            for (int i = used; i < MaxArrows; i++) _arrows[i].enabled = false;

            _flashLevel = Mathf.MoveTowards(_flashLevel, 0f, Time.unscaledDeltaTime * 2.2f);
            Color f = _flash.color;
            f.a = _flashLevel * 0.85f;
            _flash.color = f;
        }

        private void BuildAudio(AudioClip tensionLoop, AudioClip hiddenHeartbeat)
        {
            _tension = Loop(tensionLoop);
            _heartbeat = Loop(hiddenHeartbeat);
        }

        private AudioSource Loop(AudioClip clip)
        {
            if (clip == null) return null;
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // heard in the head, not from a place
            src.volume = 0f;
            return src;
        }

        private void UpdateAudio()
        {
            float sfx = Lalim.Core.GameAudio.Gain(Lalim.Core.AudioChannel.Sfx);
            bool paused = Time.timeScale <= 0f;
            Drive(_tension, TensionVolume * _shown * sfx, paused);
            // The heartbeat swells while the diver hides with a predator near, and fades as the tag does.
            Drive(_heartbeat, HeartbeatVolume * _hidden.alpha * sfx, paused);
        }

        private static void Drive(AudioSource src, float volume, bool paused)
        {
            if (src == null) return;
            volume *= Lalim.Core.GameAudio.ClipGain(src.clip);   // loudness-matched like every other clip
            src.volume = volume;
            bool audible = volume > 0.001f && !paused;
            if (audible && !src.isPlaying) src.Play();
            else if (!audible && src.isPlaying) src.Pause();
        }

        private void Update()
        {
            bool hudUp = HUDController.Instance == null || HUDController.Instance.IsShowing;
            float threat = hudUp ? Predator.Threat : 0f;
            _shown = Mathf.MoveTowards(_shown, threat, Time.unscaledDeltaTime * 2.5f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 11f, _shown));
            Color c = _edges.color;
            c.a = _shown * 0.75f * pulse;
            _edges.color = c;

            bool near = false;
            if (HidingZone.PlayerHidden && Mechanics.PlayerController.Instance != null)
            {
                Vector3 p = Mechanics.PlayerController.Instance.transform.position;
                foreach (Predator pr in Predator.Active)
                    if ((pr.transform.position - p).sqrMagnitude < 30f * 30f) { near = true; break; }
            }
            _hidden.alpha = Mathf.MoveTowards(_hidden.alpha, hudUp && near ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            UpdateAudio();
            UpdateArrows(hudUp);
        }

        private static Sprite _vignette;

        /// <summary>Clear in the middle, opaque toward the edges and corners.</summary>
        private static Sprite VignetteSprite()
        {
            if (_vignette != null) return _vignette;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PredatorVignette" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u * 0.8f + v * v * 1.1f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.25f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            _vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _vignette;
        }
    }
}
