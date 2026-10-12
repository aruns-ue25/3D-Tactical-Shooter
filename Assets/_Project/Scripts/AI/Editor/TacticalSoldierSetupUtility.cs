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
    public static class TacticalSoldierSetupUtility
    {
        private const string PrefabDirectory = "Assets/_Project/Prefabs/Agents";
        private const string PrefabPath = "Assets/_Project/Prefabs/Agents/TacticalSoldier.prefab";

        [MenuItem("Tools/AI/Setup Tactical Soldier (Guard) in Scene", false, 1)]
        public static void SetupSoldierInScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[TacticalSoldierSetupUtility] Configuring Tactical Soldier (Guard) in scene: {scene.name}...");

            // 1. Locate or create AI Agents parent
            GameObject aiRoot = GameObject.Find("AI_Agents");
            if (aiRoot == null)
            {
                aiRoot = new GameObject("AI_Agents");
                Undo.RegisterCreatedObjectUndo(aiRoot, "Create AI Agents Root");
            }

            // 2. Check if Tactical Soldier already exists in scene
            TacticalSoldierAgent existingSoldier = Object.FindFirstObjectByType<TacticalSoldierAgent>();
            if (existingSoldier != null)
            {
                Debug.Log($"[TacticalSoldierSetupUtility] Tactical Soldier already present at {existingSoldier.transform.position}.");
                Selection.activeGameObject = existingSoldier.gameObject;
                return;
            }

            // 3. Spawn at Corridor Intersection Choke Point (guarding approach to Lab)
            Vector3 spawnPos = new Vector3(30f, 0.1f, 25f);
            Vector3[] waypoints = new Vector3[]
            {
                new Vector3(30f, 0.1f, 24f),
                new Vector3(34f, 0.1f, 26f),
                new Vector3(34f, 0.1f, 34f),
                new Vector3(28f, 0.1f, 32f)
            };
            float leashRadius = 16f;

            TacticalSoldierAgent soldier = TacticalSoldierSpawner.SpawnTacticalSoldier(spawnPos, waypoints, leashRadius);
            soldier.transform.SetParent(aiRoot.transform, true);

            Undo.RegisterCreatedObjectUndo(soldier.gameObject, "Setup Tactical Soldier Guard");
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = soldier.gameObject;
            Debug.Log("[TacticalSoldierSetupUtility] Tactical Soldier successfully configured and selected!");
        }

        [MenuItem("Tools/AI/Create Tactical Soldier Prefab", false, 2)]
        public static void CreateTacticalSoldierPrefab()
        {
            if (!Directory.Exists(PrefabDirectory))
            {
                Directory.CreateDirectory(PrefabDirectory);
            }

            GameObject tempSoldier = new GameObject("TacticalSoldier_Guard");
            tempSoldier.transform.position = Vector3.zero;

            CharacterController cc = tempSoldier.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.radius = 0.38f;
            cc.stepOffset = 0.4f;

            tempSoldier.AddComponent<Health>();
            tempSoldier.AddComponent<AIPerception>();
            tempSoldier.AddComponent<AIMovement>();
            tempSoldier.AddComponent<TacticalSoldierAgent>();

            PrefabUtility.SaveAsPrefabAsset(tempSoldier, PrefabPath);
            Object.DestroyImmediate(tempSoldier);
            AssetDatabase.Refresh();

            Debug.Log($"[TacticalSoldierSetupUtility] Successfully generated prefab at: {PrefabPath}");
        }
    }
}
