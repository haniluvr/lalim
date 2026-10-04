using UnityEngine;
using UnityEngine.Rendering;
using Lalim.Core;
using Lalim.Mechanics;

namespace Lalim.Abilities
{
    /// <summary>
    /// Ultimate: a 20s bubble around the player that blocks all toxic-water damage and absorbs
    /// one contact hit. No stamina and no cooldown — it charges from ACTIVITY: while the player
    /// swims (~90s of movement from empty), plus a bonus every time another skill is used
    /// (the inventory never counts). Idle time charges nothing. Starts each level half-charged;
    /// empties when used and can't charge while the bubble is up. Radius is 3m solo, 5m when the dugong is mounted or within 6m.
    /// After the contact charge is used the bubble flickers but keeps blocking toxic water
    /// until the duration runs out. Also removes the toxic-water stamina regen penalty.
    /// </summary>
    public class BubbleBarrierAbility : AbilityBase
    {
        public override AbilityId Id => AbilityId.BubbleBarrier;
        public override int CostOrbs => Config != null ? Config.bubbleBarrierCostOrbs : GameConstants.Abilities.BUBBLE_BARRIER_COST_ORBS;
        protected override float DurationSeconds => Config != null ? Config.bubbleBarrierDuration : GameConstants.Abilities.BUBBLE_BARRIER_DURATION;
        protected override float CooldownSeconds => 0f; // no cooldown: it charges from activity instead
        private float _charge;

        /// <summary>Activity charge 0..1. Empty while the bubble is up.</summary>
        public override float Charge01 => IsActive ? 0f : _charge;
        public override bool IsCharged => State == AbilityState.Ready && _charge >= 1f;
        protected override bool RequiresFreeMovement => true;

        [Header("Visuals")]
        [Tooltip("Optional bubble visual (unit sphere). If empty, a sphere is created at runtime with the bubble material.")]
        [SerializeField] private Transform _bubbleVisual;
        [Tooltip("Material for the runtime bubble (Lalim/BubbleRim). Assigned by Tools > Lalim > Abilities setup.")]
        [SerializeField] private Material _bubbleMaterial;
        [Tooltip("Visible bubble size as a fraction of the protection radius (3m solo / 5m with the dugong). Keeps it hugging the player so it reads as a sphere.")]
        [SerializeField, Range(0.2f, 1f)] private float _visualScale = 0.45f;
        [Tooltip("The visible bubble never exceeds this fraction of the camera's distance, so the camera always sees it from outside.")]
        [SerializeField, Range(0.2f, 0.9f)] private float _maxCameraFraction = 0.6f;
        [SerializeField] private float _minVisualRadius = 0.9f;
        [Tooltip("Rough radius of the player's body, used when wrapping the bubble around player + dugong.")]
        [SerializeField] private float _playerBodyRadius = 0.9f;
        [Tooltip("How far the camera eases back while the bubble is up, so it's seen from outside.")]
        [SerializeField] private float _cameraPullBack = 2.2f;
        [SerializeField] private float _radiusLerpSpeed = 4f;
        [SerializeField] private AudioClip _raiseSfx;
        [SerializeField] private AudioClip _popHitSfx;

        public int ContactChargesRemaining { get; private set; }
        public float CurrentRadius { get; private set; }

        private Renderer _bubbleRenderer;
        private MaterialPropertyBlock _mpb;
        private float _flickerTimer;

        private float SoloRadius => Config != null ? Config.bubbleBarrierRadiusSolo : GameConstants.Abilities.BUBBLE_BARRIER_RADIUS_SOLO;
        private float DugongRadius => Config != null ? Config.bubbleBarrierRadiusWithDugong : GameConstants.Abilities.BUBBLE_BARRIER_RADIUS_WITH_DUGONG;
        private float DugongProximity => Config != null ? Config.bubbleBarrierDugongProximity : GameConstants.Abilities.BUBBLE_BARRIER_DUGONG_PROXIMITY;

        protected override void Awake()
        {
            base.Awake();
            _mpb = new MaterialPropertyBlock();
            EnsureVisual();
            SetVisualActive(false);
        }

        protected override void Start()
        {
            base.Start();
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.Barrier = this;
            // Half-charged at the start of each level (the player is re-created per level).
            _charge = Mathf.Clamp01(Config != null ? Config.bubbleBarrierStartCharge : 0.5f);
        }

        private void OnEnable() => EventBus.Subscribe<AbilityActivatedEvent>(OnSkillUsed);

        protected override void Update()
        {
            base.Update();
            ChargeFromMovement(GameplayClock.DeltaTime);
        }

        public override float ChargeForSave => IsActive ? 0f : _charge;

        protected override void RestoreCharge(float charge) => _charge = Mathf.Clamp01(charge);

        protected override AbilityBlockReason CheckContext()
        {
            return _charge >= 1f ? AbilityBlockReason.None : AbilityBlockReason.NotCharged;
        }

        /// <summary>Dev-tool reset: fill the charge.</summary>
        public override void ResetCooldown()
        {
            base.ResetCooldown();
            _charge = 1f;
        }

        private void ChargeFromMovement(float deltaTime)
        {
            if (IsActive || _charge >= 1f || deltaTime <= 0f) return;
            PlayerController player = PlayerController.Instance;
            if (player == null || !player.IsMovementEnabled) return;
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

            float minSpeed = Config != null ? Config.bubbleBarrierMinMoveSpeed : 0.5f;
            if (player.Velocity.magnitude < minSpeed) return;

            float rate = Config != null ? Config.bubbleBarrierChargePerSecondMoving : 1f / 90f;
            if (player.IsMounted) rate *= Config != null ? Config.bubbleBarrierMountedChargeFactor : 0.5f;
            AddCharge(rate * deltaTime);
        }

        private void OnSkillUsed(AbilityActivatedEvent evt)
        {
            if (evt.Ability == AbilityId.BubbleBarrier || IsActive) return;
            AddCharge(Config != null ? Config.GetBarrierChargeForSkill(evt.Ability) : 0.03f);
        }

        private void AddCharge(float amount)
        {
            _charge = Mathf.Clamp01(_charge + amount);
        }

        /// <summary>Called by PlayerHealth before applying damage. Returns true if this hit is absorbed.</summary>
        public bool TryBlock(DamageKind kind)
        {
            if (!IsActive) return false;

            if (kind == DamageKind.Toxic) return true;

            if (kind == DamageKind.Contact && ContactChargesRemaining > 0)
            {
                ContactChargesRemaining--;
                _flickerTimer = 0.6f;
                if (_popHitSfx != null) GameAudio.PlayAt(_popHitSfx, transform.position);
                return true;
            }

            return false;
        }

        protected override void OnActivate()
        {
            _charge = 0f; // spent — refills from 0 through activity after the bubble ends
            ContactChargesRemaining = Config != null ? Config.bubbleBarrierContactCharges : 1;
            CurrentRadius = SoloRadius;
            if (PlayerStamina.Instance != null) PlayerStamina.Instance.SetBarrierBlockingToxic(true);

            // Ease the camera back so the bubble (now wrapping the dugong too) is seen from outside.
            if (CameraController.Instance != null) CameraController.Instance.SetExtraDistance(this, _cameraPullBack);

            SetVisualActive(true);
            if (_raiseSfx != null) GameAudio.PlayAt(_raiseSfx, transform.position);
        }

        protected override void OnTick(float deltaTime)
        {
            bool dugongClose = (PlayerController.Instance != null && PlayerController.Instance.IsMounted)
                || (DugongCompanion.Instance != null && DugongCompanion.Instance.IsWithin(DugongProximity));
            float targetRadius = dugongClose ? DugongRadius : SoloRadius;
            CurrentRadius = Mathf.MoveTowards(CurrentRadius, targetRadius, _radiusLerpSpeed * deltaTime);

            UpdateVisual(deltaTime);
        }

        protected override void OnEnd(bool cancelled)
        {
            ContactChargesRemaining = 0;
            if (PlayerStamina.Instance != null) PlayerStamina.Instance.SetBarrierBlockingToxic(false);
            if (CameraController.Instance != null) CameraController.Instance.ClearExtraDistance(this);
            SetVisualActive(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AbilityActivatedEvent>(OnSkillUsed);
            ForceEnd();
        }

        // ─────────────────────────────────────────────
        // Visuals
        // ─────────────────────────────────────────────

        /// <summary>World-space radius of the visible bubble this frame (≤ the protection radius).</summary>
        public float VisualRadius { get; private set; }

        private void UpdateVisual(float deltaTime)
        {
            if (_bubbleVisual == null) return;

            // Centre on the body, not the feet.
            CharacterController cc = GetComponent<CharacterController>();
            Vector3 playerCentre = transform.TransformPoint(cc != null ? cc.center : Vector3.up * 0.9f);
            Vector3 centre = playerCentre;
            float radius = CurrentRadius * _visualScale;

            // The dugong shares the bubble when it's with the player (riding or swimming alongside):
            // centre between the two and grow just enough to wrap both bodies.
            DugongCompanion dugong = DugongCompanion.Instance;
            bool mounted = PlayerController.Instance != null && PlayerController.Instance.IsMounted;
            if (dugong != null && (mounted || dugong.IsWithin(DugongProximity)))
            {
                Vector3 dugongCentre = dugong.BodyCenter;
                centre = (playerCentre + dugongCentre) * 0.5f;
                float wrap = Mathf.Max(Vector3.Distance(centre, playerCentre) + _playerBodyRadius,
                                       Vector3.Distance(centre, dugongCentre) + dugong.BodyRadius);
                radius = Mathf.Max(radius, wrap);
            }
            _bubbleVisual.position = centre;

            // Keep the camera outside the bubble so it's seen as a physical sphere, not a screen
            // tint. The protection radius (CurrentRadius) is unaffected.
            Camera cam = Camera.main;
            if (cam != null)
            {
                float camDistance = Vector3.Distance(cam.transform.position, _bubbleVisual.position);
                radius = Mathf.Min(radius, camDistance * _maxCameraFraction);
            }
            VisualRadius = Mathf.Max(_minVisualRadius, radius);

            // Compensate for the player's own scale so the radius is in world meters.
            float parentScale = _bubbleVisual.parent != null ? Mathf.Max(0.0001f, _bubbleVisual.parent.lossyScale.x) : 1f;
            _bubbleVisual.localScale = Vector3.one * (VisualRadius * 2f / parentScale);

            if (_bubbleRenderer == null) return;

            float fade = 1f;
            if (_flickerTimer > 0f)
            {
                _flickerTimer -= deltaTime;
                fade = Mathf.PingPong(Time.time * 20f, 1f) > 0.5f ? 1f : 0.15f;
            }
            else if (ContactChargesRemaining <= 0)
            {
                // Hit charge spent: a weaker, shimmering bubble that's still visibly up.
                fade = 0.55f + Mathf.Sin(Time.time * 9f) * 0.15f;
            }

            // Warn during the last 3 seconds.
            if (DurationRemaining < 3f) fade *= Mathf.PingPong(Time.time * 4f, 1f) * 0.7f + 0.3f;

            _bubbleRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Fade", fade);
            _bubbleRenderer.SetPropertyBlock(_mpb);
        }

        private void SetVisualActive(bool active)
        {
            if (_bubbleVisual != null) _bubbleVisual.gameObject.SetActive(active);
            if (active) UpdateVisual(0f);
        }

        private void EnsureVisual()
        {
            if (_bubbleVisual != null)
            {
                _bubbleRenderer = _bubbleVisual.GetComponentInChildren<Renderer>();
                return;
            }

            Material mat = _bubbleMaterial;
            if (mat == null)
            {
                Shader shader = Shader.Find("Lalim/BubbleRim");
                if (shader == null) return;
                mat = new Material(shader);
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "BubbleBarrier (runtime)";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);

            _bubbleRenderer = go.GetComponent<Renderer>();
            _bubbleRenderer.sharedMaterial = mat;
            _bubbleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _bubbleRenderer.receiveShadows = false;
            _bubbleVisual = go.transform;
        }
    }
}
