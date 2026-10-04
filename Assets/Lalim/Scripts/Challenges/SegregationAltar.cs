using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Challenges
{
    /// <summary>One sorting shrine: the shrine prop, and the bowl (with its floating icon orb) on top.</summary>
    [System.Serializable]
    public class OfferingBowl
    {
        public OfferingCategory category;
        [Tooltip("The shrine pillar that rises out of the sand.")]
        public Transform shrine;
        [Tooltip("The bowl and orb sitting on top of the shrine (rises with it).")]
        public Transform top;
        [Tooltip("Where accepted trash settles inside the bowl.")]
        public Transform dropPoint;
        [Tooltip("The glowing orb (with the category icon) floating in the bowl.")]
        public Transform orb;

        [System.NonSerialized] public int count;
        [System.NonSerialized] public Vector3 shrineFinal;
        [System.NonSerialized] public Vector3 topFinal;
        [System.NonSerialized] public Vector3 orbScale;
        [System.NonSerialized] public Transform halo;
        [System.NonSerialized] public Vector3 haloScale;
        [System.NonSerialized] public Light light;
        [System.NonSerialized] public float lightIntensity;
    }

    /// <summary>
    /// Zone 1, minigame 2: waste segregation (first person). Three shrines rise out of the sand,
    /// each with a bowl and a floating orb whose icon shows what it takes (plastic, glass, metal).
    /// The collected trash floats along the bottom of the screen; the player drags each piece up
    /// into a bowl. Right bowl: it settles in. Wrong bowl: it shakes and floats back.
    ///
    /// SegregationStep publishes the ChallengeStartedEvent (which switches the camera to first
    /// person) and calls Begin(); this reports every result to ChallengeManager.
    /// </summary>
    public class SegregationAltar : MonoBehaviour
    {
        [Header("Shrines")]
        [SerializeField] private List<OfferingBowl> _bowls = new List<OfferingBowl>();
        [Tooltip("How far below their resting place the shrines start.")]
        [SerializeField, Min(0f)] private float _riseDepth = 5.5f;
        [SerializeField, Min(0.1f)] private float _riseSeconds = 1.8f;
        [Tooltip("Delay between one shrine and the next starting to rise.")]
        [SerializeField, Min(0f)] private float _riseStagger = 0.3f;
        [Tooltip("Where the dugong waits while the player sorts (out of the first-person view). Optional.")]
        [SerializeField] private Transform _dugongSpot;

        [Header("Floating trash (bottom of the screen)")]
        [SerializeField, Range(1, 8)] private int _visibleSlots = 5;
        [Tooltip("Distance in front of the camera the trash floats at.")]
        [SerializeField, Min(0.3f)] private float _floatDepth = 1.3f;
        [Tooltip("Height of the row on screen (0 = bottom, 1 = top).")]
        [SerializeField, Range(0f, 0.5f)] private float _rowHeight = 0.16f;
        [Tooltip("Width the row spreads over (fraction of the screen).")]
        [SerializeField, Range(0.2f, 1f)] private float _rowWidth = 0.62f;
        [Tooltip("Size of each piece, as a fraction of the screen height.")]
        [SerializeField, Range(0.05f, 0.4f)] private float _itemScreenSize = 0.2f;
        [Tooltip("Size of a piece once it has settled in a bowl (metres).")]
        [SerializeField, Min(0.05f)] private float _inBowlSize = 0.35f;

        [Header("Dragging")]
        [Tooltip("How close (fraction of the screen height) a press must be to grab a piece.")]
        [SerializeField, Range(0.02f, 0.3f)] private float _grabRadius = 0.11f;
        [Tooltip("How close (fraction of the screen height) a piece must be dropped to a bowl.")]
        [SerializeField, Range(0.05f, 0.4f)] private float _dropRadius = 0.16f;
        [SerializeField, Min(0.05f)] private float _acceptSeconds = 0.55f;
        [SerializeField, Min(0.05f)] private float _returnSeconds = 0.35f;

        public UnityEvent onAltarEmerged;

        private string _zoneId;
        private int _labelMode;                  // DDA: 0/1 a wrong drop names the bowl, 2 it doesn't
        private bool _interactive;
        private int _totalItems;
        private int _sortedCount;

        private readonly Queue<OfferingItem> _queue = new Queue<OfferingItem>();
        private OfferingItem[] _slots;
        private readonly Dictionary<OfferingItem, float> _baseLargest = new Dictionary<OfferingItem, float>();
        private readonly Dictionary<OfferingItem, Vector3> _baseScale = new Dictionary<OfferingItem, Vector3>();
        private readonly HashSet<OfferingItem> _busy = new HashSet<OfferingItem>();   // flying, not grabbable
        private OfferingItem _held;
        private int _heldPointer = -1;
        private OfferingBowl _hover;

        private TMP_Text _feedbackText;
        private Coroutine _feedback;
        private GameObject _overlay;

        // ─────────────────────────────────────────────
        // Setup
        // ─────────────────────────────────────────────

        private void Awake()
        {
            foreach (OfferingBowl b in _bowls)
            {
                if (b.shrine != null) { b.shrineFinal = b.shrine.position; b.shrine.gameObject.SetActive(false); }
                if (b.top != null) { b.topFinal = b.top.position; b.top.gameObject.SetActive(false); }
                if (b.orb != null)
                {
                    b.orbScale = b.orb.localScale;
                    b.halo = b.orb.Find("Halo");
                    if (b.halo != null) b.haloScale = b.halo.localScale;
                    b.light = b.orb.GetComponentInChildren<Light>(true);
                    if (b.light != null) b.lightIntensity = b.light.intensity;
                }
            }
        }

        private void OnEnable() => EventBus.Subscribe<ChallengeCompletedEvent>(OnChallengeCompleted);
        private void OnDisable() => EventBus.Unsubscribe<ChallengeCompletedEvent>(OnChallengeCompleted);

        /// <summary>DDA knob (bowls have no text labels; the icons show what each takes): 2 = wrong drops don't name the bowl.</summary>
        public void SetLabelMode(int mode) => _labelMode = Mathf.Clamp(mode, 0, 2);

        /// <summary>Where to aim the first-person view: a little below the bowls, so the trash row fits under them.</summary>
        public Vector3 ViewTarget
        {
            get
            {
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (OfferingBowl b in _bowls)
                {
                    if (b.top == null) continue;
                    sum += b.top.gameObject.activeSelf ? b.topFinal : b.top.position;
                    n++;
                }
                return n > 0 ? sum / n - Vector3.up * 0.7f : transform.position;
            }
        }

        /// <summary>Restoring a save where the sorting is already done: the shrines are simply up.</summary>
        public void ShowRisen()
        {
            foreach (OfferingBowl b in _bowls)
            {
                if (b.shrine != null) { b.shrine.position = b.shrineFinal; b.shrine.gameObject.SetActive(true); }
                if (b.top != null) { b.top.position = b.topFinal; b.top.gameObject.SetActive(true); }
                if (b.orb != null) b.orb.localScale = b.orbScale;
            }
        }

        // ─────────────────────────────────────────────
        // Flow
        // ─────────────────────────────────────────────

        public void Begin(IReadOnlyList<OfferingItem> items, string zoneId)
        {
            _zoneId = zoneId;
            StartCoroutine(BeginRoutine(new List<OfferingItem>(items)));
        }

        private IEnumerator BeginRoutine(List<OfferingItem> items)
        {
            PlayerController.Instance?.SetMovementEnabled(false);
            InputManager.Instance?.SetInputEnabled(false);
            if (_dugongSpot != null && Lalim.Abilities.DugongCompanion.Instance != null)
                Lalim.Abilities.DugongCompanion.Instance.HoldAt(_dugongSpot);

            // The shrines rise one after another, then their orbs bloom.
            for (int i = 0; i < _bowls.Count; i++)
            {
                StartCoroutine(RiseShrine(_bowls[i]));
                yield return new WaitForSecondsRealtime(_riseStagger);
            }
            yield return new WaitForSecondsRealtime(_riseSeconds);
            foreach (OfferingBowl b in _bowls) StartCoroutine(BloomOrb(b));
            onAltarEmerged.Invoke();

            BuildOverlay();
            _queue.Clear();
            _slots = new OfferingItem[_visibleSlots];
            foreach (OfferingItem item in items)
            {
                if (item == null) continue;
                item.EnterSortMode();
                Bounds bounds = item.GetRenderBounds();
                _baseScale[item] = item.transform.localScale;
                _baseLargest[item] = Mathf.Max(0.0001f, Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)));
                item.gameObject.SetActive(false);
                _queue.Enqueue(item);
            }
            _totalItems = _queue.Count;
            _sortedCount = 0;
            for (int s = 0; s < _slots.Length; s++) FillSlot(s);

            ShowFeedback("Drag each piece into its bowl.", 4f);
            _interactive = true;
        }

        private IEnumerator RiseShrine(OfferingBowl b)
        {
            Vector3 down = Vector3.down * _riseDepth;
            if (b.shrine != null) { b.shrine.position = b.shrineFinal + down; b.shrine.gameObject.SetActive(true); }
            if (b.top != null) { b.top.position = b.topFinal + down; b.top.gameObject.SetActive(true); }
            if (b.orb != null) b.orb.localScale = Vector3.zero;
            for (float t = 0f; t < _riseSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / _riseSeconds);
                Vector3 offset = Vector3.Lerp(down, Vector3.zero, k);
                if (b.shrine != null) b.shrine.position = b.shrineFinal + offset;
                if (b.top != null) b.top.position = b.topFinal + offset;
                yield return null;
            }
            if (b.shrine != null) b.shrine.position = b.shrineFinal;
            if (b.top != null) b.top.position = b.topFinal;
        }

        private IEnumerator BloomOrb(OfferingBowl b)
        {
            if (b.orb == null) yield break;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.2f;
                b.orb.localScale = b.orbScale * Mathf.SmoothStep(0f, 1f, k) * overshoot;
                yield return null;
            }
            b.orb.localScale = b.orbScale;
        }

        private void FillSlot(int slot)
        {
            if (_slots[slot] != null || _queue.Count == 0) return;
            OfferingItem item = _queue.Dequeue();
            _slots[slot] = item;
            item.gameObject.SetActive(true);
            item.transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
            Camera cam = Camera.main;
            if (cam != null) item.transform.position = SlotPosition(cam, slot) - cam.transform.up * 0.6f;  // floats up into place
            SetWorldSize(item, ScreenSizeToWorld(cam));
        }

        // ─────────────────────────────────────────────
        // Per frame: float the row, handle dragging
        // ─────────────────────────────────────────────

        private void LateUpdate()
        {
            AnimateOrbs();   // the orbs keep glowing after the sorting too
            if (_slots == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            float size = ScreenSizeToWorld(cam);
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            for (int s = 0; s < _slots.Length; s++)
            {
                OfferingItem item = _slots[s];
                if (item == null || item == _held || _busy.Contains(item)) continue;
                float bob = Mathf.Sin(Time.unscaledTime * 1.6f + s * 1.3f) * 0.025f;
                Vector3 target = SlotPosition(cam, s) + cam.transform.up * bob;
                item.transform.position = Vector3.Lerp(item.transform.position, target, k);
                item.transform.Rotate(Vector3.up, 25f * Time.unscaledDeltaTime, Space.World);
                SetWorldSize(item, size);
            }

            if (_interactive) HandlePointer(cam, size);
        }

        private void HandlePointer(Camera cam, float size)
        {
            if (!ReadPointer(out Vector2 pos, out bool pressed, out bool released, out int pointerId)) return;
            float h = Screen.height;

            if (_held == null)
            {
                if (!pressed || OverUI(pointerId)) return;
                OfferingItem best = null;
                float bestDist = _grabRadius * h;
                foreach (OfferingItem item in _slots)
                {
                    if (item == null || _busy.Contains(item)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(item.GetRenderBounds().center);
                    float d = Vector2.Distance(pos, sp);
                    if (sp.z > 0f && d < bestDist) { best = item; bestDist = d; }
                }
                if (best == null) return;
                _held = best;
                _heldPointer = pointerId;
                ShowFeedback(best.DisplayName, 1.5f);
                return;
            }

            if (pointerId != _heldPointer) return;

            // Follow the finger at the row's depth, a little bigger while held.
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(pos.x, pos.y, _floatDepth));
            float follow = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            _held.transform.position = Vector3.Lerp(_held.transform.position, world, follow);
            SetWorldSize(_held, size * 1.15f);

            _hover = null;
            float bestBowl = _dropRadius * h;
            foreach (OfferingBowl b in _bowls)
            {
                Vector3 sp = cam.WorldToScreenPoint(BowlCenter(b));
                float d = Vector2.Distance(pos, sp);
                if (sp.z > 0f && d < bestBowl) { _hover = b; bestBowl = d; }
            }

            if (!released) return;
            OfferingItem dropped = _held;
            OfferingBowl bowl = _hover;
            _held = null;
            _heldPointer = -1;
            _hover = null;
            if (bowl != null) Resolve(dropped, bowl);
        }

        private void Resolve(OfferingItem item, OfferingBowl bowl)
        {
            int slot = System.Array.IndexOf(_slots, item);
            if (item.Category == bowl.category)
            {
                if (slot >= 0) _slots[slot] = null;
                _sortedCount++;
                StartCoroutine(AcceptRoutine(item, bowl, slot, _sortedCount >= _totalItems));
            }
            else
            {
                ChallengeManager.Instance?.ReportError();
                ShowFeedback(_labelMode == 2 ? "That bowl refuses it. Look again." : $"The {bowl.category} bowl refuses it. Look again.", 2f);
                StartCoroutine(ShakeRoutine(item));
            }
        }

        private IEnumerator AcceptRoutine(OfferingItem item, OfferingBowl bowl, int slot, bool last)
        {
            _busy.Add(item);
            Transform t = item.transform;
            Vector3 start = t.position;
            Vector2 ring = Random.insideUnitCircle * 0.3f;
            Vector3 end = (bowl.dropPoint != null ? bowl.dropPoint.position : BowlCenter(bowl)) + new Vector3(ring.x, 0.05f * bowl.count % 0.3f, ring.y);
            Camera cam = Camera.main;
            float startSize = cam != null ? ScreenSizeToWorld(cam) * 1.15f : _inBowlSize;
            for (float el = 0f; el < _acceptSeconds; el += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, el / _acceptSeconds);
                t.position = Vector3.Lerp(start, end, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.6f);
                SetWorldSize(item, Mathf.Lerp(startSize, _inBowlSize, k));
                yield return null;
            }
            t.position = end;
            SetWorldSize(item, _inBowlSize);
            if (bowl.top != null) t.SetParent(bowl.top, true);
            bowl.count++;
            _busy.Remove(item);
            if (slot >= 0) FillSlot(slot);
            StartCoroutine(PulseOrb(bowl));

            if (last) yield return new WaitForSecondsRealtime(0.8f);   // let the last piece settle before it ends
            ChallengeManager.Instance?.ReportTaskCompleted();
        }

        private IEnumerator ShakeRoutine(OfferingItem item)
        {
            _busy.Add(item);
            Transform t = item.transform;
            Vector3 start = t.position;
            Camera cam = Camera.main;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            for (float el = 0f; el < 0.35f; el += Time.unscaledDeltaTime)
            {
                t.position = start + right * Mathf.Sin(el * 60f) * 0.04f * (1f - el / 0.35f);
                yield return null;
            }
            _busy.Remove(item);   // LateUpdate floats it back to its slot
        }

        // ─────────────────────────────────────────────
        // Orbs
        // ─────────────────────────────────────────────

        private readonly Dictionary<OfferingBowl, float> _pulse = new Dictionary<OfferingBowl, float>();

        private IEnumerator PulseOrb(OfferingBowl b)
        {
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                _pulse[b] = Mathf.Sin(t / 0.5f * Mathf.PI) * 0.35f;
                yield return null;
            }
            _pulse[b] = 0f;
        }

        private void AnimateOrbs()
        {
            foreach (OfferingBowl b in _bowls)
            {
                if (b.orb == null || !b.orb.gameObject.activeInHierarchy || b.orb.localScale == Vector3.zero) continue;
                float pulse;
                _pulse.TryGetValue(b, out pulse);
                float hover = b == _hover ? 0.25f : 0f;
                float bob = Mathf.Sin(Time.unscaledTime * 1.4f + (int)b.category) * 0.06f;
                b.orb.localScale = Vector3.Lerp(b.orb.localScale, b.orbScale * (1f + hover + pulse), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                b.orb.localPosition = new Vector3(b.orb.localPosition.x, OrbBaseY(b) + bob, b.orb.localPosition.z);

                // The glow breathes, and flares while a piece hovers over it or lands in it.
                float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f + (int)b.category * 1.7f);
                float flare = 1f + hover * 2f + pulse * 2f;
                if (b.halo != null) b.halo.localScale = b.haloScale * (0.9f + 0.2f * breath) * (1f + hover + pulse);
                if (b.light != null) b.light.intensity = b.lightIntensity * (0.75f + 0.5f * breath) * flare;
            }
        }

        private readonly Dictionary<OfferingBowl, float> _orbBaseY = new Dictionary<OfferingBowl, float>();

        private float OrbBaseY(OfferingBowl b)
        {
            float y;
            if (!_orbBaseY.TryGetValue(b, out y)) { y = b.orb.localPosition.y; _orbBaseY[b] = y; }
            return y;
        }

        // ─────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────

        private Vector3 SlotPosition(Camera cam, int slot)
        {
            int n = _slots.Length;
            float x = n == 1 ? 0.5f : 0.5f - _rowWidth * 0.5f + _rowWidth * slot / (n - 1);
            return cam.ViewportToWorldPoint(new Vector3(x, _rowHeight, _floatDepth));
        }

        private float ScreenSizeToWorld(Camera cam)
        {
            if (cam == null) return _inBowlSize;
            float screenHeightAtDepth = 2f * _floatDepth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return screenHeightAtDepth * _itemScreenSize;
        }

        private void SetWorldSize(OfferingItem item, float size)
        {
            if (!_baseLargest.TryGetValue(item, out float largest)) return;
            item.transform.localScale = _baseScale[item] * (size / largest);
        }

        private static Vector3 BowlCenter(OfferingBowl b)
        {
            if (b.orb != null) return b.orb.position;
            if (b.dropPoint != null) return b.dropPoint.position;
            return b.top != null ? b.top.position : Vector3.zero;
        }

        /// <summary>One finger (or the mouse): its position, and whether it went down / up this frame.</summary>
        private static bool ReadPointer(out Vector2 pos, out bool pressed, out bool released, out int id)
        {
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                pos = t.position;
                id = t.fingerId;
                pressed = t.phase == TouchPhase.Began;
                released = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                return true;
            }
            pos = Input.mousePosition;
            id = -100;
            pressed = Input.GetMouseButtonDown(0);
            released = Input.GetMouseButtonUp(0);
            return pressed || released || Input.GetMouseButton(0);
        }

        private static bool OverUI(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return pointerId == -100 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId);
        }

        // ─────────────────────────────────────────────
        // On-screen text
        // ─────────────────────────────────────────────

        private void BuildOverlay()
        {
            if (_overlay != null) { _overlay.SetActive(true); return; }
            _overlay = new GameObject("SortingOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _overlay.transform.SetParent(transform, false);
            var canvas = _overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = _overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _feedbackText = MakeText("Feedback", new Vector2(0.5f, 1f), new Vector2(0f, -40f), 38f);
        }

        private TMP_Text MakeText(string name, Vector2 anchor, Vector2 offset, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_overlay.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(1400f, 60f);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.color = Color.white;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 160);
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private void ShowFeedback(string message, float seconds)
        {
            if (_feedbackText == null) return;
            if (_feedback != null) StopCoroutine(_feedback);
            _feedback = StartCoroutine(FeedbackRoutine(message, seconds));
        }

        private IEnumerator FeedbackRoutine(string message, float seconds)
        {
            _feedbackText.text = message;
            yield return new WaitForSecondsRealtime(seconds);
            _feedbackText.text = "";
            _feedback = null;
        }

        private void OnChallengeCompleted(ChallengeCompletedEvent evt)
        {
            if (evt.ChallengeId != _zoneId || !evt.Success) return;
            _interactive = false;
            _slots = null;
            if (_overlay != null) _overlay.SetActive(false);
            if (Lalim.Abilities.DugongCompanion.Instance != null) Lalim.Abilities.DugongCompanion.Instance.ReleaseHold();
            PlayerController.Instance?.SetMovementEnabled(true);
            InputManager.Instance?.SetInputEnabled(true);
        }

#if UNITY_EDITOR
        public void EditorSetup(List<OfferingBowl> bowls, Transform dugongSpot)
        {
            _bowls = bowls;
            _dugongSpot = dugongSpot;
        }

        public IReadOnlyList<OfferingBowl> EditorBowls => _bowls;
#endif
    }
}
