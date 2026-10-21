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
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private bool isDefeated = false;

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
            if (currentHealth <= 0f && !isDefeated)
            {
                currentHealth = maxHealth;
            }
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!CanTakeDamage || damageInfo.Amount <= 0f)
            {
                return;
            }

            // Player is extracted when mission is completed; ignore all post-extraction damage
            if (TacticalShooter.Systems.Mission.MissionManager.Instance != null &&
                TacticalShooter.Systems.Mission.MissionManager.Instance.CurrentState == TacticalShooter.Systems.Mission.MissionState.MissionCompleted)
            {
                if (CompareTag("Player") || GetComponent<PlayerController>() != null || GetComponent<PlayerCombatController>() != null)
                {
                    return;
                }
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
        /// Restores health up to maxHealth. Returns true if health was restored.
        /// </summary>
        public bool Heal(float amount)
        {
            if (!IsAlive || currentHealth >= maxHealth || amount <= 0f)
            {
                return false;
            }

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            return true;
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

        /// <summary>
        /// Configures maximum health and optionally heals to maximum.
        /// </summary>
        public void SetMaxHealth(float max, bool healToFull = true)
        {
            maxHealth = Mathf.Max(1f, max);
            if (healToFull)
            {
                currentHealth = maxHealth;
                isDefeated = false;
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}
