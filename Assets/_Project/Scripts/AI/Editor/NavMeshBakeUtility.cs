using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace TacticalShooter.AI.Editor
{
    public static class NavMeshBakeUtility
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main/Main.unity";
        private const string NavMeshAssetPath = "Assets/_Project/Scenes/Main/NavMesh-Main.asset";

        [MenuItem("Tools/AI/Bake Main Scene NavMesh", false, 1)]
        public static void BakeMainSceneNavMesh()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            Debug.Log($"[NavMeshBakeUtility] Opened scene {scene.name} for NavMesh baking.");

            GameObject envObj = GameObject.Find("Environment");
            if (envObj == null)
            {
                Debug.LogError("[NavMeshBakeUtility] Environment GameObject not found in Main scene!");
                return;
            }

            // Ensure upstairs ramp collider and stairs are static
            GameObject smoothRamp = GameObject.Find("Staircase_Smooth_Ramp_Collider");
            if (smoothRamp != null)
            {
                smoothRamp.isStatic = true;
            }
            GameObject stairs = GameObject.Find("Stairs_To_Upstairs");
            if (stairs != null)
            {
                stairs.isStatic = true;
            }

            NavMeshSurface surface = envObj.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = envObj.AddComponent<NavMeshSurface>();
            }

            // Configure surface parameters matching runtime agent dimensions
            // Agent radius = 0.38m, height = 1.9m, climb = 0.4m, max slope = 45 deg
            surface.agentTypeID = 0; // Humanoid
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0; // Walkable
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.08f; // high resolution for stairs and narrow doorways
            surface.overrideTileSize = true;
            surface.tileSize = 256;

            // Remove any old data
            surface.RemoveData();

            // Build NavMesh
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                if (!AssetDatabase.Contains(surface.navMeshData))
                {
                    if (File.Exists(NavMeshAssetPath))
                    {
                        AssetDatabase.DeleteAsset(NavMeshAssetPath);
                    }
                    AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
                }
                EditorUtility.SetDirty(surface.navMeshData);
                Debug.Log($"[NavMeshBakeUtility] Successfully baked NavMesh and saved to {NavMeshAssetPath}!");
            }
            else
            {
                Debug.LogError("[NavMeshBakeUtility] BuildNavMesh produced null navMeshData!");
            }

            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateBakedNavMesh();
        }

        [MenuItem("Tools/AI/Validate Main NavMesh Reachability", false, 2)]
        public static void ValidateBakedNavMesh()
        {
            if (EditorSceneManager.GetActiveScene().path != MainScenePath)
            {
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            Debug.Log("[NavMeshBakeUtility] ========== VALIDATING MAIN NAVMESH REACHABILITY ==========");

            // 1. Guard Positions (9 Tactical Guards across all sectors)
            Vector3 guardPost = new Vector3(30f, 0.1f, 25f);
            Vector3 labGuardPost = new Vector3(45f, 0.1f, 35f);
            Vector3 upstairsApproachGuard = new Vector3(30f, 0.1f, 26.5f);
            Vector3 upstairsCommandGuard = new Vector3(30f, 3.2f, 46.5f);
            Vector3 mezzanineGuard = new Vector3(45f, 3.2f, 35f);
            Vector3 storageGuardPost = new Vector3(15f, 0.1f, 22.5f);
            Vector3 securityGuardPost = new Vector3(15f, 0.1f, 37.5f);
            Vector3 sideRoom2GuardPost = new Vector3(45f, 0.1f, 12.5f);
            Vector3 southEntranceGuardPost = new Vector3(30f, 0.1f, 14.0f);

            // 2. Hunter and Flanker Spawns (2 Hunters, 2 Flankers)
            Vector3 hunter1Spawn = new Vector3(30f, 0.1f, 8f);      // South Entrance Tracker
            Vector3 hunter2Spawn = new Vector3(46f, 0.1f, 22f);     // East Cargo Prowler
            Vector3 flanker1Spawn = new Vector3(38f, 0.1f, 32f);    // East Stealth Junction
            Vector3 flanker2Spawn = new Vector3(22f, 0.1f, 28f);    // West Sniper Overwatch

            // 3. Player & Objective Points
            Vector3 playerSpawn = new Vector3(30f, 0.1f, 2f);
            Vector3 upstairsExtraction = new Vector3(30f, 3.2f, 47.5f);
            Vector3 labTerminal = new Vector3(45f, 0.1f, 35f);

            // Sample checks
            CheckPoint("Player Spawn (South Entrance)", playerSpawn);
            CheckPoint("Hunter 1 (Tracker South Entrance)", hunter1Spawn);
            CheckPoint("Hunter 2 (East Cargo Prowler)", hunter2Spawn);
            CheckPoint("Central Corridor Guard", guardPost);
            CheckPoint("Flanker 1 (East Stealth Junction)", flanker1Spawn);
            CheckPoint("Flanker 2 (West Sniper Overwatch)", flanker2Spawn);
            CheckPoint("Laboratory Guard & Terminal", labGuardPost);
            CheckPoint("Staircase Approach Guard", upstairsApproachGuard);
            CheckPoint("2nd Floor Command Guard", upstairsCommandGuard);
            CheckPoint("2nd Floor Mezzanine Guard", mezzanineGuard);
            CheckPoint("Mid-West Storage Guard", storageGuardPost);
            CheckPoint("North-West Security Guard", securityGuardPost);
            CheckPoint("South-East Tech Office Guard", sideRoom2GuardPost);
            CheckPoint("South Main Entrance Checkpoint Guard", southEntranceGuardPost);
            CheckPoint("Upstairs Extraction Zone", upstairsExtraction);

            // Critical route connectivity checks
            Debug.Log("[NavMeshBakeUtility] ---------- CHECKING CRITICAL PATH CONNECTIVITY ----------");
            CheckRoute("Hunter 1 -> Central Corridor", hunter1Spawn, guardPost);
            CheckRoute("Hunter 1 -> Hunter 2 (Cargo)", hunter1Spawn, hunter2Spawn);
            CheckRoute("Hunter 2 -> Flanker 1 (East)", hunter2Spawn, flanker1Spawn);
            CheckRoute("Flanker 1 -> Lab Guard / Terminal", flanker1Spawn, labTerminal);
            CheckRoute("Flanker 2 (West Sniper) -> Central Corridor", flanker2Spawn, guardPost);
            CheckRoute("Flanker 2 -> Storage Room Guard", flanker2Spawn, storageGuardPost);
            CheckRoute("Storage Room Guard -> Security Room Guard", storageGuardPost, securityGuardPost);
            CheckRoute("Security Room Guard -> Staircase Base", securityGuardPost, new Vector3(30f, 0.1f, 28f));
            CheckRoute("South Entrance Checkpoint -> Tech Office Guard", southEntranceGuardPost, sideRoom2GuardPost);
            CheckRoute("Tech Office Guard -> East Wing Cargo", sideRoom2GuardPost, hunter2Spawn);
            CheckRoute("Central Corridor -> Staircase Base (z=29 -> z=37)", guardPost, new Vector3(30f, 0.1f, 37f));
            CheckRoute("Staircase Base -> Upstairs Platform (Stairs Test)", new Vector3(30f, 0.1f, 28f), upstairsExtraction);
            CheckRoute("Ground Central Corridor -> Upstairs Extraction", guardPost, upstairsExtraction);
            CheckRoute("Hunter 1 Spawn -> Upstairs Extraction (Full Facility Traversal)", hunter1Spawn, upstairsExtraction);
            CheckRoute("Hunter 2 Spawn -> Upstairs Extraction (East to 2nd Floor)", hunter2Spawn, upstairsExtraction);
            CheckRoute("Flanker 2 Spawn -> Upstairs Extraction (West to 2nd Floor)", flanker2Spawn, upstairsExtraction);
            CheckRoute("Lab Terminal -> Upstairs Command Deck", labTerminal, upstairsCommandGuard);

            Debug.Log("[NavMeshBakeUtility] ========================================================");
        }

        private static bool CheckPoint(string name, Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(point, hit.position);
                Debug.Log($"[NavMeshBakeUtility] ✓ {name} mapped to NavMesh at ({hit.position.x:F2}, {hit.position.y:F2}, {hit.position.z:F2}) [offset {dist:F2}m]");
                return true;
            }
            Debug.LogWarning($"[NavMeshBakeUtility] ✗ {name} at {point} FAILED to sample on NavMesh!");
            return false;
        }

        private static bool CheckRoute(string routeName, Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit startHit, 3.0f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[NavMeshBakeUtility] ✗ Route [{routeName}] FAILED: start point {from} not on NavMesh!");
                return false;
            }

            if (!NavMesh.SamplePosition(to, out NavMeshHit destHit, 3.0f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"[NavMeshBakeUtility] ✗ Route [{routeName}] FAILED: end point {to} not on NavMesh!");
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(startHit.position, destHit.position, NavMesh.AllAreas, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    float length = 0f;
                    for (int i = 0; i < path.corners.Length - 1; i++)
                    {
                        length += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                    }
                    Debug.Log($"[NavMeshBakeUtility] ✓ Route [{routeName}] COMPLETE! Distance: {length:F2}m, Corners: {path.corners.Length}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[NavMeshBakeUtility] ⚠ Route [{routeName}] PARTIAL / INCOMPLETE (status: {path.status})");
                    return false;
                }
            }

            Debug.LogWarning($"[NavMeshBakeUtility] ✗ Route [{routeName}] FAILED: CalculatePath returned false");
            return false;
        }
    }
}
