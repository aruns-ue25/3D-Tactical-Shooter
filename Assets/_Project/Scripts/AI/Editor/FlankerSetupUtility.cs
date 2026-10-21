using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TacticalShooter.AI.Agents;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Editor
{
    public static class FlankerSetupUtility
    {
        private const string PrefabDirectory = "Assets/_Project/Prefabs/Agents";
        private const string PrefabPath = "Assets/_Project/Prefabs/Agents/FlankerAgent.prefab";

        [MenuItem("Tools/AI/Setup Flanker Agent in Scene", false, 3)]
        public static void SetupFlankerInScene()
        {
            const string mainScenePath = "Assets/_Project/Scenes/Main/Main.unity";
            var scene = EditorSceneManager.OpenScene(mainScenePath, OpenSceneMode.Single);
            Debug.Log($"[FlankerSetupUtility] Configuring Stealth / Flanker Agent in scene: {scene.name}...");

            GameObject aiRoot = GameObject.Find("AI_Agents");
            if (aiRoot == null)
            {
                aiRoot = new GameObject("AI_Agents");
                Undo.RegisterCreatedObjectUndo(aiRoot, "Create AI Agents Root");
            }

            FlankerAgent existing = Object.FindFirstObjectByType<FlankerAgent>();
            if (existing != null)
            {
                Debug.Log($"[FlankerSetupUtility] Flanker Agent already present at {existing.transform.position}.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Spawn at strategic side flank approach (East facility sector guarding entrance to Lab)
            Vector3 spawnPos = new Vector3(38f, 0.1f, 32f);
            Vector3[] waypoints = new Vector3[]
            {
                new Vector3(38f, 0.1f, 32f),
                new Vector3(44f, 0.1f, 32f),
                new Vector3(44f, 0.1f, 26f),
                new Vector3(38f, 0.1f, 26f)
            };

            FlankerAgent flanker = FlankerSpawner.SpawnFlanker(spawnPos, waypoints);
            flanker.transform.SetParent(aiRoot.transform, true);

            Undo.RegisterCreatedObjectUndo(flanker.gameObject, "Setup Flanker Agent");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Selection.activeGameObject = flanker.gameObject;
            Debug.Log("[FlankerSetupUtility] Flanker Agent successfully configured and selected in scene!");
        }

        [MenuItem("Tools/AI/Create Flanker Agent Prefab", false, 4)]
        public static void CreateFlankerPrefab()
        {
            if (!Directory.Exists(PrefabDirectory))
            {
                Directory.CreateDirectory(PrefabDirectory);
            }

            GameObject tempFlanker = new GameObject("FlankerAgent_Stealth");
            tempFlanker.transform.position = Vector3.zero;

            CharacterController cc = tempFlanker.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.radius = 0.38f;
            cc.stepOffset = 0.4f;

            tempFlanker.AddComponent<Health>();
            tempFlanker.AddComponent<AIPerception>();
            tempFlanker.AddComponent<AIMovement>();
            tempFlanker.AddComponent<FlankerAgent>();

            PrefabUtility.SaveAsPrefabAsset(tempFlanker, PrefabPath);
            Object.DestroyImmediate(tempFlanker);
            AssetDatabase.Refresh();

            Debug.Log($"[FlankerSetupUtility] Successfully generated prefab at: {PrefabPath}");
        }
    }
}
