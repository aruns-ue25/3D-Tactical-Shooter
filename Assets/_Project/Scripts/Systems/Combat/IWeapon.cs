using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Shared weapon contract.
    /// Weapons execute hit detection and damage application when asked to fire,
    /// completely decoupled from player or AI input logic.
    /// </summary>
    public interface IWeapon
    {
        /// <summary>
        /// Indicates if the weapon is ready to fire (e.g. not on cooldown).
        /// </summary>
        bool CanFire { get; }

        /// <summary>
        /// Cooldown time remaining before next shot can be taken.
        /// </summary>
        float CooldownRemaining { get; }

        /// <summary>
        /// Attempts to fire the weapon from a specific origin along a specific direction.
        /// </summary>
        /// <param name="origin">World space firing origin (e.g. camera position or muzzle).</param>
        /// <param name="direction">World space forward firing vector.</param>
        /// <param name="instigator">The entity requesting the shot (to avoid self-damage).</param>
        /// <returns>True if the shot was accepted and executed; false if rejected (e.g. on cooldown).</returns>
        bool TryFire(Vector3 origin, Vector3 direction, GameObject instigator);
    }
}
