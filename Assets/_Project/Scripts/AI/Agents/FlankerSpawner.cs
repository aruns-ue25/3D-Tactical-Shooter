using UnityEngine;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    /// <summary>
    /// Spawner and manager for the Stealth / Flanker Agent.
    /// Configures spawn position, patrol perimeter, flank distances, and safe lifecycle initialization.
    /// </summary>
    public class FlankerSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private bool autoSpawnOnStart = true;
        [SerializeField] private Vector3 spawnPosition = new Vector3(38f, 0.1f, 32f); // Tactical junction near East Wing

        [Header("Patrol Perimeter")]
        [SerializeField] private Vector3[] patrolWaypoints = new Vector3[]
        {
            new Vector3(38f, 0.1f, 32f),
            new Vector3(44f, 0.1f, 32f),
            new Vector3(44f, 0.1f, 26f),
            new Vector3(38f, 0.1f, 26f)
        };

        private void Start()
        {
            if (autoSpawnOnStart)
            {
                EnsureFlankerInScene();
            }
        }

        /// <summary>
        /// Ensures a FlankerAgent exists in the scene; creates one if absent.
        /// </summary>
        public FlankerAgent EnsureFlankerInScene()
        {
            FlankerAgent existing = FindFirstObjectByType<FlankerAgent>();
            if (existing != null)
            {
                return existing;
            }

            return SpawnFlanker(spawnPosition, patrolWaypoints);
        }

        /// <summary>
        /// Spawns and initializes a configured Flanker agent.
        /// </summary>
        public static FlankerAgent SpawnFlanker(Vector3 position, Vector3[] waypoints)
        {
            GameObject flankerObj = new GameObject("FlankerAgent_Stealth");
            flankerObj.transform.position = position;
            flankerObj.transform.rotation = Quaternion.Euler(0f, -90f, 0f);

            // 1. CharacterController
            CharacterController cc = flankerObj.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.radius = 0.38f;
            cc.stepOffset = 0.4f;

            // 2. Health
            flankerObj.AddComponent<Health>();

            // 3. Perception
            flankerObj.AddComponent<AIPerception>();

            // 4. Movement
            flankerObj.AddComponent<AIMovement>();

            // 5. FlankerAgent
            FlankerAgent agent = flankerObj.AddComponent<FlankerAgent>();

            // 6. 3D Character Visuals
            RoyalGuardVisuals visuals = flankerObj.AddComponent<RoyalGuardVisuals>();
            visuals.BuildVisualHierarchy();

            // Set serialized fields via reflection
            var homeField = typeof(FlankerAgent).GetField("homeBasePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (homeField != null) homeField.SetValue(agent, position);

            var waypointsField = typeof(FlankerAgent).GetField("patrolWaypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (waypointsField != null) waypointsField.SetValue(agent, waypoints);

            return agent;
        }
    }
}
