using UnityEngine;
using Lalim.Core;

namespace Lalim.Abilities
{
    /// <summary>
    /// A hazard that deals a lump of contact damage on touch (debris burst, sludge
    /// pocket, drifting mine, etc.), then can't re-trigger for a short delay so the
    /// player isn't shredded by staying inside it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ContactHazard : MonoBehaviour
    {
        [SerializeField] private AbilityConfig _config;
        [SerializeField] private float _damageOverride = -1f; // -1 = use config default

        private float _cooldownRemaining;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f) _cooldownRemaining -= Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (_cooldownRemaining > 0f) return;
            if (!other.CompareTag(GameConstants.Tags.PLAYER)) return;

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;

            float damage = _damageOverride >= 0f
                ? _damageOverride
                : (_config != null ? _config.contactHazardDamage : GameConstants.Abilities.CONTACT_HAZARD_DAMAGE);

            health.TakeDamage(damage, DamageKind.Contact);
            _cooldownRemaining = _config != null ? _config.contactHazardReTriggerDelay : 1f;
        }
    }
}
