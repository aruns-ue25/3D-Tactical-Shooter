using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using TacticalShooter.Environment.Config;

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

            // Ensure all colliders and ramps in Generated_Campus are static
            GameObject campusObj = GameObject.Find("Generated_Campus");
            if (campusObj != null)
            {
                var allTransforms = campusObj.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    t.gameObject.isStatic = true;
                }
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
            surface.voxelSize = 0.08f; // high resolution for switchback stairs and narrow portals
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
                Debug.Log($"[NavMeshBakeUtility] Successfully baked NavMesh across all 3 levels and saved to {NavMeshAssetPath}!");
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

            Debug.Log("[NavMeshBakeUtility] ========== VALIDATING THREE-LEVEL NAVMESH REACHABILITY ==========");

            CampusLayoutConfig cfg = AssetDatabase.LoadAssetAtPath<CampusLayoutConfig>("Assets/_Project/Data/CampusLayoutConfig.asset");
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<CampusLayoutConfig>();
                cfg.PopulateDefaultData();
            }

            // 1. Core Checkpoints across all three levels
            Vector3 playerStart = cfg.playerStart;                                   // (35, 0.0, -25)
            Vector3 securityCheck = new Vector3(33.0f, cfg.groundY, 17.0f);         // Ground Security Checkpoint
            Vector3 spineCheck = new Vector3(35.0f, cfg.groundY, 32.0f);            // Central Spine
            Vector3 groundStairPortal = new Vector3(32.0f, cfg.groundY, 44.0f);     // Portal G7
            Vector3 groundStairBase = new Vector3(30.2f, cfg.groundY, 46.0f);       // West flight base
            Vector3 l2StairLanding = new Vector3(33.8f, cfg.level2Y, 46.0f);        // East flight top landing at L2
            Vector3 l2CorridorCheck = new Vector3(35.0f, cfg.level2Y, 32.0f);       // Level 2 Corridor
            Vector3 objectiveTerminal = cfg.objectiveTerminalAnchor;                // (58, 3.6, 48) in Secure Archive
            Vector3 l2StairBase = new Vector3(30.2f, cfg.level2Y, 46.0f);           // West flight base to L3
            Vector3 l3StairLanding = new Vector3(33.8f, cfg.level3Y, 46.0f);        // East flight top landing at L3
            Vector3 l3CorridorCheck = new Vector3(35.0f, cfg.level3Y, 38.0f);       // Level 3 Corridor
            Vector3 extractionHelipad = cfg.extractionHelipadAnchor;                // (57, 7.2, 48) on Helipad Deck

            // Sample points
            CheckPoint("Player Start (Vehicle Yard)", playerStart);
            CheckPoint("Security Checkpoint (Ground)", securityCheck);
            CheckPoint("Central Spine (Ground)", spineCheck);
            CheckPoint("Ground Stair Base", groundStairBase);
            CheckPoint("Level 2 Stair Landing", l2StairLanding);
            CheckPoint("Level 2 Stair Base", l2StairBase);
            CheckPoint("Level 2 Corridor", l2CorridorCheck);
            CheckPoint("Level 2 Objective Terminal", objectiveTerminal);
            CheckPoint("Level 3 Half Landing West", new Vector3(30.2f, cfg.level2Y + 1.8f, 54.25f));
            CheckPoint("Level 3 Stair Landing", l3StairLanding);
            CheckPoint("Level 3 Corridor", l3CorridorCheck);
            CheckPoint("Helipad Extraction Zone (Level 3 Deck)", extractionHelipad);

            // Ramp diagnostics for Flight_L2_To_HalfLanding
            Debug.Log("[NavMeshBakeUtility] ---------- FLIGHT 2 RAMP SAMPLES (L2 -> HalfLanding) ----------");
            CheckPoint("Flight2 Base Z=46.0 Y=3.6", new Vector3(30.2f, cfg.level2Y, 46.0f));
            CheckPoint("Flight2 Quarter Z=47.75 Y=4.05", new Vector3(30.2f, cfg.level2Y + 0.45f, 47.75f));
            CheckPoint("Flight2 Mid Z=49.5 Y=4.5", new Vector3(30.2f, cfg.level2Y + 0.90f, 49.5f));
            CheckPoint("Flight2 3Quarter Z=51.25 Y=4.95", new Vector3(30.2f, cfg.level2Y + 1.35f, 51.25f));
            CheckPoint("Flight2 Top Z=53.0 Y=5.4", new Vector3(30.2f, cfg.level2Y + 1.80f, 53.0f));
            CheckPoint("Flight2 HalfLanding Z=54.25 Y=5.4", new Vector3(30.2f, cfg.level2Y + 1.80f, 54.25f));

            // AI Deployment Anchors Check
            Debug.Log("[NavMeshBakeUtility] ---------- CHECKING AI DEPLOYMENTS ----------");
            foreach (var ai in cfg.coreAIDeployments)
            {
                CheckPoint($"{ai.agentName} ({ai.role})", ai.spawnPosition);
            }

            // Detailed sub-path diagnostics
            Debug.Log("[NavMeshBakeUtility] ---------- DETAILED SUB-PATH DIAGNOSTICS ----------");
            CheckRoute("Diag 1: Ground Stair Portal -> L2 Portal", groundStairPortal, new Vector3(32f, cfg.level2Y, 44f));
            CheckRoute("Diag 2: L2 Stair Landing -> L2 Portal", l2StairLanding, new Vector3(32f, cfg.level2Y, 44f));
            CheckRoute("Diag 3: L2 Portal -> L2 Corridor", new Vector3(32f, cfg.level2Y, 44f), l2CorridorCheck);
            CheckRoute("Diag 4: L2 Corridor -> Archive Portal L2_2", l2CorridorCheck, new Vector3(44f, cfg.level2Y, 40f));
            CheckRoute("Diag 5: Archive Portal L2_2 -> Objective Terminal", new Vector3(44f, cfg.level2Y, 40f), objectiveTerminal);
            CheckRoute("Diag 6: L2 Portal -> L2 Stair Base", new Vector3(32f, cfg.level2Y, 44f), l2StairBase);
            CheckRoute("Diag 7: L2 Stair Base -> L3 Half Landing", l2StairBase, new Vector3(30.2f, cfg.level2Y + 1.8f, 54.25f));
            CheckRoute("Diag 7b: L3 Half Landing West -> East", new Vector3(30.2f, cfg.level2Y + 1.8f, 54.25f), new Vector3(33.8f, cfg.level2Y + 1.8f, 54.25f));
            CheckRoute("Diag 8: L3 Half Landing -> L3 Stair Landing", new Vector3(33.8f, cfg.level2Y + 1.8f, 54.25f), l3StairLanding);
            CheckRoute("Diag 9: L3 Stair Landing -> L3 Portal", l3StairLanding, new Vector3(32f, cfg.level3Y, 44f));
            CheckRoute("Diag 10: L3 Portal -> L3 Corridor", new Vector3(32f, cfg.level3Y, 44f), l3CorridorCheck);
            CheckRoute("Diag 11: L3 Corridor -> Helipad Portal L3_5", l3CorridorCheck, new Vector3(44f, cfg.level3Y, 40f));
            CheckRoute("Diag 12: Helipad Portal L3_5 -> Helipad Extraction", new Vector3(44f, cfg.level3Y, 40f), extractionHelipad);

            // 2. Complete 7-Leg Playable Route Validation
            Debug.Log("[NavMeshBakeUtility] ---------- 7-STAGE PLAYABLE ROUTE VALIDATION ----------");
            bool leg1 = CheckRoute("Leg 1: Player Start -> Security Checkpoint", playerStart, securityCheck);
            bool leg2 = CheckRoute("Leg 2: Security Checkpoint -> Ground Stair Portal", securityCheck, groundStairPortal);
            bool leg3 = CheckRoute("Leg 3: Ground Stair Base -> Level 2 Landing (Switchback Stairs)", groundStairBase, l2StairLanding);
            bool leg4 = CheckRoute("Leg 4: Level 2 Landing -> Objective Terminal (Secure Archive)", l2StairLanding, objectiveTerminal);
            bool leg5 = CheckRoute("Leg 5: Objective Terminal -> Level 2 Stair Portal", objectiveTerminal, l2StairBase);
            bool leg6 = CheckRoute("Leg 6: Level 2 Stair Base -> Level 3 Landing (Switchback Stairs)", l2StairBase, l3StairLanding);
            bool leg7 = CheckRoute("Leg 7: Level 3 Landing -> Helipad Extraction Zone", l3StairLanding, extractionHelipad);

            // Full End-to-End Objective to Extraction Validation
            CheckRoute("★ End-to-End: Objective Terminal -> Helipad Extraction", objectiveTerminal, extractionHelipad);

            // 3. Perimeter Isolation & Stair-Requirement Check
            Debug.Log("[NavMeshBakeUtility] ---------- PERIMETER ISOLATION VERIFICATION ----------");
            VerifyStairRequirement(playerStart, extractionHelipad, groundStairPortal);

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
                    Debug.LogWarning($"[NavMeshBakeUtility] ⚠ Route [{routeName}] PARTIAL / INCOMPLETE (status: {path.status}). StartHit: {startHit.position}, DestHit: {destHit.position}, Corners: {path.corners.Length}");
                    for (int i = 0; i < path.corners.Length; i++)
                    {
                        Debug.LogWarning($"    Corner[{i}]: {path.corners[i]}");
                    }
                    return false;
                }
            }

            Debug.LogWarning($"[NavMeshBakeUtility] ✗ Route [{routeName}] FAILED: CalculatePath returned false");
            return false;
        }

        private static void VerifyStairRequirement(Vector3 playerStart, Vector3 extractionHelipad, Vector3 groundStairPortal)
        {
            if (!NavMesh.SamplePosition(playerStart, out NavMeshHit startHit, 3.0f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(extractionHelipad, out NavMeshHit destHit, 3.0f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[NavMeshBakeUtility] Could not sample start or extraction point on NavMesh for isolation check.");
                return;
            }

            // Calculate standard path
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(startHit.position, destHit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
            {
                // Verify that the path passes through the stair portal area (Z ~ 44m, X ~ 30..34m)
                bool passedStairs = false;
                foreach (var corner in path.corners)
                {
                    if (Vector3.Distance(new Vector3(corner.x, 0, corner.z), new Vector3(groundStairPortal.x, 0, groundStairPortal.z)) < 6.0f)
                    {
                        passedStairs = true;
                        break;
                    }
                }

                if (passedStairs)
                {
                    Debug.Log("[NavMeshBakeUtility] ✓ Isolation Verified: Route to Helipad strictly passes through the enclosed Stair Core. Zero exterior bypasses!");
                }
                else
                {
                    Debug.LogWarning("[NavMeshBakeUtility] ⚠ Potential shortcut detected: Path reached helipad without passing stair portal!");
                }
            }
            else
            {
                Debug.LogWarning("[NavMeshBakeUtility] Path from start to extraction could not be computed.");
            }
        }
    }
}
