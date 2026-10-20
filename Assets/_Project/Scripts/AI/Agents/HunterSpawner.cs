using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    /// <summary>Creates the Main-scene Hunters (Tracker and East Prowler), while keeping test and sample scenes untouched.</summary>
    public static class HunterSpawner
    {
        private const string MainSceneName = "Main";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureAllHuntersInMainScene()
        {
            if (SceneManager.GetActiveScene().name != MainSceneName) return;

            // 1. South Entrance Hunter (Tracker)
            if (GameObject.Find("HunterAgent_Tracker") == null)
            {
                SpawnHunter("HunterAgent_Tracker", new Vector3(30f, 0.1f, 8f), new Vector3[]
                {
                    new Vector3(27.5f, 0.1f, 8f),
                    new Vector3(27.5f, 0.1f, 11f),
                    new Vector3(32.5f, 0.1f, 11f),
                    new Vector3(32.5f, 0.1f, 8f)
                });
            }

            // 2. East Wing / Cargo Dock Hunter (East Prowler)
            if (GameObject.Find("HunterAgent_EastProwler") == null)
            {
                SpawnHunter("HunterAgent_EastProwler", new Vector3(46f, 0.1f, 22f), new Vector3[]
                {
                    new Vector3(44f, 0.1f, 18f),
                    new Vector3(44f, 0.1f, 26f),
                    new Vector3(48f, 0.1f, 26f),
                    new Vector3(48f, 0.1f, 18f)
                });
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
