using System.Collections.Generic;
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
    public class FlankerAgentTests
    {
        [Test]
        public void FlankerAgent_InitializesWithFlankerRoleAndComponents()
        {
            GameObject go = new GameObject("TestFlanker");
            var cc = go.AddComponent<CharacterController>();
            var health = go.AddComponent<Health>();
            var perception = go.AddComponent<AIPerception>();
            var movement = go.AddComponent<AIMovement>();
            var flanker = go.AddComponent<FlankerAgent>();
            var awakeMethod = typeof(FlankerAgent).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awakeMethod != null) awakeMethod.Invoke(flanker, null);

            Assert.AreEqual(AgentRole.Flanker, flanker.Role);
            Assert.AreEqual("Stealth / Flanker Agent", flanker.DisplayName);
            Assert.IsNotNull(flanker.HealthComponent);
            Assert.IsNotNull(flanker.Perception);
            Assert.IsNotNull(flanker.Movement);
            Assert.IsTrue(flanker.IsAlive);
            Assert.AreEqual(FlankerAgent.FlankerPhase.Patrol, flanker.CurrentPhase);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlankPositionEvaluator_GeneratesAndScoresCandidates()
        {
            Vector3 agentPos = new Vector3(30f, 0f, 10f);
            Vector3 targetPos = new Vector3(30f, 0f, 25f); // 15m North
            Vector3 targetForward = Vector3.forward;

            List<FlankPositionEvaluator.FlankCandidateResult> candidates = FlankPositionEvaluator.EvaluateFlankCandidates(
                agentPos,
                targetPos,
                targetForward,
                minFlankDist: 8f,
                maxFlankDist: 16f,
                obstacleMask: 0,
                candidateAngleSteps: 12,
                distanceRings: 2
            );

            Assert.IsNotNull(candidates);
            Assert.Greater(candidates.Count, 0, "Evaluator should produce candidate positions.");

            // The highest scoring candidate should NOT be directly in front of the target
            FlankPositionEvaluator.FlankCandidateResult best = candidates[0];
            Vector3 toBestFromTarget = (best.Position - targetPos).normalized;
            float dotWithTargetForward = Vector3.Dot(toBestFromTarget, targetForward);

            // A frontal approach has dot ~ 1. Flank positions must favor sides (dot ~ 0) or rear (dot < 0)
            Assert.Less(dotWithTargetForward, 0.75f, "Best flank candidate should avoid frontal view of target.");
        }

        [Test]
        public void FlankPositionEvaluator_RejectsBlockedGeometry()
        {
            Vector3 agentPos = new Vector3(0f, 0f, 0f);
            Vector3 targetPos = new Vector3(0f, 0f, 10f);
            Vector3 targetForward = Vector3.forward;

            // Create a temporary obstacle right at target + right * 8m
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = targetPos + Vector3.right * 8f;
            obstacle.transform.localScale = new Vector3(2f, 2f, 2f);
            Physics.SyncTransforms();
            LayerMask mask = ~0;

            List<FlankPositionEvaluator.FlankCandidateResult> candidates = FlankPositionEvaluator.EvaluateFlankCandidates(
                agentPos,
                targetPos,
                targetForward,
                minFlankDist: 8f,
                maxFlankDist: 8f,
                obstacleMask: mask,
                candidateAngleSteps: 8,
                distanceRings: 1
            );

            // Verify that no candidate was placed directly inside the cube obstacle
            foreach (var c in candidates)
            {
                float distToObstacle = Vector3.Distance(c.Position, obstacle.transform.position);
                Assert.Greater(distToObstacle, 0.6f, "Candidate position must not be inside obstacle collider.");
            }

            Object.DestroyImmediate(obstacle);
        }

        [Test]
        public void FlankerAgent_TakesDamage_ReducesHealth_AndDefeats()
        {
            GameObject go = new GameObject("TestFlanker");
            go.AddComponent<CharacterController>();
            var health = go.AddComponent<Health>();
            go.AddComponent<AIPerception>();
            go.AddComponent<AIMovement>();
            var flanker = go.AddComponent<FlankerAgent>();
            // In EditMode, invoke Awake via reflection or public method
            var awakeMethod = typeof(FlankerAgent).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (awakeMethod != null) awakeMethod.Invoke(flanker, null);

            GameObject attacker = new GameObject("PlayerMock");
            DamageInfo shot = new DamageInfo(40f, attacker, go.transform.position, Vector3.forward);

            health.TakeDamage(shot);
            Assert.AreEqual(60f, health.CurrentHealth);
            Assert.IsTrue(flanker.IsAlive);

            DamageInfo fatalShot = new DamageInfo(70f, attacker, go.transform.position, Vector3.forward);
            health.TakeDamage(fatalShot);

            Assert.AreEqual(0f, health.CurrentHealth);
            Assert.IsFalse(flanker.IsAlive);
            Assert.AreEqual(AgentState.Defeated, flanker.CurrentState);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(attacker);
        }

        [Test]
        public void FlankerAgent_InitiateFlankSelection_SelectsAdvantageousDestination()
        {
            GameObject go = new GameObject("TestFlanker");
            go.AddComponent<CharacterController>();
            go.AddComponent<Health>();
            go.AddComponent<AIPerception>();
            var mov = go.AddComponent<AIMovement>();
            var flanker = go.AddComponent<FlankerAgent>();

            Vector3 targetPos = go.transform.position + new Vector3(0f, 0f, 15f);
            bool initiated = flanker.InitiateFlankSelection(targetPos, Vector3.forward, forceReplan: true);

            Assert.IsTrue(initiated);
            Assert.AreEqual(AgentState.Reposition, flanker.CurrentState);
            Assert.AreEqual(FlankerAgent.FlankerPhase.MovingToFlank, flanker.CurrentPhase);
            Assert.AreNotEqual(Vector3.zero, flanker.SelectedFlankDestination);

            Object.DestroyImmediate(go);
        }
    }
}
