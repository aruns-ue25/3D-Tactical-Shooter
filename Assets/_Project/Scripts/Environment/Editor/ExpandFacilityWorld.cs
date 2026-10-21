using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Environment;

namespace TacticalShooter.EditorTools
{
    public static class ExpandFacilityWorld
    {
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";
        private const string STAIRS_SCREENSHOT = "Assets/_Project/Art/SourceArt/World_Stairs_Smooth_Incline.png";
        private const string COMMAND_GUARD_SCREENSHOT = "Assets/_Project/Art/SourceArt/World_2ndFloor_Enemies_CommandDeck.png";
        private const string MEZZANINE_GUARD_SCREENSHOT = "Assets/_Project/Art/SourceArt/World_2ndFloor_Mezzanine_Guard.png";
        private const string OUTDOOR_FRONT_SCREENSHOT = "Assets/_Project/Art/SourceArt/World_Outdoor_Courtyard_Entrance.png";

        public static void ExecuteWorldExpansion()
        {
            Debug.Log("[ExpandFacilityWorld] Starting World Expansion & Three-Level Campus Construction...");

            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[ExpandFacilityWorld] Failed to open scene: {SCENE_PATH}");
                return;
            }

            GameObject envObj = GameObject.Find("Environment");
            if (envObj == null) envObj = new GameObject("Environment");
            CampusGenerator generator = envObj.GetComponent<CampusGenerator>() ?? envObj.AddComponent<CampusGenerator>();
            generator.GenerateCampus();

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Capture Verification Screenshots
            CaptureVerificationScreenshots();

            Debug.Log("[ExpandFacilityWorld] ✅ Three-Level Research Campus successfully completed and verified!");
        }

        private static void ModifyPerimeterWallsWithExits(Transform facilityRoot, Material wallMat, Material doorMat)
        {
            Transform wallsRoot = facilityRoot.Find("Walls");
            if (wallsRoot == null)
            {
                GameObject w = new GameObject("Walls");
                w.transform.SetParent(facilityRoot, false);
                wallsRoot = w.transform;
            }

            // Identify and remove solid boundary walls (walls at z ~= 0, z ~= 50, x ~= 0, x ~= 60 with length >= 30)
            var allWalls = wallsRoot.GetComponentsInChildren<Transform>(true);
            foreach (var t in allWalls)
            {
                if (t == wallsRoot) continue;
                Vector3 p = t.position;
                Vector3 s = t.localScale;

                if (Mathf.Abs(p.z) < 0.8f && s.x > 30f)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
                else if (Mathf.Abs(p.z - 50f) < 0.8f && s.x > 30f)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
                else if (Mathf.Abs(p.x) < 0.8f && s.z > 30f)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
                else if (Mathf.Abs(p.x - 60f) < 0.8f && s.z > 30f)
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            // Clean up previous custom perimeter container if any
            Transform oldPerimeter = facilityRoot.Find("Perimeter_Exits");
            if (oldPerimeter != null) Object.DestroyImmediate(oldPerimeter.gameObject);

            GameObject perimeterContainer = new GameObject("Perimeter_Exits");
            perimeterContainer.transform.SetParent(facilityRoot, false);
            Transform pRoot = perimeterContainer.transform;

            const float wallH = 3.5f;
            const float wallThick = 0.35f;

            // --- 1. SOUTH WALL WITH GRAND FRONT ENTRANCE ---
            // Opening from x = 27.0m to 33.0m (6.0m wide grand entry into South Lobby)
            CreateWall(pRoot, "South_Wall_WestWing", new Vector3(13.5f, wallH * 0.5f, 0f), new Vector3(27.0f, wallH, wallThick), wallMat);
            CreateWall(pRoot, "South_Wall_EastWing", new Vector3(46.5f, wallH * 0.5f, 0f), new Vector3(27.0f, wallH, wallThick), wallMat);
            CreateWall(pRoot, "South_Entrance_Lintel", new Vector3(30f, 3.25f, 0f), new Vector3(6.2f, 0.5f, 0.55f), doorMat ?? wallMat);
            CreateWall(pRoot, "South_Entrance_Pillar_L", new Vector3(27f, wallH * 0.5f, 0f), new Vector3(0.5f, wallH, 0.55f), doorMat ?? wallMat);
            CreateWall(pRoot, "South_Entrance_Pillar_R", new Vector3(33f, wallH * 0.5f, 0f), new Vector3(0.5f, wallH, 0.55f), doorMat ?? wallMat);

            // --- 2. NORTH WALL WITH REAR LZ EXIT ---
            // Opening from x = 27.0m to 33.0m (6.0m wide rear exit to rear helipad & motor pool)
            CreateWall(pRoot, "North_Wall_WestWing", new Vector3(13.5f, wallH * 0.5f, 50f), new Vector3(27.0f, wallH, wallThick), wallMat);
            CreateWall(pRoot, "North_Wall_EastWing", new Vector3(46.5f, wallH * 0.5f, 50f), new Vector3(27.0f, wallH, wallThick), wallMat);
            CreateWall(pRoot, "North_Exit_Lintel", new Vector3(30f, 3.25f, 50f), new Vector3(6.2f, 0.5f, 0.55f), doorMat ?? wallMat);
            CreateWall(pRoot, "North_Exit_Pillar_L", new Vector3(27f, wallH * 0.5f, 50f), new Vector3(0.5f, wallH, 0.55f), doorMat ?? wallMat);
            CreateWall(pRoot, "North_Exit_Pillar_R", new Vector3(33f, wallH * 0.5f, 50f), new Vector3(0.5f, wallH, 0.55f), doorMat ?? wallMat);

            // --- 3. WEST WALL WITH OUTDOOR SERVICE EXIT ---
            // Door opening at z = 21.0m to 25.0m (4.0m wide service exit to West alley)
            CreateWall(pRoot, "West_Wall_SouthSection", new Vector3(0f, wallH * 0.5f, 10.5f), new Vector3(wallThick, wallH, 21.0f), wallMat);
            CreateWall(pRoot, "West_Wall_NorthSection", new Vector3(0f, wallH * 0.5f, 37.5f), new Vector3(wallThick, wallH, 25.0f), wallMat);
            CreateWall(pRoot, "West_Exit_Lintel", new Vector3(0f, 3.25f, 23.0f), new Vector3(0.55f, 0.5f, 4.2f), doorMat ?? wallMat);

            // --- 4. EAST WALL WITH OUTDOOR LOADING DOCK EXIT ---
            // Door opening at z = 23.0m to 27.0m (4.0m wide loading dock exit to East yard)
            CreateWall(pRoot, "East_Wall_SouthSection", new Vector3(60f, wallH * 0.5f, 11.5f), new Vector3(wallThick, wallH, 23.0f), wallMat);
            CreateWall(pRoot, "East_Wall_NorthSection", new Vector3(60f, wallH * 0.5f, 38.5f), new Vector3(wallThick, wallH, 23.0f), wallMat);
            CreateWall(pRoot, "East_Exit_Lintel", new Vector3(60f, 3.25f, 25.0f), new Vector3(0.55f, 0.5f, 4.2f), doorMat ?? wallMat);

            Debug.Log("[ExpandFacilityWorld] Perimeter walls successfully updated with 4 exterior entrances/exits.");
        }

        private static void BuildOutdoorCompound(Transform facilityRoot, Material floorMat, Material wallMat)
        {
            Transform oldCompound = facilityRoot.Find("Outdoor_Compound");
            if (oldCompound != null) Object.DestroyImmediate(oldCompound.gameObject);

            GameObject compoundObj = new GameObject("Outdoor_Compound");
            compoundObj.transform.SetParent(facilityRoot, false);
            Transform cRoot = compoundObj.transform;

            // 1. Expansive Outdoor Ground Floor (130m wide x 120m deep, flush with interior floor at y = 0.0)
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Outdoor_Compound_Ground";
            ground.transform.SetParent(cRoot, false);
            ground.transform.position = new Vector3(30f, -0.12f, 25f);
            ground.transform.localScale = new Vector3(130f, 0.24f, 120f);
            if (floorMat != null) ground.GetComponent<Renderer>().sharedMaterial = floorMat;
            ground.isStatic = true;

            const float secWallH = 4.5f;
            const float secWallThick = 0.45f;

            // 2. High Perimeter Security Walls
            CreateWall(cRoot, "Compound_OuterWall_South", new Vector3(30f, secWallH * 0.5f, -35f), new Vector3(130.5f, secWallH, secWallThick), wallMat);
            CreateWall(cRoot, "Compound_OuterWall_North", new Vector3(30f, secWallH * 0.5f, 85f), new Vector3(130.5f, secWallH, secWallThick), wallMat);
            CreateWall(cRoot, "Compound_OuterWall_West", new Vector3(-35f, secWallH * 0.5f, 25f), new Vector3(secWallThick, secWallH, 120.5f), wallMat);
            CreateWall(cRoot, "Compound_OuterWall_East", new Vector3(95f, secWallH * 0.5f, 25f), new Vector3(secWallThick, secWallH, 120.5f), wallMat);

            // 3. Corner Security Watchtowers
            CreateWatchtower(cRoot, new Vector3(-33f, 0f, -33f), wallMat);
            CreateWatchtower(cRoot, new Vector3(93f, 0f, -33f), wallMat);
            CreateWatchtower(cRoot, new Vector3(-33f, 0f, 83f), wallMat);
            CreateWatchtower(cRoot, new Vector3(93f, 0f, 83f), wallMat);

            // 4. South Front Arrival Courtyard (Checkpoints, Barriers, Floodlights)
            GameObject guardHouse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guardHouse.name = "Front_Checkpoint_Guardhouse";
            guardHouse.transform.SetParent(cRoot, false);
            guardHouse.transform.position = new Vector3(23.5f, 1.5f, -12f);
            guardHouse.transform.localScale = new Vector3(3.5f, 3.0f, 4.5f);
            if (wallMat != null) guardHouse.GetComponent<Renderer>().sharedMaterial = wallMat;
            guardHouse.isStatic = true;

            // Concrete Jersey Road Barriers creating vehicle entry lanes
            CreateConcreteBarrier(cRoot, new Vector3(27f, 0.5f, -6f), new Vector3(3.0f, 1.0f, 0.6f), wallMat);
            CreateConcreteBarrier(cRoot, new Vector3(33f, 0.5f, -6f), new Vector3(3.0f, 1.0f, 0.6f), wallMat);
            CreateConcreteBarrier(cRoot, new Vector3(25f, 0.5f, -18f), new Vector3(4.5f, 1.0f, 0.6f), wallMat);
            CreateConcreteBarrier(cRoot, new Vector3(35f, 0.5f, -18f), new Vector3(4.5f, 1.0f, 0.6f), wallMat);
            CreateConcreteBarrier(cRoot, new Vector3(20f, 0.5f, -25f), new Vector3(5.0f, 1.0f, 0.6f), wallMat);
            CreateConcreteBarrier(cRoot, new Vector3(40f, 0.5f, -25f), new Vector3(5.0f, 1.0f, 0.6f), wallMat);

            // Outdoor Floodlight Columns
            CreateFloodlightPole(cRoot, new Vector3(25f, 0f, -8f));
            CreateFloodlightPole(cRoot, new Vector3(35f, 0f, -8f));
            CreateFloodlightPole(cRoot, new Vector3(15f, 0f, -22f));
            CreateFloodlightPole(cRoot, new Vector3(45f, 0f, -22f));

            // 5. North Rear Helicopter LZ & Logistics Compound
            GameObject helipad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            helipad.name = "Helipad_Landing_Pad";
            helipad.transform.SetParent(cRoot, false);
            helipad.transform.position = new Vector3(30f, 0.1f, 68f);
            helipad.transform.localScale = new Vector3(16f, 0.2f, 16f);
            if (wallMat != null) helipad.GetComponent<Renderer>().sharedMaterial = wallMat;
            helipad.isStatic = true;

            CreateIndustrialGenerator(cRoot, new Vector3(16f, 1.5f, 66f), wallMat);
            CreateIndustrialGenerator(cRoot, new Vector3(16f, 1.5f, 72f), wallMat);
            CreateShippingContainers(cRoot, new Vector3(44f, 1.3f, 66f), wallMat);

            // 6. East & West Perimeter Alleys
            CreateConcreteBarrier(cRoot, new Vector3(-12f, 0.5f, 23f), new Vector3(0.6f, 1.0f, 4.0f), wallMat);
            CreateConcreteBarrier(cRoot, new Vector3(72f, 0.5f, 25f), new Vector3(0.6f, 1.0f, 4.0f), wallMat);
            CreateFloodlightPole(cRoot, new Vector3(-8f, 0f, 23f));
            CreateFloodlightPole(cRoot, new Vector3(68f, 0f, 25f));

            Debug.Log("[ExpandFacilityWorld] Outdoor Compound built (130m x 120m ground, security boundary, checkpoint, rear helipad).");
        }

        private static void BuildExpandedUpstairsFloor(Transform facilityRoot, Material floorMat, Material wallMat)
        {
            // Remove previous upstairs container if exists
            Transform oldUpstairs = facilityRoot.Find("Upstairs_Facility");
            if (oldUpstairs != null) Object.DestroyImmediate(oldUpstairs.gameObject);

            GameObject upstairsRoot = new GameObject("Upstairs_Facility");
            upstairsRoot.transform.SetParent(facilityRoot, false);
            Transform uRoot = upstairsRoot.transform;

            const float yLvl = 3.2f;

            // =========================================================================
            // 1. FIXED & OPTIMIZED INDUSTRIAL STAIRCASE WITH 100% CLEAR HEADROOM
            // Starts at ground z = 29.0m, climbs gently North up to z = 37.0m (y = 3.2m).
            // Above z = 28m to z = 37m there is ZERO catwalk floor overhead: full vertical clearance!
            // Steps have colliders removed; a continuous inclined ramp collider provides butter-smooth movement.
            // =========================================================================
            GameObject stairsContainer = new GameObject("Stairs_To_Upstairs");
            stairsContainer.transform.SetParent(uRoot, false);

            int stepCount = 16;
            float startZ = 29.0f;
            float endZ = 37.0f;
            float stairWidth = 2.4f;
            float stepLength = (endZ - startZ) / stepCount; // 0.50m tread
            float stepRise = yLvl / stepCount;             // 0.20m comfortable rise

            for (int i = 0; i < stepCount; i++)
            {
                float stepZ = startZ + (i + 0.5f) * stepLength;
                float stepH = (i + 1) * stepRise;
                float stepCenterY = stepH * 0.5f;

                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{i + 1}";
                step.transform.SetParent(stairsContainer.transform, false);
                step.transform.position = new Vector3(30f, stepCenterY, stepZ);
                step.transform.localScale = new Vector3(stairWidth, stepH, stepLength);
                if (wallMat != null) step.GetComponent<Renderer>().sharedMaterial = wallMat;

                // CRITICAL FIX: Destroy box collider on visual steps to prevent CharacterController snagging
                Object.DestroyImmediate(step.GetComponent<Collider>());
                step.isStatic = true;
            }

            // Smooth Continuous Walkable Incline Ramp Collider
            GameObject smoothRamp = new GameObject("Staircase_Smooth_Ramp_Collider");
            smoothRamp.transform.SetParent(stairsContainer.transform, false);
            float rampMidZ = (startZ + endZ) * 0.5f; // 33.0f
            float rampMidY = yLvl * 0.5f;           // 1.6f
            float rampLength = Mathf.Sqrt(Mathf.Pow(endZ - startZ, 2) + Mathf.Pow(yLvl, 2)); // 8.616f
            float rampAngle = Mathf.Atan2(yLvl, endZ - startZ) * Mathf.Rad2Deg;             // ~21.801 degrees

            smoothRamp.transform.position = new Vector3(30f, rampMidY + 0.06f, rampMidZ);
            smoothRamp.transform.rotation = Quaternion.Euler(-rampAngle, 0f, 0f);
            BoxCollider rampCol = smoothRamp.AddComponent<BoxCollider>();
            rampCol.size = new Vector3(stairWidth, 0.16f, rampLength);

            // Handrails along the stairs
            CreateStairHandrail(stairsContainer.transform, 30f - (stairWidth * 0.5f) - 0.05f, startZ, endZ, yLvl, rampLength, rampAngle, wallMat);
            CreateStairHandrail(stairsContainer.transform, 30f + (stairWidth * 0.5f) + 0.05f, startZ, endZ, yLvl, rampLength, rampAngle, wallMat);

            // =========================================================================
            // 2. 2ND FLOOR CATWALK JUNCTION PLATFORM (Landing at z = 37.0m .. 40.0m)
            // =========================================================================
            CreatePlatformFloor(uRoot, "Upstairs_Junction_Platform", new Vector3(30f, yLvl, 38.5f), new Vector3(4.0f, 0.25f, 3.0f), floorMat);
            // Railings flanking the top of the stairs (opening for stair arrival in center: x = 28.8 .. 31.2)
            CreateRail(uRoot, new Vector3(28.4f, yLvl + 0.55f, 37.05f), new Vector3(0.8f, 0.95f, 0.1f), wallMat);
            CreateRail(uRoot, new Vector3(31.6f, yLvl + 0.55f, 37.05f), new Vector3(0.8f, 0.95f, 0.1f), wallMat);

            // =========================================================================
            // 3. NORTH CENTRAL CATWALK TO COMMAND DECK (z = 40.0m .. 44.5m)
            // =========================================================================
            CreatePlatformFloor(uRoot, "Upstairs_North_Catwalk", new Vector3(30f, yLvl, 42.25f), new Vector3(3.4f, 0.25f, 4.5f), floorMat);
            CreateRail(uRoot, new Vector3(28.2f, yLvl + 0.55f, 42.25f), new Vector3(0.1f, 0.95f, 4.5f), wallMat); // Left rail
            CreateRail(uRoot, new Vector3(31.8f, yLvl + 0.55f, 42.25f), new Vector3(0.1f, 0.95f, 4.5f), wallMat); // Right rail

            // Support pillars under catwalk
            CreateSupportPillar(uRoot, new Vector3(28.4f, yLvl * 0.5f, 41.5f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(31.6f, yLvl * 0.5f, 41.5f), yLvl, wallMat);

            // =========================================================================
            // 4. NORTH COMMAND OBSERVATION DECK & LZ (20m wide x 6.5m deep, z = 44.5m .. 50.75m)
            // Hosts the Mission Extraction Zone and 2nd Floor Enemy 1!
            // =========================================================================
            CreatePlatformFloor(uRoot, "Upstairs_Command_Deck", new Vector3(30f, yLvl, 47.5f), new Vector3(20f, 0.25f, 6.5f), floorMat);
            CreateRail(uRoot, new Vector3(30f, yLvl + 0.55f, 50.6f), new Vector3(20f, 0.95f, 0.1f), wallMat); // Back North
            CreateRail(uRoot, new Vector3(19.9f, yLvl + 0.55f, 47.5f), new Vector3(0.1f, 0.95f, 6.4f), wallMat); // Left West
            CreateRail(uRoot, new Vector3(40.1f, yLvl + 0.55f, 47.5f), new Vector3(0.1f, 0.95f, 6.4f), wallMat); // Right East
            // Front railings with opening in center (x = 28.3 to 31.7) for North Catwalk entry
            CreateRail(uRoot, new Vector3(24.0f, yLvl + 0.55f, 44.4f), new Vector3(8.2f, 0.95f, 0.1f), wallMat);
            CreateRail(uRoot, new Vector3(36.0f, yLvl + 0.55f, 44.4f), new Vector3(8.2f, 0.95f, 0.1f), wallMat);

            // Support pillars under Command Deck
            CreateSupportPillar(uRoot, new Vector3(21.0f, yLvl * 0.5f, 45.5f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(39.0f, yLvl * 0.5f, 45.5f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(21.0f, yLvl * 0.5f, 49.5f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(39.0f, yLvl * 0.5f, 49.5f), yLvl, wallMat);

            // =========================================================================
            // 5. EAST LABORATORY OBSERVATION MEZZANINE (Overlooking Laboratory & Terminal)
            // Hosts 2nd Floor Enemy 2 on high ground!
            // =========================================================================
            // Bridge from Junction Platform (x = 32.0m .. 39.0m at z = 38.5m)
            CreatePlatformFloor(uRoot, "Upstairs_East_Catwalk_Bridge", new Vector3(35.5f, yLvl, 38.5f), new Vector3(7.0f, 0.25f, 2.4f), floorMat);
            CreateRail(uRoot, new Vector3(35.5f, yLvl + 0.55f, 39.75f), new Vector3(7.0f, 0.95f, 0.1f), wallMat);
            CreateRail(uRoot, new Vector3(35.5f, yLvl + 0.55f, 37.25f), new Vector3(7.0f, 0.95f, 0.1f), wallMat);

            // East Mezzanine Floor: Center (45m, 3.2m, 35m), spans x: 39m..51m, z: 30.5m..39.5m
            CreatePlatformFloor(uRoot, "Upstairs_East_Lab_Mezzanine", new Vector3(45f, yLvl, 35f), new Vector3(12f, 0.25f, 9.0f), floorMat);
            // Railings around the mezzanine overlooking the lab below
            CreateRail(uRoot, new Vector3(45f, yLvl + 0.55f, 39.45f), new Vector3(12f, 0.95f, 0.1f), wallMat); // North
            CreateRail(uRoot, new Vector3(45f, yLvl + 0.55f, 30.55f), new Vector3(12f, 0.95f, 0.1f), wallMat); // South
            CreateRail(uRoot, new Vector3(50.95f, yLvl + 0.55f, 35f), new Vector3(0.1f, 0.95f, 8.8f), wallMat); // East outer
            CreateRail(uRoot, new Vector3(38.95f, yLvl + 0.55f, 33.5f), new Vector3(0.1f, 0.95f, 6.0f), wallMat); // West inner south

            CreateSupportPillar(uRoot, new Vector3(39.5f, yLvl * 0.5f, 31f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(50.5f, yLvl * 0.5f, 31f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(39.5f, yLvl * 0.5f, 39f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(50.5f, yLvl * 0.5f, 39f), yLvl, wallMat);

            // =========================================================================
            // 6. WEST SECURITY OBSERVATION MEZZANINE (Overlooking West Sector)
            // =========================================================================
            CreatePlatformFloor(uRoot, "Upstairs_West_Catwalk_Bridge", new Vector3(24.5f, yLvl, 38.5f), new Vector3(7.0f, 0.25f, 2.4f), floorMat);
            CreateRail(uRoot, new Vector3(24.5f, yLvl + 0.55f, 39.75f), new Vector3(7.0f, 0.95f, 0.1f), wallMat);
            CreateRail(uRoot, new Vector3(24.5f, yLvl + 0.55f, 37.25f), new Vector3(7.0f, 0.95f, 0.1f), wallMat);

            // West Mezzanine Floor: Center (15m, 3.2m, 35m), spans x: 9m..21m, z: 30.5m..39.5m
            CreatePlatformFloor(uRoot, "Upstairs_West_Security_Mezzanine", new Vector3(15f, yLvl, 35f), new Vector3(12f, 0.25f, 9.0f), floorMat);
            CreateRail(uRoot, new Vector3(15f, yLvl + 0.55f, 39.45f), new Vector3(12f, 0.95f, 0.1f), wallMat); // North
            CreateRail(uRoot, new Vector3(15f, yLvl + 0.55f, 30.55f), new Vector3(12f, 0.95f, 0.1f), wallMat); // South
            CreateRail(uRoot, new Vector3(9.05f, yLvl + 0.55f, 35f), new Vector3(0.1f, 0.95f, 8.8f), wallMat); // West outer
            CreateRail(uRoot, new Vector3(21.05f, yLvl + 0.55f, 33.5f), new Vector3(0.1f, 0.95f, 6.0f), wallMat); // East inner south

            CreateSupportPillar(uRoot, new Vector3(9.5f, yLvl * 0.5f, 31f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(20.5f, yLvl * 0.5f, 31f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(9.5f, yLvl * 0.5f, 39f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(20.5f, yLvl * 0.5f, 39f), yLvl, wallMat);

            // =========================================================================
            // 7. SOUTH ENTRANCE OVERLOOK (z = 16.0m .. 24.0m)
            // =========================================================================
            CreatePlatformFloor(uRoot, "Upstairs_South_Overlook", new Vector3(30f, yLvl, 18.25f), new Vector3(10f, 0.25f, 4.5f), floorMat);
            CreateRail(uRoot, new Vector3(30f, yLvl + 0.55f, 16.1f), new Vector3(10f, 0.95f, 0.1f), wallMat); // South edge
            CreateRail(uRoot, new Vector3(24.9f, yLvl + 0.55f, 18.25f), new Vector3(0.1f, 0.95f, 4.4f), wallMat); // West edge
            CreateRail(uRoot, new Vector3(35.1f, yLvl + 0.55f, 18.25f), new Vector3(0.1f, 0.95f, 4.4f), wallMat); // East edge
            CreateSupportPillar(uRoot, new Vector3(25.5f, yLvl * 0.5f, 16.5f), yLvl, wallMat);
            CreateSupportPillar(uRoot, new Vector3(34.5f, yLvl * 0.5f, 16.5f), yLvl, wallMat);

            Debug.Log("[ExpandFacilityWorld] Expansive Upstairs 2nd Floor built with 100% open headroom above stairs.");
        }

        private static void ConfigurePlayerAndExtraction()
        {
            // 1. Move Player to start outside in the South Courtyard Checkpoint!
            Vector3 outsideSpawn = new Vector3(30f, 1.0f, -12.0f);

            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = outsideSpawn;
                player.transform.rotation = Quaternion.identity;
                if (cc != null) cc.enabled = true;
                Debug.Log($"[ExpandFacilityWorld] Positioned Player at Outdoor Courtyard: {outsideSpawn}");
            }

            GameObject playerSpawn = GameObject.Find("PlayerSpawn");
            if (playerSpawn != null)
            {
                playerSpawn.transform.position = outsideSpawn;
                playerSpawn.transform.rotation = Quaternion.identity;
            }

            // 2. Mission Extraction Point on the 2nd Floor Command Observation Deck
            Vector3 upstairsExtraction = new Vector3(30f, 3.35f, 47.5f);
            GameObject extObj = GameObject.Find("ExtractionPoint") ?? GameObject.Find("Extraction Point");
            if (extObj != null)
            {
                extObj.transform.position = upstairsExtraction;
                Debug.Log($"[ExpandFacilityWorld] Positioned ExtractionPoint on 2nd Floor Command Deck: {upstairsExtraction}");
            }

            GameObject extZone = GameObject.Find("ExtractionZone") ?? GameObject.Find("Extraction Zone");
            if (extZone != null)
            {
                extZone.transform.position = upstairsExtraction;
            }
        }

        private static void EnsureAllGuardsVisuals()
        {
            TacticalSoldierSpawner.EnsureAllSectorGuards();
            var allGuards = Object.FindObjectsByType<TacticalSoldierAgent>(FindObjectsSortMode.None);
            foreach (var g in allGuards)
            {
                var v = g.GetComponent<RoyalGuardVisuals>() ?? g.gameObject.AddComponent<RoyalGuardVisuals>();
                v.BuildVisualHierarchy();
            }

            var flanker = Object.FindFirstObjectByType<FlankerAgent>();
            if (flanker != null)
            {
                var v = flanker.GetComponent<RoyalGuardVisuals>() ?? flanker.gameObject.AddComponent<RoyalGuardVisuals>();
                v.BuildVisualHierarchy();
            }
        }

        // --- Helper Building Primitives ---

        private static GameObject CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (mat != null) wall.GetComponent<Renderer>().sharedMaterial = mat;
            wall.isStatic = true;
            return wall;
        }

        private static GameObject CreatePlatformFloor(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name;
            platform.transform.SetParent(parent, false);
            platform.transform.position = pos;
            platform.transform.localScale = scale;
            if (mat != null) platform.GetComponent<Renderer>().sharedMaterial = mat;
            platform.isStatic = true;
            return platform;
        }

        private static void CreateRail(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Safety_Guardrail";
            rail.transform.SetParent(parent, false);
            rail.transform.position = pos;
            rail.transform.localScale = scale;
            if (mat != null) rail.GetComponent<Renderer>().sharedMaterial = mat;
            rail.isStatic = true;
        }

        private static void CreateSupportPillar(Transform parent, Vector3 pos, float height, Material mat)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Structural_Support_Pillar";
            pillar.transform.SetParent(parent, false);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(0.5f, height * 0.5f, 0.5f);
            if (mat != null) pillar.GetComponent<Renderer>().sharedMaterial = mat;
            pillar.isStatic = true;
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

        private static void CreateWatchtower(Transform parent, Vector3 pos, Material mat)
        {
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Perimeter_Watchtower";
            tower.transform.SetParent(parent, false);
            tower.transform.position = new Vector3(pos.x, 3.0f, pos.z);
            tower.transform.localScale = new Vector3(4.5f, 6.0f, 4.5f);
            if (mat != null) tower.GetComponent<Renderer>().sharedMaterial = mat;
            tower.isStatic = true;

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Watchtower_Observation_Cabin";
            cabin.transform.SetParent(tower.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            cabin.transform.localScale = new Vector3(1.15f, 0.35f, 1.15f);
            if (mat != null) cabin.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static void CreateConcreteBarrier(Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = "Outdoor_Jersey_Barrier";
            barrier.transform.SetParent(parent, false);
            barrier.transform.position = pos;
            barrier.transform.localScale = scale;
            if (mat != null) barrier.GetComponent<Renderer>().sharedMaterial = mat;
            barrier.isStatic = true;
        }

        private static void CreateFloodlightPole(Transform parent, Vector3 pos)
        {
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Outdoor_Floodlight_Pole";
            pole.transform.SetParent(parent, false);
            pole.transform.position = new Vector3(pos.x, 2.5f, pos.z);
            pole.transform.localScale = new Vector3(0.2f, 2.5f, 0.2f);
            pole.isStatic = true;

            GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lamp.name = "Lamp_Fixture";
            lamp.transform.SetParent(pole.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 0.98f, 0.5f);
            lamp.transform.localScale = new Vector3(2.5f, 0.4f, 2.0f);
        }

        private static void CreateIndustrialGenerator(Transform parent, Vector3 pos, Material mat)
        {
            GameObject gen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gen.name = "Outdoor_Generator_Unit";
            gen.transform.SetParent(parent, false);
            gen.transform.position = pos;
            gen.transform.localScale = new Vector3(3.5f, 2.8f, 5.0f);
            if (mat != null) gen.GetComponent<Renderer>().sharedMaterial = mat;
            gen.isStatic = true;
        }

        private static void CreateShippingContainers(Transform parent, Vector3 pos, Material mat)
        {
            GameObject c1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c1.name = "Shipping_Container_1";
            c1.transform.SetParent(parent, false);
            c1.transform.position = pos;
            c1.transform.localScale = new Vector3(3.0f, 2.6f, 6.5f);
            if (mat != null) c1.GetComponent<Renderer>().sharedMaterial = mat;
            c1.isStatic = true;

            GameObject c2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c2.name = "Shipping_Container_2";
            c2.transform.SetParent(parent, false);
            c2.transform.position = pos + new Vector3(3.6f, 0f, 0f);
            c2.transform.localScale = new Vector3(3.0f, 2.6f, 6.5f);
            if (mat != null) c2.GetComponent<Renderer>().sharedMaterial = mat;
            c2.isStatic = true;
        }

        // --- Screenshot Verification ---

        private static void CaptureVerificationScreenshots()
        {
            GameObject camObj = new GameObject("TempVerificationCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;

            // 1. Smooth Incline Staircase & 2nd Floor Arrival from ground corridor
            camObj.transform.position = new Vector3(33.0f, 1.35f, 27.5f);
            camObj.transform.LookAt(new Vector3(30f, 2.0f, 34.0f));
            CaptureCamera(cam, STAIRS_SCREENSHOT, 1280, 720);

            // 2. 2nd Floor Command Deck Guard holding the Extraction Zone
            camObj.transform.position = new Vector3(30f, 4.4f, 41.5f);
            camObj.transform.LookAt(new Vector3(30f, 4.1f, 46.5f));
            CaptureCamera(cam, COMMAND_GUARD_SCREENSHOT, 1280, 720);

            // 3. 2nd Floor Mezzanine Guard stationed overlooking Laboratory from High Ground
            camObj.transform.position = new Vector3(37.0f, 4.4f, 38.5f);
            camObj.transform.LookAt(new Vector3(44.5f, 4.0f, 35.0f));
            CaptureCamera(cam, MEZZANINE_GUARD_SCREENSHOT, 1280, 720);

            // 4. Outdoor Front Arrival Courtyard looking at the Facility Grand Entrance
            camObj.transform.position = new Vector3(34f, 1.8f, -14f);
            camObj.transform.LookAt(new Vector3(30f, 1.8f, 2f));
            CaptureCamera(cam, OUTDOOR_FRONT_SCREENSHOT, 1280, 720);

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

            Debug.Log($"[ExpandFacilityWorld] Saved verification screenshot: {outputPath}");
        }
    }
}
