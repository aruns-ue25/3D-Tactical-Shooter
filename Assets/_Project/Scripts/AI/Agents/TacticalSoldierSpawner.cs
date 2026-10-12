using UnityEngine;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    /// <summary>
    /// Runtime spawner and manager for Tactical Soldier (Guard) AI agents.
    /// Ensures tactical soldiers are spawned and assigned to strategic guard sectors (e.g. Corridor Intersection, Laboratory).
    /// </summary>
    public class TacticalSoldierSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private bool autoSpawnOnStart = true;
        [SerializeField] private Vector3 guardPostPosition = new Vector3(30f, 0.1f, 25f); // Central Corridor Intersection
        [SerializeField] private float guardLeashRadius = 28f;

        [Header("Patrol Route")]
        [SerializeField] private Vector3[] patrolWaypoints = new Vector3[]
        {
            new Vector3(30f, 0.1f, 27.5f),  // North central corridor
            new Vector3(34.5f, 0.1f, 27.5f),// East corridor entry (north of cover)
            new Vector3(34.5f, 0.1f, 22.5f),// East corridor entry (south of cover)
            new Vector3(30f, 0.1f, 22.5f)   // South central corridor (north of crates)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            var existingGuard = FindFirstObjectByType<TacticalSoldierAgent>();
            if (existingGuard == null)
            {
                var existingSpawner = FindFirstObjectByType<TacticalSoldierSpawner>();
                if (existingSpawner == null)
                {
                    GameObject spawnerObj = new GameObject("TacticalSoldierSpawner_Auto");
                    existingSpawner = spawnerObj.AddComponent<TacticalSoldierSpawner>();
                }
                existingSpawner.EnsureSoldierInScene();
            }
        }

        private void Start()
        {
            if (autoSpawnOnStart)
            {
                EnsureSoldierInScene();
            }
        }

        /// <summary>
        /// Checks if a Tactical Soldier exists in the scene; if not, instantiates and configures one.
        /// </summary>
        public TacticalSoldierAgent EnsureSoldierInScene()
        {
            TacticalSoldierAgent existingSoldier = FindFirstObjectByType<TacticalSoldierAgent>();
            if (existingSoldier != null)
            {
                return existingSoldier;
            }

            return SpawnTacticalSoldier(guardPostPosition, patrolWaypoints, guardLeashRadius);
        }

        /// <summary>
        /// Procedurally instantiates a fully functional Tactical Soldier agent in the scene.
        /// </summary>
        public static TacticalSoldierAgent SpawnTacticalSoldier(Vector3 spawnPos, Vector3[] waypoints, float leashRadius)
        {
            GameObject soldierObj = new GameObject("TacticalSoldier_Guard");
            soldierObj.transform.position = spawnPos;
            soldierObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Face towards South Entrance

            // 1. CharacterController
            CharacterController cc = soldierObj.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.radius = 0.38f;
            cc.stepOffset = 0.4f;

            // 2. Health Component
            Health health = soldierObj.AddComponent<Health>();

            // 3. Perception System
            AIPerception perception = soldierObj.AddComponent<AIPerception>();

            // 4. Movement System
            AIMovement movement = soldierObj.AddComponent<AIMovement>();

            // 5. Tactical Soldier Agent (Guard)
            TacticalSoldierAgent agent = soldierObj.AddComponent<TacticalSoldierAgent>();

            // Configure via reflection / serialized fields or helper init
            var postField = typeof(TacticalSoldierAgent).GetField("guardPostPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (postField != null) postField.SetValue(agent, spawnPos);

            var leashField = typeof(TacticalSoldierAgent).GetField("guardLeashRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (leashField != null) leashField.SetValue(agent, leashRadius);

            var waypointsField = typeof(TacticalSoldierAgent).GetField("patrolWaypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (waypointsField != null) waypointsField.SetValue(agent, waypoints);

            var attackRangeField = typeof(TacticalSoldierAgent).GetField("attackRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (attackRangeField != null) attackRangeField.SetValue(agent, 22f);

            var viewDistField = typeof(AIPerception).GetField("viewDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (viewDistField != null) viewDistField.SetValue(perception, 35f);

            var fovField = typeof(AIPerception).GetField("fieldOfViewAngle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fovField != null) fovField.SetValue(perception, 125f);

            var hearingField = typeof(AIPerception).GetField("hearingRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (hearingField != null) hearingField.SetValue(perception, 35f);

            var proxField = typeof(AIPerception).GetField("proximityDetectionRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (proxField != null) proxField.SetValue(perception, 5.0f);

            Debug.Log($"[TacticalSoldierSpawner] Successfully spawned Tactical Soldier (Guard) at {spawnPos} defending {leashRadius}m perimeter.");
            return agent;
        }
    }
}
