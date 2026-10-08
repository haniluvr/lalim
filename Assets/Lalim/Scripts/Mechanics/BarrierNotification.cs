using UnityEngine;
using TMPro;
using Lalim.Core;
using Lalim.UI;

namespace Lalim.Mechanics
{
    /// <summary>
    /// Tells the player why a barrier blocks them ("Complete Zone 1 to pass through.").
    ///
    /// Proximity-based: the player is a CharacterController, which never sends OnCollisionEnter,
    /// so the barrier checks the player's distance to its collider instead. Shown with the
    /// on-screen ZoneBannerUI. A barrier that has been unlocked is disabled, so any barrier that
    /// is still here is locked.
    ///
    /// A teleport arch's blocker (under an ArchPortalSurface) only warns when the player is trying
    /// to go through the portal itself — in front of the video, within its outline — not when
    /// swimming past or around the arch.
    /// </summary>
    public class BarrierNotification : MonoBehaviour
    {
        [SerializeField, TextArea(1, 2)] private string _warningMessage = "Complete Zone 1 to pass through.";
        [Tooltip("Show the message when the player gets this close to the barrier (metres).")]
        [SerializeField, Min(0.5f)] private float _showDistance = 3f;
        [Tooltip("Minimum seconds between two messages.")]
        [SerializeField, Min(1f)] private float _cooldownSeconds = 6f;

        [Header("Region gate (teleporter-style barriers only)")]
        [Tooltip("Only warn while this region is still locked. Off for zone barriers.")]
        [SerializeField] private bool _checkRegion;
        [SerializeField] private Region _targetRegion = Region.Visayas;
        [Tooltip("Arch blockers: how close (metres) to the portal video counts as trying to enter.")]
        [SerializeField, Min(0.2f)] private float _archReach = 1.5f;

        [Header("Legacy (unused): old world-space label")]
        [SerializeField] private CanvasGroup _notificationCanvasGroup;
        [SerializeField] private TMP_Text _notificationText;

        private Collider _collider;
        private ArchPortalSurface _arch;
        private float _nextAllowed;

        public string Message
        {
            get => _warningMessage;
            set => _warningMessage = value;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _arch = GetComponentInParent<ArchPortalSurface>();
            // The old world-space label floated far above the barrier; keep it hidden.
            if (_notificationCanvasGroup != null) _notificationCanvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (_collider == null || !_collider.enabled || Time.time < _nextAllowed) return;
            PlayerController player = PlayerController.Instance;
            if (player == null) return;
            if (_checkRegion && GameManager.Instance != null && GameManager.Instance.IsRegionAccessible(_targetRegion)) return;

            Vector3 p = player.transform.position;
            if (_arch != null) { if (!AtPortalOpening(player)) return; }
            else if ((_collider.ClosestPoint(p) - p).sqrMagnitude > _showDistance * _showDistance) return;

            _nextAllowed = Time.time + _cooldownSeconds;
            ZoneBannerUI.Post(_warningMessage);
        }

        // In the portal's own space (pivot bottom centre, XY plane): inside the opening's outline
        // and close to the video, from either side.
        private bool AtPortalOpening(PlayerController player)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            Vector3 body = player.transform.TransformPoint(cc != null ? cc.center : Vector3.up * 0.5f);
            Vector3 local = _arch.transform.InverseTransformPoint(body);
            float w = _arch.Width, h = _arch.Height;
            float metresPerUnit = Mathf.Max(0.0001f, Mathf.Abs(_arch.transform.lossyScale.z));
            return Mathf.Abs(local.x) <= w * 0.5f
                && local.y >= -0.1f * h && local.y <= h
                && Mathf.Abs(local.z) * metresPerUnit <= _archReach;
        }
    }
}
