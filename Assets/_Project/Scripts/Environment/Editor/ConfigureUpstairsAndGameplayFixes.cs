using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Systems.Mission;

namespace TacticalShooter.EditorTools
{
    public static class ConfigureUpstairsAndGameplayFixes
    {
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";
        private const string CRATES_SCREENSHOT = "Assets/_Project/Art/SourceArt/Fixed_Crates_Grounded.png";
        private const string UPSTAIRS_SCREENSHOT = "Assets/_Project/Art/SourceArt/Fixed_Upstairs_Destination.png";
        private const string LAB_GUARD_SCREENSHOT = "Assets/_Project/Art/SourceArt/Fixed_Lab_Guardian_Enemy.png";

        [MenuItem("Tactical Shooter/Apply Upstairs & Gameplay Fixes")]
        public static void ExecuteAllFixes()
        {
            Debug.Log("[ConfigureUpstairsAndGameplayFixes] Starting comprehensive game fixes...");
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            // 1. Remove Dummy Target
            RemoveDummyTarget();

            // 2. Fix Floating Crates (ground them to floor level)
            FixFloatingCrates();

            // 3. Build Upstairs Platform & Staircase, and Move Destination Point Upstairs
            BuildUpstairsAndMoveExtraction();

            // 4. Ensure Sector Enemies (Lab Terminal Guardian, Staircase Guard, Central Guard, Flanker)
            SetupSectorEnemies();

            // 5. Update Player Max Health to 250
            SetupPlayerHealth();

            // 6. Capture Verification Screenshots
            CaptureVerificationScreenshots();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[ConfigureUpstairsAndGameplayFixes] ✅ All fixes applied and scene saved successfully!");
        }

        private static void RemoveDummyTarget()
        {
            GameObject dummy = GameObject.Find("TargetDummy_CombatTest");
            if (dummy != null)
            {
                Object.DestroyImmediate(dummy);
                Debug.Log("[ConfigureUpstairsAndGameplayFixes] Removed TargetDummy_CombatTest from scene.");
            }

            foreach (var d in Object.FindObjectsByType<TargetDummy>(FindObjectsSortMode.None))
            {
                if (d != null) Object.DestroyImmediate(d.gameObject);
            }
        }

        private static void FixFloatingCrates()
        {
            int groundedCount = 0;
            // Ground all crate obstacles in the scene
            var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var rend in allRenderers)
            {
                GameObject go = rend.gameObject;
                string n = go.name;
                if (n.Contains("Crate_Obstacle_1") || n.Contains("Crate_Obstacle_2"))
                {
                    Vector3 pos = go.transform.position;
                    // Lower to y = 0.5f so bottom rests at floor y = 0.0f
                    if (pos.y > 0.55f)
                    {
                        go.transform.position = new Vector3(pos.x, 0.5f, pos.z);
                        groundedCount++;
                    }
                }
                else if (n.Contains("Crate_Obstacle_Top"))
                {
                    Vector3 pos = go.transform.position;
                    // Lower to y = 1.5f so bottom rests at y = 1.0f on top of bottom crates
                    if (pos.y > 1.55f)
                    {
                        go.transform.position = new Vector3(pos.x, 1.5f, pos.z);
                        groundedCount++;
                    }
                }
            }
            Debug.Log($"[ConfigureUpstairsAndGameplayFixes] Grounded {groundedCount} floating crates to floor level.");
        }

        private static void BuildUpstairsAndMoveExtraction()
        {
            Transform envRoot = GameObject.Find("Environment")?.transform;
            if (envRoot == null)
            {
                GameObject e = new GameObject("Environment");
                envRoot = e.transform;
            }

            Transform facilityRoot = GameObject.Find("Generated_Facility")?.transform ?? envRoot;

            // Remove any previous upstairs container under Architecture, Generated_Facility, or envRoot
            Transform oldUpstairsArch = envRoot.Find("Architecture/Upstairs_Facility");
            if (oldUpstairsArch != null) Object.DestroyImmediate(oldUpstairsArch.gameObject);
            Transform oldUpstairsGen = facilityRoot.Find("Upstairs_Facility");
            if (oldUpstairsGen != null) Object.DestroyImmediate(oldUpstairsGen.gameObject);
            Transform oldUpstairsEnv = envRoot.Find("Upstairs_Facility");
            if (oldUpstairsEnv != null) Object.DestroyImmediate(oldUpstairsEnv.gameObject);

            GameObject upstairsRoot = new GameObject("Upstairs_Facility");
            upstairsRoot.transform.SetParent(facilityRoot, true);
            upstairsRoot.SetActive(true);

            Material floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/FloorMaterial.mat");
            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/WallMaterial.mat");

            const float landingY = 3.2f;
            const float landingCenterX = 30f;
            const float landingCenterZ = 47.5f;

            // 1. Elevated Catwalk / Platform Floor (8m wide x 5m deep x 0.25m thick)
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Upstairs_Platform_Floor";
            platform.transform.SetParent(upstairsRoot.transform, false);
            platform.transform.position = new Vector3(landingCenterX, landingY, landingCenterZ);
            platform.transform.localScale = new Vector3(8.5f, 0.25f, 5.0f);
            if (floorMat != null) platform.GetComponent<Renderer>().sharedMaterial = floorMat;
            platform.isStatic = true;

            // 2. Platform Support Pillars
            CreateSupportPillar(upstairsRoot.transform, new Vector3(landingCenterX - 3.8f, landingY * 0.5f, landingCenterZ - 2.2f), landingY, wallMat);
            CreateSupportPillar(upstairsRoot.transform, new Vector3(landingCenterX + 3.8f, landingY * 0.5f, landingCenterZ - 2.2f), landingY, wallMat);
            CreateSupportPillar(upstairsRoot.transform, new Vector3(landingCenterX - 3.8f, landingY * 0.5f, landingCenterZ + 2.2f), landingY, wallMat);
            CreateSupportPillar(upstairsRoot.transform, new Vector3(landingCenterX + 3.8f, landingY * 0.5f, landingCenterZ + 2.2f), landingY, wallMat);

            // 3. Platform Guardrails
            CreateRail(upstairsRoot.transform, new Vector3(landingCenterX, landingY + 0.55f, landingCenterZ + 2.45f), new Vector3(8.5f, 0.95f, 0.1f), wallMat); // Back
            CreateRail(upstairsRoot.transform, new Vector3(landingCenterX - 4.2f, landingY + 0.55f, landingCenterZ), new Vector3(0.1f, 0.95f, 4.9f), wallMat); // Left
            CreateRail(upstairsRoot.transform, new Vector3(landingCenterX + 4.2f, landingY + 0.55f, landingCenterZ), new Vector3(0.1f, 0.95f, 4.9f), wallMat); // Right
            // Front railings with opening in center (x = 28.5 to 31.5) for the stairs
            CreateRail(upstairsRoot.transform, new Vector3(landingCenterX - 2.7f, landingY + 0.55f, landingCenterZ - 2.45f), new Vector3(3.0f, 0.95f, 0.1f), wallMat); // Front Left
            CreateRail(upstairsRoot.transform, new Vector3(landingCenterX + 2.7f, landingY + 0.55f, landingCenterZ - 2.45f), new Vector3(3.0f, 0.95f, 0.1f), wallMat); // Front Right

            // 4. Industrial Military Staircase leading from z = 39.0m (ground y=0) to z = 45.0m (platform y=3.2)
            GameObject stairsContainer = new GameObject("Stairs_To_Upstairs");
            stairsContainer.transform.SetParent(upstairsRoot.transform, false);

            int stepCount = 10;
            float startZ = 39.0f;
            float endZ = 45.0f;
            float stairWidth = 2.4f;
            float stepLength = (endZ - startZ) / stepCount; // 0.60m
            float stepRise = landingY / stepCount;          // 0.32m

            for (int i = 0; i < stepCount; i++)
            {
                float stepZ = startZ + (i + 0.5f) * stepLength;
                float stepH = (i + 1) * stepRise;
                float stepCenterY = stepH * 0.5f;

                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{i + 1}";
                step.transform.SetParent(stairsContainer.transform, false);
                step.transform.position = new Vector3(landingCenterX, stepCenterY, stepZ);
                step.transform.localScale = new Vector3(stairWidth, stepH, stepLength);
                if (wallMat != null) step.GetComponent<Renderer>().sharedMaterial = wallMat;
                step.isStatic = true;
            }

            // Smooth Incline Ramp Collider (prevents player CharacterController from snagging on step risers)
            GameObject smoothRamp = new GameObject("Staircase_Smooth_Ramp_Collider");
            smoothRamp.transform.SetParent(stairsContainer.transform, false);
            float rampMidZ = (startZ + endZ) * 0.5f;
            float rampMidY = landingY * 0.5f;
            float rampLength = Mathf.Sqrt(Mathf.Pow(endZ - startZ, 2) + Mathf.Pow(landingY, 2));
            float rampAngle = Mathf.Atan2(landingY, endZ - startZ) * Mathf.Rad2Deg;

            smoothRamp.transform.position = new Vector3(landingCenterX, rampMidY + 0.08f, rampMidZ);
            smoothRamp.transform.rotation = Quaternion.Euler(-rampAngle, 0f, 0f);
            BoxCollider rampCol = smoothRamp.AddComponent<BoxCollider>();
            rampCol.size = new Vector3(stairWidth, 0.15f, rampLength);

            // Side handrails along the stairs
            CreateStairHandrail(stairsContainer.transform, landingCenterX - (stairWidth * 0.5f) - 0.05f, startZ, endZ, landingY, rampLength, rampAngle, wallMat);
            CreateStairHandrail(stairsContainer.transform, landingCenterX + (stairWidth * 0.5f) + 0.05f, startZ, endZ, landingY, rampLength, rampAngle, wallMat);

            // 5. Relocate Destination Point (ExtractionPoint & ExtractionZone) to Upstairs!
            Vector3 upstairsDestPos = new Vector3(landingCenterX, landingY + 0.15f, landingCenterZ);

            GameObject extPointObj = GameObject.Find("ExtractionPoint") ?? GameObject.Find("Extraction Point");
            if (extPointObj != null)
            {
                extPointObj.transform.position = upstairsDestPos;
                Debug.Log($"[ConfigureUpstairsAndGameplayFixes] Moved ExtractionPoint to Upstairs: {upstairsDestPos}");
            }

            GameObject extZoneObj = GameObject.Find("ExtractionZone");
            if (extZoneObj != null)
            {
                extZoneObj.transform.position = upstairsDestPos;
                ExtractionInteractable interactable = extZoneObj.GetComponent<ExtractionInteractable>();
                if (interactable != null)
                {
                    // Ensure touch radius and height allow smooth upstairs extraction
                    var radiusField = typeof(ExtractionInteractable).GetField("touchRadius", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (radiusField != null) radiusField.SetValue(interactable, 3.5f);
                }
                Debug.Log($"[ConfigureUpstairsAndGameplayFixes] Moved ExtractionZone to Upstairs: {upstairsDestPos}");
            }
        }

        private static void CreateSupportPillar(Transform parent, Vector3 pos, float height, Material mat)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Support_Pillar";
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(0.35f, height * 0.5f, 0.35f);
            if (mat != null) pillar.GetComponent<Renderer>().sharedMaterial = mat;
            pillar.isStatic = true;
        }

        private static void CreateRail(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Guard_Railing";
            rail.transform.SetParent(parent, false);
            rail.transform.position = pos;
            rail.transform.localScale = scale;
            if (mat != null) rail.GetComponent<Renderer>().sharedMaterial = mat;
            rail.isStatic = true;
        }

        private static void CreateStairHandrail(Transform parent, float x, float startZ, float endZ, float height, float length, float angle, Material mat)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Stair_Handrail";
            rail.transform.SetParent(parent, false);
            float midZ = (startZ + endZ) * 0.5f;
            float midY = (height * 0.5f) + 0.85f;
            rail.transform.position = new Vector3(x, midY, midZ);
            rail.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            rail.transform.localScale = new Vector3(0.08f, 0.08f, length);
            if (mat != null) rail.GetComponent<Renderer>().sharedMaterial = mat;
            rail.isStatic = true;
        }

        private static void SetupSectorEnemies()
        {
            GameObject aiRoot = GameObject.Find("AI_Agents");
            if (aiRoot == null) aiRoot = new GameObject("AI_Agents");

            // 1. Central Corridor Guard
            GameObject centralGuardObj = GameObject.Find("TacticalSoldier_Guard");
            if (centralGuardObj == null)
            {
                var guard = TacticalSoldierSpawner.SpawnTacticalSoldier("TacticalSoldier_Guard", new Vector3(30f, 0.1f, 25f), new Vector3[]
                {
                    new Vector3(30f, 0.1f, 27.5f),
                    new Vector3(34.5f, 0.1f, 27.5f),
                    new Vector3(34.5f, 0.1f, 22.5f),
                    new Vector3(30f, 0.1f, 22.5f)
                }, 28f);
                guard.transform.SetParent(aiRoot.transform, true);
                centralGuardObj = guard.gameObject;
            }
            EnsureEnemyVisuals(centralGuardObj);

            // 2. Laboratory Data Terminal Guard (circling the data collecting point)
            GameObject labGuardObj = GameObject.Find("TacticalSoldier_LabGuard");
            if (labGuardObj == null)
            {
                var labGuard = TacticalSoldierSpawner.SpawnTacticalSoldier("TacticalSoldier_LabGuard", new Vector3(43.5f, 0.1f, 37.0f), new Vector3[]
                {
                    new Vector3(43.5f, 0.1f, 35.5f),
                    new Vector3(46.5f, 0.1f, 35.5f),
                    new Vector3(46.5f, 0.1f, 39.5f),
                    new Vector3(43.5f, 0.1f, 39.5f)
                }, 16f);
                labGuard.transform.SetParent(aiRoot.transform, true);
                labGuardObj = labGuard.gameObject;
            }
            labGuardObj.transform.position = new Vector3(43.5f, 0.1f, 37.0f);
            labGuardObj.transform.rotation = Quaternion.Euler(0f, 105f, 0f);
            EnsureEnemyVisuals(labGuardObj);
            Debug.Log("[ConfigureUpstairsAndGameplayFixes] Stationed TacticalSoldier_LabGuard defending Data Terminal.");

            // 3. Staircase / Upstairs Guard
            GameObject stairGuardObj = GameObject.Find("TacticalSoldier_UpstairsGuard");
            if (stairGuardObj == null)
            {
                var stairGuard = TacticalSoldierSpawner.SpawnTacticalSoldier("TacticalSoldier_UpstairsGuard", new Vector3(30f, 0.1f, 38.0f), new Vector3[]
                {
                    new Vector3(28.5f, 0.1f, 38.0f),
                    new Vector3(31.5f, 0.1f, 38.0f),
                    new Vector3(30f, 0.1f, 36.5f)
                }, 14f);
                stairGuard.transform.SetParent(aiRoot.transform, true);
                stairGuardObj = stairGuard.gameObject;
            }
            stairGuardObj.transform.position = new Vector3(30f, 0.1f, 38.0f);
            stairGuardObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            EnsureEnemyVisuals(stairGuardObj);
            Debug.Log("[ConfigureUpstairsAndGameplayFixes] Stationed TacticalSoldier_UpstairsGuard at Staircase.");

            // 4. Ensure Flanker Agent is in scene
            FlankerAgent existingFlanker = Object.FindFirstObjectByType<FlankerAgent>();
            if (existingFlanker == null)
            {
                var flanker = FlankerSpawner.SpawnFlanker(new Vector3(38f, 0.1f, 32f), new Vector3[]
                {
                    new Vector3(38f, 0.1f, 32f),
                    new Vector3(44f, 0.1f, 32f),
                    new Vector3(44f, 0.1f, 26f),
                    new Vector3(38f, 0.1f, 26f)
                });
                flanker.transform.SetParent(aiRoot.transform, true);
                existingFlanker = flanker;
                Debug.Log("[ConfigureUpstairsAndGameplayFixes] Spawned FlankerAgent_Stealth.");
            }
            EnsureEnemyVisuals(existingFlanker.gameObject);
        }

        private static void EnsureEnemyVisuals(GameObject enemyObj)
        {
            if (enemyObj == null) return;
            RoyalGuardVisuals visuals = enemyObj.GetComponent<RoyalGuardVisuals>();
            if (visuals == null)
            {
                visuals = enemyObj.AddComponent<RoyalGuardVisuals>();
            }
            visuals.BuildVisualHierarchy();
        }

        private static void SetupPlayerHealth()
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Health h = player.GetComponent<Health>();
                if (h != null)
                {
                    h.SetMaxHealth(250f, true);
                    SerializedObject so = new SerializedObject(h);
                    so.FindProperty("maxHealth").floatValue = 250f;
                    so.FindProperty("currentHealth").floatValue = 250f;
                    so.ApplyModifiedProperties();
                }

                PlayerCombatController pcc = player.GetComponent<PlayerCombatController>();
                if (pcc != null)
                {
                    SerializedObject soPcc = new SerializedObject(pcc);
                    soPcc.FindProperty("startInThirdPerson").boolValue = true;
                    soPcc.ApplyModifiedProperties();
                }
            }
        }

        private static void CaptureVerificationScreenshots()
        {
            GameObject camObj = new GameObject("TempVerificationCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // Screenshot 1: Grounded Crates View (in Central Hallway z = 20)
            camObj.transform.position = new Vector3(33.5f, 1.4f, 17.5f);
            camObj.transform.LookAt(new Vector3(30.5f, 0.8f, 20f));
            CaptureCamera(cam, CRATES_SCREENSHOT, 1280, 720);

            // Screenshot 2: Upstairs & Staircase with Destination Point
            camObj.transform.position = new Vector3(30f, 1.4f, 33.5f);
            camObj.transform.LookAt(new Vector3(30f, 2.5f, 44f));
            CaptureCamera(cam, UPSTAIRS_SCREENSHOT, 1280, 720);

            // Screenshot 3: Laboratory Data Terminal with Guardian Enemy
            camObj.transform.position = new Vector3(41.2f, 1.35f, 34.0f);
            camObj.transform.LookAt(new Vector3(44.2f, 1.0f, 37.3f));
            CaptureCamera(cam, LAB_GUARD_SCREENSHOT, 1280, 720);

            Object.DestroyImmediate(camObj);
        }

        private static void CaptureCamera(Camera cam, string outputPath, int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());

            cam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            Debug.Log($"[ConfigureUpstairsAndGameplayFixes] Saved screenshot: {outputPath}");
        }
    }
}
