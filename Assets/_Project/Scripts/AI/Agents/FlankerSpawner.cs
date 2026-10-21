using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Environment.Config;

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
        [SerializeField] private Vector3 spawnPosition = new Vector3(35f, 3.6f, 26f); // Level 2 Upper Corridor

        [Header("Patrol Perimeter")]
        [SerializeField] private Vector3[] patrolWaypoints = new Vector3[]
        {
            new Vector3(30f, 3.6f, 22f),
            new Vector3(40f, 3.6f, 22f),
            new Vector3(40f, 3.6f, 32f),
            new Vector3(30f, 3.6f, 32f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            if (SceneManager.GetActiveScene().name != "Main") return;
            EnsureAllFlankersInMainScene();
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "Main") return;
            if (autoSpawnOnStart)
            {
                EnsureAllFlankersInMainScene();
            }
        }

        /// <summary>
        /// Ensures all Flanker agents exist in the scene.
        /// </summary>
        public static void EnsureAllFlankersInMainScene()
        {
            if (SceneManager.GetActiveScene().name != "Main") return;

            CampusLayoutConfig cfg = null;
#if UNITY_EDITOR
            cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<CampusLayoutConfig>("Assets/_Project/Data/CampusLayoutConfig.asset");
#endif

            if (cfg != null && cfg.coreAIDeployments != null)
            {
                foreach (var profile in cfg.coreAIDeployments)
                {
                    if (profile.role == AgentRole.Flanker)
                    {
                        if (GameObject.Find(profile.agentName) == null)
                        {
                            SpawnFlanker(profile.agentName, profile.spawnPosition, profile.patrolWaypoints);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ensures a FlankerAgent exists in the scene; creates one if absent.
        /// </summary>
        public FlankerAgent EnsureFlankerInScene()
        {
            if (SceneManager.GetActiveScene().name != "Main") return null;
            EnsureAllFlankersInMainScene();
            return FindFirstObjectByType<FlankerAgent>();
        }

        /// <summary>
        /// Spawns and initializes a configured Flanker agent by name.
        /// </summary>
        public static FlankerAgent SpawnFlanker(string agentName, Vector3 position, Vector3[] waypoints)
        {
            GameObject existingObj = GameObject.Find(agentName);
            if (existingObj != null)
            {
                return existingObj.GetComponent<FlankerAgent>();
            }

            Vector3 spawn = position;
            if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                spawn = hit.position;
            }

            GameObject flankerObj = new GameObject(agentName);
            flankerObj.transform.position = spawn;
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

            // Parent under AI_Agents if available
            GameObject aiRoot = GameObject.Find("AI_Agents");
            if (aiRoot != null)
            {
                flankerObj.transform.SetParent(aiRoot.transform, true);
            }

            // Set serialized fields via reflection
            var homeField = typeof(FlankerAgent).GetField("homeBasePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (homeField != null) homeField.SetValue(agent, spawn);

            var waypointsField = typeof(FlankerAgent).GetField("patrolWaypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (waypointsField != null) waypointsField.SetValue(agent, waypoints);

            Debug.Log($"[FlankerSpawner] Spawned {agentName} at {spawn}.");
            return agent;
        }

        /// <summary>
        /// Backward-compatible spawn overload.
        /// </summary>
        public static FlankerAgent SpawnFlanker(Vector3 position, Vector3[] waypoints)
        {
            return SpawnFlanker("FlankerAgent_Stealth", position, waypoints);
        }
    }
}
