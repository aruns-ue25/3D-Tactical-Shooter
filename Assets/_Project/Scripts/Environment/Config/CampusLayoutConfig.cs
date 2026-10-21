using System;
using System.Collections.Generic;
using UnityEngine;
using TacticalShooter.AI.Core;

namespace TacticalShooter.Environment.Config
{
    [System.Serializable]
    public struct RoomDefinition
    {
        public string roomName;
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
        public float floorElevation;
        public float ceilingElevation;
        public string floorType; // "Tarmac", "Tile", "LabEpoxy", "ServerTile", "Helipad"

        public RoomDefinition(string name, float x0, float x1, float z0, float z1, float floorY, float ceilingY, string type = "Tile")
        {
            roomName = name;
            minX = x0;
            maxX = x1;
            minZ = z0;
            maxZ = z1;
            floorElevation = floorY;
            ceilingElevation = ceilingY;
            floorType = type;
        }

        public Vector3 Center => new Vector3((minX + maxX) * 0.5f, floorElevation, (minZ + maxZ) * 0.5f);
        public Vector3 Size => new Vector3(maxX - minX, ceilingElevation - floorElevation, maxZ - minZ);
    }

    public enum PortalOrientation
    {
        HorizontalX, // Wall parallel to X axis (portal along X at fixed Z)
        VerticalZ    // Wall parallel to Z axis (portal along Z at fixed X)
    }

    [System.Serializable]
    public struct PortalDefinition
    {
        public string portalId;
        public string fromRoom;
        public string toRoom;
        public PortalOrientation orientation;
        public float wallCoord; // fixed Z for HorizontalX, or fixed X for VerticalZ
        public float openingMin;
        public float openingMax;
        public float floorElevation;
        public float portalHeight;

        public PortalDefinition(string id, string from, string to, PortalOrientation orient, float fixedCoord, float openStart, float openEnd, float floorY, float height = 3.0f)
        {
            portalId = id;
            fromRoom = from;
            toRoom = to;
            orientation = orient;
            wallCoord = fixedCoord;
            openingMin = openStart;
            openingMax = openEnd;
            floorElevation = floorY;
            portalHeight = height;
        }

        public float Width => openingMax - openingMin;
        public Vector3 CenterPosition
        {
            get
            {
                if (orientation == PortalOrientation.HorizontalX)
                {
                    return new Vector3((openingMin + openingMax) * 0.5f, floorElevation + portalHeight * 0.5f, wallCoord);
                }
                else
                {
                    return new Vector3(wallCoord, floorElevation + portalHeight * 0.5f, (openingMin + openingMax) * 0.5f);
                }
            }
        }
    }

    [System.Serializable]
    public struct AIDeploymentProfile
    {
        public string agentName;
        public AgentRole role;
        public Vector3 spawnPosition;
        public float leashRadius;
        public Vector3[] patrolWaypoints;

        public AIDeploymentProfile(string name, AgentRole roleType, Vector3 spawn, float leash, Vector3[] waypoints)
        {
            agentName = name;
            role = roleType;
            spawnPosition = spawn;
            leashRadius = leash;
            patrolWaypoints = waypoints;
        }
    }

    public enum PickupType
    {
        Ammo,
        Health
    }

    [System.Serializable]
    public struct SupplyPickupDefinition
    {
        public string pickupId;
        public PickupType type;
        public string roomName;
        public Vector3 position;
        public int amount;

        public SupplyPickupDefinition(string id, PickupType pType, string room, Vector3 pos, int amt = 60)
        {
            pickupId = id;
            type = pType;
            roomName = room;
            position = pos;
            amount = amt;
        }
    }

    [System.Serializable]
    public struct DoorPlacementDefinition
    {
        public string doorName;
        public string portalId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public DoorPlacementDefinition(string name, string portal, Vector3 pos, Quaternion rot, Vector3 scl)
        {
            doorName = name;
            portalId = portal;
            position = pos;
            rotation = rot;
            scale = scl;
        }
    }

    [CreateAssetMenu(fileName = "CampusLayoutConfig", menuName = "Tactical Shooter/Campus Layout Config")]
    public class CampusLayoutConfig : ScriptableObject
    {
        [Header("Campus Boundaries")]
        public Rect campusBounds = new Rect(-35f, -35f, 130f, 120f); // X: -35..95, Z: -35..85
        public Rect buildingBounds = new Rect(0f, 0f, 70f, 60f);      // X: 0..70, Z: 0..60

        [Header("Floor Elevations")]
        public float groundY = 0.0f;
        public float level2Y = 3.6f;
        public float level3Y = 7.2f;
        public float mainRoofParapetY = 10.8f;
        public float wallHeight = 3.5f;
        public float wallThickness = 0.35f;

        [Header("Gameplay Anchors (Floor-Level Surface)")]
        public Vector3 playerStart = new Vector3(35.0f, 0.0f, -25.0f);
        public Vector3 objectiveTerminalAnchor = new Vector3(58.0f, 3.6f, 48.0f); // Level 2 Secure Archive (Legacy fallback)
        public Vector3 terminal1_StoresAnchor = new Vector3(13.0f, 0.0f, 15.0f); // Floor 1 Stores Room Terminal
        public Vector3 terminal2_RecordsAnchor = new Vector3(13.0f, 3.6f, 44.0f); // Level 2 Classified Records Terminal
        public Vector3 terminal3_ArchiveAnchor = new Vector3(58.0f, 3.6f, 48.0f); // Level 2 Secure Archive Terminal
        public Vector3 extractionHelipadAnchor = new Vector3(57.0f, 7.2f, 48.0f); // Level 3 Open-Air Helipad

        [Header("Stair Shaft Coordinates")]
        public Rect stairShaftBounds = new Rect(28.0f, 44.0f, 8.0f, 12.0f); // X: 28..36, Z: 44..56
        public float stairHalfLandingZ = 53.0f;
        public float stairRunLength = 7.0f; // 46.0 to 53.0
        public float stairFlightRise = 1.8f;
        public float stairFlightAngle = 14.42f; // arctan(1.8 / 7.0)
        public float stairFlightWidth = 4.0f; // Wall-to-wall flight width completely eliminating any drop-off gaps

        [Header("Room Layout Definitions")]
        public List<RoomDefinition> rooms = new List<RoomDefinition>();

        [Header("Walk-Through Portals")]
        public List<PortalDefinition> portals = new List<PortalDefinition>();

        [Header("Terminal Security Doors (Strictly 3 Doors in Facility)")]
        public List<DoorPlacementDefinition> securityDoors = new List<DoorPlacementDefinition>();

        [Header("Supply Pickups (Ammo Crates & Trauma Medkits)")]
        public List<SupplyPickupDefinition> supplyPickups = new List<SupplyPickupDefinition>();

        [Header("AI Core Deployments")]
        public List<AIDeploymentProfile> coreAIDeployments = new List<AIDeploymentProfile>();

        public void PopulateDefaultData()
        {
            rooms.Clear();
            portals.Clear();
            coreAIDeployments.Clear();

            // ==========================================
            // 1. ROOM DEFINITIONS (Three Levels)
            // ==========================================
            // Ground Floor (Y = 0.0m)
            rooms.Add(new RoomDefinition("Entry_Vestibule", 26f, 44f, 0f, 12f, groundY, level2Y, "Tile"));
            rooms.Add(new RoomDefinition("Security_Checkpoint", 26f, 44f, 12f, 22f, groundY, level2Y, "Tile"));
            rooms.Add(new RoomDefinition("Central_Service_Spine", 26f, 44f, 22f, 60f, groundY, level2Y, "Tile"));
            rooms.Add(new RoomDefinition("Ground_Stair_Core", 28f, 36f, 44f, 56f, groundY, level2Y, "Tile"));
            rooms.Add(new RoomDefinition("Stores_Equipment_Room", 0f, 26f, 0f, 30f, groundY, level2Y, "Tile"));
            rooms.Add(new RoomDefinition("Workshop_Maintenance", 0f, 26f, 30f, 60f, groundY, level2Y, "Tile"));
            rooms.Add(new RoomDefinition("Research_Laboratory", 44f, 70f, 0f, 36f, groundY, level2Y, "LabEpoxy"));
            rooms.Add(new RoomDefinition("Ground_Logistics_Bay", 44f, 70f, 36f, 60f, groundY, level2Y, "Tile"));

            // Level 2 (Y = 3.6m) - Objective Floor
            rooms.Add(new RoomDefinition("Level2_Stair_Landing", 28f, 36f, 44f, 56f, level2Y, level3Y, "Tile"));
            rooms.Add(new RoomDefinition("Level2_Central_Corridor", 26f, 44f, 18f, 44f, level2Y, level3Y, "Tile"));
            rooms.Add(new RoomDefinition("Level2_Secure_Archive", 44f, 70f, 28f, 60f, level2Y, level3Y, "ServerTile")); // Objective Terminal Room
            rooms.Add(new RoomDefinition("Level2_Data_Analysis_Hub", 44f, 70f, 0f, 28f, level2Y, level3Y, "LabEpoxy"));
            rooms.Add(new RoomDefinition("Level2_Classified_Records", 0f, 26f, 28f, 60f, level2Y, level3Y, "Tile"));
            rooms.Add(new RoomDefinition("Level2_Armoury_Prep", 0f, 26f, 0f, 28f, level2Y, level3Y, "Tile"));

            // Level 3 (Y = 7.2m) - Command & Helipad
            rooms.Add(new RoomDefinition("Level3_Stair_Landing", 28f, 36f, 44f, 56f, level3Y, mainRoofParapetY, "Tile"));
            rooms.Add(new RoomDefinition("Level3_Command_Corridor", 26f, 44f, 18f, 44f, level3Y, mainRoofParapetY, "Tile"));
            rooms.Add(new RoomDefinition("Level3_Mechanical_Penthouse", 26f, 44f, 10f, 18f, level3Y, mainRoofParapetY, "Tile"));
            rooms.Add(new RoomDefinition("Level3_Command_Deck", 0f, 26f, 32f, 60f, level3Y, mainRoofParapetY, "Tile"));
            rooms.Add(new RoomDefinition("Level3_Briefing_WarRoom", 0f, 26f, 14f, 32f, level3Y, mainRoofParapetY, "Tile"));
            rooms.Add(new RoomDefinition("Level3_Helipad_Terrace", 44f, 70f, 36f, 60f, level3Y, level3Y + 1.2f, "Helipad")); // Extraction Helipad

            // Outdoor Grounds
            rooms.Add(new RoomDefinition("Outdoor_Vehicle_Yard", -35f, 95f, -35f, 0f, groundY, groundY, "Tarmac"));
            rooms.Add(new RoomDefinition("Outdoor_Loading_Yard", 70f, 95f, 0f, 60f, groundY, groundY, "Tarmac"));
            rooms.Add(new RoomDefinition("Outdoor_Generator_Yard", -35f, 0f, 0f, 60f, groundY, groundY, "Tarmac"));
            rooms.Add(new RoomDefinition("Outdoor_Service_Yard", -35f, 95f, 60f, 85f, groundY, groundY, "Tarmac"));

            // ==========================================
            // 2. EXPLICIT WALK-THROUGH PORTALS (Continuous Solid Walls with Open Portals)
            // ==========================================
            // Ground Floor Portals
            portals.Add(new PortalDefinition("G1_Main_Entrance", "Outdoor_Vehicle_Yard", "Entry_Vestibule", PortalOrientation.HorizontalX, 0f, 32f, 38f, groundY, 3.2f));
            portals.Add(new PortalDefinition("G2_Vestibule_Security", "Entry_Vestibule", "Security_Checkpoint", PortalOrientation.HorizontalX, 12f, 29f, 33f, groundY, 3.0f));
            portals.Add(new PortalDefinition("G3_Security_Spine", "Security_Checkpoint", "Central_Service_Spine", PortalOrientation.HorizontalX, 22f, 37f, 41f, groundY, 3.0f));
            portals.Add(new PortalDefinition("G4_Spine_Stores", "Central_Service_Spine", "Stores_Equipment_Room", PortalOrientation.VerticalZ, 26f, 24f, 28f, groundY, 3.0f));
            portals.Add(new PortalDefinition("G5_Stores_Workshop", "Stores_Equipment_Room", "Workshop_Maintenance", PortalOrientation.HorizontalX, 30f, 10f, 14f, groundY, 3.0f));
            portals.Add(new PortalDefinition("G6_Spine_ResearchLab", "Central_Service_Spine", "Research_Laboratory", PortalOrientation.VerticalZ, 44f, 18f, 22f, groundY, 3.0f));
            portals.Add(new PortalDefinition("G7_Spine_StairCore", "Central_Service_Spine", "Ground_Stair_Core", PortalOrientation.HorizontalX, 44f, 30f, 34f, groundY, 3.0f));
            portals.Add(new PortalDefinition("G8_Logistics_LoadingYard", "Ground_Logistics_Bay", "Outdoor_Loading_Yard", PortalOrientation.VerticalZ, 70f, 46f, 50f, groundY, 3.2f));

            // Level 2 Portals
            portals.Add(new PortalDefinition("L2_1_Stair_Corridor", "Level2_Stair_Landing", "Level2_Central_Corridor", PortalOrientation.HorizontalX, 44f, 30f, 34f, level2Y, 3.0f));
            portals.Add(new PortalDefinition("L2_2_Corridor_Archive", "Level2_Central_Corridor", "Level2_Secure_Archive", PortalOrientation.VerticalZ, 44f, 38f, 42f, level2Y, 3.0f)); // To Objective
            portals.Add(new PortalDefinition("L2_3_Corridor_Analysis", "Level2_Central_Corridor", "Level2_Data_Analysis_Hub", PortalOrientation.VerticalZ, 44f, 20f, 24f, level2Y, 3.0f));
            portals.Add(new PortalDefinition("L2_4_Corridor_Records", "Level2_Central_Corridor", "Level2_Classified_Records", PortalOrientation.VerticalZ, 26f, 34f, 38f, level2Y, 3.0f));
            portals.Add(new PortalDefinition("L2_5_Corridor_Armoury", "Level2_Central_Corridor", "Level2_Armoury_Prep", PortalOrientation.VerticalZ, 26f, 18f, 22f, level2Y, 3.0f));

            // Level 3 Portals
            portals.Add(new PortalDefinition("L3_1_Stair_Corridor", "Level3_Stair_Landing", "Level3_Command_Corridor", PortalOrientation.HorizontalX, 44f, 30f, 34f, level3Y, 3.0f));
            portals.Add(new PortalDefinition("L3_2_Corridor_Command", "Level3_Command_Corridor", "Level3_Command_Deck", PortalOrientation.VerticalZ, 26f, 38f, 42f, level3Y, 3.0f));
            portals.Add(new PortalDefinition("L3_3_Corridor_Briefing", "Level3_Command_Corridor", "Level3_Briefing_WarRoom", PortalOrientation.VerticalZ, 26f, 20f, 24f, level3Y, 3.0f));
            portals.Add(new PortalDefinition("L3_4_Corridor_Mech", "Level3_Command_Corridor", "Level3_Mechanical_Penthouse", PortalOrientation.HorizontalX, 18f, 33f, 37f, level3Y, 3.0f));
            portals.Add(new PortalDefinition("L3_5_Corridor_Helipad", "Level3_Command_Corridor", "Level3_Helipad_Terrace", PortalOrientation.VerticalZ, 44f, 38f, 42f, level3Y, 3.0f)); // To Extraction

            // Outdoor Main Vehicle Gate
            portals.Add(new PortalDefinition("Gate_Vehicle_Entrance", "Exterior", "Outdoor_Vehicle_Yard", PortalOrientation.HorizontalX, -35f, 29f, 41f, groundY, 4.5f));

            // ==========================================
            // 2b. TERMINAL SECURITY DOORS (Strictly 3 Doors in Facility)
            // ==========================================
            securityDoors.Clear();
            // Door 1: G4_Spine_Stores (Floor 1 Stores Terminal, opening Z: 24..28, center Z = 26.0)
            securityDoors.Add(new DoorPlacementDefinition(
                "Door_G_Stores",
                "G4_Spine_Stores",
                new Vector3(26.0f, groundY, 26.0f),
                Quaternion.Euler(0f, 90f, 0f),
                Vector3.one
            ));

            // Door 2: L2_4_Corridor_Records (Level 2 Classified Records Terminal, opening Z: 34..38, center Z = 36.0)
            securityDoors.Add(new DoorPlacementDefinition(
                "Door_L2_Records",
                "L2_4_Corridor_Records",
                new Vector3(26.0f, level2Y, 36.0f),
                Quaternion.Euler(0f, 90f, 0f),
                Vector3.one
            ));

            // Door 3: L2_2_Corridor_Archive (Level 2 Secure Archive Terminal, opening Z: 38..42, center Z = 40.0)
            securityDoors.Add(new DoorPlacementDefinition(
                "Door_L2_Archive",
                "L2_2_Corridor_Archive",
                new Vector3(44.0f, level2Y, 40.0f),
                Quaternion.Euler(0f, 90f, 0f),
                Vector3.one
            ));

            // ==========================================
            // 2c. SUPPLY PICKUPS (Ammo Crates & Combat Medkits)
            // ==========================================
            supplyPickups.Clear();
            // Ground Floor (Empty non-objective rooms)
            // 1. Workshop Maintenance (Ammo Crate +30)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Ammo_Workshop", PickupType.Ammo, "Workshop_Maintenance", new Vector3(13.0f, groundY + 0.35f, 50.0f), 30));
            // 2. Workshop Maintenance (Combat Medkit +100 HP in ammo room)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Medkit_Workshop", PickupType.Health, "Workshop_Maintenance", new Vector3(8.0f, groundY + 0.35f, 44.0f), 100));
            // 3. Research Laboratory (Combat Medkit +100 HP)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Medkit_ResearchLab", PickupType.Health, "Research_Laboratory", new Vector3(57.0f, groundY + 0.35f, 24.0f), 100));
            // 4. Ground Logistics Bay (Ammo Crate +30)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Ammo_Logistics", PickupType.Ammo, "Ground_Logistics_Bay", new Vector3(54.0f, groundY + 0.35f, 48.0f), 30));
            // 5. Ground Logistics Bay (Combat Medkit +100 HP)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Medkit_Logistics", PickupType.Health, "Ground_Logistics_Bay", new Vector3(62.0f, groundY + 0.35f, 52.0f), 100));

            // Level 2 (Empty non-objective rooms)
            // 6. Armoury Prep (Ammo Crate 1 +30)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Ammo_Armoury_1", PickupType.Ammo, "Level2_Armoury_Prep", new Vector3(10.0f, level2Y + 0.35f, 10.0f), 30));
            // 7. Armoury Prep (Ammo Crate 2 +30)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Ammo_Armoury_2", PickupType.Ammo, "Level2_Armoury_Prep", new Vector3(18.0f, level2Y + 0.35f, 20.0f), 30));
            // 8. Armoury Prep (Combat Medkit +100 HP in ammo room)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Medkit_Armoury", PickupType.Health, "Level2_Armoury_Prep", new Vector3(14.0f, level2Y + 0.35f, 15.0f), 100));
            // 9. Data Analysis Hub (Combat Medkit +100 HP)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Medkit_AnalysisHub", PickupType.Health, "Level2_Data_Analysis_Hub", new Vector3(57.0f, level2Y + 0.35f, 20.0f), 100));

            // Level 3 (Empty non-objective rooms)
            // 10. Briefing War Room (Ammo Crate +30)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Ammo_WarRoom", PickupType.Ammo, "Level3_Briefing_WarRoom", new Vector3(13.0f, level3Y + 0.35f, 22.0f), 30));
            // 11. Command Deck (Combat Medkit +100 HP)
            supplyPickups.Add(new SupplyPickupDefinition("Pickup_Medkit_CommandDeck", PickupType.Health, "Level3_Command_Deck", new Vector3(13.0f, level3Y + 0.35f, 46.0f), 100));

            // ==========================================
            // 3. COMPLETE AI DEPLOYMENTS (Core Tier)
            // ==========================================
            // ==========================================
            // FULL FACILITY AI ROSTER (13 AGENTS TOTAL)
            // - Outdoor Player Spawn: 0 enemies (Clear & Safe spawn area)
            // - Ground Floor: 4 enemies (Stores terminal guarded by Flanker; Workshop & Research Lab by Guards; 1 Hunter)
            // - Level 2 (Objective Floor): 5 enemies (2 Terminal Guardian Bosses + 1 Hunter + 1 Flanker + 1 Guard)
            // - Level 3 (Helipad Floor): 4 enemies (Extraction Boss + 1 Hunter + 1 Flanker + 1 Guard)
            // - Exactly 3 Bosses total across entire facility (L2 Records Boss, L2 Archive Boss, L3 Helipad Boss)
            // ==========================================
            coreAIDeployments.Clear();

            // =========================================================================
            // GROUND FLOOR (Y = 0.0m) - 4 ENEMIES
            // =========================================================================
            // 1. Ground Spine Stalker (Hunter #1): Mobile tracker roaming ground spine & stair core
            coreAIDeployments.Add(new AIDeploymentProfile(
                "HunterAgent_GroundSpine",
                AgentRole.Hunter,
                new Vector3(38.0f, groundY, 32.0f),
                35.0f,
                new Vector3[] {
                    new Vector3(34.0f, groundY, 25.0f),
                    new Vector3(41.0f, groundY, 25.0f),
                    new Vector3(41.0f, groundY, 40.0f),
                    new Vector3(34.0f, groundY, 40.0f)
                }
            ));

            // 2. Stores & Equipment Ambusher (Flanker): Long-range marksman protecting F1 Terminal
            coreAIDeployments.Add(new AIDeploymentProfile(
                "FlankerAgent_Stores",
                AgentRole.Flanker,
                new Vector3(13.0f, groundY, 20.0f),
                25.0f,
                new Vector3[] {
                    new Vector3(8.0f, groundY, 12.0f),
                    new Vector3(18.0f, groundY, 12.0f),
                    new Vector3(18.0f, groundY, 22.0f),
                    new Vector3(8.0f, groundY, 22.0f)
                }
            ));

            // 3. Workshop Maintenance Sentinel (Guard): Short-range line defender in machinery bay
            coreAIDeployments.Add(new AIDeploymentProfile(
                "TacticalSoldier_Workshop",
                AgentRole.Guard,
                new Vector3(13.0f, groundY, 45.0f),
                30.0f,
                new Vector3[] {
                    new Vector3(8.0f, groundY, 38.0f),
                    new Vector3(18.0f, groundY, 38.0f),
                    new Vector3(18.0f, groundY, 52.0f),
                    new Vector3(8.0f, groundY, 52.0f)
                }
            ));

            // 4. Research Laboratory Sentinel (Guard): Short-range line defender in biotech lab
            coreAIDeployments.Add(new AIDeploymentProfile(
                "TacticalSoldier_ResearchLab",
                AgentRole.Guard,
                new Vector3(57.0f, groundY, 18.0f),
                30.0f,
                new Vector3[] {
                    new Vector3(50.0f, groundY, 10.0f),
                    new Vector3(64.0f, groundY, 10.0f),
                    new Vector3(64.0f, groundY, 26.0f),
                    new Vector3(50.0f, groundY, 26.0f)
                }
            ));

            // =========================================================================
            // LEVEL 2 (Y = 3.6m) - 5 ENEMIES (2 TERMINAL GUARDIAN BOSSES)
            // =========================================================================
            // 6. Classified Records Objective Guardian (Boss Agent #1 of 3): Circling F2 West Terminal
            coreAIDeployments.Add(new AIDeploymentProfile(
                "BossAgent_Records",
                AgentRole.Boss,
                new Vector3(17.2f, level2Y, 44.0f),
                7.5f,
                new Vector3[] {
                    new Vector3(17.2f, level2Y, 44.0f),
                    new Vector3(16.0f, level2Y, 47.0f),
                    new Vector3(13.0f, level2Y, 48.2f),
                    new Vector3(10.0f, level2Y, 47.0f),
                    new Vector3(8.8f, level2Y, 44.0f),
                    new Vector3(10.0f, level2Y, 41.0f),
                    new Vector3(13.0f, level2Y, 39.8f),
                    new Vector3(16.0f, level2Y, 41.0f)
                }
            ));

            // 7. Secure Archive Objective Guardian (Boss Agent #2 of 3): Circling F2 East Terminal
            coreAIDeployments.Add(new AIDeploymentProfile(
                "BossAgent_ArchiveGuardian",
                AgentRole.Boss,
                new Vector3(62.2f, level2Y, 48.0f),
                7.5f,
                new Vector3[] {
                    new Vector3(62.2f, level2Y, 48.0f),
                    new Vector3(61.0f, level2Y, 51.0f),
                    new Vector3(58.0f, level2Y, 52.2f),
                    new Vector3(55.0f, level2Y, 51.0f),
                    new Vector3(53.8f, level2Y, 48.0f),
                    new Vector3(55.0f, level2Y, 45.0f),
                    new Vector3(58.0f, level2Y, 43.8f),
                    new Vector3(61.0f, level2Y, 45.0f)
                }
            ));

            // 8. Level 2 Facility Prowler (Hunter #2): Mobile hunter stalking corridor & stairs
            coreAIDeployments.Add(new AIDeploymentProfile(
                "HunterAgent_L2Prowler",
                AgentRole.Hunter,
                new Vector3(35.0f, level2Y, 25.0f),
                35.0f,
                new Vector3[] {
                    new Vector3(30.0f, level2Y, 20.0f),
                    new Vector3(40.0f, level2Y, 20.0f),
                    new Vector3(40.0f, level2Y, 40.0f),
                    new Vector3(30.0f, level2Y, 40.0f)
                }
            ));

            // 9. Armoury Depot Marksman (Flanker): Long-range shooter around weapons cages
            coreAIDeployments.Add(new AIDeploymentProfile(
                "FlankerAgent_Armoury",
                AgentRole.Flanker,
                new Vector3(13.0f, level2Y, 15.0f),
                25.0f,
                new Vector3[] {
                    new Vector3(8.0f, level2Y, 8.0f),
                    new Vector3(18.0f, level2Y, 8.0f),
                    new Vector3(18.0f, level2Y, 22.0f),
                    new Vector3(8.0f, level2Y, 22.0f)
                }
            ));

            // 10. Data Analysis Hub Sentinel (Guard): Short-range defender at comms consoles
            coreAIDeployments.Add(new AIDeploymentProfile(
                "TacticalSoldier_AnalysisHub",
                AgentRole.Guard,
                new Vector3(57.0f, level2Y, 15.0f),
                30.0f,
                new Vector3[] {
                    new Vector3(50.0f, level2Y, 8.0f),
                    new Vector3(64.0f, level2Y, 8.0f),
                    new Vector3(64.0f, level2Y, 22.0f),
                    new Vector3(50.0f, level2Y, 22.0f)
                }
            ));

            // =========================================================================
            // LEVEL 3 (Y = 7.2m) - 4 ENEMIES (COMMAND & EXTRACTION DECK)
            // =========================================================================
            // 9. Helipad Extraction Boss (Boss Agent): Circling and defending Extraction Helipad
            coreAIDeployments.Add(new AIDeploymentProfile(
                "BossAgent_HelipadExtraction",
                AgentRole.Boss,
                new Vector3(62.5f, level3Y, 48.0f),
                8.0f,
                new Vector3[] {
                    new Vector3(62.5f, level3Y, 48.0f),
                    new Vector3(60.9f, level3Y, 51.9f),
                    new Vector3(57.0f, level3Y, 53.5f),
                    new Vector3(53.1f, level3Y, 51.9f),
                    new Vector3(51.5f, level3Y, 48.0f),
                    new Vector3(53.1f, level3Y, 44.1f),
                    new Vector3(57.0f, level3Y, 42.5f),
                    new Vector3(60.9f, level3Y, 44.1f)
                }
            ));

            // 10. Penthouse Apex Hunter (Hunter #3): Fast pursuer roving upper spine
            coreAIDeployments.Add(new AIDeploymentProfile(
                "HunterAgent_L3Apex",
                AgentRole.Hunter,
                new Vector3(35.0f, level3Y, 32.0f),
                35.0f,
                new Vector3[] {
                    new Vector3(30.0f, level3Y, 22.0f),
                    new Vector3(40.0f, level3Y, 22.0f),
                    new Vector3(40.0f, level3Y, 40.0f),
                    new Vector3(30.0f, level3Y, 40.0f)
                }
            ));

            // 11. Helipad Terrace Marksman (Flanker): Sniping from eastern landing pad edge
            coreAIDeployments.Add(new AIDeploymentProfile(
                "FlankerAgent_HelipadTerrace",
                AgentRole.Flanker,
                new Vector3(64.0f, level3Y, 42.0f),
                25.0f,
                new Vector3[] {
                    new Vector3(58.0f, level3Y, 38.0f),
                    new Vector3(66.0f, level3Y, 38.0f),
                    new Vector3(66.0f, level3Y, 50.0f),
                    new Vector3(58.0f, level3Y, 50.0f)
                }
            ));

            // 12. Command Deck Sentinel (Guard): Short-range defender at command consoles
            coreAIDeployments.Add(new AIDeploymentProfile(
                "TacticalSoldier_CommandDeck",
                AgentRole.Guard,
                new Vector3(13.0f, level3Y, 46.0f),
                25.0f,
                new Vector3[] {
                    new Vector3(8.0f, level3Y, 38.0f),
                    new Vector3(18.0f, level3Y, 38.0f),
                    new Vector3(18.0f, level3Y, 54.0f),
                    new Vector3(8.0f, level3Y, 54.0f)
                }
            ));
        }
    }
}
