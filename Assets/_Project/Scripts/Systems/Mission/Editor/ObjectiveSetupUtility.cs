using UnityEditor;
using UnityEngine;

namespace TacticalShooter.Systems.Mission.Editor
{
    public static class ObjectiveSetupUtility
    {
        [MenuItem("Tools/Setup Objective System in Scene")]
        public static void SetupObjectiveSystem()
        {
            // 1. Attach MissionManager to Systems GameObject
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

            // 2. Setup Objective Terminal under Environment/Objective Point
            GameObject objPoint = GameObject.Find("Objective Point");
            if (objPoint == null)
            {
                objPoint = GameObject.Find("ObjectivePoint");
            }

            if (objPoint != null)
            {
                Transform existingTerminal = objPoint.transform.Find("ObjectiveTerminal");
                if (existingTerminal == null)
                {
                    GameObject terminal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    terminal.name = "ObjectiveTerminal";
                    terminal.transform.SetParent(objPoint.transform, false);
                    terminal.transform.localPosition = Vector3.zero;
                    terminal.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);

                    terminal.AddComponent<ObjectiveInteractable>();

                    Undo.RegisterCreatedObjectUndo(terminal, "Create Objective Terminal");
                    Debug.Log($"[ObjectiveSetupUtility] Created ObjectiveTerminal child under '{objPoint.name}' at local position Vector3.zero!");
                }
                else
                {
                    if (existingTerminal.GetComponent<ObjectiveInteractable>() == null)
                    {
                        existingTerminal.gameObject.AddComponent<ObjectiveInteractable>();
                    }
                }
            }
            else
            {
                Debug.LogWarning("[ObjectiveSetupUtility] Could not locate 'Objective Point' in the scene.");
            }

            Debug.Log("[ObjectiveSetupUtility] Mission Objective System successfully initialized in scene!");
        }
    }
}
