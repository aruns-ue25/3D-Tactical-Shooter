using UnityEditor;
using UnityEngine;

namespace TacticalShooter.Environment
{
    [CustomEditor(typeof(FacilityGenerator))]
    public class FacilityGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FacilityGenerator generator = (FacilityGenerator)target;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Tactical Facility Level Generation", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("Generate Facility", GUILayout.Height(35)))
            {
                generator.GenerateFacility();
            }

            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.2f);
            if (GUILayout.Button("Clear Facility", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Clear Facility", "Are you sure you want to clear the generated facility geometry from the scene?", "Yes", "No"))
                {
                    generator.ClearExistingGeneratedFacility();
                }
            }

            GUI.backgroundColor = Color.white;
        }
    }
}
