using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
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
        /// Ensures all tactical enemy guards exist across their designated sectors:
        /// 1. Central Corridor Guard
        /// 2. Laboratory Data Terminal Guard (circling the data collecting point)
        /// 3. Staircase / Upstairs Approach Guard
        /// </summary>
        public static void EnsureAllSectorGuards()
        {
            GameObject aiRoot = GameObject.Find("AI_Agents");

            // 1. Central Corridor Guard
            if (GameObject.Find("TacticalSoldier_Guard") == null)
            {
                var guard = SpawnTacticalSoldier("TacticalSoldier_Guard", new Vector3(30f, 0.1f, 25f), new Vector3[]
                {
                    new Vector3(30f, 0.1f, 27.5f),
                    new Vector3(34.5f, 0.1f, 27.5f),
                    new Vector3(34.5f, 0.1f, 22.5f),
                    new Vector3(30f, 0.1f, 22.5f)
                }, 28f);
                if (aiRoot != null && guard != null) guard.transform.SetParent(aiRoot.transform, true);
            }

            // 2. Laboratory Data Terminal Guard (circling the data collecting terminal)
            if (GameObject.Find("TacticalSoldier_LabGuard") == null)
            {
                var labGuard = SpawnTacticalSoldier("TacticalSoldier_LabGuard", new Vector3(45f, 0.1f, 35f), new Vector3[]
                {
                    new Vector3(43f, 0.1f, 35f),
                    new Vector3(47f, 0.1f, 35f),
                    new Vector3(47f, 0.1f, 39.5f),
                    new Vector3(43f, 0.1f, 39.5f)
                }, 16f);
                if (aiRoot != null && labGuard != null) labGuard.transform.SetParent(aiRoot.transform, true);
            }

            // 3. Staircase / Ground Approach Guard
            if (GameObject.Find("TacticalSoldier_UpstairsGuard") == null)
            {
                var stairGuard = SpawnTacticalSoldier("TacticalSoldier_UpstairsGuard", new Vector3(30f, 0.1f, 26.5f), new Vector3[]
                {
                    new Vector3(28f, 0.1f, 26.5f),
                    new Vector3(32f, 0.1f, 26.5f),
                    new Vector3(30f, 0.1f, 28.5f)
                }, 14f);
                if (aiRoot != null && stairGuard != null) stairGuard.transform.SetParent(aiRoot.transform, true);
            }

            // 4. 2nd Floor Command Deck Guard (Guarding Upstairs Extraction Zone)
            if (GameObject.Find("TacticalSoldier_2ndFloor_CommandGuard") == null)
            {
                var cmdGuard = SpawnTacticalSoldier("TacticalSoldier_2ndFloor_CommandGuard", new Vector3(30f, 3.2f, 46.5f), new Vector3[]
                {
                    new Vector3(26f, 3.2f, 46.5f),
                    new Vector3(34f, 3.2f, 46.5f),
                    new Vector3(34f, 3.2f, 48.5f),
                    new Vector3(26f, 3.2f, 48.5f)
                }, 18f);
                if (aiRoot != null && cmdGuard != null) cmdGuard.transform.SetParent(aiRoot.transform, true);
            }

            // 5. 2nd Floor Mezzanine Guard (High Ground Overlooking Laboratory from 2nd Floor)
            if (GameObject.Find("TacticalSoldier_2ndFloor_MezzanineGuard") == null)
            {
                var mezGuard = SpawnTacticalSoldier("TacticalSoldier_2ndFloor_MezzanineGuard", new Vector3(45f, 3.2f, 35f), new Vector3[]
                {
                    new Vector3(41f, 3.2f, 33f),
                    new Vector3(48f, 3.2f, 33f),
                    new Vector3(48f, 3.2f, 37f),
                    new Vector3(41f, 3.2f, 37f)
                }, 16f);
                if (aiRoot != null && mezGuard != null) mezGuard.transform.SetParent(aiRoot.transform, true);
            }

            // 6. Mid-West Storage Room Guard (Defending Storage Room Choke Points & Crates)
            if (GameObject.Find("TacticalSoldier_StorageGuard") == null)
            {
                var storageGuard = SpawnTacticalSoldier("TacticalSoldier_StorageGuard", new Vector3(15f, 0.1f, 22.5f), new Vector3[]
                {
                    new Vector3(12f, 0.1f, 20f),
                    new Vector3(18f, 0.1f, 20f),
                    new Vector3(18f, 0.1f, 25f),
                    new Vector3(12f, 0.1f, 25f)
                }, 15f);
                if (aiRoot != null && storageGuard != null) storageGuard.transform.SetParent(aiRoot.transform, true);
            }

            // 7. North-West Security & Control Room Guard (Defending Security Terminal & West Passage)
            if (GameObject.Find("TacticalSoldier_SecurityGuard") == null)
            {
                var secGuard = SpawnTacticalSoldier("TacticalSoldier_SecurityGuard", new Vector3(15f, 0.1f, 37.5f), new Vector3[]
                {
                    new Vector3(11f, 0.1f, 35f),
                    new Vector3(19f, 0.1f, 35f),
                    new Vector3(19f, 0.1f, 40f),
                    new Vector3(11f, 0.1f, 40f)
                }, 16f);
                if (aiRoot != null && secGuard != null) secGuard.transform.SetParent(aiRoot.transform, true);
            }

            // 8. South-East Tech Office Guard (Defending East Wing Access Doorway)
            if (GameObject.Find("TacticalSoldier_SideRoom2Guard") == null)
            {
                var side2Guard = SpawnTacticalSoldier("TacticalSoldier_SideRoom2Guard", new Vector3(45f, 0.1f, 12.5f), new Vector3[]
                {
                    new Vector3(42f, 0.1f, 10f),
                    new Vector3(48f, 0.1f, 10f),
                    new Vector3(48f, 0.1f, 15f),
                    new Vector3(42f, 0.1f, 15f)
                }, 14f);
                if (aiRoot != null && side2Guard != null) side2Guard.transform.SetParent(aiRoot.transform, true);
            }

            // 9. South Main Entrance Checkpoint Guard (Outer Perimeter Foyer Defense)
            if (GameObject.Find("TacticalSoldier_SouthEntranceGuard") == null)
            {
                var southGuard = SpawnTacticalSoldier("TacticalSoldier_SouthEntranceGuard", new Vector3(30f, 0.1f, 14.0f), new Vector3[]
                {
                    new Vector3(27f, 0.1f, 12f),
                    new Vector3(33f, 0.1f, 12f),
                    new Vector3(33f, 0.1f, 16f),
                    new Vector3(27f, 0.1f, 16f)
                }, 16f);
                if (aiRoot != null && southGuard != null) southGuard.transform.SetParent(aiRoot.transform, true);
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
            if (attackRangeField != null) attackRangeField.SetValue(agent, 16.5f);

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
