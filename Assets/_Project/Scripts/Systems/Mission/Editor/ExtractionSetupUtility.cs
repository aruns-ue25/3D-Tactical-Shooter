using UnityEditor;
using UnityEngine;

namespace TacticalShooter.Systems.Mission.Editor
{
    public static class ExtractionSetupUtility
    {
        [MenuItem("Tools/Setup Extraction System in Scene")]
        public static void SetupExtractionSystem()
        {
            // 1. Ensure MissionManager exists on Systems GameObject
            GameObject systemsObj = GameObject.Find("Systems");
            if (systemsObj == null)
            {
                systemsObj = new GameObject("Systems");
                Undo.RegisterCreatedObjectUndo(systemsObj, "Create Systems GameObject");
            }

            MissionManager missionMgr = systemsObj.GetComponent<MissionManager>();
            if (missionMgr == null)
            {
                missionMgr = systemsObj.AddComponent<MissionManager>();
                Undo.RegisterCreatedObjectUndo(missionMgr, "Add MissionManager to Systems");
            }

            // 2. Setup ExtractionZone under Environment/ExtractionPoint
            GameObject extPoint = GameObject.Find("ExtractionPoint");
            if (extPoint == null)
            {
                extPoint = GameObject.Find("Extraction Point");
            }

            if (extPoint != null)
            {
                Transform existingZone = extPoint.transform.Find("ExtractionZone");
                if (existingZone == null)
                {
                    GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    zone.name = "ExtractionZone";
                    zone.transform.SetParent(extPoint.transform, false);
                    zone.transform.localPosition = Vector3.zero;
                    zone.transform.localScale = new Vector3(2.5f, 0.1f, 2.5f);

                    zone.AddComponent<ExtractionInteractable>();

                    Undo.RegisterCreatedObjectUndo(zone, "Create Extraction Zone");
                    Debug.Log($"[ExtractionSetupUtility] Created ExtractionZone child under '{extPoint.name}' at local position Vector3.zero!");
                }
                else
                {
                    if (existingZone.GetComponent<ExtractionInteractable>() == null)
                    {
                        existingZone.gameObject.AddComponent<ExtractionInteractable>();
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ExtractionSetupUtility] Could not locate 'ExtractionPoint' in the active scene!");
            }

            Debug.Log("[ExtractionSetupUtility] Extraction System successfully setup in scene!");
        }
    }
}
