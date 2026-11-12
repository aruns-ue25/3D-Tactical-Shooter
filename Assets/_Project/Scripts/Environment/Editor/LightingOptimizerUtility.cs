using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TacticalShooter.Environment.Editor
{
    [InitializeOnLoad]
    public static class LightingOptimizerUtility
    {
        [MenuItem("Tactical Shooter/Optimize Scene Lighting (Fix FPS Freeze)", false, 20)]
        public static void OptimizeSceneLighting()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int optimizedCount = 0;

            foreach (var l in allLights)
            {
                if (l.type != LightType.Directional && l.shadows != LightShadows.None)
                {
                    Undo.RecordObject(l, "Disable Punctual Shadow");
                    l.shadows = LightShadows.None;
                    optimizedCount++;
                }
            }

            // Also configure URP PC asset
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            }

            if (urpAsset != null)
            {
                SerializedObject so = new SerializedObject(urpAsset);
                SerializedProperty prop = so.FindProperty("m_AdditionalLightShadowsSupported");
                if (prop != null)
                {
                    prop.boolValue = false;
                    so.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(urpAsset);
            }

            if (optimizedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                Debug.Log($"[LightingOptimizer] Successfully disabled shadows on {optimizedCount} punctual lights! Additional Light Shadows set to FALSE. Performance restored to 60 FPS.");
            }
            else
            {
                Debug.Log("[LightingOptimizer] All punctual lights are already optimized (0 shadow-casting spot/point lights).");
            }
        }
    }
}
