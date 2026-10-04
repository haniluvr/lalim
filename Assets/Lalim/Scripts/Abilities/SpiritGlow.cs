using System.Collections.Generic;
using UnityEngine;
using Lalim.Core;
using Lalim.Challenges;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>What Spirit Sense knows when deciding which targets should glow.</summary>
    public struct SpiritSenseContext
    {
        public Vector3 Origin;
        public float Radius;
        public bool InChallenge;
        public ChallengeType Challenge;
        public Transform Objective;
        /// <summary>The debris the player is carrying right now, if any (SortingBin uses it).</summary>
        public DebrisItem CarriedDebris;

        /// <summary>In a minigame the whole area counts; in exploration only the reveal radius does.</summary>
        public bool InRange(Vector3 position) => InChallenge || (position - Origin).sqrMagnitude <= Radius * Radius;
    }

    /// <summary>
    /// Anything Spirit Sense can light up. Future minigames (Ghost Net, Hazard Defusal, ...) either
    /// add a SpiritGlow to whatever should light up, or implement this on their own component to
    /// supply custom relevance — Spirit Sense itself never needs to change.
    /// </summary>
    public interface ISpiritSenseTarget
    {
        bool IsRelevant(SpiritSenseContext ctx);
        void SetGlow(bool on, float intensity);
    }

    /// <summary>
    /// Shared glow visual + relevance for Spirit Sense targets. Uses a runtime point light (same
    /// approach as OfferingItem's glow) because we can't assume any emission setup on the meshes.
    /// On an OfferingItem it drives the item's own ToggleGlow instead of adding a second light.
    ///
    /// Spirit Sense attaches this automatically to the known target types on activation, so
    /// existing scenes need no setup. Add it manually to anything else that should glow.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpiritGlow : MonoBehaviour, ISpiritSenseTarget
    {
        public enum RelevanceMode
        {
            /// <summary>Decide from the sibling component (debris not yet sorted, coral slot not planted, ...).</summary>
            Auto,
            /// <summary>Always glows while in range.</summary>
            Always,
            /// <summary>Only glows when it's Spirit Sense's current objective.</summary>
            WhenObjective
        }

        private static readonly HashSet<SpiritGlow> s_All = new HashSet<SpiritGlow>();
        public static IReadOnlyCollection<SpiritGlow> All => s_All;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_All.Clear(); }

        [SerializeField] private RelevanceMode _mode = RelevanceMode.Auto;
        [SerializeField] private Color _color = new Color(0.15f, 1f, 0.85f);
        [SerializeField, Min(0f)] private float _intensity = 4f;
        [SerializeField, Min(0.5f)] private float _range = 6f;
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 5f;
        [SerializeField] private Vector3 _lightOffset = new Vector3(0f, 0.5f, 0f);

        private Light _light;
        private OfferingItem _offering;
        private ISpiritSenseTarget _customTarget;
        private float _blend;
        private float _target;
        private float _intensityScale = 1f;
        private float _phase;

        public bool IsGlowing => _target > 0f;

        /// <summary>Adds a SpiritGlow to 'host' if it doesn't have one yet.</summary>
        public static SpiritGlow EnsureOn(Component host, Color color, RelevanceMode mode = RelevanceMode.Auto)
        {
            if (host == null) return null;
            SpiritGlow glow = host.GetComponent<SpiritGlow>();
            if (glow != null) return glow;

            glow = host.gameObject.AddComponent<SpiritGlow>();
            glow._color = color;
            glow._mode = mode;
            return glow;
        }

        private void Awake()
        {
            _offering = GetComponent<OfferingItem>();
            _phase = Random.value * 10f;

            foreach (ISpiritSenseTarget t in GetComponents<ISpiritSenseTarget>())
            {
                if (!ReferenceEquals(t, this)) { _customTarget = t; break; }
            }
        }

        private void OnEnable() => s_All.Add(this);

        private void OnDisable()
        {
            s_All.Remove(this);
            _target = 0f;
            _blend = 0f;
            if (_light != null) _light.enabled = false;
        }

        public bool IsRelevant(SpiritSenseContext ctx)
        {
            if (!isActiveAndEnabled || !ctx.InRange(transform.position)) return false;
            if (_customTarget != null) return _customTarget.IsRelevant(ctx);

            switch (_mode)
            {
                case RelevanceMode.Always: return true;
                case RelevanceMode.WhenObjective: return ctx.Objective == transform;
                default: return SpiritSenseRelevance.Evaluate(gameObject, ctx);
            }
        }

        public void SetGlow(bool on, float intensity)
        {
            _intensityScale = Mathf.Max(0f, intensity);

            if (_offering != null)
            {
                _offering.ToggleGlow(on);
                return;
            }

            _target = on ? 1f : 0f;
            if (on) EnsureLight();
            _customTarget?.SetGlow(on, intensity);
        }

        private void Update()
        {
            if (_light == null) return;
            if (_target <= 0f && _blend <= 0f) return;

            _blend = Mathf.MoveTowards(_blend, _target, Time.deltaTime * _fadeSpeed);
            float pulse = 1f + Mathf.Sin((Time.time + _phase) * 2.2f) * 0.2f;
            _light.enabled = _blend > 0.01f;
            _light.intensity = _intensity * _intensityScale * _blend * pulse;
        }

        private void EnsureLight()
        {
            if (_light != null) return;
            GameObject go = new GameObject("SpiritGlowLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = _lightOffset;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _color;
            _light.range = _range;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
        }
    }

    /// <summary>
    /// Relevance rules for the existing target types, kept in one place so none of the
    /// challenge scripts need Spirit Sense code of their own.
    /// </summary>
    /// <summary>Lets new target types (minigame areas, stations) say whether they still need the player.</summary>
    public interface ISpiritSenseRelevant
    {
        bool IsSpiritRelevant { get; }
    }

    public static class SpiritSenseRelevance
    {
        public static bool Evaluate(GameObject go, SpiritSenseContext ctx)
        {
            if (go.TryGetComponent(out OfferingItem offering)) return !offering.IsCollected && offering.PickupEnabled;
            if (go.TryGetComponent(out DebrisItem debris)) return !debris.IsSorted && !debris.IsCarried;
            if (go.TryGetComponent(out CoralFragment fragment)) return !fragment.IsCollected;
            if (go.TryGetComponent(out CoralSlot slot)) return !slot.IsPlanted && !slot.IsPlanting;
            if (go.TryGetComponent(out SamplePoint sample)) return !sample.IsSampled && !sample.IsSampling;

            // A bin only lights up when it's the right one for what the player is holding.
            if (go.TryGetComponent(out SortingBin bin))
                return ctx.CarriedDebris != null && ctx.CarriedDebris.Category == bin.AcceptedCategory;

            // Statues glow when they're the objective or have something to say right now.
            if (go.TryGetComponent(out StatueDialogueRouter statue))
                return ctx.Objective == go.transform || statue.HasPendingQuest;

            if (go.TryGetComponent(out ISpiritSenseRelevant custom)) return custom.IsSpiritRelevant;

            if (go.TryGetComponent(out DialogueTrigger _)) return ctx.Objective == go.transform;

            if (go.TryGetComponent(out HiddenObject _)) return true;

            return true;
        }

        /// <summary>Attaches SpiritGlow to every known target type in the loaded scenes.</summary>
        public static void AttachToKnownTargets()
        {
            Color item = new Color(0.15f, 1f, 0.85f);
            Color place = new Color(1f, 0.85f, 0.3f);
            Color npc = new Color(0.75f, 0.55f, 1f);

            foreach (OfferingItem o in OfferingItem.ActiveItems) SpiritGlow.EnsureOn(o, item);
            Attach<DebrisItem>(item);
            Attach<CoralFragment>(item);
            Attach<CoralSlot>(place);
            Attach<SamplePoint>(place);
            Attach<SortingBin>(place);
            Attach<HiddenObject>(item);
            Attach<StatueDialogueRouter>(npc);
            Attach<DialogueTrigger>(npc, SpiritGlow.RelevanceMode.WhenObjective);
            Attach<Lalim.Mechanics.Quest.AreaStation>(place);
        }

        private static void Attach<T>(Color color, SpiritGlow.RelevanceMode mode = SpiritGlow.RelevanceMode.Auto) where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Exclude))
            {
                SpiritGlow.EnsureOn(c, color, mode);
            }
        }

        public static DebrisItem FindCarriedDebris()
        {
            foreach (DebrisItem d in Object.FindObjectsByType<DebrisItem>(FindObjectsInactive.Exclude))
            {
                if (d.IsCarried) return d;
            }
            return null;
        }
    }
}
