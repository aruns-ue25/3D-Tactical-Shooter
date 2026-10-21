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
    /// Runtime spawner and manager for Tactical Soldier (Guard) AI agents.
    /// Ensures tactical soldiers are spawned and assigned to strategic guard sectors according to CampusLayoutConfig.
    /// </summary>
    public class TacticalSoldierSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private bool autoSpawnOnStart = true;
        [SerializeField] private Vector3 guardPostPosition = new Vector3(38f, 0.1f, 30f);
        [SerializeField] private float guardLeashRadius = 20f;

        [Header("Patrol Route")]
        [SerializeField] private Vector3[] patrolWaypoints = new Vector3[]
        {
            new Vector3(36f, 0.1f, 26f),
            new Vector3(40f, 0.1f, 26f),
            new Vector3(40f, 0.1f, 36f),
            new Vector3(36f, 0.1f, 36f)
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapInScene()
        {
            if (SceneManager.GetActiveScene().name != "Main") return;
            EnsureAllSectorGuards();
        }

        private void Start()
        {
            if (SceneManager.GetActiveScene().name != "Main") return;
            if (autoSpawnOnStart)
            {
                EnsureAllSectorGuards();
            }
        }

        /// <summary>
        /// Ensures all tactical enemy guards exist across their designated sectors.
        /// Reads directly from CampusLayoutConfig when available.
        /// </summary>
        public static void EnsureAllSectorGuards()
        {
            GameObject aiRoot = GameObject.Find("AI_Agents");

            CampusLayoutConfig cfg = null;
#if UNITY_EDITOR
            cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<CampusLayoutConfig>("Assets/_Project/Data/CampusLayoutConfig.asset");
#endif

            if (cfg != null && cfg.coreAIDeployments != null && cfg.coreAIDeployments.Count > 0)
            {
                foreach (var profile in cfg.coreAIDeployments)
                {
                    if (profile.role == AgentRole.Guard)
                    {
                        if (GameObject.Find(profile.agentName) == null)
                        {
                            var guard = SpawnTacticalSoldier(profile.agentName, profile.spawnPosition, profile.patrolWaypoints, profile.leashRadius);
                            if (aiRoot != null && guard != null) guard.transform.SetParent(aiRoot.transform, true);
                        }
                    }
                }
            }
        }

        public TacticalSoldierAgent EnsureSoldierInScene()
        {
            EnsureAllSectorGuards();
            return FindFirstObjectByType<TacticalSoldierAgent>();
        }

        public static TacticalSoldierAgent SpawnTacticalSoldier(Vector3 spawnPos, Vector3[] waypoints, float leashRadius)
        {
            return SpawnTacticalSoldier("TacticalSoldier_Guard", spawnPos, waypoints, leashRadius);
        }

        /// <summary>
        /// Procedurally instantiates a fully functional Tactical Soldier agent in the scene.
        /// </summary>
        public static TacticalSoldierAgent SpawnTacticalSoldier(string soldierName, Vector3 spawnPos, Vector3[] waypoints, float leashRadius)
        {
            GameObject existing = GameObject.Find(soldierName);
            if (existing != null)
            {
                return existing.GetComponent<TacticalSoldierAgent>();
            }

            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                spawnPos = hit.position;
            }

            GameObject soldierObj = new GameObject(soldierName);
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

            // 6. 3D Character Visuals
            RoyalGuardVisuals visuals = soldierObj.AddComponent<RoyalGuardVisuals>();
            visuals.BuildVisualHierarchy();

            // Configure via reflection / serialized fields or helper init
            var postField = typeof(TacticalSoldierAgent).GetField("guardPostPosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (postField != null) postField.SetValue(agent, spawnPos);

            var leashField = typeof(TacticalSoldierAgent).GetField("guardLeashRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (leashField != null) leashField.SetValue(agent, leashRadius);

            var waypointsField = typeof(TacticalSoldierAgent).GetField("patrolWaypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (waypointsField != null) waypointsField.SetValue(agent, waypoints);

            var attackRangeField = typeof(TacticalSoldierAgent).GetField("attackRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (attackRangeField != null) attackRangeField.SetValue(agent, 13.0f);

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
