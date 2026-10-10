using System;
using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Basic hitscan raycast weapon.
    /// Performs instantaneous ray hit detection, applies damage through IDamageable,
    /// and invokes rich audio/visual effects (procedural gunshot, tracer, impact spark).
    /// </summary>
    public class HitscanWeapon : MonoBehaviour, IWeapon
    {
        [Header("Weapon Tuning")]
        [SerializeField] private float damagePerShot = 25f;
        [SerializeField] private float maxRange = 100f;
        [SerializeField] private float fireRate = 4f; // Shots per second (4 = 0.25s cooldown)

        [Header("Targeting")]
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Visual & Audio Feedback")]
        [SerializeField] private bool enableAudiovisualFX = true;
        [SerializeField] private Transform muzzlePoint;

        private float nextAllowedFireTime = 0f;

        public float DamagePerShot => damagePerShot;
        public float MaxRange => maxRange;
        public float FireRate => fireRate;
        public float CooldownDuration => fireRate > 0f ? (1f / fireRate) : 0f;

        public bool CanFire => Time.time >= nextAllowedFireTime;
        public float CooldownRemaining => Mathf.Max(0f, nextAllowedFireTime - Time.time);

        public Transform MuzzlePoint
        {
            get => muzzlePoint;
            set => muzzlePoint = value;
        }

        /// <summary>
        /// Event invoked whenever a shot is accepted and fired.
        /// Parameters: (origin, hitPointOrEndpoint, hitDamageable)
        /// </summary>
        public event Action<Vector3, Vector3, bool> OnShotFired;

        public bool TryFire(Vector3 origin, Vector3 direction, GameObject instigator)
        {
            if (!CanFire)
            {
                return false;
            }

            nextAllowedFireTime = Time.time + CooldownDuration;
            ExecuteHitscanShot(origin, direction.normalized, instigator);
            return true;
        }

        private void ExecuteHitscanShot(Vector3 origin, Vector3 direction, GameObject instigator)
        {
            Ray ray = new Ray(origin, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, maxRange, hitLayers, triggerInteraction);

            // Sort hits by distance
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit? validHit = null;

            foreach (var hit in hits)
            {
                // Ignore the instigator and its child colliders to prevent self-damage
                if (instigator != null && (hit.collider.gameObject == instigator || hit.collider.transform.IsChildOf(instigator.transform)))
                {
                    continue;
                }

                validHit = hit;
                break;
            }

            Vector3 endPoint;
            bool hitDamageable = false;
            Vector3 hitNormal = -direction;

            if (validHit.HasValue)
            {
                RaycastHit hit = validHit.Value;
                endPoint = hit.point;
                hitNormal = hit.normal;

                // Hit detection uses IDamageable
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.CanTakeDamage)
                {
                    DamageInfo damageInfo = new DamageInfo(damagePerShot, instigator, hit.point, direction);
                    target.TakeDamage(damageInfo);
                    hitDamageable = true;
                }
            }
            else
            {
                endPoint = origin + direction * maxRange;
            }

            // Audiovisual Effects
            if (enableAudiovisualFX)
            {
                Vector3 tracerStart = muzzlePoint != null ? muzzlePoint.position : origin;
                CombatEffects.PlayGunshot(origin);
                CombatEffects.SpawnTracer(tracerStart, endPoint, this);

                if (validHit.HasValue)
                {
                    CombatEffects.SpawnImpactSpark(endPoint, hitNormal, this);
                    if (hitDamageable)
                    {
                        CombatEffects.PlayHitImpact(endPoint);
                    }
                }
            }

            OnShotFired?.Invoke(origin, endPoint, hitDamageable);
        }
    }
}
