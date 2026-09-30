using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TacticalShooter.Environment
{
    [ExecuteInEditMode]
    public class FacilityGenerator : MonoBehaviour
    {
        [Header("Facility Dimensions")]
        [SerializeField] private float facilityWidth = 60f;
        [SerializeField] private float facilityDepth = 50f;
        [SerializeField] private float gridSize = 5f;
        [SerializeField] private float wallHeight = 3.5f;
        [SerializeField] private float wallThickness = 0.3f;
        [SerializeField] private float doorWidth = 2.5f;
        [SerializeField] private float coverHeight = 1.2f;

        [Header("Materials")]
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;

        [Header("Scene Object References")]
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private Transform playerSpawn;
        [SerializeField] private Transform objectivePoint;
        [SerializeField] private Transform extractionPoint;

        private const string GENERATED_CONTAINER_NAME = "Generated_Facility";

        public void GenerateFacility()
        {
            FindSceneReferences();
            ClearExistingGeneratedFacility();

            if (environmentRoot == null)
            {
                Debug.LogError("FacilityGenerator: Environment root transform could not be found!");
                return;
            }

            // Create container for generated geometry
            GameObject facilityContainer = new GameObject(GENERATED_CONTAINER_NAME);
            facilityContainer.transform.SetParent(environmentRoot, false);

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(facilityContainer, "Generate Tactical Facility");
#endif

            Transform floorsRoot = CreateSubContainer(facilityContainer.transform, "Floors");
            Transform wallsRoot = CreateSubContainer(facilityContainer.transform, "Walls");
            Transform doorwaysRoot = CreateSubContainer(facilityContainer.transform, "Doorways");
            Transform coverRoot = CreateSubContainer(facilityContainer.transform, "Cover_Obstacles");

            int cols = Mathf.RoundToInt(facilityWidth / gridSize);
            int rows = Mathf.RoundToInt(facilityDepth / gridSize);

            // 1. Generate Floor Slabs
            GenerateFloors(floorsRoot, cols, rows);

            // 2. Generate Facility Layout (Walls, Doorways, Cover)
            GenerateLayout(wallsRoot, doorwaysRoot, coverRoot, cols, rows);

            // 3. Reposition Target Markers
            RepositionMarkers();

            Debug.Log($"Tactical Facility successfully generated! ({facilityWidth}m x {facilityDepth}m greybox)");
        }

        public void ClearExistingGeneratedFacility()
        {
            FindSceneReferences();

            if (environmentRoot == null) return;

            List<GameObject> objectsToDestroy = new List<GameObject>();
            foreach (Transform child in environmentRoot)
            {
                if (child.name == GENERATED_CONTAINER_NAME)
                {
                    objectsToDestroy.Add(child.gameObject);
                }
            }

            foreach (GameObject obj in objectsToDestroy)
            {
#if UNITY_EDITOR
                if (Application.isPlaying)
                {
                    Destroy(obj);
                }
                else
                {
                    Undo.DestroyObjectImmediate(obj);
                }
#else
                Destroy(obj);
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

            if (objectivePoint == null)
            {
                GameObject objPoint = GameObject.Find("Objective Point");
                if (objPoint == null) objPoint = GameObject.Find("ObjectivePoint");
                if (objPoint != null) objectivePoint = objPoint.transform;
            }

            if (extractionPoint == null)
            {
                GameObject extObj = GameObject.Find("ExtractionPoint");
                if (extObj == null) extObj = GameObject.Find("Extraction Point");
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
#endif
        }

        private Transform CreateSubContainer(Transform parent, string name)
        {
            GameObject container = new GameObject(name);
            container.transform.SetParent(parent, false);
            return container.transform;
        }

        private void GenerateFloors(Transform parent, int cols, int rows)
        {
            float floorThickness = 0.2f;

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    Vector3 position = new Vector3(
                        (c + 0.5f) * gridSize,
                        -floorThickness * 0.5f,
                        (r + 0.5f) * gridSize
                    );

                    GameObject floorTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floorTile.name = $"Floor_{c}_{r}";
                    floorTile.transform.SetParent(parent, false);
                    floorTile.transform.position = position;
                    floorTile.transform.localScale = new Vector3(gridSize, floorThickness, gridSize);

                    if (floorMaterial != null)
                    {
                        floorTile.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                    }
                    floorTile.isStatic = true;
                }
            }
        }

        private void GenerateLayout(Transform wallsRoot, Transform doorwaysRoot, Transform coverRoot, int cols, int rows)
        {
            // Define room zones on grid:
            // Cols 0..11, Rows 0..9
            // South Spawn: Col 5..6, Row 0..1
            // Main Central Corridor: Col 5..6, Row 1..8
            // Side Room 1 (SW): Col 1..4, Row 1..2
            // Storage Room (MW): Col 1..4, Row 3..5
            // Security / Control Room (NW): Col 1..4, Row 6..8
            // West Flank Passage: Col 1..2, Row 5..6
            // Side Room 2 (SE): Col 7..10, Row 1..3
            // Laboratory (NE): Col 7..10, Row 6..8
            // East Flank Passage: Col 9..10, Row 3..6
            // North Extraction LZ: Col 5..6, Row 9

            // 1. Perimeter Walls (Boundary around 60m x 50m)
            BuildWallSegment(wallsRoot, new Vector3(facilityWidth * 0.5f, wallHeight * 0.5f, 0), new Vector3(facilityWidth, wallHeight, wallThickness)); // South
            BuildWallSegment(wallsRoot, new Vector3(facilityWidth * 0.5f, wallHeight * 0.5f, facilityDepth), new Vector3(facilityWidth, wallHeight, wallThickness)); // North
            BuildWallSegment(wallsRoot, new Vector3(0, wallHeight * 0.5f, facilityDepth * 0.5f), new Vector3(wallThickness, wallHeight, facilityDepth)); // West
            BuildWallSegment(wallsRoot, new Vector3(facilityWidth, wallHeight * 0.5f, facilityDepth * 0.5f), new Vector3(wallThickness, wallHeight, facilityDepth)); // East

            // 2. Interior Division Walls with Doorway Choke Points

            // Central Corridor West Wall (Col 5 boundary: x = 25m)
            BuildWallWithDoorway(wallsRoot, doorwaysRoot,
                start: new Vector3(25f, 0f, 5f),
                end: new Vector3(25f, 0f, 45f),
                doorCenterZ: 10f); // Doorway to Side Room 1

            BuildWallWithDoorway(wallsRoot, doorwaysRoot,
                start: new Vector3(25f, 0f, 15f),
                end: new Vector3(25f, 0f, 30f),
                doorCenterZ: 22.5f); // Doorway to Storage Room

            BuildWallWithDoorway(wallsRoot, doorwaysRoot,
                start: new Vector3(25f, 0f, 30f),
                end: new Vector3(25f, 0f, 45f),
                doorCenterZ: 37.5f); // Doorway to Security Room

            // Central Corridor East Wall (Col 7 boundary: x = 35m)
            BuildWallWithDoorway(wallsRoot, doorwaysRoot,
                start: new Vector3(35f, 0f, 5f),
                end: new Vector3(35f, 0f, 20f),
                doorCenterZ: 12.5f); // Doorway to Side Room 2

            BuildWallWithDoorway(wallsRoot, doorwaysRoot,
                start: new Vector3(35f, 0f, 30f),
                end: new Vector3(35f, 0f, 45f),
                doorCenterZ: 37.5f); // Doorway to Laboratory

            // Room Horizontal Partition Walls
            // Partition between Side Room 1 & Storage Room (z = 15m, x = 5m..25m)
            BuildWallWithDoorwayHorizontal(wallsRoot, doorwaysRoot,
                startX: 5f, endX: 25f, z: 15f, doorCenterX: 15f);

            // Partition between Storage Room & Security Room (z = 30m, x = 5m..25m)
            BuildWallWithDoorwayHorizontal(wallsRoot, doorwaysRoot,
                startX: 5f, endX: 25f, z: 30f, doorCenterX: 10f);

            // Partition between Side Room 2 & East Flank Corridor (z = 20m, x = 35m..55m)
            BuildWallWithDoorwayHorizontal(wallsRoot, doorwaysRoot,
                startX: 35f, endX: 55f, z: 20f, doorCenterX: 47.5f);

            // Partition between East Flank Corridor & Laboratory (z = 30m, x = 35m..55m)
            BuildWallWithDoorwayHorizontal(wallsRoot, doorwaysRoot,
                startX: 35f, endX: 55f, z: 30f, doorCenterX: 47.5f);

            // Entrance Choke Point (z = 5m, x = 25m..35m)
            BuildWallWithDoorwayHorizontal(wallsRoot, doorwaysRoot,
                startX: 25f, endX: 35f, z: 5f, doorCenterX: 30f);

            // Exit Choke Point (z = 45m, x = 25m..35m)
            BuildWallWithDoorwayHorizontal(wallsRoot, doorwaysRoot,
                startX: 25f, endX: 35f, z: 45f, doorCenterX: 30f);

            // 3. Cover Objects & Tactical Obstacles
            // Central Corridor Intersection Cover
            SpawnCoverBlock(coverRoot, new Vector3(28f, coverHeight * 0.5f, 25f), new Vector3(2.5f, coverHeight, 0.4f));
            SpawnCoverBlock(coverRoot, new Vector3(32f, coverHeight * 0.5f, 25f), new Vector3(2.5f, coverHeight, 0.4f));
            SpawnCrateStack(coverRoot, new Vector3(30f, 0.5f, 20f));

            // Storage Room Cover (Crates & Barriers)
            SpawnCrateStack(coverRoot, new Vector3(12f, 0.5f, 20f));
            SpawnCrateStack(coverRoot, new Vector3(18f, 0.5f, 25f));
            SpawnCoverBlock(coverRoot, new Vector3(15f, coverHeight * 0.5f, 22f), new Vector3(3f, coverHeight, 0.4f));

            // Laboratory Cover (Tech tables & low barriers around Objective)
            SpawnCoverBlock(coverRoot, new Vector3(42f, coverHeight * 0.5f, 35f), new Vector3(3f, coverHeight, 0.5f));
            SpawnCoverBlock(coverRoot, new Vector3(48f, coverHeight * 0.5f, 35f), new Vector3(3f, coverHeight, 0.5f));
            SpawnCrateStack(coverRoot, new Vector3(40f, 0.5f, 40f));

            // Security Room Cover
            SpawnCoverBlock(coverRoot, new Vector3(15f, coverHeight * 0.5f, 38f), new Vector3(3.5f, coverHeight, 0.5f));
            SpawnCrateStack(coverRoot, new Vector3(20f, 0.5f, 42f));

            // Side Rooms Cover
            SpawnCrateStack(coverRoot, new Vector3(12f, 0.5f, 10f));
            SpawnCrateStack(coverRoot, new Vector3(45f, 0.5f, 12f));
        }

        private void BuildWallSegment(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_Segment";
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.transform.localScale = scale;

            if (wallMaterial != null)
            {
                wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            }
            wall.isStatic = true;
        }

        private void BuildWallWithDoorway(Transform wallsRoot, Transform doorwaysRoot, Vector3 start, Vector3 end, float doorCenterZ)
        {
            float x = start.x;
            float zMin = Mathf.Min(start.z, end.z);
            float zMax = Mathf.Max(start.z, end.z);
            float doorMinZ = doorCenterZ - (doorWidth * 0.5f);
            float doorMaxZ = doorCenterZ + (doorWidth * 0.5f);

            // Wall Segment 1 (start to door)
            if (doorMinZ > zMin)
            {
                float len1 = doorMinZ - zMin;
                float center1 = zMin + (len1 * 0.5f);
                BuildWallSegment(wallsRoot, new Vector3(x, wallHeight * 0.5f, center1), new Vector3(wallThickness, wallHeight, len1));
            }

            // Wall Segment 2 (door to end)
            if (zMax > doorMaxZ)
            {
                float len2 = zMax - doorMaxZ;
                float center2 = doorMaxZ + (len2 * 0.5f);
                BuildWallSegment(wallsRoot, new Vector3(x, wallHeight * 0.5f, center2), new Vector3(wallThickness, wallHeight, len2));
            }

            // Doorway Frame Marker
            GameObject doorFrame = new GameObject($"Doorway_Chokepoint_z{doorCenterZ}");
            doorFrame.transform.SetParent(doorwaysRoot, false);
            doorFrame.transform.position = new Vector3(x, wallHeight * 0.5f, doorCenterZ);
        }

        private void BuildWallWithDoorwayHorizontal(Transform wallsRoot, Transform doorwaysRoot, float startX, float endX, float z, float doorCenterX)
        {
            float xMin = Mathf.Min(startX, endX);
            float xMax = Mathf.Max(startX, endX);
            float doorMinX = doorCenterX - (doorWidth * 0.5f);
            float doorMaxX = doorCenterX + (doorWidth * 0.5f);

            // Segment 1
            if (doorMinX > xMin)
            {
                float len1 = doorMinX - xMin;
                float center1 = xMin + (len1 * 0.5f);
                BuildWallSegment(wallsRoot, new Vector3(center1, wallHeight * 0.5f, z), new Vector3(len1, wallHeight, wallThickness));
            }

            // Segment 2
            if (xMax > doorMaxX)
            {
                float len2 = xMax - doorMaxX;
                float center2 = doorMaxX + (len2 * 0.5f);
                BuildWallSegment(wallsRoot, new Vector3(center2, wallHeight * 0.5f, z), new Vector3(len2, wallHeight, wallThickness));
            }

            // Doorway Frame Marker
            GameObject doorFrame = new GameObject($"Doorway_Chokepoint_x{doorCenterX}");
            doorFrame.transform.SetParent(doorwaysRoot, false);
            doorFrame.transform.position = new Vector3(doorCenterX, wallHeight * 0.5f, z);
        }

        private void SpawnCoverBlock(Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Cover_Barrier";
            cover.transform.SetParent(parent, false);
            cover.transform.position = position;
            cover.transform.localScale = scale;

            if (wallMaterial != null)
            {
                cover.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            }
            cover.isStatic = true;
        }

        private void SpawnCrateStack(Transform parent, Vector3 basePosition)
        {
            Vector3 crateSize = new Vector3(1f, 1f, 1f);

            GameObject crate1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate1.name = "Crate_Obstacle_1";
            crate1.transform.SetParent(parent, false);
            crate1.transform.position = basePosition + new Vector3(0, 0.5f, 0);
            crate1.transform.localScale = crateSize;
            if (wallMaterial != null) crate1.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            crate1.isStatic = true;

            GameObject crate2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate2.name = "Crate_Obstacle_2";
            crate2.transform.SetParent(parent, false);
            crate2.transform.position = basePosition + new Vector3(1.1f, 0.5f, 0);
            crate2.transform.localScale = crateSize;
            if (wallMaterial != null) crate2.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            crate2.isStatic = true;

            GameObject crate3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate3.name = "Crate_Obstacle_Top";
            crate3.transform.SetParent(parent, false);
            crate3.transform.position = basePosition + new Vector3(0.55f, 1.5f, 0);
            crate3.transform.localScale = crateSize;
            if (wallMaterial != null) crate3.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            crate3.isStatic = true;
        }

        private void RepositionMarkers()
        {
            // Direct repositioning without animation or smoothing
            if (playerSpawn != null)
            {
                // Set at South Entrance
                playerSpawn.position = new Vector3(30f, 0.5f, 2.5f);
                playerSpawn.rotation = Quaternion.identity;

                // Also align the main Player parent transform if attached
                if (playerSpawn.parent != null && playerSpawn.parent.name == "Player")
                {
                    playerSpawn.parent.position = new Vector3(30f, 1.0f, 2.5f);
                }
            }

            if (objectivePoint != null)
            {
                // Set deep in Laboratory room
                objectivePoint.position = new Vector3(45f, 0.5f, 37.5f);
            }

            if (extractionPoint != null)
            {
                // Set at North Extraction LZ
                extractionPoint.position = new Vector3(30f, 0.5f, 47.5f);
            }
        }
    }
}
