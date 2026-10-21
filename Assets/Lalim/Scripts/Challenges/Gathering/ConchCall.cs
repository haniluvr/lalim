using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Inventory;
using Lalim.Mechanics;

namespace Lalim.Challenges.Gathering
{
    /// <summary>
    /// The Radiant Conch's call. When its HUD button is tapped the diver raises the shell and a
    /// glowing mandala of sound ripples out across the water: two rings and a slowly turning ring
    /// of spokes, growing to <see cref="Radius"/>. Each scattered herd it passes calms (yellow →
    /// blue) and joins the ribbon behind the diver. Then the conch rests for <see cref="Cooldown"/>.
    ///
    /// Used in Caluya to gather the herds and again anywhere in Level 2 to re-gather dispersed
    /// schools. The active minigame sets the radius and cooldown from its difficulty profile.
    /// </summary>
    public class ConchCall : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float _radius = 12f;
        [SerializeField, Min(0.5f)] private float _cooldown = 6f;
        [SerializeField, Min(0.2f)] private float _rippleSeconds = 1.1f;
        [Tooltip("Additive glow material for the rings.")]
        [SerializeField] private Material _rippleMaterial;
        [Tooltip("Optional: the conch model shown in front of the diver while it sounds.")]
        [SerializeField] private GameObject _conchModel;
        [SerializeField] private AudioClip _callClip;
        [SerializeField] private Color _rippleColor = new Color(1f, 0.92f, 0.65f);

        public static ConchCall Instance { get; private set; }

        public float Radius { get => _radius; set => _radius = Mathf.Max(2f, value); }
        public float Cooldown { get => _cooldown; set => _cooldown = Mathf.Max(0.5f, value); }

        /// <summary>When the last call's sound finishes (Time.time): success sounds wait for it, so the two never overlap.</summary>
        public static float SoundingUntil { get; private set; }

        /// <summary>Raised after each call with the number of herds it reached.</summary>
        public static event System.Action<int> Called;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnEnable() => EventBus.Subscribe<RelicUsedEvent>(OnRelicUsed);
        private void OnDisable() => EventBus.Unsubscribe<RelicUsedEvent>(OnRelicUsed);

        private void OnRelicUsed(RelicUsedEvent evt)
        {
            if (evt.ItemId != GameConstants.Relics.RADIANT_CONCH) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            RelicLoadout.StartCooldown(GameConstants.Relics.RADIANT_CONCH, _cooldown);
            ChallengeManager.Instance?.ReportPlayerAction();
            GameAudio.PlayAt(_callClip, player.transform.position, 0.9f);
            if (_callClip != null) SoundingUntil = Time.time + _callClip.length;
            StartCoroutine(Ripple(player.transform));
        }

        private IEnumerator Ripple(Transform diver)
        {
            // The rings lie flat on the water plane a little below the diver, so they never sweep
            // through the camera.
            Vector3 origin = diver.position + Vector3.down * 0.6f;
            GameObject shell = ShowShell(diver);

            // Herds in reach, called as the ring passes them.
            var pending = new List<(Herd herd, float at)>();
            foreach (Herd h in Herd.All)
            {
                if (h == null || h.State != Herd.HerdState.Scattered) continue;
                float d = Vector3.Distance(h.Center, origin);
                if (d <= _radius) pending.Add((h, d / _radius));
            }
            Called?.Invoke(pending.Count);

            var rings = new[] { MakeRing("Ring_A", 96, 0.4f), MakeRing("Ring_B", 96, 0.25f) };
            LineRenderer spokes = MakeRing("Spokes", 0, 0.14f);
            spokes.loop = false;
            const int SpokeCount = 8;
            spokes.positionCount = SpokeCount * 3;

            float t = 0f;
            while (t < _rippleSeconds + 0.35f)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / _rippleSeconds);
                float b = Mathf.Clamp01((t - 0.18f) / _rippleSeconds);
                SetCircle(rings[0], origin, Mathf.Lerp(0.5f, _radius, EaseOut(a)), (1f - a) * 0.8f);
                SetCircle(rings[1], origin, Mathf.Lerp(0.5f, _radius * 0.82f, EaseOut(b)), (1f - b) * 0.6f);
                SetSpokes(spokes, origin, Mathf.Lerp(0.4f, _radius * 0.6f, EaseOut(a)), t * 40f, SpokeCount, (1f - a) * 0.5f);

                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (EaseOut(a) < pending[i].at) continue;
                    pending[i].herd?.Call();
                    pending.RemoveAt(i);
                }
                yield return null;
            }

            foreach (var p in pending) p.herd?.Call();
            foreach (LineRenderer r in rings) Destroy(r.gameObject);
            Destroy(spokes.gameObject);
            if (shell != null) Destroy(shell);
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        // ───────────────────────── Visuals ─────────────────────────

        private GameObject ShowShell(Transform diver)
        {
            if (_conchModel == null) return null;
            GameObject shell = Instantiate(_conchModel, diver.position + diver.forward * 0.7f + Vector3.up * 0.45f, diver.rotation);
            foreach (Collider c in shell.GetComponentsInChildren<Collider>()) Destroy(c);
            shell.transform.localScale *= 0.35f;
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(shell.transform, false);
            light.type = LightType.Point;
            light.color = _rippleColor;
            light.range = 6f;
            light.intensity = 3f;
            return shell;
        }

        private LineRenderer MakeRing(string name, int points, float width)
        {
            var go = new GameObject("[ConchRipple] " + name);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // TransformZ faces up: the lines lie flat
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = points;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;
            if (_rippleMaterial != null) lr.sharedMaterial = _rippleMaterial;
            return lr;
        }

        private void SetCircle(LineRenderer lr, Vector3 centre, float radius, float alpha)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float ang = i * Mathf.PI * 2f / n;
                // A soft wobble so it reads as sound, not a perfect hoop.
                float r = radius * (1f + 0.025f * Mathf.Sin(ang * 6f + Time.time * 3f));
                lr.SetPosition(i, centre + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r));
            }
            Color c = _rippleColor;
            c.a = Mathf.Clamp01(alpha);
            lr.startColor = lr.endColor = c;
        }

        private void SetSpokes(LineRenderer lr, Vector3 centre, float radius, float spinDegrees, int count, float alpha)
        {
            // Each spoke is drawn out and back along the same line, so one renderer draws them all.
            for (int i = 0; i < count; i++)
            {
                float ang = (spinDegrees + i * 360f / count) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                lr.SetPosition(i * 3, centre + dir * radius * 0.55f);
                lr.SetPosition(i * 3 + 1, centre + dir * radius * 0.8f);
                lr.SetPosition(i * 3 + 2, centre + dir * radius * 0.55f);
            }
            Color c = Color.Lerp(_rippleColor, Herd.CalmColor, 0.5f);
            c.a = Mathf.Clamp01(alpha);
            lr.startColor = lr.endColor = c;
        }

#if UNITY_EDITOR
        public void EditorSetup(Material rippleMaterial, GameObject conchModel)
        {
            _rippleMaterial = rippleMaterial;
            _conchModel = conchModel;
        }
#endif
    }
}
