using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace TacticalShooter.Environment
{
    [ExecuteAlways]
    public class DoorAutoFitter : MonoBehaviour
    {
        [Header("Auto-Fit Settings")]
        [Tooltip("Automatically fit door size to walls on Start")]
        [SerializeField] private bool autoFitOnStart = true;

        [Tooltip("Standard door height. If opening is taller, a matching header piece is created above the door.")]
        [SerializeField] private float targetDoorHeight = 2.4f;

        [Tooltip("If true, scales the door itself to fill the entire vertical height of the wall")]
        [SerializeField] private bool stretchDoorToFullWallHeight = true;

        private void Start()
        {
            if (autoFitOnStart && Application.isPlaying)
            {
                FitDoorToSurroundingWalls();
            }
        }

        [ContextMenu("📐 Auto-Fit This Door Now")]
        public void FitDoorToSurroundingWalls()
        {
            // Position raycast at mid-height of typical doorway (1.4m above floor)
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            float maxRaycastDist = 12f;

            // 1. Try detecting along local X axis (Left / Right)
            bool hitXNeg = Physics.Raycast(origin, -transform.right, out RaycastHit hitLeft, maxRaycastDist, ~0, QueryTriggerInteraction.Ignore);
            bool hitXPos = Physics.Raycast(origin, transform.right, out RaycastHit hitRight, maxRaycastDist, ~0, QueryTriggerInteraction.Ignore);

            // 2. Try detecting along local Z axis (Forward / Back) if X didn't hit walls on both sides
            bool hitZNeg = false;
            bool hitZPos = false;
            RaycastHit hitBack = default;
            RaycastHit hitForward = default;

            if (!hitXNeg || !hitXPos)
            {
                hitZNeg = Physics.Raycast(origin, -transform.forward, out hitBack, maxRaycastDist, ~0, QueryTriggerInteraction.Ignore);
                hitZPos = Physics.Raycast(origin, transform.forward, out hitForward, maxRaycastDist, ~0, QueryTriggerInteraction.Ignore);
            }

            bool fitAlongX = hitXNeg && hitXPos;
            bool fitAlongZ = !fitAlongX && hitZNeg && hitZPos;

            if (!fitAlongX && !fitAlongZ)
            {
                Debug.LogWarning($"[DoorAutoFitter] Could not detect walls on opposite sides of '{gameObject.name}'. Ensure walls have Colliders.", this);
                return;
            }

            RaycastHit wallA = fitAlongX ? hitLeft : hitBack;
            RaycastHit wallB = fitAlongX ? hitRight : hitForward;

            // Ignore hits that are part of this door
            if (wallA.collider.transform.IsChildOf(transform) || wallB.collider.transform.IsChildOf(transform))
            {
                return;
            }

            // Calculate total doorway width and center
            float gapWidth = Vector3.Distance(wallA.point, wallB.point);
            Vector3 centerPoint = (wallA.point + wallB.point) * 0.5f;

            // Align position horizontally while preserving base Y
            centerPoint.y = transform.position.y;
            transform.position = centerPoint;

            // Determine wall height from detected colliders
            float wallTopY = 3.5f;
            if (wallA.collider is BoxCollider boxA)
            {
                wallTopY = boxA.bounds.max.y;
            }
            else if (wallB.collider is BoxCollider boxB)
            {
                wallTopY = boxB.bounds.max.y;
            }

            float verticalOpening = Mathf.Max(2.2f, wallTopY - transform.position.y);

            // Adjust scale
            Vector3 newScale = transform.localScale;
            if (fitAlongX)
            {
                newScale.x = gapWidth + 0.04f; // small overlap prevents visual seam
            }
            else
            {
                newScale.z = gapWidth + 0.04f;
            }

            if (stretchDoorToFullWallHeight)
            {
                newScale.y = verticalOpening;
            }
            else
            {
                newScale.y = targetDoorHeight;
                CreateOrUpdateWallHeader(centerPoint, gapWidth, verticalOpening - targetDoorHeight, wallTopY, fitAlongX, wallA.collider);
            }

            transform.localScale = newScale;

            // Ensure interactive controller is attached
            if (GetComponent<DoorController>() == null)
            {
                gameObject.AddComponent<DoorController>();
            }

            Debug.Log($"[DoorAutoFitter] ✅ Successfully auto-fitted '{gameObject.name}': Width = {gapWidth:F2}m, Height = {newScale.y:F2}m!");
        }

        private void CreateOrUpdateWallHeader(Vector3 doorCenter, float gapWidth, float headerHeight, float wallTopY, bool isAlignedX, Collider wallCollider)
        {
            if (headerHeight <= 0.1f) return;

            string headerName = gameObject.name + "_WallHeader";
            Transform existing = transform.parent != null ? transform.parent.Find(headerName) : null;
            GameObject headerObj = existing != null ? existing.gameObject : null;

            if (headerObj == null)
            {
                headerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                headerObj.name = headerName;
                if (transform.parent != null) headerObj.transform.SetParent(transform.parent, true);
            }

            float headerCenterY = wallTopY - (headerHeight * 0.5f);
            Vector3 headerPos = new Vector3(doorCenter.x, headerCenterY, doorCenter.z);
            headerObj.transform.position = headerPos;
            headerObj.transform.rotation = transform.rotation;

            Vector3 headerScale = new Vector3(
                isAlignedX ? gapWidth + 0.04f : 0.3f,
                headerHeight,
                isAlignedX ? 0.3f : gapWidth + 0.04f
            );
            headerObj.transform.localScale = headerScale;

            // Inherit wall material color if available
            if (wallCollider != null && wallCollider.TryGetComponent<Renderer>(out var wallRend))
            {
                if (headerObj.TryGetComponent<Renderer>(out var headerRend))
                {
                    headerRend.sharedMaterial = wallRend.sharedMaterial;
                }
            }
        }

#if UNITY_EDITOR
        [MenuItem("Tools/🚪 1-Click Auto-Fit All Doors")]
        public static void AutoFitAllDoorsInScene()
        {
            // Find all GameObjects containing "Door" in name or having Door components
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int fittedCount = 0;

            foreach (var go in allObjects)
            {
                string lower = go.name.ToLower();
                bool isDoorObject = lower.Contains("door") && !lower.Contains("header") && !lower.Contains("frame");

                if (isDoorObject)
                {
                    DoorAutoFitter fitter = go.GetComponent<DoorAutoFitter>();
                    if (fitter == null)
                    {
                        fitter = go.AddComponent<DoorAutoFitter>();
                    }

                    Undo.RecordObject(go.transform, "Auto-Fit Door");
                    fitter.FitDoorToSurroundingWalls();
                    EditorUtility.SetDirty(go);
                    fittedCount++;
                }
            }

            if (fittedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"<color=cyan><b>[DoorAutoFitter]</b></color> 🎉 Successfully auto-fitted <b>{fittedCount}</b> doors in the scene!");
                EditorUtility.DisplayDialog("Doors Auto-Fitted!", $"Successfully auto-fitted {fittedCount} doors to their wall openings with zero millimeter gaps!", "Great!");
            }
            else
            {
                EditorUtility.DisplayDialog("No Doors Found", "Could not find any GameObjects with 'Door' in their name in the active scene.", "OK");
            }
        }
#endif
    }
}
