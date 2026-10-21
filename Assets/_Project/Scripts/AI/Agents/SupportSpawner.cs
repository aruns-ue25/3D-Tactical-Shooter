using UnityEngine;
using UnityEngine.AI;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    public static class SupportSpawner
    {
        public static SupportAgent SpawnSupportAgent(string agentName, Vector3 spawnPosition, Vector3[] waypoints, float leashRadius)
        {
            GameObject existing = GameObject.Find(agentName);
            if (existing != null)
            {
                return existing.GetComponent<SupportAgent>();
            }

            Vector3 spawn = spawnPosition;
            if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                spawn = hit.position;
            }

            GameObject agentObj = new GameObject(agentName);
            agentObj.transform.position = spawn;
            agentObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // 1. CharacterController
            CharacterController cc = agentObj.AddComponent<CharacterController>();
            cc.height = 1.95f;
            cc.center = new Vector3(0f, 0.975f, 0f);
            cc.radius = 0.45f;
            cc.stepOffset = 0.4f;

            // 2. Health (Heavy Boss HP)
            Health health = agentObj.AddComponent<Health>();
            health.SetMaxHealth(750f);

            // 3. Perception
            AIPerception perception = agentObj.AddComponent<AIPerception>();

            // 4. Movement
            AIMovement movement = agentObj.AddComponent<AIMovement>();

            // 5. Support Agent Behavior
            SupportAgent support = agentObj.AddComponent<SupportAgent>();
            support.ConfigureDeployment(spawn, leashRadius, waypoints);

            // 6. Visuals
            RoyalGuardVisuals visuals = agentObj.AddComponent<RoyalGuardVisuals>();
            visuals.BuildVisualHierarchy();

            GameObject aiRoot = GameObject.Find("AI_Agents");
            if (aiRoot != null)
            {
                agentObj.transform.SetParent(aiRoot.transform, true);
            }

            Debug.Log($"[SupportSpawner] Spawned heavy Support Agent '{agentName}' at {spawn}.");
            return support;
        }
    }
}
