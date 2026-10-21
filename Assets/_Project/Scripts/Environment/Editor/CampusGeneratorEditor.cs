using UnityEditor;
using UnityEngine;

namespace TacticalShooter.Environment
{
    [CustomEditor(typeof(CampusGenerator))]
    public class CampusGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CampusGenerator generator = (CampusGenerator)target;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Three-Level Research Campus Pipeline", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f);
            if (GUILayout.Button("🏛️ Generate Three-Level Campus", GUILayout.Height(38)))
            {
                generator.GenerateCampus();
            }

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.9f, 0.3f, 0.25f);
            if (GUILayout.Button("🗑️ Clear Generated Campus", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Clear Generated Campus", "Are you sure you want to remove Generated_Campus geometry?", "Yes", "No"))
                {
                    generator.ClearExistingGeneratedCampus();
                }
            }

            GUI.backgroundColor = Color.white;
        }

        [MenuItem("Tactical Shooter/Generate Three-Level Research Campus", false, 1)]
        public static void GenerateCampusMenu()
        {
            CampusGenerator generator = Object.FindFirstObjectByType<CampusGenerator>();
            if (generator == null)
            {
                GameObject env = GameObject.Find("Environment");
                if (env == null) env = new GameObject("Environment");
                generator = env.GetComponent<CampusGenerator>() ?? env.AddComponent<CampusGenerator>();
            }

            generator.GenerateCampus();
        }
    }
}
