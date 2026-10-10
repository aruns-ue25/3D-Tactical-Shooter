using NUnit.Framework;
using UnityEngine;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Systems.Combat.Editor
{
    public class CombatTests
    {
        private class CustomMockDamageable : MonoBehaviour, IDamageable
        {
            public bool IsAlive { get; set; } = true;
            public bool CanTakeDamage => IsAlive;
            public DamageInfo LastReceivedDamage { get; private set; }
            public int DamageCallCount { get; private set; }

            public void TakeDamage(DamageInfo damageInfo)
            {
                LastReceivedDamage = damageInfo;
                DamageCallCount++;
            }
        }

        [Test]
        public void DamageInfo_InitializesCorrectly_AndClampsNegativeDamage()
        {
            GameObject instigator = new GameObject("TestInstigator");
            Vector3 hitPoint = new Vector3(1, 2, 3);
            Vector3 hitDir = Vector3.forward;

            DamageInfo damage = new DamageInfo(25f, instigator, hitPoint, hitDir);
            Assert.AreEqual(25f, damage.Amount);
            Assert.AreEqual(instigator, damage.Instigator);
            Assert.AreEqual(hitPoint, damage.HitPoint);
            Assert.AreEqual(hitDir, damage.HitDirection);

            DamageInfo negativeDamage = new DamageInfo(-10f, instigator, hitPoint, hitDir);
            Assert.AreEqual(0f, negativeDamage.Amount);

            Object.DestroyImmediate(instigator);
        }

        [Test]
        public void Health_InitializesAtMaxHealth_AndIsAlive()
        {
            GameObject go = new GameObject("HealthTarget");
            Health health = go.AddComponent<Health>();

            Assert.AreEqual(100f, health.MaxHealth);
            Assert.AreEqual(100f, health.CurrentHealth);
            Assert.IsTrue(health.IsAlive);
            Assert.IsTrue(health.CanTakeDamage);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Health_TakesDamage_ReducesHealth_AndFiresEvents()
        {
            GameObject go = new GameObject("HealthTarget");
            Health health = go.AddComponent<Health>();

            bool changedFired = false;
            bool damagedFired = false;
            float newHealth = 0f;

            health.OnHealthChanged += (curr, max) =>
            {
                changedFired = true;
                newHealth = curr;
            };

            health.OnDamaged += (info) =>
            {
                damagedFired = true;
            };

            health.TakeDamage(new DamageInfo(30f, null, Vector3.zero, Vector3.forward));

            Assert.AreEqual(70f, health.CurrentHealth);
            Assert.IsTrue(changedFired);
            Assert.IsTrue(damagedFired);
            Assert.AreEqual(70f, newHealth);
            Assert.IsTrue(health.IsAlive);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Health_ClampsToZero_AndMarksDefeated()
        {
            GameObject go = new GameObject("HealthTarget");
            Health health = go.AddComponent<Health>();

            bool defeatedFired = false;
            health.OnDefeated += (info) => defeatedFired = true;

            health.TakeDamage(new DamageInfo(150f, null, Vector3.zero, Vector3.forward));

            Assert.AreEqual(0f, health.CurrentHealth);
            Assert.IsFalse(health.IsAlive);
            Assert.IsFalse(health.CanTakeDamage);
            Assert.IsTrue(defeatedFired);

            // Verify defeated target ignores further damage
            health.TakeDamage(new DamageInfo(50f, null, Vector3.zero, Vector3.forward));
            Assert.AreEqual(0f, health.CurrentHealth);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void HitscanWeapon_AppliesDamageThroughIDamageable_WithoutConcreteHealthDependency()
        {
            // Setup target with custom IDamageable (not Health)
            GameObject target = new GameObject("CustomDamageableTarget");
            target.transform.position = new Vector3(0, 0, 10);
            SphereCollider collider = target.AddComponent<SphereCollider>();
            collider.radius = 1f;
            CustomMockDamageable mock = target.AddComponent<CustomMockDamageable>();

            // Setup weapon
            GameObject weaponGo = new GameObject("WeaponGo");
            HitscanWeapon weapon = weaponGo.AddComponent<HitscanWeapon>();

            // Fire at target
            bool accepted = weapon.TryFire(Vector3.zero, Vector3.forward, weaponGo);

            Assert.IsTrue(accepted, "Shot should be accepted");
            Assert.AreEqual(1, mock.DamageCallCount, "Custom IDamageable should receive 1 damage event");
            Assert.AreEqual(25f, mock.LastReceivedDamage.Amount);
            Assert.AreEqual(weaponGo, mock.LastReceivedDamage.Instigator);

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(weaponGo);
        }

        [Test]
        public void HitscanWeapon_RespectsFiringCooldown()
        {
            GameObject weaponGo = new GameObject("WeaponGo");
            HitscanWeapon weapon = weaponGo.AddComponent<HitscanWeapon>();

            bool firstShot = weapon.TryFire(Vector3.zero, Vector3.forward, weaponGo);
            bool secondShotImmediate = weapon.TryFire(Vector3.zero, Vector3.forward, weaponGo);

            Assert.IsTrue(firstShot, "First shot must be accepted");
            Assert.IsFalse(secondShotImmediate, "Immediate second shot must be rejected by cooldown");
            Assert.Greater(weapon.CooldownRemaining, 0f, "Cooldown remaining must be greater than 0");

            Object.DestroyImmediate(weaponGo);
        }

        [Test]
        public void HitscanWeapon_RespectsMaxRange()
        {
            // Setup target beyond max range (default maxRange is 100m, place at 150m)
            GameObject target = new GameObject("FarTarget");
            target.transform.position = new Vector3(0, 0, 150);
            SphereCollider collider = target.AddComponent<SphereCollider>();
            collider.radius = 1f;
            CustomMockDamageable mock = target.AddComponent<CustomMockDamageable>();

            // Setup weapon
            GameObject weaponGo = new GameObject("WeaponGo");
            HitscanWeapon weapon = weaponGo.AddComponent<HitscanWeapon>();

            bool shotAccepted = weapon.TryFire(Vector3.zero, Vector3.forward, weaponGo);

            Assert.IsTrue(shotAccepted);
            Assert.AreEqual(0, mock.DamageCallCount, "Target beyond max range should take no damage");

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(weaponGo);
        }

        [Test]
        public void HitscanWeapon_IgnoresInstigatorColliders()
        {
            // Setup shooter with its own collider
            GameObject shooter = new GameObject("Shooter");
            shooter.transform.position = Vector3.zero;
            SphereCollider shooterCol = shooter.AddComponent<SphereCollider>();
            shooterCol.radius = 2f;
            CustomMockDamageable shooterDamageable = shooter.AddComponent<CustomMockDamageable>();

            HitscanWeapon weapon = shooter.AddComponent<HitscanWeapon>();

            // Setup target in front of shooter
            GameObject target = new GameObject("Target");
            target.transform.position = new Vector3(0, 0, 10);
            SphereCollider targetCol = target.AddComponent<SphereCollider>();
            targetCol.radius = 1f;
            CustomMockDamageable targetDamageable = target.AddComponent<CustomMockDamageable>();

            // Shooter fires from its center
            bool shot = weapon.TryFire(shooter.transform.position, Vector3.forward, shooter);

            Assert.IsTrue(shot);
            Assert.AreEqual(0, shooterDamageable.DamageCallCount, "Instigator should NOT damage itself");
            Assert.AreEqual(1, targetDamageable.DamageCallCount, "Target in front of instigator should receive damage");

            Object.DestroyImmediate(shooter);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void HitscanWeapon_CanBeFiredByAI_WithoutPlayerInput()
        {
            GameObject aiCharacter = new GameObject("AI_Character");
            aiCharacter.transform.position = new Vector3(10, 0, 10);
            HitscanWeapon aiWeapon = aiCharacter.AddComponent<HitscanWeapon>();

            GameObject target = new GameObject("Target");
            target.transform.position = new Vector3(10, 0, 20);
            target.AddComponent<SphereCollider>();
            CustomMockDamageable targetDamageable = target.AddComponent<CustomMockDamageable>();

            // Simulated AI action
            Vector3 aiAimDirection = (target.transform.position - aiCharacter.transform.position).normalized;
            bool fired = aiWeapon.TryFire(aiCharacter.transform.position, aiAimDirection, aiCharacter);

            Assert.IsTrue(fired, "AI must be able to call TryFire directly without PlayerController or Player input");
            Assert.AreEqual(1, targetDamageable.DamageCallCount);

            Object.DestroyImmediate(aiCharacter);
            Object.DestroyImmediate(target);
        }
    }
}
