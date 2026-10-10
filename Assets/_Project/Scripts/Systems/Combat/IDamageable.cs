namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Shared damage contract implemented by any object that can receive damage.
    /// Allows weapons to apply damage without hard-coding dependencies on specific health or character classes.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Indicates if the entity is currently alive.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Indicates if the entity can currently receive damage.
        /// </summary>
        bool CanTakeDamage { get; }

        /// <summary>
        /// Applies damage using the provided damage specification.
        /// </summary>
        void TakeDamage(DamageInfo damageInfo);
    }
}
