using System;
using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Reusable health component that implements IDamageable.
    /// Can be used on players, AI characters, destructibles, or target dummies.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Runtime State")]
        [SerializeField] private float currentHealth;
        [SerializeField] private bool isDefeated;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => !isDefeated && currentHealth > 0f;
        public bool CanTakeDamage => IsAlive;

        // Events for UI, AI, and audio/effects systems to listen to
        public event Action<float, float> OnHealthChanged; // (currentHealth, maxHealth)
        public event Action<DamageInfo> OnDamaged;
        public event Action<DamageInfo> OnDefeated;

        private void Awake()
        {
            currentHealth = maxHealth;
            isDefeated = false;
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!CanTakeDamage || damageInfo.Amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth - damageInfo.Amount, 0f, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamaged?.Invoke(damageInfo);

            if (currentHealth <= 0f && !isDefeated)
            {
                isDefeated = true;
                OnDefeated?.Invoke(damageInfo);
            }
        }

        /// <summary>
        /// Resets health back to full for respawns or test resets.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDefeated = false;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
