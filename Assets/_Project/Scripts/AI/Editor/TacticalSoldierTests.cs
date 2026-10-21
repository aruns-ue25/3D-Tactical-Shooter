using NUnit.Framework;
using UnityEngine;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.AI.Decision;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Editor
{
    public class TacticalSoldierTests
    {
        [Test]
        public void TacticalSoldier_InitializesWithCorrectRoleAndComponents()
        {
            GameObject go = new GameObject("TestSoldier");
            var cc = go.AddComponent<CharacterController>();
            var health = go.AddComponent<Health>();
            var perception = go.AddComponent<AIPerception>();
            var movement = go.AddComponent<AIMovement>();
            var soldier = go.AddComponent<TacticalSoldierAgent>();

            Assert.AreEqual(AgentRole.Guard, soldier.Role);
            Assert.AreEqual("Tactical Soldier (Guard)", soldier.DisplayName);
            Assert.IsNotNull(soldier.HealthComponent);
            Assert.IsNotNull(soldier.Perception);
            Assert.IsNotNull(soldier.Movement);
            Assert.IsTrue(soldier.IsAlive);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TacticalSoldier_StateTransition_FiresEvents()
        {
            GameObject go = new GameObject("TestSoldier");
            go.AddComponent<CharacterController>();
            go.AddComponent<Health>();
            go.AddComponent<AIPerception>();
            go.AddComponent<AIMovement>();
            var soldier = go.AddComponent<TacticalSoldierAgent>();

            AgentState capturedPrev = AgentState.Idle;
            AgentState capturedNew = AgentState.Idle;
            bool eventFired = false;

            soldier.OnStateChanged += (prev, next) =>
            {
                capturedPrev = prev;
                capturedNew = next;
                eventFired = true;
            };

            soldier.ChangeState(AgentState.Engage);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(AgentState.Engage, soldier.CurrentState);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TacticalPositionEvaluator_ConstrainedByGuardLeashRadius()
        {
            Vector3 anchor = new Vector3(30f, 0f, 25f);
            float maxLeash = 15f;
            Vector3 agentPos = anchor;
            Vector3 distantTarget = new Vector3(80f, 0f, 25f); // 50m away!

            Vector3 evaluatedPos = TacticalPositionEvaluator.FindDefensivePosition(
                agentPos,
                distantTarget,
                anchor,
                maxLeash,
                10f,
                ~0
            );

            float distFromAnchor = Vector3.Distance(evaluatedPos, anchor);
            Assert.LessOrEqual(distFromAnchor, maxLeash + 0.01f, "Tactical Soldier must never break its guard anchor leash!");
        }

        [Test]
        public void TacticalSoldier_TakesDamage_ReducesHealth_AndDefeats()
        {
            GameObject go = new GameObject("TestSoldier");
            go.AddComponent<CharacterController>();
            var health = go.AddComponent<Health>();
            go.AddComponent<AIPerception>();
            go.AddComponent<AIMovement>();
            var soldier = go.AddComponent<TacticalSoldierAgent>();

            GameObject attacker = new GameObject("PlayerMock");
            DamageInfo shot = new DamageInfo(35f, attacker, go.transform.position, Vector3.forward);

            health.TakeDamage(shot);
            Assert.AreEqual(65f, health.CurrentHealth);
            Assert.IsTrue(soldier.IsAlive);

            // Lethal damage
            DamageInfo lethalShot = new DamageInfo(70f, attacker, go.transform.position, Vector3.forward);
            health.TakeDamage(lethalShot);

            Assert.AreEqual(0f, health.CurrentHealth);
            Assert.IsFalse(soldier.IsAlive);
            Assert.AreEqual(AgentState.Defeated, soldier.CurrentState);

            Object.DestroyImmediate(attacker);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void AIPerception_DetectsNoiseAndRecordsLKP()
        {
            GameObject go = new GameObject("PerceptionAgent");
            var perception = go.AddComponent<AIPerception>();

            Vector3 gunshotLocation = new Vector3(5f, 0f, 5f);
            perception.ReportNoise(gunshotLocation, 1.5f);

            Assert.IsTrue(perception.IsTargetDetected);
            Assert.AreEqual(gunshotLocation, perception.LastKnownPosition);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Player_TakesDamage_ReducesHealth()
        {
            GameObject playerGo = new GameObject("Player");
            playerGo.AddComponent<CharacterController>();
            playerGo.AddComponent<CapsuleCollider>();
            var health = playerGo.AddComponent<Health>();

            DamageInfo bulletHit = new DamageInfo(15f, null, playerGo.transform.position, Vector3.forward);
            health.TakeDamage(bulletHit);

            Assert.AreEqual(85f, health.CurrentHealth);
            Assert.IsTrue(health.IsAlive);

            Object.DestroyImmediate(playerGo);
        }

        [Test]
        public void AIPerception_ProximityDetection_DetectsCloseTargetEvenFromBehind()
        {
            GameObject agentGo = new GameObject("Agent");
            agentGo.transform.position = Vector3.zero;
            agentGo.transform.forward = Vector3.forward; // Facing north (0, 0, 1)
            var perception = agentGo.AddComponent<AIPerception>();

            // Player is 2 meters directly behind the agent (0, 0, -2)
            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0f, 0f, -2f);
            playerGo.AddComponent<CapsuleCollider>();

            perception.SetTarget(playerGo.transform);
            perception.UpdatePerception();

            Assert.IsTrue(perception.CanSeeTarget, "Proximity detection must spot player standing 2 meters directly behind the agent!");

            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(agentGo);
        }

        [Test]
        public void RoyalGuardVisuals_LeftArmRestsNaturallyAtSide_NoStomachProtrusion()
        {
            GameObject guardGo = new GameObject("TestGuard");
            var visuals = guardGo.AddComponent<RoyalGuardVisuals>();

            Transform handL = guardGo.transform.Find("Visuals/Torso/UpperArm_L/Forearm_L/Hand_L");
            Assert.IsNotNull(handL, "Hand_L must exist in visual hierarchy!");

            // Verify Hand_L is situated on the left flank/side (x < -0.15m), NOT protruding in front of stomach (z should not be > 0.15m)
            Vector3 handWorldPos = handL.position;
            Transform torso = guardGo.transform.Find("Visuals/Torso");
            Vector3 handInTorso = torso.InverseTransformPoint(handWorldPos);

            Assert.Less(handInTorso.x, -0.15f, "Left hand must rest naturally along the left flank of the soldier!");
            Assert.Less(handInTorso.z, 0.12f, "Left hand must not protrude out from the front of the stomach/waist!");

            Object.DestroyImmediate(guardGo);
        }

        [Test]
        public void TacticalSoldier_GetBestLineOfFire_ReturnsExposedPointWhenPlayerPeeks()
        {
            GameObject soldierGo = new GameObject("Soldier");
            soldierGo.transform.position = Vector3.zero;
            var cc = soldierGo.AddComponent<CharacterController>();
            soldierGo.AddComponent<Health>();
            var perception = soldierGo.AddComponent<AIPerception>();
            soldierGo.AddComponent<AIMovement>();
            var soldier = soldierGo.AddComponent<TacticalSoldierAgent>();

            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0f, 0f, 10f);
            playerGo.AddComponent<CapsuleCollider>();
            playerGo.AddComponent<Health>();

            // Setup a wall blocking only the player's center chest (x = 0, z = 5)
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_Cover";
            wall.transform.position = new Vector3(0f, 1.15f, 5f);
            wall.transform.localScale = new Vector3(0.5f, 2f, 0.2f); // blocks center x=0 from x=-0.25 to x=+0.25

            // Position player slightly to the right side (x = 0.2m)
            playerGo.transform.position = new Vector3(0.2f, 0f, 10f);

            perception.SetTarget(playerGo.transform);

            bool hasLoF = soldier.GetBestLineOfFire(out Vector3 targetPoint, out Vector3 muzzleOrigin);
            Assert.IsTrue(hasLoF, "Soldier must acquire Line of Fire on exposed part of player peeking from cover!");

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(soldierGo);
        }

        [Test]
        public void TacticalSoldier_WallBlocksBullets_PlayerBehindWallTakesZeroDamage()
        {
            GameObject soldierGo = new GameObject("Soldier");
            soldierGo.transform.position = Vector3.zero;
            soldierGo.AddComponent<CharacterController>();
            soldierGo.AddComponent<Health>();
            var perception = soldierGo.AddComponent<AIPerception>();
            soldierGo.AddComponent<AIMovement>();
            var soldier = soldierGo.AddComponent<TacticalSoldierAgent>();

            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0f, 0f, 8f);
            var playerCol = playerGo.AddComponent<CapsuleCollider>();
            playerCol.height = 1.9f;
            playerCol.radius = 0.42f;
            playerCol.center = new Vector3(0f, 0.95f, 0f);
            var playerHealth = playerGo.AddComponent<Health>();

            // Setup a solid wall completely occluding the player
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Concrete_Wall";
            wall.transform.position = new Vector3(0f, 1f, 4f);
            wall.transform.localScale = new Vector3(10f, 4f, 1f);

            perception.SetTarget(playerGo.transform);

            // 1. Line of fire check must be strictly blocked
            bool hasLoF = soldier.GetBestLineOfFire(out _, out _);
            Assert.IsFalse(hasLoF, "Soldier must have NO line of fire when a solid wall stands between them and the player!");

            // 2. Firing a shot must NOT hit the player behind the wall
            soldier.ExecuteSingleShot();
            Assert.AreEqual(100f, playerHealth.CurrentHealth, "Player behind a wall must take ZERO damage from gunfire!");

            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(soldierGo);
        }

        [Test]
        public void TacticalSoldier_ArbitraryObstacle_BlocksBulletsWithoutPenetration()
        {
            GameObject soldierGo = new GameObject("Soldier");
            soldierGo.transform.position = Vector3.zero;
            soldierGo.AddComponent<CharacterController>();
            soldierGo.AddComponent<Health>();
            var perception = soldierGo.AddComponent<AIPerception>();
            soldierGo.AddComponent<AIMovement>();
            var soldier = soldierGo.AddComponent<TacticalSoldierAgent>();

            GameObject playerGo = new GameObject("Player");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(0f, 0f, 8f);
            var playerCol = playerGo.AddComponent<CapsuleCollider>();
            playerCol.height = 1.9f;
            playerCol.radius = 0.42f;
            playerCol.center = new Vector3(0f, 0.95f, 0f);
            var playerHealth = playerGo.AddComponent<Health>();

            // An obstacle with an arbitrary non-wall name and dynamic flag
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = "SciFi_Generator_04";
            prop.transform.position = new Vector3(0f, 1f, 4f);
            prop.transform.localScale = new Vector3(8f, 3f, 1f);

            perception.SetTarget(playerGo.transform);

            // Attempting to shoot must not penetrate this obstacle
            soldier.ExecuteSingleShot();
            Assert.AreEqual(100f, playerHealth.CurrentHealth, "Bullets must not penetrate through generic non-static environment props!");

            Object.DestroyImmediate(prop);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(soldierGo);
        }
    }
}
