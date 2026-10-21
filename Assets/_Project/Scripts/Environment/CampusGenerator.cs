using System.Collections.Generic;

using UnityEngine;

using UnityEngine.SceneManagement;

using TacticalShooter.Environment.Config;

using TacticalShooter.AI.Agents;

using TacticalShooter.AI.Core;



#if UNITY_EDITOR

using UnityEditor;

#endif



namespace TacticalShooter.Environment

{

    [ExecuteInEditMode]

    public class CampusGenerator : MonoBehaviour

    {

        public const string GENERATED_CONTAINER_NAME = "Generated_Campus";



        [Header("Configuration Asset")]

        [SerializeField] private CampusLayoutConfig config;



        [Header("Materials")]

        [SerializeField] private Material floorMaterial;

        [SerializeField] private Material wallMaterial;

        [SerializeField] private Material accentMaterial;



        [Header("Scene Object References")]

        [SerializeField] private Transform environmentRoot;

        [SerializeField] private Transform playerTransform;

        [SerializeField] private Transform playerSpawn;

        [SerializeField] private Transform objectivePoint;

        [SerializeField] private Transform extractionPoint;



        public CampusLayoutConfig Config

        {

            get

            {

                if (config == null)

                {

#if UNITY_EDITOR

                    config = AssetDatabase.LoadAssetAtPath<CampusLayoutConfig>("Assets/_Project/Data/CampusLayoutConfig.asset");

#endif

                    if (config == null)

                    {

                        config = ScriptableObject.CreateInstance<CampusLayoutConfig>();

                        config.PopulateDefaultData();

                    }

                }

                return config;

            }

            set => config = value;

        }



        public void GenerateCampus()

        {

            FindSceneReferences();

            ClearExistingGeneratedCampus();



            if (environmentRoot == null)

            {

                GameObject envObj = GameObject.Find("Environment");

                if (envObj != null) environmentRoot = envObj.transform;

                else environmentRoot = this.transform;

            }



            CampusLayoutConfig cfg = Config;



            // Master container for all generated geometry

            GameObject campusContainer = new GameObject(GENERATED_CONTAINER_NAME);

            campusContainer.transform.SetParent(environmentRoot, false);



#if UNITY_EDITOR

            Undo.RegisterCreatedObjectUndo(campusContainer, "Generate Research Facility Campus");

#endif



            Transform compoundRoot = CreateSubContainer(campusContainer.transform, "Ground_Compound");

            Transform floorsRoot = CreateSubContainer(campusContainer.transform, "Floors");

            Transform wallsRoot = CreateSubContainer(campusContainer.transform, "Walls");

            Transform portalsRoot = CreateSubContainer(campusContainer.transform, "Portals");

            Transform stairsRoot = CreateSubContainer(campusContainer.transform, "Stairwell_Core");

            Transform propsRoot = CreateSubContainer(campusContainer.transform, "Props_And_Signage");

            Transform lightingRoot = CreateSubContainer(campusContainer.transform, "Facility_Interior_Lighting");

            Transform doorsRoot = CreateSubContainer(campusContainer.transform, "Security_Doors");

            Transform suppliesRoot = CreateSubContainer(campusContainer.transform, "Supplies_And_Pickups");



            // 1. Exterior Campus Grounds, Perimeter Security Walls & Entrance Gate

            BuildExteriorCompound(compoundRoot, cfg);



            // 2. Three-Level Floor Slabs with Open Stair Shaft Cutout

            BuildThreeFloorSlabs(floorsRoot, cfg);



            // 3. Continuous Solid Walls and Open Walk-Through Portals across all 3 Levels

            BuildAllWallsAndPortals(wallsRoot, portalsRoot, cfg);



            // 4. Stacked Continuous Switchback Stair Core (Ground -> L2 -> L3)

            BuildStackedSwitchbackStairCore(stairsRoot, cfg);



            // 5. Environmental Props, Server Racks, Helipad Deck, and Signage

            BuildPropsAndSignage(propsRoot, cfg);



            // 5b. Modular Ceiling Light Panels and Soft Shadow Interior Lighting

            BuildFacilityInteriorLighting(lightingRoot, cfg);



            // 5c. Terminal Security Doors (Strictly 3 interactable doors guarding the 3 terminal rooms)

            BuildTerminalSecurityDoors(doorsRoot, cfg);



            // 5d. Supply Pickups (Ammo Crates & Trauma Medkits in empty rooms)

            BuildSuppliesAndPickups(suppliesRoot, cfg);



            // 6. Reposition Scene Anchors (Player, 3 Terminals across F1 & L2, Extraction on L3 Helipad)

            RepositionGameplayAnchors(cfg);



            // 7. Synchronize AI Deployments to Configured Waypoints

            DeployOrSyncAIAgents(cfg);



            Debug.Log("[CampusGenerator] ✅ Three-level research facility campus generated successfully!");

        }



        public void ClearExistingGeneratedCampus()

        {

            FindSceneReferences();



            // Clear Generated_Campus

            if (environmentRoot != null)

            {

                List<GameObject> toDestroy = new List<GameObject>();

                foreach (Transform child in environmentRoot)

                {

                    if (child.name == GENERATED_CONTAINER_NAME || child.name == "Generated_Facility")

                    {

                        toDestroy.Add(child.gameObject);

                    }

                }

                foreach (GameObject obj in toDestroy)

                {

#if UNITY_EDITOR

                    if (Application.isPlaying) Destroy(obj);

                    else Undo.DestroyObjectImmediate(obj);

#else

                    Destroy(obj);

#endif

                }

            }



            // Also check root scene for accidental orphan containers

            GameObject rootGen = GameObject.Find(GENERATED_CONTAINER_NAME);

            if (rootGen != null)

            {

#if UNITY_EDITOR

                if (Application.isPlaying) Destroy(rootGen);

                else Undo.DestroyObjectImmediate(rootGen);

#else

                Destroy(rootGen);

#endif

            }

        }



        private void FindSceneReferences()

        {

            if (environmentRoot == null)

            {

                GameObject envObj = GameObject.Find("Environment");

                if (envObj != null) environmentRoot = envObj.transform;

                else environmentRoot = this.transform;

            }



            if (playerSpawn == null)

            {

                GameObject spawnObj = GameObject.Find("PlayerSpawn");

                if (spawnObj != null) playerSpawn = spawnObj.transform;

            }



            if (playerTransform == null)

            {

                GameObject p = GameObject.Find("Player");

                if (p != null) playerTransform = p.transform;

            }



            if (objectivePoint == null)

            {

                GameObject objPoint = GameObject.Find("Objective Point") ?? GameObject.Find("ObjectivePoint");

                if (objPoint != null) objectivePoint = objPoint.transform;

            }



            if (extractionPoint == null)

            {

                GameObject extObj = GameObject.Find("ExtractionPoint") ?? GameObject.Find("Extraction Point");

                if (extObj != null) extractionPoint = extObj.transform;

            }



#if UNITY_EDITOR

            if (floorMaterial == null)

            {

                floorMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/FloorMaterial.mat");

            }

            if (wallMaterial == null)

            {

                wallMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/WallMaterial.mat");

            }

            if (accentMaterial == null)

            {

                accentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/DoorMaterial.mat");

            }

#endif

        }



        private Transform CreateSubContainer(Transform parent, string name)

        {

            GameObject container = new GameObject(name);

            container.transform.SetParent(parent, false);

            return container.transform;

        }



        // =========================================================================

        // 1. EXTERIOR CAMPUS COMPOUND (130m x 120m Footprint)

        // =========================================================================

        private void BuildExteriorCompound(Transform parent, CampusLayoutConfig cfg)

        {

            Rect cb = cfg.campusBounds; // X: -35..95 (130m), Z: -35..85 (120m)

            float groundY = cfg.groundY;



            // Exterior Compound Perimeter Yard Slabs (Tiled around the 70m x 60m building with ZERO interior overlap, preventing Z-fighting)

            // 1. South Arrival Yard (Z: -35..0)

            CreateFloorSlab(parent, "Campus_Outdoor_Ground_South", new Vector3(cb.center.x, groundY - 0.15f, -17.5f), new Vector3(cb.width, 0.3f, 35.0f), floorMaterial);

            // 2. North Service Yard (Z: 60..85)

            CreateFloorSlab(parent, "Campus_Outdoor_Ground_North", new Vector3(cb.center.x, groundY - 0.15f, 72.5f), new Vector3(cb.width, 0.3f, 25.0f), floorMaterial);

            // 3. West Generator Yard (X: -35..0, Z: 0..60)

            CreateFloorSlab(parent, "Campus_Outdoor_Ground_West", new Vector3(-17.5f, groundY - 0.15f, 30.0f), new Vector3(35.0f, 0.3f, 60.0f), floorMaterial);

            // 4. East Logistics Yard (X: 70..95, Z: 0..60)

            CreateFloorSlab(parent, "Campus_Outdoor_Ground_East", new Vector3(82.5f, groundY - 0.15f, 30.0f), new Vector3(25.0f, 0.3f, 60.0f), floorMaterial);



            const float secWallH = 4.5f;

            const float secWallThick = 0.45f;



            // 1. South Perimeter Security Wall with Main Vehicle Entrance Gate (Z = -35m)

            // Opening from X = 29.0m to 41.0m (12m gate)

            CreateWall(parent, "Perimeter_Wall_South_West", new Vector3((cb.xMin + 29f) * 0.5f, secWallH * 0.5f, cb.yMin), new Vector3(29f - cb.xMin, secWallH, secWallThick), wallMaterial);

            CreateWall(parent, "Perimeter_Wall_South_East", new Vector3((41f + cb.xMax) * 0.5f, secWallH * 0.5f, cb.yMin), new Vector3(cb.xMax - 41f, secWallH, secWallThick), wallMaterial);

            // Gate Pillars & Lintel

            CreateWall(parent, "Gate_Pillar_L", new Vector3(28.5f, secWallH * 0.5f, cb.yMin), new Vector3(1.0f, secWallH, 1.0f), accentMaterial ?? wallMaterial);

            CreateWall(parent, "Gate_Pillar_R", new Vector3(41.5f, secWallH * 0.5f, cb.yMin), new Vector3(1.0f, secWallH, 1.0f), accentMaterial ?? wallMaterial);

            CreateWall(parent, "Gate_Overhead_Lintel", new Vector3(35.0f, secWallH + 0.3f, cb.yMin), new Vector3(14.0f, 0.6f, 1.0f), accentMaterial ?? wallMaterial);



            // Fortified 3D Security Gate (Seals south perimeter behind player spawn, preventing falling into void)

            CreatePerimeterSecurityGate(parent, new Vector3(35.0f, 0f, cb.yMin), secWallH, wallMaterial, accentMaterial);



            // 2. North Perimeter Security Wall (Z = 85m) - Solid

            CreateWall(parent, "Perimeter_Wall_North", new Vector3(cb.center.x, secWallH * 0.5f, cb.yMax), new Vector3(cb.width + secWallThick, secWallH, secWallThick), wallMaterial);



            // 3. West Perimeter Security Wall (X = -35m) - Solid

            CreateWall(parent, "Perimeter_Wall_West", new Vector3(cb.xMin, secWallH * 0.5f, cb.center.y), new Vector3(secWallThick, secWallH, cb.height + secWallThick), wallMaterial);



            // 4. East Perimeter Security Wall (X = 95m) - Solid

            CreateWall(parent, "Perimeter_Wall_East", new Vector3(cb.xMax, secWallH * 0.5f, cb.center.y), new Vector3(secWallThick, secWallH, cb.height + secWallThick), wallMaterial);



            // 5. Four Corner Watchtowers

            CreateWatchtower(parent, new Vector3(cb.xMin + 2.5f, 0f, cb.yMin + 2.5f), wallMaterial);

            CreateWatchtower(parent, new Vector3(cb.xMax - 2.5f, 0f, cb.yMin + 2.5f), wallMaterial);

            CreateWatchtower(parent, new Vector3(cb.xMin + 2.5f, 0f, cb.yMax - 2.5f), wallMaterial);

            CreateWatchtower(parent, new Vector3(cb.xMax - 2.5f, 0f, cb.yMax - 2.5f), wallMaterial);



            // 6. Vehicle Yard Arrival Checkpoint (Guardhouse, Boom Barriers, Jersey Barriers)

            CreateGuardhouse(parent, new Vector3(25.0f, 0f, -22.0f), wallMaterial);

            CreateConcreteBarrier(parent, new Vector3(33.0f, 0.5f, -22.0f), new Vector3(3.5f, 1.0f, 0.6f), wallMaterial);

            CreateConcreteBarrier(parent, new Vector3(37.0f, 0.5f, -16.0f), new Vector3(4.0f, 1.0f, 0.6f), wallMaterial);

            CreateConcreteBarrier(parent, new Vector3(31.0f, 0.5f, -10.0f), new Vector3(4.0f, 1.0f, 0.6f), wallMaterial);



            // 7. Yard Floodlight Poles

            CreateFloodlightPole(parent, new Vector3(26.0f, 0f, -32.0f));

            CreateFloodlightPole(parent, new Vector3(44.0f, 0f, -32.0f));

            CreateFloodlightPole(parent, new Vector3(24.0f, 0f, -6.0f));

            CreateFloodlightPole(parent, new Vector3(46.0f, 0f, -6.0f));

            CreateFloodlightPole(parent, new Vector3(82.0f, 0f, 30.0f)); // East yard

            CreateFloodlightPole(parent, new Vector3(-18.0f, 0f, 30.0f)); // West yard

            CreateFloodlightPole(parent, new Vector3(35.0f, 0f, 72.0f)); // North service yard



            // 8. Industrial Yard Details

            // West Utility Yard (Transformers & Generators)

            CreateIndustrialGenerator(parent, new Vector3(-18.0f, 1.5f, 20.0f), wallMaterial);

            CreateIndustrialGenerator(parent, new Vector3(-18.0f, 1.5f, 32.0f), wallMaterial);

            CreateIndustrialGenerator(parent, new Vector3(-18.0f, 1.5f, 44.0f), wallMaterial);



            // East Loading Yard (Shipping Containers)

            CreateShippingContainers(parent, new Vector3(82.0f, 1.3f, 20.0f), wallMaterial);

            CreateShippingContainers(parent, new Vector3(82.0f, 1.3f, 40.0f), wallMaterial);

        }



        // =========================================================================

        // 2. THREE-LEVEL FLOOR SLABS WITH OPEN STAIR SHAFT CUTOUT

        // =========================================================================

        private void BuildThreeFloorSlabs(Transform parent, CampusLayoutConfig cfg)

        {

            Rect bb = cfg.buildingBounds; // X: 0..70, Z: 0..60

            float slabThickness = 0.25f;



            // --- GROUND FLOOR SLAB (Y = 0.0m) ---

            CreateFloorSlab(parent, "Floor_Ground_Full", new Vector3(bb.center.x, cfg.groundY - (slabThickness * 0.5f), bb.center.y), new Vector3(bb.width, slabThickness, bb.height), floorMaterial);



            // --- LEVEL 2 FLOOR SLAB (Y = 3.6m) WITH STAIR SHAFT CUTOUT ---

            // Building spans X: 0..70, Z: 0..60.

            // Stair shaft is X: 28..36, Z: 46..56 (stair opening cut out so stairs rise freely).

            // Landing at X: 28..36, Z: 44..46 is bridged by a landing slab.

            // Split Level 2 floor into clean non-overlapping slabs around the shaft:

            // West Wing: X 0..28, Z 0..60

            CreateFloorSlab(parent, "Floor_L2_WestWing", new Vector3(14.0f, cfg.level2Y - (slabThickness * 0.5f), 30.0f), new Vector3(28.0f, slabThickness, 60.0f), floorMaterial);

            // East Wing: X 36..70, Z 0..60

            CreateFloorSlab(parent, "Floor_L2_EastWing", new Vector3(53.0f, cfg.level2Y - (slabThickness * 0.5f), 30.0f), new Vector3(34.0f, slabThickness, 60.0f), floorMaterial);

            // Center South: X 28..36, Z 0..46.0 (seamless landing in front of portal L2_1)

            CreateFloorSlab(parent, "Floor_L2_CenterSouth", new Vector3(32.0f, cfg.level2Y - (slabThickness * 0.5f), 23.0f), new Vector3(8.0f, slabThickness, 46.0f), floorMaterial);

            // West Flight Base Underlay: X 28..32, Z 46.0..48.5 (under flight takeoff, eliminating drop-off edge erosion)

            CreateFloorSlab(parent, "Floor_L2_StairTakeoff_Underlay", new Vector3(30.0f, cfg.level2Y - (slabThickness * 0.5f), 47.25f), new Vector3(4.0f, slabThickness, 2.5f), floorMaterial);

            // Center North: X 28..36, Z 55.5..60 (north edge past stairwell)

            CreateFloorSlab(parent, "Floor_L2_CenterNorth", new Vector3(32.0f, cfg.level2Y - (slabThickness * 0.5f), 57.75f), new Vector3(8.0f, slabThickness, 4.5f), floorMaterial);

            // Notice: X 28..36, Z 46.5..55.5 is open as a vertical stairwell shaft!



            // --- LEVEL 3 FLOOR SLABS (Y = 7.2m) ---

            // 1. Enclosed West & Central Wing Floor: X 0..44, Z 10..60

            // Split around Level 3 stair shaft:

            CreateFloorSlab(parent, "Floor_L3_WestWing", new Vector3(13.0f, cfg.level3Y - (slabThickness * 0.5f), 35.0f), new Vector3(26.0f, slabThickness, 50.0f), floorMaterial);

            CreateFloorSlab(parent, "Floor_L3_CenterSouth", new Vector3(35.0f, cfg.level3Y - (slabThickness * 0.5f), 28.25f), new Vector3(18.0f, slabThickness, 36.5f), floorMaterial); // covers Z: 10..46.5

            CreateFloorSlab(parent, "Floor_L3_CenterNorth", new Vector3(32.0f, cfg.level3Y - (slabThickness * 0.5f), 57.75f), new Vector3(8.0f, slabThickness, 4.5f), floorMaterial);

            CreateFloorSlab(parent, "Floor_L3_CenterEastSpine", new Vector3(40.0f, cfg.level3Y - (slabThickness * 0.5f), 51.0f), new Vector3(8.0f, slabThickness, 10.0f), floorMaterial); // X: 36..44, Z: 46..56



            // 2. Open-Air Helipad Terrace Deck: X 44..70, Z 36..60 (Reinforced flight deck at Y = 7.2m)

            CreateFloorSlab(parent, "Floor_L3_Helipad_Deck", new Vector3(57.0f, cfg.level3Y - (slabThickness * 0.5f), 48.0f), new Vector3(26.0f, slabThickness, 24.0f), floorMaterial);



            // 3. Main Roof Slab above Level 3 Enclosed Wings (Y = 10.8m)

            CreateFloorSlab(parent, "Main_Roof_Slab_L3_Capping", new Vector3(22.0f, cfg.mainRoofParapetY, 35.0f), new Vector3(44.0f, slabThickness, 50.0f), wallMaterial);

            // Roof Safety Parapet on top of Y = 10.8m

            CreateWall(parent, "Roof_Parapet_West", new Vector3(0f, cfg.mainRoofParapetY + 0.45f, 35.0f), new Vector3(0.4f, 0.9f, 50.0f), wallMaterial);

            CreateWall(parent, "Roof_Parapet_South", new Vector3(22.0f, cfg.mainRoofParapetY + 0.45f, 10.0f), new Vector3(44.0f, 0.9f, 0.4f), wallMaterial);

            CreateWall(parent, "Roof_Parapet_North", new Vector3(22.0f, cfg.mainRoofParapetY + 0.45f, 60.0f), new Vector3(44.0f, 0.9f, 0.4f), wallMaterial);

        }



        // =========================================================================

        // 3. CONTINUOUS SOLID WALLS & OPEN PORTALS ACROSS ALL THREE FLOORS

        // =========================================================================

        private void BuildAllWallsAndPortals(Transform wallsRoot, Transform portalsRoot, CampusLayoutConfig cfg)

        {

            float wH = cfg.wallHeight;

            float wT = cfg.wallThickness;



            // --- GROUND FLOOR WALLS (Y = 0.0m) ---

            // 1. South Exterior Wall (Z = 0.0m, X 0..70) with Portal G1 (X 32..38)

            BuildWallHorizontalWithPortal(wallsRoot, portalsRoot, 0f, 70f, 0f, cfg.groundY, wH, wT, 32f, 38f, "G1_Main_Entrance");



            // 2. North Exterior Wall (Z = 60.0m, X 0..70) - Solid

            CreateWall(wallsRoot, "Wall_G_Exterior_North", new Vector3(35.0f, cfg.groundY + wH * 0.5f, 60.0f), new Vector3(70.0f, wH, wT), wallMaterial);



            // 3. West Exterior Wall (X = 0.0m, Z 0..60) - Solid

            CreateWall(wallsRoot, "Wall_G_Exterior_West", new Vector3(0f, cfg.groundY + wH * 0.5f, 30.0f), new Vector3(wT, wH, 60.0f), wallMaterial);



            // 4. East Exterior Wall (X = 70.0m, Z 0..60) with Portal G8 (Z 46..50 to Loading Yard)

            BuildWallVerticalWithPortal(wallsRoot, portalsRoot, 0f, 60f, 70f, cfg.groundY, wH, wT, 46f, 50f, "G8_Logistics_LoadingYard");



            // 5. Ground Interior Partitions

            // Z = 12m (Vestibule / Security) with Portal G2 (X 29..33)

            BuildWallHorizontalWithPortal(wallsRoot, portalsRoot, 26f, 44f, 12f, cfg.groundY, wH, wT, 29f, 33f, "G2_Vestibule_Security");



            // Z = 22m (Security / Central Spine) with Portal G3 (X 37..41)

            BuildWallHorizontalWithPortal(wallsRoot, portalsRoot, 26f, 44f, 22f, cfg.groundY, wH, wT, 37f, 41f, "G3_Security_Spine");



            // X = 26m (West Spine Dividing Wall, Z 0..60) with Portal G4 (Z 24..28)

            BuildWallVerticalWithPortal(wallsRoot, portalsRoot, 0f, 60f, 26f, cfg.groundY, wH, wT, 24f, 28f, "G4_Spine_Stores");



            // Z = 30m (Stores / Workshop Dividing Wall, X 0..26) with Portal G5 (X 10..14)

            BuildWallHorizontalWithPortal(wallsRoot, portalsRoot, 0f, 26f, 30f, cfg.groundY, wH, wT, 10f, 14f, "G5_Stores_Workshop");



            // X = 44m (East Spine Dividing Wall, Z 0..60) with Portal G6 (Z 18..22 to Lab)

            BuildWallVerticalWithPortal(wallsRoot, portalsRoot, 0f, 60f, 44f, cfg.groundY, wH, wT, 18f, 22f, "G6_Spine_ResearchLab");



            // Z = 36m (Lab / Logistics Bay Partition, X 44..70) - Solid partition

            CreateWall(wallsRoot, "Wall_G_Lab_Logistics_Partition", new Vector3(57.0f, cfg.groundY + wH * 0.5f, 36.0f), new Vector3(26.0f, wH, wT), wallMaterial);



            // --- LEVEL 2 WALLS (Y = 3.6m) ---

            // 1. Exterior Perimeter Walls (X 0..70, Z 0..60 at Y = 3.6m)

            CreateWall(wallsRoot, "Wall_L2_Ext_South", new Vector3(35.0f, cfg.level2Y + wH * 0.5f, 0f), new Vector3(70.0f, wH, wT), wallMaterial);

            CreateWall(wallsRoot, "Wall_L2_Ext_North", new Vector3(35.0f, cfg.level2Y + wH * 0.5f, 60.0f), new Vector3(70.0f, wH, wT), wallMaterial);

            CreateWall(wallsRoot, "Wall_L2_Ext_West", new Vector3(0f, cfg.level2Y + wH * 0.5f, 30.0f), new Vector3(wT, wH, 60.0f), wallMaterial);

            CreateWall(wallsRoot, "Wall_L2_Ext_East", new Vector3(70.0f, cfg.level2Y + wH * 0.5f, 30.0f), new Vector3(wT, wH, 60.0f), wallMaterial);



            // 2. Level 2 Corridor West Wall (X = 26m, Z 0..60) with Portals L2_4 (Z 34..38 Records) & L2_5 (Z 18..22 Armoury)

            BuildWallVerticalWithTwoPortals(wallsRoot, portalsRoot, 0f, 60f, 26f, cfg.level2Y, wH, wT, 18f, 22f, "L2_5_Corridor_Armoury", 34f, 38f, "L2_4_Corridor_Records");



            // 3. Level 2 Corridor East Wall (X = 44m, Z 0..60) with Portals L2_3 (Z 20..24 Analysis) & L2_2 (Z 38..42 Secure Archive)

            BuildWallVerticalWithTwoPortals(wallsRoot, portalsRoot, 0f, 60f, 44f, cfg.level2Y, wH, wT, 20f, 24f, "L2_3_Corridor_Analysis", 38f, 42f, "L2_2_Corridor_Archive");



            // 4. Secure Archive Baffle (Blocks direct sightline from portal into terminal)

            CreateWall(wallsRoot, "Wall_L2_Archive_Chicane", new Vector3(48.0f, cfg.level2Y + wH * 0.5f, 40.0f), new Vector3(wT, wH, 4.5f), wallMaterial);



            // 5. Partition between Analysis and Secure Archive (Z = 28m, X 44..70)

            CreateWall(wallsRoot, "Wall_L2_Analysis_Archive_Partition", new Vector3(57.0f, cfg.level2Y + wH * 0.5f, 28.0f), new Vector3(26.0f, wH, wT), wallMaterial);



            // 6. Partition between Armoury and Records (Z = 28m, X 0..26)

            CreateWall(wallsRoot, "Wall_L2_Armoury_Records_Partition", new Vector3(13.0f, cfg.level2Y + wH * 0.5f, 28.0f), new Vector3(26.0f, wH, wT), wallMaterial);



            // --- LEVEL 3 WALLS (Y = 7.2m) ---

            // 1. Level 3 Corridor West Wall (X = 26m, Z 10..44) with Portals L3_2 (Z 38..42 Command) & L3_3 (Z 20..24 Briefing)

            BuildWallVerticalWithTwoPortals(wallsRoot, portalsRoot, 10f, 44f, 26f, cfg.level3Y, wH, wT, 20f, 24f, "L3_3_Corridor_Briefing", 38f, 42f, "L3_2_Corridor_Command");

            // 1b. Level 3 Command Deck East Wall (X = 26m, Z 44..60) sealing gap to North Exterior Wall

            CreateWall(wallsRoot, "Wall_L3_Command_Deck_East_North", new Vector3(26.0f, cfg.level3Y + wH * 0.5f, 52.0f), new Vector3(wT, wH, 16.0f), wallMaterial);



            // 2. Level 3 Corridor East Wall (X = 44m, Z 30..46) with Portal L3_5 (Z 38..42 Helipad Terrace)

            BuildWallVerticalWithPortal(wallsRoot, portalsRoot, 30f, 46f, 44f, cfg.level3Y, wH, wT, 38f, 42f, "L3_5_Corridor_Helipad");



            // 3. Level 3 Corridor South Wall (Z = 18m, X 26..44) with Portal L3_4 (X 33..37 Mechanical Penthouse)

            BuildWallHorizontalWithPortal(wallsRoot, portalsRoot, 26f, 44f, 18f, cfg.level3Y, wH, wT, 33f, 37f, "L3_4_Corridor_Mech");



            // 4. Command & Briefing West Exterior Walls

            CreateWall(wallsRoot, "Wall_L3_Ext_West", new Vector3(0f, cfg.level3Y + wH * 0.5f, 35.0f), new Vector3(wT, wH, 50.0f), wallMaterial);

            CreateWall(wallsRoot, "Wall_L3_Ext_South", new Vector3(22.0f, cfg.level3Y + wH * 0.5f, 10.0f), new Vector3(44.0f, wH, wT), wallMaterial);

            CreateWall(wallsRoot, "Wall_L3_Ext_North", new Vector3(22.0f, cfg.level3Y + wH * 0.5f, 60.0f), new Vector3(44.0f, wH, wT), wallMaterial);



            // 5. Partition between Briefing and Command (Z = 32m, X 0..26)

            CreateWall(wallsRoot, "Wall_L3_Briefing_Command_Partition", new Vector3(13.0f, cfg.level3Y + wH * 0.5f, 32.0f), new Vector3(26.0f, wH, wT), wallMaterial);



            // 6. Helipad Terrace Perimeter Safety Barrier (1.2m high railing/curb around open-air deck X 44..70, Z 36..60)

            CreateWall(wallsRoot, "Helipad_Barrier_North", new Vector3(57.0f, cfg.level3Y + 0.6f, 60.0f), new Vector3(26.0f, 1.2f, 0.3f), accentMaterial ?? wallMaterial);

            CreateWall(wallsRoot, "Helipad_Barrier_East", new Vector3(70.0f, cfg.level3Y + 0.6f, 48.0f), new Vector3(0.3f, 1.2f, 24.0f), accentMaterial ?? wallMaterial);

            CreateWall(wallsRoot, "Helipad_Barrier_South", new Vector3(57.0f, cfg.level3Y + 0.6f, 36.0f), new Vector3(26.0f, 1.2f, 0.3f), accentMaterial ?? wallMaterial);

        }



        // =========================================================================

        // 4. STACKED CONTINUOUS SWITCHBACK STAIR CORE (Ground -> L2 -> L3)

        // =========================================================================

        private void BuildStackedSwitchbackStairCore(Transform parent, CampusLayoutConfig cfg)

        {

            float wH = cfg.wallHeight;

            float wT = cfg.wallThickness;



            // Stair core footprint: X: 28..36, Z: 44..56

            // 1. Enclosing Solid Walls on All Levels

            // East Wall (X = 36m, Z 44..56)

            CreateWall(parent, "StairCore_Wall_East_G", new Vector3(36.0f, cfg.groundY + wH * 0.5f, 50.0f), new Vector3(wT, wH, 12.0f), wallMaterial);

            CreateWall(parent, "StairCore_Wall_East_L2", new Vector3(36.0f, cfg.level2Y + wH * 0.5f, 50.0f), new Vector3(wT, wH, 12.0f), wallMaterial);

            CreateWall(parent, "StairCore_Wall_East_L3", new Vector3(36.0f, cfg.level3Y + wH * 0.5f, 50.0f), new Vector3(wT, wH, 12.0f), wallMaterial);



            // West Wall (X = 28m, Z 44..56)

            CreateWall(parent, "StairCore_Wall_West_G", new Vector3(28.0f, cfg.groundY + wH * 0.5f, 50.0f), new Vector3(wT, wH, 12.0f), wallMaterial);

            CreateWall(parent, "StairCore_Wall_West_L2", new Vector3(28.0f, cfg.level2Y + wH * 0.5f, 50.0f), new Vector3(wT, wH, 12.0f), wallMaterial);

            CreateWall(parent, "StairCore_Wall_West_L3", new Vector3(28.0f, cfg.level3Y + wH * 0.5f, 50.0f), new Vector3(wT, wH, 12.0f), wallMaterial);



            // North Wall (Z = 56m, X 28..36)

            CreateWall(parent, "StairCore_Wall_North_G", new Vector3(32.0f, cfg.groundY + wH * 0.5f, 56.0f), new Vector3(8.0f, wH, wT), wallMaterial);

            CreateWall(parent, "StairCore_Wall_North_L2", new Vector3(32.0f, cfg.level2Y + wH * 0.5f, 56.0f), new Vector3(8.0f, wH, wT), wallMaterial);

            CreateWall(parent, "StairCore_Wall_North_L3", new Vector3(32.0f, cfg.level3Y + wH * 0.5f, 56.0f), new Vector3(8.0f, wH, wT), wallMaterial);



            // South Wall (Z = 44m, X 28..36) with Portals G7, L2_1, L3_1 at X 30..34

            BuildWallHorizontalWithPortal(parent, parent, 28f, 36f, 44f, cfg.groundY, wH, wT, 30f, 34f, "G7_Spine_StairCore");

            BuildWallHorizontalWithPortal(parent, parent, 28f, 36f, 44f, cfg.level2Y, wH, wT, 30f, 34f, "L2_1_Stair_Corridor");

            BuildWallHorizontalWithPortal(parent, parent, 28f, 36f, 44f, cfg.level3Y, wH, wT, 30f, 34f, "L3_1_Stair_Corridor");



            // Central Dividing Spine inside Stair Core between West & East flights (X = 32.0m, Z 45.5..53.0)

            // Thickness 0.30m spans X 31.85..32.15m flush against both 4.0m wide stair flights

            CreateWall(parent, "Stair_Dividing_Spine_G", new Vector3(32.0f, cfg.groundY + 1.8f, 49.25f), new Vector3(0.30f, 3.6f, 7.5f), wallMaterial);

            CreateWall(parent, "Stair_Dividing_Spine_L2", new Vector3(32.0f, cfg.level2Y + 1.8f, 49.25f), new Vector3(0.30f, 3.6f, 7.5f), wallMaterial);



            // Full-Height Safety Balustrades preventing any accidental falls along inner shaft edges

            CreateWall(parent, "Stair_InnerGuardrail_L2", new Vector3(32.0f, cfg.level2Y + 0.6f, 47.0f), new Vector3(0.30f, 1.2f, 6.0f), wallMaterial);

            CreateWall(parent, "Stair_InnerGuardrail_L3", new Vector3(32.0f, cfg.level3Y + 0.6f, 47.0f), new Vector3(0.30f, 1.2f, 6.0f), wallMaterial);



            // Level 3 Perimeter Safety Balustrades around the open stairwell shaft

            CreateWall(parent, "Stair_L3_Perimeter_West", new Vector3(28.15f, cfg.level3Y + 0.6f, 50.0f), new Vector3(0.30f, 1.2f, 8.0f), wallMaterial);

            CreateWall(parent, "Stair_L3_Perimeter_North", new Vector3(30.0f, cfg.level3Y + 0.6f, 55.85f), new Vector3(4.0f, 1.2f, 0.30f), wallMaterial);



            // --- FLIGHT 1: GROUND TO LEVEL 2 SWITCHBACK (0.0m -> 3.6m) ---

            // 1. Half-Landing Platform at North end (Z 52.5..56.0, Y = 1.8m, wall-to-wall X: 28.0..36.0m)

            CreateFloorSlab(parent, "Stair_HalfLanding_G_L2", new Vector3(32.0f, cfg.groundY + 1.8f - 0.1f, 54.25f), new Vector3(8.0f, 0.2f, 3.5f), floorMaterial);



            // 2. West Flight (Ascends North from Z = 46.0 to 53.0, Y = 0.0 -> 1.8m) - Spans X: 28.0..32.0m

            BuildSwitchbackFlight(parent, "Flight_G_To_HalfLanding", 30.0f, 46.0f, 53.0f, cfg.groundY, 1.8f, cfg.stairFlightWidth, cfg.stairFlightAngle, true);



            // 3. East Flight (Switchbacks and Ascends South from Z = 53.0 to 46.0, Y = 1.8m -> 3.6m) - Spans X: 32.0..36.0m

            BuildSwitchbackFlight(parent, "Flight_HalfLanding_To_L2", 34.0f, 53.0f, 46.0f, cfg.groundY + 1.8f, 1.8f, cfg.stairFlightWidth, cfg.stairFlightAngle, false);



            // --- FLIGHT 2: LEVEL 2 TO LEVEL 3 SWITCHBACK (3.6m -> 7.2m) ---

            // 1. Half-Landing Platform at North end (Z 52.5..56.0, Y = 5.4m, wall-to-wall X: 28.0..36.0m)

            CreateFloorSlab(parent, "Stair_HalfLanding_L2_L3", new Vector3(32.0f, cfg.level2Y + 1.8f - 0.1f, 54.25f), new Vector3(8.0f, 0.2f, 3.5f), floorMaterial);



            // 2. West Flight (Ascends North from Z = 46.0 to 53.0, Y = 3.6m -> 5.4m) - Spans X: 28.0..32.0m

            BuildSwitchbackFlight(parent, "Flight_L2_To_HalfLanding", 30.0f, 46.0f, 53.0f, cfg.level2Y, 1.8f, cfg.stairFlightWidth, cfg.stairFlightAngle, true);



            // 3. East Flight (Switchbacks and Ascends South from Z = 53.0 to 46.0, Y = 5.4m -> 7.2m) - Spans X: 32.0..36.0m

            BuildSwitchbackFlight(parent, "Flight_HalfLanding_To_L3", 34.0f, 53.0f, 46.0f, cfg.level2Y + 1.8f, 1.8f, cfg.stairFlightWidth, cfg.stairFlightAngle, false);

        }



        private void BuildSwitchbackFlight(Transform parent, string flightName, float x, float startZ, float endZ, float baseY, float rise, float width, float angle, bool ascendNorth)

        {

            GameObject flightObj = new GameObject(flightName);

            flightObj.transform.SetParent(parent, false);



            int steps = 10;

            float length = Mathf.Abs(endZ - startZ);

            float stepLength = length / steps;

            float stepRise = rise / steps;



            for (int i = 0; i < steps; i++)

            {

                float z = ascendNorth ? startZ + (i + 0.5f) * stepLength : startZ - (i + 0.5f) * stepLength;

                float stepTopY = baseY + (i + 1) * stepRise;



                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);

                step.name = $"Step_{i + 1}";

                step.transform.SetParent(flightObj.transform, false);

                step.transform.position = new Vector3(x, stepTopY - (stepRise * 0.5f), z);

                step.transform.localScale = new Vector3(width, stepRise, stepLength);

                if (wallMaterial != null) step.GetComponent<Renderer>().sharedMaterial = wallMaterial;



                // Strip box collider on visual steps to prevent CharacterController snagging

                Object.DestroyImmediate(step.GetComponent<Collider>());

                step.isStatic = true;

            }



            // Continuous Smooth Ramp Box Collider with seamless flush overlap at both landings

            GameObject ramp = new GameObject("Smooth_Ramp_Collider");

            ramp.transform.SetParent(flightObj.transform, false);

            float midZ = (startZ + endZ) * 0.5f;

            float midY = baseY + (rise * 0.5f);

            float thickness = 0.24f;

            float rampLength = Mathf.Sqrt(length * length + rise * rise) + 0.6f; // 0.3m extension at both ends for seamless merge

            float yOffset = (thickness * 0.5f) / Mathf.Cos(angle * Mathf.Deg2Rad);



            ramp.transform.position = new Vector3(x, midY - yOffset, midZ);

            ramp.transform.rotation = Quaternion.Euler(ascendNorth ? -angle : angle, 0f, 0f);

            BoxCollider col = ramp.AddComponent<BoxCollider>();

            col.size = new Vector3(width, thickness, rampLength);

            ramp.isStatic = true;

        }



        // =========================================================================

        // 5. PROPS, LAB BENCHES, SERVER RACKS, HELIPAD MARKINGS & SIGNAGE

        // =========================================================================

        private void BuildPropsAndSignage(Transform parent, CampusLayoutConfig cfg)

        {

            // 1. Helipad Terrace 'H' Markings on Level 3 (Y = 7.2m) - Visuals only, no obstruction colliders

            Vector3 helipadCenter = new Vector3(57.0f, cfg.level3Y + 0.02f, 48.0f);

            GameObject helipadH = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            helipadH.name = "Helipad_Circle_Ring";

            helipadH.transform.SetParent(parent, false);

            helipadH.transform.position = helipadCenter;

            helipadH.transform.localScale = new Vector3(18f, 0.02f, 18f);

            if (accentMaterial != null) helipadH.GetComponent<Renderer>().sharedMaterial = accentMaterial;

            Object.DestroyImmediate(helipadH.GetComponent<Collider>());

            helipadH.isStatic = true;



            // 'H' Stencil Bars (Visuals only)

            CreateDecalBar(parent, "Helipad_H_BarL", new Vector3(helipadCenter.x - 3.0f, cfg.level3Y + 0.04f, helipadCenter.z), new Vector3(1.2f, 0.03f, 8.0f), accentMaterial);

            CreateDecalBar(parent, "Helipad_H_BarR", new Vector3(helipadCenter.x + 3.0f, cfg.level3Y + 0.04f, helipadCenter.z), new Vector3(1.2f, 0.03f, 8.0f), accentMaterial);

            CreateDecalBar(parent, "Helipad_H_BarMid", new Vector3(helipadCenter.x, cfg.level3Y + 0.04f, helipadCenter.z), new Vector3(6.0f, 0.03f, 1.2f), accentMaterial);



            // Helipad Perimeter Navigation Beacons

            CreateFloodlightPole(parent, new Vector3(45.0f, cfg.level3Y, 37.0f));

            CreateFloodlightPole(parent, new Vector3(69.0f, cfg.level3Y, 37.0f));

            CreateFloodlightPole(parent, new Vector3(69.0f, cfg.level3Y, 59.0f));

            CreateFloodlightPole(parent, new Vector3(45.0f, cfg.level3Y, 59.0f));



            // 2. Level 2 Secure Archive Mainframe Server Aisles (Cleanly spaced around central objective)

            float l2Y = cfg.level2Y;

            CreateServerRack(parent, new Vector3(52.0f, l2Y + 1.25f, 32.0f), new Vector3(1.2f, 2.5f, 5.0f), wallMaterial);

            CreateServerRack(parent, new Vector3(64.0f, l2Y + 1.25f, 32.0f), new Vector3(1.2f, 2.5f, 5.0f), wallMaterial);

            CreateServerRack(parent, new Vector3(52.0f, l2Y + 1.25f, 55.0f), new Vector3(1.2f, 2.5f, 5.0f), wallMaterial);

            CreateServerRack(parent, new Vector3(64.0f, l2Y + 1.25f, 55.0f), new Vector3(1.2f, 2.5f, 5.0f), wallMaterial);



            // 3. Ground Research Lab Clean Benches & Diagnostic Terminals

            float gY = cfg.groundY;

            CreateLabBench(parent, new Vector3(52.0f, gY + 0.5f, 12.0f), new Vector3(5.0f, 1.0f, 1.6f), wallMaterial);

            CreateLabBench(parent, new Vector3(62.0f, gY + 0.5f, 12.0f), new Vector3(5.0f, 1.0f, 1.6f), wallMaterial);

            CreateLabBench(parent, new Vector3(52.0f, gY + 0.5f, 24.0f), new Vector3(5.0f, 1.0f, 1.6f), wallMaterial);

            CreateLabBench(parent, new Vector3(62.0f, gY + 0.5f, 24.0f), new Vector3(5.0f, 1.0f, 1.6f), wallMaterial);



            // 4. Level 3 Command Holomap Table

            float l3Y = cfg.level3Y;

            GameObject holoTable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            holoTable.name = "Command_Holomap_Table";

            holoTable.transform.SetParent(parent, false);

            holoTable.transform.position = new Vector3(14.0f, l3Y + 0.5f, 46.0f);

            holoTable.transform.localScale = new Vector3(4.5f, 0.5f, 4.5f);

            if (accentMaterial != null) holoTable.GetComponent<Renderer>().sharedMaterial = accentMaterial;

            holoTable.isStatic = true;



            // 5. Wayfinding Signage Headers above key portals

            CreatePortalSign(parent, "Sign_SecurityCheckpoint", new Vector3(31.0f, gY + 3.2f, 12.0f), "SECURITY CHECKPOINT");

            CreatePortalSign(parent, "Sign_ResearchLab", new Vector3(44.0f, gY + 3.2f, 20.0f), "RESEARCH LAB");

            CreatePortalSign(parent, "Sign_StairCore_Ground", new Vector3(32.0f, gY + 3.2f, 44.0f), "STAIRWELL - ACCESS TO LEVEL 2 & 3");

            CreatePortalSign(parent, "Sign_SecureArchive_L2", new Vector3(44.0f, l2Y + 3.2f, 40.0f), "SECURE ARCHIVE - LEVEL 2");

            CreatePortalSign(parent, "Sign_CommandHelipad_L3", new Vector3(44.0f, l3Y + 3.2f, 40.0f), "HELIPAD - LEVEL 3 LZ");

        }



        // =========================================================================

        // 5b. FACILITY INTERIOR LIGHTING (Equal-Spacing Modular Panels & Soft Shadows)

        // =========================================================================

        private void BuildFacilityInteriorLighting(Transform parent, CampusLayoutConfig cfg)

        {

            // Monochrome Whitish-Yellow Tactical Illumination (~4000K Neutral Warm LED)

            Color lightColor = new Color(1.0f, 0.95f, 0.82f);

            Color emissionColor = new Color(1.0f, 0.95f, 0.82f) * 3.5f;



            Material matHousing = CreatePbrMaterial("Mat_LightFixtureHousing", new Color(0.12f, 0.13f, 0.15f), 0.85f, 0.6f);

            Material matPanel = CreatePbrMaterial("Mat_LightPanel_WhitishYellow", new Color(1.0f, 0.96f, 0.86f), 0.1f, 0.95f, emissionColor);



            int panelIndex = 1;



            // 1. Equal-Spacing Room Light Grids for all enclosed interior rooms & corridors

            foreach (var room in cfg.rooms)

            {

                // Outdoor compound grounds and open-air Helipad deck rely on natural sky/directional lighting

                if (room.roomName.StartsWith("Outdoor") || room.roomName == "Level3_Helipad_Terrace") continue;



                // Stairwell core handled with specialized vertical shaft layout below

                if (room.roomName.Contains("Stair")) continue;



                // Under open vertical drop from Level 3: Use wall-mounted luminaires instead of floating ceiling fixtures

                if (room.roomName == "Level2_Data_Analysis_Hub")

                {

                    float wallLightY = cfg.level2Y + 1.8f; // 5.4m (Level 2 wall height)

                    // West wall (X = 44.25)

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.minX + 0.25f, wallLightY, 3.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.minX + 0.25f, wallLightY, 8.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.minX + 0.25f, wallLightY, 14.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.minX + 0.25f, wallLightY, 20.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.minX + 0.25f, wallLightY, 25.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    // East wall (X = 69.75)

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.maxX - 0.25f, wallLightY, 3.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.maxX - 0.25f, wallLightY, 8.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.maxX - 0.25f, wallLightY, 14.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.maxX - 0.25f, wallLightY, 20.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(room.maxX - 0.25f, wallLightY, 25.0f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    // South wall (Z = 0.35)

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(48.0f, wallLightY, room.minZ + 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(52.5f, wallLightY, room.minZ + 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(57.0f, wallLightY, room.minZ + 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(61.5f, wallLightY, room.minZ + 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(66.0f, wallLightY, room.minZ + 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    // North wall (Z = 27.65)

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(48.0f, wallLightY, room.maxZ - 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(52.5f, wallLightY, room.maxZ - 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(57.0f, wallLightY, room.maxZ - 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(61.5f, wallLightY, room.maxZ - 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(66.0f, wallLightY, room.maxZ - 0.35f), matHousing, matPanel, lightColor, 2.5f, 6.0f, 135f);

                    continue;

                }



                float width = room.maxX - room.minX;

                float depth = room.maxZ - room.minZ;



                // Calculate equal spacing: 1 panel every ~5.5 to 6.5 meters in X and Z

                int cols = Mathf.Max(1, Mathf.RoundToInt(width / 6.0f));

                int rows = Mathf.Max(1, Mathf.RoundToInt(depth / 6.0f));



                float xStep = width / cols;

                float zStep = depth / rows;

                float ceilingY = room.ceilingElevation - 0.04f;



                for (int c = 0; c < cols; c++)

                {

                    float x = room.minX + (c + 0.5f) * xStep;

                    for (int r = 0; r < rows; r++)

                    {

                        float z = room.minZ + (r + 0.5f) * zStep;

                        // Avoid spawning in the open drop void south of the Level 3 Helipad Deck

                        if (x >= 44.0f && z < 36.5f)

                        {

                            z = 36.8f;

                        }

                        CreateCeilingLightPanel(parent, $"LightPanel_{room.roomName}_{panelIndex++}", new Vector3(x, ceilingY, z), matHousing, matPanel, lightColor, intensity: 2.5f, range: 5.4f, spotAngle: 135f);

                    }

                }

            }



            // 2. Stairwell Core Shaft Downlights (Illuminating flights and switchback landings)

            CreateCeilingLightPanel(parent, "Light_StairCore_G_Takeoff", new Vector3(30.05f, cfg.level2Y - 0.04f, 46.5f), matHousing, matPanel, lightColor, intensity: 2.6f, range: 5.5f, spotAngle: 130f);

            CreateCeilingLightPanel(parent, "Light_StairCore_G_HalfLanding", new Vector3(32.0f, cfg.groundY + 1.8f + 3.0f, 54.3f), matHousing, matPanel, lightColor, intensity: 2.8f, range: 6.0f, spotAngle: 135f);

            CreateCeilingLightPanel(parent, "Light_StairCore_L2_Landing", new Vector3(33.95f, cfg.level3Y - 0.04f, 46.5f), matHousing, matPanel, lightColor, intensity: 2.6f, range: 5.5f, spotAngle: 130f);

            CreateCeilingLightPanel(parent, "Light_StairCore_L2_HalfLanding", new Vector3(32.0f, cfg.level2Y + 1.8f + 3.0f, 54.3f), matHousing, matPanel, lightColor, intensity: 2.8f, range: 6.0f, spotAngle: 135f);

            CreateCeilingLightPanel(parent, "Light_StairCore_L3_Landing", new Vector3(33.95f, cfg.mainRoofParapetY - 0.04f, 46.5f), matHousing, matPanel, lightColor, intensity: 2.6f, range: 5.5f, spotAngle: 130f);

        }



        private GameObject CreateCeilingLightPanel(Transform parent, string fixtureName, Vector3 ceilingPos, Material matHousing, Material matPanel, Color lightColor, float intensity = 2.4f, float range = 5.2f, float spotAngle = 135f)

        {

            GameObject fixture = new GameObject(fixtureName);

            fixture.transform.SetParent(parent, false);

            fixture.transform.position = ceilingPos;



            // 1. Chassis Housing (Dark Aluminum Recessed Rim)

            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);

            housing.name = "Troffer_Housing";

            housing.transform.SetParent(fixture.transform, false);

            housing.transform.localPosition = Vector3.zero;

            housing.transform.localScale = new Vector3(1.4f, 0.05f, 0.6f);

            if (matHousing != null) housing.GetComponent<Renderer>().sharedMaterial = matHousing;

            DestroyImmediate(housing.GetComponent<Collider>());

            housing.isStatic = true;



            // 2. Translucent Emissive Whitish-Yellow Diffuser Panel (Glows when looking up)

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);

            panel.name = "Diffuser_Panel";

            panel.transform.SetParent(fixture.transform, false);

            panel.transform.localPosition = new Vector3(0f, -0.026f, 0f);

            panel.transform.localScale = new Vector3(1.3f, 0.015f, 0.52f);

            if (matPanel != null) panel.GetComponent<Renderer>().sharedMaterial = matPanel;

            DestroyImmediate(panel.GetComponent<Collider>());

            panel.isStatic = true;



            // 3. Wide Downward-Facing Optical Spotlight with Soft Shadow Physics

            GameObject lightObj = new GameObject("LightSource");

            lightObj.transform.SetParent(fixture.transform, false);

            lightObj.transform.localPosition = new Vector3(0f, -0.05f, 0f);

            lightObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Pointing straight down



            Light l = lightObj.AddComponent<Light>();

            l.type = LightType.Spot;

            l.spotAngle = spotAngle;

            l.innerSpotAngle = spotAngle * 0.65f;

            l.range = range;

            l.color = lightColor;

            l.intensity = intensity;

            l.shadows = LightShadows.Soft;

            l.shadowNormalBias = 0.4f;

            l.shadowBias = 0.05f;

            l.shadowNearPlane = 0.2f;



            fixture.isStatic = true;

            return fixture;

        }



        private Material CreatePbrMaterial(string name, Color albedo, float metallic = 0.5f, float smoothness = 0.5f, Color emission = default)

        {

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");

            Material mat = new Material(shader);

            mat.name = name;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", albedo);

            if (mat.HasProperty("_Color")) mat.SetColor("_Color", albedo);

            mat.color = albedo;

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);

            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);



            if (emission != default && emission != Color.black)

            {

                mat.EnableKeyword("_EMISSION");

                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);

            }

            return mat;

        }



        // =========================================================================

        // 6. GAMEPLAY SCENE ANCHORS (Floor-Surface Alignment)

        // =========================================================================

        private void RepositionGameplayAnchors(CampusLayoutConfig cfg)

        {

            // 1. Player Start Anchor (Vehicle Yard: X = 35, Y = 0.0, Z = -25)

            if (playerSpawn != null)

            {

                playerSpawn.position = cfg.playerStart;

                playerSpawn.rotation = Quaternion.identity;

            }

            if (playerTransform != null)

            {

                CharacterController cc = playerTransform.GetComponent<CharacterController>();

                if (cc != null) cc.enabled = false;

                playerTransform.position = cfg.playerStart + Vector3.up * 0.1f;

                playerTransform.rotation = Quaternion.identity;

                if (cc != null) cc.enabled = true;

                Debug.Log($"[CampusGenerator] Player repositioned to outdoor yard: {cfg.playerStart}");

            }



            // 2. Three Objective Terminals Across Facility

            // Terminal 3: Level 2 Secure Archive (X = 58, Y = 3.6, Z = 48) - Base template

            if (objectivePoint != null)

            {

                objectivePoint.position = cfg.terminal3_ArchiveAnchor;

                objectivePoint.rotation = Quaternion.Euler(0f, -90f, 0f);



                Transform terminalChild = objectivePoint.Find("ObjectiveTerminal");

                if (terminalChild != null)

                {

                    terminalChild.localPosition = new Vector3(0f, 0.45f, 0f);

                }

                var inter3 = objectivePoint.GetComponentInChildren<TacticalShooter.Systems.Mission.ObjectiveInteractable>();

                if (inter3 != null)

                {

                    inter3.TerminalId = "TERM_L2_ARCHIVE";

                    inter3.DisplayName = "Secure Archive Terminal";

                }

                Debug.Log($"[CampusGenerator] Terminal 3 (Secure Archive) positioned: {cfg.terminal3_ArchiveAnchor}");

            }



            // Terminal 1: Ground Floor Stores Room (X = 13, Y = 0, Z = 15)

            GameObject storesObj = GameObject.Find("ObjectivePoint_Stores");

            if (storesObj == null && objectivePoint != null)

            {

                storesObj = Instantiate(objectivePoint.gameObject, objectivePoint.parent);

                storesObj.name = "ObjectivePoint_Stores";

            }

            if (storesObj != null)

            {

                storesObj.transform.position = cfg.terminal1_StoresAnchor;

                storesObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                var inter1 = storesObj.GetComponentInChildren<TacticalShooter.Systems.Mission.ObjectiveInteractable>();

                if (inter1 != null)

                {

                    inter1.TerminalId = "TERM_G_STORES";

                    inter1.DisplayName = "Stores Logistics Terminal";

                }

                Debug.Log($"[CampusGenerator] Terminal 1 (Stores Logistics) positioned: {cfg.terminal1_StoresAnchor}");

            }



            // Terminal 2: Level 2 Classified Records (X = 13, Y = 3.6, Z = 44)

            GameObject recordsObj = GameObject.Find("ObjectivePoint_Records");

            if (recordsObj == null && objectivePoint != null)

            {

                recordsObj = Instantiate(objectivePoint.gameObject, objectivePoint.parent);

                recordsObj.name = "ObjectivePoint_Records";

            }

            if (recordsObj != null)

            {

                recordsObj.transform.position = cfg.terminal2_RecordsAnchor;

                recordsObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                var inter2 = recordsObj.GetComponentInChildren<TacticalShooter.Systems.Mission.ObjectiveInteractable>();

                if (inter2 != null)

                {

                    inter2.TerminalId = "TERM_L2_RECORDS";

                    inter2.DisplayName = "Classified Records Terminal";

                }

                Debug.Log($"[CampusGenerator] Terminal 2 (Classified Records) positioned: {cfg.terminal2_RecordsAnchor}");

            }



            // 3. Extraction Helipad Anchor (Level 3 Helipad: X = 57, Y = 7.2, Z = 48)

            if (extractionPoint != null)

            {

                extractionPoint.position = cfg.extractionHelipadAnchor; // Root is flush at Y = 7.2m

                extractionPoint.rotation = Quaternion.identity;



                Transform zoneChild = extractionPoint.Find("ExtractionZone");

                if (zoneChild != null)

                {

                    zoneChild.localPosition = new Vector3(0f, 0.1f, 0f); // child handles trigger offset

                }

                Debug.Log($"[CampusGenerator] Extraction Zone repositioned to Level 3 Helipad Deck: {cfg.extractionHelipadAnchor}");

            }

        }



        // =========================================================================

        // 5c. TERMINAL SECURITY DOORS (Strictly 3 Doors in Facility)

        // =========================================================================

        private void BuildTerminalSecurityDoors(Transform doorsRoot, CampusLayoutConfig cfg)

        {

            // Clean up any extraneous doors across the scene so only the 3 designated terminal doors exist

            var targetDoorNames = new System.Collections.Generic.HashSet<string>();

            foreach (var dDef in cfg.securityDoors)

            {

                targetDoorNames.Add(dDef.doorName);

            }



            var existingDoors = FindObjectsByType<TacticalShooter.Systems.Interaction.DoorInteractable>(FindObjectsSortMode.None);

            foreach (var d in existingDoors)

            {

                if (d != null && !targetDoorNames.Contains(d.gameObject.name))

                {

#if UNITY_EDITOR

                    if (Application.isPlaying) Destroy(d.gameObject);

                    else Undo.DestroyObjectImmediate(d.gameObject);

#else

                    Destroy(d.gameObject);

#endif

                }

            }



            // Load Door Prefab if available

            GameObject doorPrefab = null;

#if UNITY_EDITOR

            doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interactables/Door.prefab");

#endif



            foreach (var dDef in cfg.securityDoors)

            {

                GameObject existingDoor = GameObject.Find(dDef.doorName);

                if (existingDoor != null)

                {

#if UNITY_EDITOR

                    DestroyImmediate(existingDoor);

#else

                    Destroy(existingDoor);

#endif

                }



                GameObject doorObj = null;

                if (doorPrefab != null)

                {

#if UNITY_EDITOR

                    doorObj = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, doorsRoot);

#else

                    doorObj = Instantiate(doorPrefab, doorsRoot);

#endif

                }

                else

                {

                    doorObj = new GameObject(dDef.doorName);

                    doorObj.transform.SetParent(doorsRoot, false);

                    doorObj.AddComponent<TacticalShooter.Systems.Interaction.DoorInteractable>();

                }



                doorObj.name = dDef.doorName;

                doorObj.transform.position = dDef.position;

                doorObj.transform.rotation = dDef.rotation;

                doorObj.transform.localScale = Vector3.one; // Uniform root scale prevents matrix shearing on rotating child



                // Dimension the hinge and door leaf properly for 4m x 3m portal

                Transform hinge = doorObj.transform.Find("HingePivot");

                if (hinge != null)

                {

                    hinge.localPosition = new Vector3(-2.0f, 0f, 0f);

                    hinge.localRotation = Quaternion.identity;

                    hinge.localScale = Vector3.one;



                    Transform leaf = hinge.Find("DoorLeaf");

                    if (leaf != null)

                    {

                        leaf.localPosition = new Vector3(2.0f, 1.5f, 0f);

                        leaf.localRotation = Quaternion.identity;

                        leaf.localScale = new Vector3(4.0f, 3.0f, 0.14f);

                    }

                }



                // Ensure doors and all children are dynamic (never static) so they animate smoothly

                doorObj.isStatic = false;

#if UNITY_EDITOR

                UnityEditor.GameObjectUtility.SetStaticEditorFlags(doorObj, 0);

#endif

                foreach (var tr in doorObj.GetComponentsInChildren<Transform>(true))

                {

                    tr.gameObject.isStatic = false;

#if UNITY_EDITOR

                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(tr.gameObject, 0);

#endif

                }

            }

            Debug.Log($"[CampusGenerator] ✅ Strictly 3 security doors configured at terminal room entrances: G4 Stores, L2 Records, L2 Archive.");

        }



        // =========================================================================

        // 5d. SUPPLY PICKUPS (Ammo Crates & Trauma Medkits in Empty Rooms)

        // =========================================================================

        private void BuildSuppliesAndPickups(Transform suppliesRoot, CampusLayoutConfig cfg)

        {

            GameObject ammoModelPrefab = null;

            GameObject medkitModelPrefab = null;

            Material ammoMat = null;

            Material medkitMat = null;



#if UNITY_EDITOR

            ammoModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/AmmoBox_Custom.fbx");

            medkitModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Medkit_Custom.fbx");

            ammoMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_AmmoBox_Custom.mat");

            medkitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Medkit_Custom.mat");

#endif



            foreach (var pDef in cfg.supplyPickups)

            {

                GameObject existingPickup = GameObject.Find(pDef.pickupId);

                if (existingPickup != null)

                {

#if UNITY_EDITOR

                    DestroyImmediate(existingPickup);

#else

                    Destroy(existingPickup);

#endif

                }



                GameObject pickupObj = new GameObject(pDef.pickupId);

                pickupObj.transform.SetParent(suppliesRoot, false);

                pickupObj.transform.position = pDef.position;

                pickupObj.isStatic = false;



                GameObject modelObj = null;

                if (pDef.type == PickupType.Ammo)

                {

                    if (ammoModelPrefab != null)

                    {

#if UNITY_EDITOR

                        modelObj = (GameObject)PrefabUtility.InstantiatePrefab(ammoModelPrefab, pickupObj.transform);

#else

                        modelObj = Instantiate(ammoModelPrefab, pickupObj.transform);

#endif

                    }

                    else

                    {

                        modelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);

                        modelObj.transform.SetParent(pickupObj.transform, false);

                        modelObj.transform.localScale = new Vector3(0.34f, 0.22f, 0.18f);

                    }



                    if (modelObj != null)

                    {

                        modelObj.name = "Model_AmmoBox";

                        modelObj.transform.localPosition = Vector3.zero;

                        modelObj.transform.localRotation = Quaternion.identity;

                        modelObj.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);



                        if (ammoMat != null)

                        {

                            foreach (var r in modelObj.GetComponentsInChildren<Renderer>(true))

                            {

                                r.sharedMaterial = ammoMat;

                            }

                        }

                    }



                    pickupObj.AddComponent<TacticalShooter.Systems.Interaction.AmmoPickup>();

                    var col = pickupObj.AddComponent<BoxCollider>();

                    col.size = new Vector3(0.9f, 0.7f, 0.9f);

                    col.center = new Vector3(0f, 0.25f, 0f);

                    col.isTrigger = true;

                }

                else

                {

                    if (medkitModelPrefab != null)

                    {

#if UNITY_EDITOR

                        modelObj = (GameObject)PrefabUtility.InstantiatePrefab(medkitModelPrefab, pickupObj.transform);

#else

                        modelObj = Instantiate(medkitModelPrefab, pickupObj.transform);

#endif

                    }

                    else

                    {

                        modelObj = GameObject.CreatePrimitive(PrimitiveType.Cube);

                        modelObj.transform.SetParent(pickupObj.transform, false);

                        modelObj.transform.localScale = new Vector3(0.8f, 0.32f, 0.56f);

                    }



                    if (modelObj != null)

                    {

                        modelObj.name = "Model_Medkit";

                        modelObj.transform.localPosition = Vector3.zero;

                        modelObj.transform.localRotation = Quaternion.identity;

                        modelObj.transform.localScale = new Vector3(2.0f, 2.0f, 2.0f);



                        if (medkitMat != null)

                        {

                            foreach (var r in modelObj.GetComponentsInChildren<Renderer>(true))

                            {

                                r.sharedMaterial = medkitMat;

                            }

                        }

                    }



                    var hp = pickupObj.AddComponent<TacticalShooter.Systems.Interaction.HealthPickup>();

                    hp.SetHealAmount(pDef.amount);

                    var col = pickupObj.AddComponent<BoxCollider>();

                    col.size = new Vector3(0.9f, 0.7f, 0.9f);

                    col.center = new Vector3(0f, 0.25f, 0f);

                    col.isTrigger = true;

                }



                pickupObj.name = pDef.pickupId;

            }



            Debug.Log($"[CampusGenerator] ✅ Placed {cfg.supplyPickups.Count} supply pickups (Ammo Crates & Trauma Medkits) across empty rooms.");

        }



        // =========================================================================

        // 7. AI AGENTS SYNCHRONIZATION

        // =========================================================================

        public void DeployOrSyncAIAgents(CampusLayoutConfig cfg)

        {

            GameObject aiRoot = GameObject.Find("AI_Agents");

            if (aiRoot == null) aiRoot = new GameObject("AI_Agents");



            var activeNames = new System.Collections.Generic.HashSet<string>();

            foreach (var profile in cfg.coreAIDeployments) activeNames.Add(profile.agentName);



            // 1. Clean up ALL AIAgentBase objects anywhere in the scene that are not in activeNames

            var allAgents = Object.FindObjectsByType<TacticalShooter.AI.Core.AIAgentBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var ag in allAgents)

            {

                if (ag != null && !activeNames.Contains(ag.gameObject.name))

                {

                    Object.DestroyImmediate(ag.gameObject);

                }

            }



            // 2. Clean up any loose root objects matching enemy names or old prototype props

            Scene activeScene = gameObject.scene;

            if (activeScene.IsValid())

            {

                GameObject[] rootObjs = activeScene.GetRootGameObjects();

                foreach (var rootObj in rootObjs)

                {

                    if (rootObj == null) continue;

                    string n = rootObj.name;

                    if (n.StartsWith("TacticalSoldier_") || n.StartsWith("HunterAgent_") || n.StartsWith("FlankerAgent_") || 

                        n.StartsWith("SupportAgent_") || n.StartsWith("BossAgent_"))

                    {

                        if (!activeNames.Contains(n))

                        {

                            Object.DestroyImmediate(rootObj);

                        }

                    }

                    else if (n == "Door" || n == "Door (1)" || n == "Door (2)" || n == "MovableCrate")

                    {

                        Object.DestroyImmediate(rootObj);

                    }

                }

            }



            // 3. Clean up any remaining obsolete children under aiRoot

            var children = new System.Collections.Generic.List<GameObject>();

            foreach (Transform child in aiRoot.transform) children.Add(child.gameObject);

            foreach (var child in children)

            {

                if (!activeNames.Contains(child.name))

                {

                    Object.DestroyImmediate(child);

                }

            }



            foreach (var profile in cfg.coreAIDeployments)

            {

                GameObject existing = GameObject.Find(profile.agentName);

                if (existing != null)

                {

                    existing.transform.SetParent(aiRoot.transform, true);

                    existing.transform.position = profile.spawnPosition;

                    var soldier = existing.GetComponent<TacticalSoldierAgent>();

                    if (soldier != null)

                    {

                        soldier.ConfigureDeployment(profile.spawnPosition, profile.leashRadius, profile.patrolWaypoints);

                    }

                    var hunter = existing.GetComponent<HunterAgent>();

                    if (hunter != null)

                    {

                        hunter.ConfigureDeployment(profile.spawnPosition, profile.patrolWaypoints);

                    }

                    var flanker = existing.GetComponent<FlankerAgent>();

                    if (flanker != null)

                    {

                        var homeFld = typeof(FlankerAgent).GetField("homeBasePosition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (homeFld != null) homeFld.SetValue(flanker, profile.spawnPosition);

                        var waypointsFld = typeof(FlankerAgent).GetField("patrolWaypoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (waypointsFld != null) waypointsFld.SetValue(flanker, profile.patrolWaypoints);

                    }

                    var support = existing.GetComponent<SupportAgent>();

                    if (support != null)

                    {

                        support.ConfigureDeployment(profile.spawnPosition, profile.leashRadius, profile.patrolWaypoints);

                    }



                    var visuals = existing.GetComponent<RoyalGuardVisuals>();

                    if (visuals != null)

                    {

                        visuals.BuildVisualHierarchy();

                    }

                }

                else

                {

                    // Spawn according to role

                    GameObject spawnedAgent = null;

                    switch (profile.role)

                    {

                        case AgentRole.Guard:

                            var g = TacticalSoldierSpawner.SpawnTacticalSoldier(profile.agentName, profile.spawnPosition, profile.patrolWaypoints, profile.leashRadius);

                            if (g != null) spawnedAgent = g.gameObject;

                            break;

                        case AgentRole.Hunter:

                            var h = HunterSpawner.SpawnHunter(profile.agentName, profile.spawnPosition, profile.patrolWaypoints);

                            if (h != null) spawnedAgent = h.gameObject;

                            break;

                        case AgentRole.Flanker:

                            var f = FlankerSpawner.SpawnFlanker(profile.agentName, profile.spawnPosition, profile.patrolWaypoints);

                            if (f != null) spawnedAgent = f.gameObject;

                            break;

                        case AgentRole.Support:

                            var s = SupportSpawner.SpawnSupportAgent(profile.agentName, profile.spawnPosition, profile.patrolWaypoints, profile.leashRadius);

                            if (s != null) spawnedAgent = s.gameObject;

                            break;

                    }



                    if (spawnedAgent != null)

                    {

                        spawnedAgent.transform.SetParent(aiRoot.transform, true);

                    }

                }

            }

        }



        // =========================================================================

        // PRIMITIVE GENERATION HELPERS

        // =========================================================================

        private GameObject CreateFloorSlab(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)

        {

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);

            floor.name = name;

            floor.transform.SetParent(parent, false);

            floor.transform.position = pos;

            floor.transform.localScale = scale;

            if (mat != null) floor.GetComponent<Renderer>().sharedMaterial = mat;

            floor.isStatic = true;

            return floor;

        }



        private GameObject CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)

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



        private void BuildWallHorizontalWithPortal(Transform wallsRoot, Transform portalsRoot, float startX, float endX, float z, float floorY, float wallH, float wallThick, float portalMinX, float portalMaxX, string portalId)

        {

            float xMin = Mathf.Min(startX, endX);

            float xMax = Mathf.Max(startX, endX);



            // Segment 1 (Left of portal)

            if (portalMinX > xMin)

            {

                float len1 = portalMinX - xMin;

                float midX1 = xMin + (len1 * 0.5f);

                CreateWall(wallsRoot, $"Wall_{portalId}_SegL", new Vector3(midX1, floorY + wallH * 0.5f, z), new Vector3(len1, wallH, wallThick), wallMaterial);

            }



            // Segment 2 (Right of portal)

            if (xMax > portalMaxX)

            {

                float len2 = xMax - portalMaxX;

                float midX2 = portalMaxX + (len2 * 0.5f);

                CreateWall(wallsRoot, $"Wall_{portalId}_SegR", new Vector3(midX2, floorY + wallH * 0.5f, z), new Vector3(len2, wallH, wallThick), wallMaterial);

            }



            // Overhead lintel

            float portalWidth = portalMaxX - portalMinX;

            float lintelH = 0.5f;

            CreateWall(wallsRoot, $"Lintel_{portalId}", new Vector3((portalMinX + portalMaxX) * 0.5f, floorY + wallH - (lintelH * 0.5f), z), new Vector3(portalWidth + 0.1f, lintelH, wallThick + 0.05f), accentMaterial ?? wallMaterial);



            // Portal marker

            GameObject portalMarker = new GameObject($"Portal_{portalId}");

            portalMarker.transform.SetParent(portalsRoot, false);

            portalMarker.transform.position = new Vector3((portalMinX + portalMaxX) * 0.5f, floorY + 1.5f, z);

        }



        private void BuildWallVerticalWithPortal(Transform wallsRoot, Transform portalsRoot, float startZ, float endZ, float x, float floorY, float wallH, float wallThick, float portalMinZ, float portalMaxZ, string portalId)

        {

            float zMin = Mathf.Min(startZ, endZ);

            float zMax = Mathf.Max(startZ, endZ);



            // Segment 1 (South of portal)

            if (portalMinZ > zMin)

            {

                float len1 = portalMinZ - zMin;

                float midZ1 = zMin + (len1 * 0.5f);

                CreateWall(wallsRoot, $"Wall_{portalId}_SegS", new Vector3(x, floorY + wallH * 0.5f, midZ1), new Vector3(wallThick, wallH, len1), wallMaterial);

            }



            // Segment 2 (North of portal)

            if (zMax > portalMaxZ)

            {

                float len2 = zMax - portalMaxZ;

                float midZ2 = portalMaxZ + (len2 * 0.5f);

                CreateWall(wallsRoot, $"Wall_{portalId}_SegN", new Vector3(x, floorY + wallH * 0.5f, midZ2), new Vector3(wallThick, wallH, len2), wallMaterial);

            }



            // Overhead lintel

            float portalLen = portalMaxZ - portalMinZ;

            float lintelH = 0.5f;

            CreateWall(wallsRoot, $"Lintel_{portalId}", new Vector3(x, floorY + wallH - (lintelH * 0.5f), (portalMinZ + portalMaxZ) * 0.5f), new Vector3(wallThick + 0.05f, lintelH, portalLen + 0.1f), accentMaterial ?? wallMaterial);



            // Portal marker

            GameObject portalMarker = new GameObject($"Portal_{portalId}");

            portalMarker.transform.SetParent(portalsRoot, false);

            portalMarker.transform.position = new Vector3(x, floorY + 1.5f, (portalMinZ + portalMaxZ) * 0.5f);

        }



        private void BuildWallVerticalWithTwoPortals(Transform wallsRoot, Transform portalsRoot, float startZ, float endZ, float x, float floorY, float wallH, float wallThick, float p1MinZ, float p1MaxZ, string p1Id, float p2MinZ, float p2MaxZ, string p2Id)

        {

            // Segment 1: startZ to p1MinZ

            if (p1MinZ > startZ)

            {

                float len1 = p1MinZ - startZ;

                CreateWall(wallsRoot, $"Wall_{p1Id}_South", new Vector3(x, floorY + wallH * 0.5f, startZ + len1 * 0.5f), new Vector3(wallThick, wallH, len1), wallMaterial);

            }

            // Lintel 1

            CreateWall(wallsRoot, $"Lintel_{p1Id}", new Vector3(x, floorY + wallH - 0.25f, (p1MinZ + p1MaxZ) * 0.5f), new Vector3(wallThick + 0.05f, 0.5f, (p1MaxZ - p1MinZ) + 0.1f), accentMaterial ?? wallMaterial);



            // Segment 2: between p1MaxZ and p2MinZ

            if (p2MinZ > p1MaxZ)

            {

                float lenMid = p2MinZ - p1MaxZ;

                CreateWall(wallsRoot, $"Wall_{p1Id}_{p2Id}_Mid", new Vector3(x, floorY + wallH * 0.5f, p1MaxZ + lenMid * 0.5f), new Vector3(wallThick, wallH, lenMid), wallMaterial);

            }

            // Lintel 2

            CreateWall(wallsRoot, $"Lintel_{p2Id}", new Vector3(x, floorY + wallH - 0.25f, (p2MinZ + p2MaxZ) * 0.5f), new Vector3(wallThick + 0.05f, 0.5f, (p2MaxZ - p2MinZ) + 0.1f), accentMaterial ?? wallMaterial);



            // Segment 3: p2MaxZ to endZ

            if (endZ > p2MaxZ)

            {

                float len3 = endZ - p2MaxZ;

                CreateWall(wallsRoot, $"Wall_{p2Id}_North", new Vector3(x, floorY + wallH * 0.5f, p2MaxZ + len3 * 0.5f), new Vector3(wallThick, wallH, len3), wallMaterial);

            }

        }



        private void CreateCoverBlock(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)

        {

            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);

            cover.name = name;

            cover.transform.SetParent(parent, false);

            cover.transform.position = pos;

            cover.transform.localScale = scale;

            if (mat != null) cover.GetComponent<Renderer>().sharedMaterial = mat;

            cover.isStatic = true;

        }



        private void CreateDecalBar(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)

        {

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);

            bar.name = name;

            bar.transform.SetParent(parent, false);

            bar.transform.position = pos;

            bar.transform.localScale = scale;

            if (mat != null) bar.GetComponent<Renderer>().sharedMaterial = mat;

            Object.DestroyImmediate(bar.GetComponent<Collider>());

            bar.isStatic = true;

        }



        private void CreateServerRack(Transform parent, Vector3 pos, Vector3 scale, Material mat)

        {

            GameObject rack = GameObject.CreatePrimitive(PrimitiveType.Cube);

            rack.name = "Server_Rack_Mainframe";

            rack.transform.SetParent(parent, false);

            rack.transform.position = pos;

            rack.transform.localScale = scale;

            if (mat != null) rack.GetComponent<Renderer>().sharedMaterial = mat;

            rack.isStatic = true;

        }



        private void CreateLabBench(Transform parent, Vector3 pos, Vector3 scale, Material mat)

        {

            GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cube);

            bench.name = "Lab_Diagnostic_Bench";

            bench.transform.SetParent(parent, false);

            bench.transform.position = pos;

            bench.transform.localScale = scale;

            if (mat != null) bench.GetComponent<Renderer>().sharedMaterial = mat;

            bench.isStatic = true;

        }



        private void CreatePortalSign(Transform parent, string name, Vector3 pos, string text)

        {

            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);

            sign.name = name;

            sign.transform.SetParent(parent, false);

            sign.transform.position = pos;

            sign.transform.localScale = new Vector3(2.5f, 0.4f, 0.08f);

            if (accentMaterial != null) sign.GetComponent<Renderer>().sharedMaterial = accentMaterial;

            sign.isStatic = true;

        }



        private void CreateWatchtower(Transform parent, Vector3 pos, Material mat)

        {

            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cube);

            tower.name = "Perimeter_Watchtower";

            tower.transform.SetParent(parent, false);

            tower.transform.position = new Vector3(pos.x, 3.5f, pos.z);

            tower.transform.localScale = new Vector3(4.0f, 7.0f, 4.0f);

            if (mat != null) tower.GetComponent<Renderer>().sharedMaterial = mat;

            tower.isStatic = true;

        }



        private void CreateGuardhouse(Transform parent, Vector3 pos, Material mat)

        {

            GameObject house = GameObject.CreatePrimitive(PrimitiveType.Cube);

            house.name = "Vehicle_Gate_Guardhouse";

            house.transform.SetParent(parent, false);

            house.transform.position = new Vector3(pos.x, 1.75f, pos.z);

            house.transform.localScale = new Vector3(4.5f, 3.5f, 5.0f);

            if (mat != null) house.GetComponent<Renderer>().sharedMaterial = mat;

            house.isStatic = true;

        }



        private void CreateConcreteBarrier(Transform parent, Vector3 pos, Vector3 scale, Material mat)

        {

            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);

            barrier.name = "Jersey_Road_Barrier";

            barrier.transform.SetParent(parent, false);

            barrier.transform.position = pos;

            barrier.transform.localScale = scale;

            if (mat != null) barrier.GetComponent<Renderer>().sharedMaterial = mat;

            barrier.isStatic = true;

        }



        private void CreatePerimeterSecurityGate(Transform parent, Vector3 pos, float height, Material defaultMat, Material accentMat)

        {

            GameObject existingGate = GameObject.Find("Perimeter_Security_Gate_Fortified");

            if (existingGate != null)

            {

#if UNITY_EDITOR

                DestroyImmediate(existingGate);

#else

                Destroy(existingGate);

#endif

            }



            GameObject gateContainer = new GameObject("Perimeter_Security_Gate_Fortified");

            gateContainer.transform.SetParent(parent, false);

            gateContainer.transform.position = pos;

            gateContainer.isStatic = true;



            // 1. Solid impassable box collider across the 12m opening

            BoxCollider col = gateContainer.AddComponent<BoxCollider>();

            col.size = new Vector3(12.5f, height, 0.6f);

            col.center = new Vector3(0f, height * 0.5f, 0f);



            // 2. Load and instantiate custom 3D model

            GameObject modelPrefab = null;

            Material gateMat = null;

#if UNITY_EDITOR

            modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/PerimeterSecurityGate_Custom.fbx");

            gateMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_PerimeterSecurityGate_Custom.mat");

#endif

            if (modelPrefab != null)

            {

                GameObject modelObj;

#if UNITY_EDITOR

                if (!Application.isPlaying)

                {

                    modelObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(modelPrefab, gateContainer.transform);

                }

                else

                {

                    modelObj = Instantiate(modelPrefab, gateContainer.transform);

                }

#else

                modelObj = Instantiate(modelPrefab, gateContainer.transform);

#endif

                if (modelObj != null)

                {

                    modelObj.name = "Model_PerimeterSecurityGate";

                    modelObj.transform.localPosition = Vector3.zero;

                    modelObj.transform.localRotation = Quaternion.identity;

                    modelObj.transform.localScale = Vector3.one;



                    if (gateMat != null)

                    {

                        foreach (var rend in modelObj.GetComponentsInChildren<Renderer>(true))

                        {

                            rend.sharedMaterial = gateMat;

                        }

                    }

                    return;

                }

            }



            // Procedural fallback if model not loaded

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);

            frame.name = "Gate_Fallback_Frame";

            frame.transform.SetParent(gateContainer.transform, false);

            frame.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);

            frame.transform.localScale = new Vector3(12.0f, height, 0.4f);

            if (accentMat != null) frame.GetComponent<Renderer>().sharedMaterial = accentMat;

        }



        private void CreateFloodlightPole(Transform parent, Vector3 pos)

        {

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            pole.name = "Floodlight_Column";

            pole.transform.SetParent(parent, false);

            pole.transform.position = new Vector3(pos.x, 3.5f, pos.z);

            pole.transform.localScale = new Vector3(0.35f, 3.5f, 0.35f);

            if (wallMaterial != null) pole.GetComponent<Renderer>().sharedMaterial = wallMaterial;

            pole.isStatic = true;

        }



        private void CreateIndustrialGenerator(Transform parent, Vector3 pos, Material mat)

        {

            GameObject gen = GameObject.CreatePrimitive(PrimitiveType.Cube);

            gen.name = "Industrial_Power_Generator";

            gen.transform.SetParent(parent, false);

            gen.transform.position = pos;

            gen.transform.localScale = new Vector3(3.5f, 3.0f, 6.0f);

            if (mat != null) gen.GetComponent<Renderer>().sharedMaterial = mat;

            gen.isStatic = true;

        }



        private void CreateShippingContainers(Transform parent, Vector3 pos, Material mat)

        {

            GameObject container = GameObject.CreatePrimitive(PrimitiveType.Cube);

            container.name = "Freight_Shipping_Container";

            container.transform.SetParent(parent, false);

            container.transform.position = pos;

            container.transform.localScale = new Vector3(3.2f, 2.8f, 10.0f);

            if (mat != null) container.GetComponent<Renderer>().sharedMaterial = mat;

            container.isStatic = true;

        }

    }

}

