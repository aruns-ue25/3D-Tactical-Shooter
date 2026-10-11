using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Value type containing contextual data for a damage event.
    /// Kept completely independent of specific weapons, players, or AI.
    /// </summary>
    public readonly struct DamageInfo
    {
        public float Amount { get; }
        public GameObject Instigator { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitDirection { get; }
        public Vector3 Direction => HitDirection;

        public DamageInfo(float amount, GameObject instigator, Vector3 hitPoint, Vector3 hitDirection)
        {
            Amount = Mathf.Max(0f, amount);
            Instigator = instigator;
            HitPoint = hitPoint;
            HitDirection = hitDirection;
        }
    }
}
