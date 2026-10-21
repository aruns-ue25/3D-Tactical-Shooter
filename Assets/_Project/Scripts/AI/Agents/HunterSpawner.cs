using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.AI.Core;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Environment.Config;

namespace TacticalShooter.AI.Agents
{
    /// <summary>Creates the Main-scene Hunters while keeping test and sample scenes untouched.</summary>
    public static class HunterSpawner
    {
        private const string MainSceneName = "Main";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureAllHuntersInMainScene()
        {
            if (SceneManager.GetActiveScene().name != MainSceneName) return;

            CampusLayoutConfig cfg = null;
#if UNITY_EDITOR
            cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<CampusLayoutConfig>("Assets/_Project/Data/CampusLayoutConfig.asset");
#endif

            if (cfg != null && cfg.coreAIDeployments != null)
            {
                foreach (var profile in cfg.coreAIDeployments)
                {
                    if (profile.role == AgentRole.Hunter)
                    {
                        if (GameObject.Find(profile.agentName) == null)
                        {
                            SpawnHunter(profile.agentName, profile.spawnPosition, profile.patrolWaypoints);
                        }
                    }
                }
            }
        }

        public static HunterAgent SpawnHunter(string hunterName, Vector3 spawnPosition, Vector3[] patrolRoute)
        {
            GameObject existing = GameObject.Find(hunterName);
            if (existing != null)
            {
                return existing.GetComponent<HunterAgent>();
            }

            Vector3 spawn = spawnPosition;
            if (NavMesh.SamplePosition(spawn, out NavMeshHit sampled, 3f, NavMesh.AllAreas))
            {
                spawn = sampled.position;
            }

            GameObject hunterObject = new GameObject(hunterName);
            hunterObject.transform.SetPositionAndRotation(spawn, Quaternion.identity);

            CharacterController controller = hunterObject.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.center = new Vector3(0f, 0.95f, 0f);
            controller.radius = 0.38f;
            controller.stepOffset = 0.4f;

            Health health = hunterObject.AddComponent<Health>();
            health.SetMaxHealth(115f);
            hunterObject.AddComponent<AIPerception>();
            hunterObject.AddComponent<AIMovement>();

            HunterAgent hunter = hunterObject.AddComponent<HunterAgent>();
            hunter.ConfigureDeployment(spawn, patrolRoute);
            hunterObject.AddComponent<RoyalGuardVisuals>();

            GameObject agentsRoot = GameObject.Find("AI_Agents");
            if (agentsRoot != null)
            {
                hunterObject.transform.SetParent(agentsRoot.transform, true);
            }

            Debug.Log($"[HunterSpawner] Spawned persistent Hunter '{hunterName}' at {spawn}.");
            return hunter;
        }
    }
}
