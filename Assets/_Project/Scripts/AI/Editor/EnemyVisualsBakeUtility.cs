using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Core;

namespace TacticalShooter.AI.Editor
{
    public static class EnemyVisualsBakeUtility
    {
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";

        [MenuItem("Tactical Shooter/Bake All Enemy Visuals In Scene", false, 10)]
        public static void BakeAllEnemyVisuals()
        {
            Debug.Log("[EnemyVisualsBakeUtility] Starting Enemy Visuals Bake...");

            Scene currentScene = EditorSceneManager.GetActiveScene();
            if (currentScene.path != SCENE_PATH)
            {
                currentScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            RoyalGuardVisuals[] allVisuals = Object.FindObjectsByType<RoyalGuardVisuals>(FindObjectsSortMode.None);
            Debug.Log($"[EnemyVisualsBakeUtility] Found {allVisuals.Length} RoyalGuardVisuals components in scene.");

            int updated = 0;
            foreach (var vis in allVisuals)
            {
                if (vis == null) continue;
                GameObject go = vis.gameObject;

                AgentRole role = vis.ResolveAgentRole();

                // Update underlying agent component
                var sup = go.GetComponent<SupportAgent>();
                if (sup != null)
                {
                    var serializedObj = new SerializedObject(sup);
                    serializedObj.FindProperty("agentRole").intValue = (int)AgentRole.Support;
                    serializedObj.FindProperty("agentDisplayName").stringValue = "★ BOSS ★ Heavy Juggernaut";
                    if (serializedObj.FindProperty("burstCount") != null) serializedObj.FindProperty("burstCount").intValue = 14;
                    if (serializedObj.FindProperty("fireRate") != null) serializedObj.FindProperty("fireRate").floatValue = 0.085f;
                    if (serializedObj.FindProperty("damagePerShot") != null) serializedObj.FindProperty("damagePerShot").floatValue = 8.5f;
                    if (serializedObj.FindProperty("burstCooldown") != null) serializedObj.FindProperty("burstCooldown").floatValue = 1.8f;
                    if (serializedObj.FindProperty("anchorLeashRadius") != null) serializedObj.FindProperty("anchorLeashRadius").floatValue = 7.5f;
                    serializedObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(sup);
                }

                var hunt = go.GetComponent<HunterAgent>();
                if (hunt != null)
                {
                    var serializedObj = new SerializedObject(hunt);
                    serializedObj.FindProperty("agentRole").intValue = (int)AgentRole.Hunter;
                    serializedObj.FindProperty("agentDisplayName").stringValue = "Hunter (Apex Stalker)";
                    serializedObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(hunt);
                }

                var flank = go.GetComponent<FlankerAgent>();
                if (flank != null)
                {
                    var serializedObj = new SerializedObject(flank);
                    serializedObj.FindProperty("agentRole").intValue = (int)AgentRole.Flanker;
                    serializedObj.FindProperty("agentDisplayName").stringValue = "Flanker (Ghost Marksman)";
                    if (serializedObj.FindProperty("burstCount") != null) serializedObj.FindProperty("burstCount").intValue = 1;
                    if (serializedObj.FindProperty("burstCooldown") != null) serializedObj.FindProperty("burstCooldown").floatValue = 3.2f;
                    if (serializedObj.FindProperty("damagePerShot") != null) serializedObj.FindProperty("damagePerShot").floatValue = 45.0f;
                    serializedObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(flank);
                }

                var guard = go.GetComponent<TacticalSoldierAgent>();
                if (guard != null)
                {
                    var serializedObj = new SerializedObject(guard);
                    serializedObj.FindProperty("agentRole").intValue = (int)AgentRole.Guard;
                    serializedObj.FindProperty("agentDisplayName").stringValue = "Guard (Frontline Sentinel)";
                    serializedObj.ApplyModifiedProperties();
                    EditorUtility.SetDirty(guard);
                }

                vis.CleanOldVisuals();
                vis.BuildVisualHierarchy();
                EditorUtility.SetDirty(go);
                updated++;
                Debug.Log($"[EnemyVisualsBakeUtility] Upgraded '{go.name}' to Archetype: {role}.");
            }

            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[EnemyVisualsBakeUtility] Successfully baked all {updated} enemies in '{SCENE_PATH}'!");
        }
    }
}
