using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TacticalShooter.Systems.Combat.Editor
{
    public static class CombatSetupUtility
    {
        private const string DummyName = "TargetDummy_CombatTest";

        [MenuItem("Tools/Combat/Setup Combat System in Scene", false, 10)]
        public static void SetupCombatSystem()
        {
            var scene = EditorSceneManager.GetActiveScene();
            Debug.Log($"[CombatSetupUtility] Setting up Complete Tactical Combat System in scene: '{scene.name}'...");

            // 1. Locate Player GameObject
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[CombatSetupUtility] Could not find GameObject named 'Player' in active scene!");
                return;
            }

            // Locate and tag Player Camera as MainCamera
            Transform playerCamera = player.transform.Find("PlayerCamera");
            if (playerCamera == null)
            {
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
            }

            if (playerCamera != null)
            {
                playerCamera.gameObject.tag = "MainCamera";
                playerCamera.localRotation = Quaternion.identity;
                playerCamera.localPosition = new Vector3(0f, 1.6f, 0f);
            }

            // 2. Attach Core Combat Components on Player
            HitscanWeapon weapon = player.GetComponent<HitscanWeapon>();
            if (weapon == null) weapon = Undo.AddComponent<HitscanWeapon>(player);

            PlayerCombatController combatController = player.GetComponent<PlayerCombatController>();
            if (combatController == null) combatController = Undo.AddComponent<PlayerCombatController>(player);

            PlayerCharacterVisuals charVisuals = player.GetComponent<PlayerCharacterVisuals>();
            if (charVisuals == null) charVisuals = Undo.AddComponent<PlayerCharacterVisuals>(player);

            TacticalMinimap minimap = player.GetComponent<TacticalMinimap>();
            if (minimap == null) minimap = Undo.AddComponent<TacticalMinimap>(player);

            // 3. Create or configure 1st-Person Weapon Viewmodel
            if (playerCamera != null)
            {
                Transform existingVm = playerCamera.Find("WeaponViewModel");
                if (existingVm != null)
                {
                    Undo.DestroyObjectImmediate(existingVm.gameObject);
                }

                GameObject vmRoot = new GameObject("WeaponViewModel");
                vmRoot.transform.SetParent(playerCamera, false);
                vmRoot.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);
                vmRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

                vmRoot.AddComponent<WeaponViewModel>();

                // Gun Parts
                GameObject receiver = CreatePrimitivePart(PrimitiveType.Cube, "Receiver", vmRoot.transform, new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.08f, 0.28f), new Color(0.18f, 0.2f, 0.22f));
                GameObject barrel = CreatePrimitivePart(PrimitiveType.Cylinder, "Barrel", vmRoot.transform, new Vector3(0f, 0.015f, 0.22f), new Vector3(0.025f, 0.09f, 0.025f), new Color(0.1f, 0.1f, 0.1f), Quaternion.Euler(90f, 0f, 0f));
                GameObject mag = CreatePrimitivePart(PrimitiveType.Cube, "Magazine", vmRoot.transform, new Vector3(0f, -0.07f, 0.03f), new Vector3(0.04f, 0.1f, 0.06f), new Color(0.1f, 0.1f, 0.1f), Quaternion.Euler(15f, 0f, 0f));
                GameObject sight = CreatePrimitivePart(PrimitiveType.Cube, "Sight", vmRoot.transform, new Vector3(0f, 0.055f, 0.12f), new Vector3(0.02f, 0.03f, 0.03f), new Color(0.08f, 0.08f, 0.08f));

                // Muzzle Point
                GameObject muzzleObj = new GameObject("MuzzlePoint");
                muzzleObj.transform.SetParent(vmRoot.transform, false);
                muzzleObj.transform.localPosition = new Vector3(0f, 0.015f, 0.32f);
                weapon.MuzzlePoint = muzzleObj.transform;

                Undo.RegisterCreatedObjectUndo(vmRoot, "Create 1st Person ViewModel");
            }

            // 4. Create Full 3D Tactical Character Operative Body for Player (for 3rd Person View)
            Transform existingBody = player.transform.Find("CharacterBody");
            if (existingBody != null)
            {
                Undo.DestroyObjectImmediate(existingBody.gameObject);
            }

            GameObject bodyRoot = new GameObject("CharacterBody");
            bodyRoot.transform.SetParent(player.transform, false);
            bodyRoot.transform.localPosition = Vector3.zero;
            bodyRoot.transform.localRotation = Quaternion.identity;

            Color militaryGreen = new Color(0.2f, 0.26f, 0.22f);
            Color darkTactical = new Color(0.12f, 0.14f, 0.16f);
            Color visorGold = new Color(0.2f, 0.6f, 0.9f);
            Color bootsBlack = new Color(0.08f, 0.08f, 0.08f);

            // Legs
            CreatePrimitivePart(PrimitiveType.Cylinder, "Leg_L", bodyRoot.transform, new Vector3(-0.13f, 0.42f, 0f), new Vector3(0.12f, 0.4f, 0.12f), militaryGreen);
            CreatePrimitivePart(PrimitiveType.Cylinder, "Leg_R", bodyRoot.transform, new Vector3(0.13f, 0.42f, 0f), new Vector3(0.12f, 0.4f, 0.12f), militaryGreen);
            CreatePrimitivePart(PrimitiveType.Cube, "Boot_L", bodyRoot.transform, new Vector3(-0.13f, 0.07f, 0.04f), new Vector3(0.13f, 0.14f, 0.22f), bootsBlack);
            CreatePrimitivePart(PrimitiveType.Cube, "Boot_R", bodyRoot.transform, new Vector3(0.13f, 0.07f, 0.04f), new Vector3(0.13f, 0.14f, 0.22f), bootsBlack);

            // Pelvis
            CreatePrimitivePart(PrimitiveType.Cube, "Pelvis", bodyRoot.transform, new Vector3(0f, 0.78f, 0f), new Vector3(0.38f, 0.16f, 0.24f), darkTactical);

            // Torso & Armor (Upper Body parent)
            GameObject torso = new GameObject("Torso");
            torso.transform.SetParent(bodyRoot.transform, false);
            torso.transform.localPosition = new Vector3(0f, 1.15f, 0f);

            CreatePrimitivePart(PrimitiveType.Cube, "Vest", torso.transform, Vector3.zero, new Vector3(0.42f, 0.44f, 0.26f), militaryGreen);
            CreatePrimitivePart(PrimitiveType.Cube, "PlateCarrier", torso.transform, new Vector3(0f, 0.02f, 0.08f), new Vector3(0.34f, 0.36f, 0.15f), darkTactical);

            // Head & Helmet
            GameObject head = CreatePrimitivePart(PrimitiveType.Sphere, "Head", torso.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.3f, 0.28f), militaryGreen);
            CreatePrimitivePart(PrimitiveType.Cube, "Visor", torso.transform, new Vector3(0f, 0.44f, 0.13f), new Vector3(0.2f, 0.07f, 0.08f), visorGold);

            // Arms holding weapon in 3rd person
            CreatePrimitivePart(PrimitiveType.Cylinder, "Arm_R", torso.transform, new Vector3(0.26f, 0.05f, 0.12f), new Vector3(0.09f, 0.22f, 0.09f), militaryGreen, Quaternion.Euler(55f, 10f, 0f));
            CreatePrimitivePart(PrimitiveType.Cylinder, "Arm_L", torso.transform, new Vector3(-0.24f, 0.04f, 0.22f), new Vector3(0.09f, 0.22f, 0.09f), militaryGreen, Quaternion.Euler(45f, -30f, 0f));

            // 3rd Person Tactical Rifle held by arms
            GameObject tpRifle = new GameObject("ThirdPersonRifle");
            tpRifle.transform.SetParent(torso.transform, false);
            tpRifle.transform.localPosition = new Vector3(0.12f, 0.02f, 0.38f);
            tpRifle.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            CreatePrimitivePart(PrimitiveType.Cube, "Rifle_Body", tpRifle.transform, Vector3.zero, new Vector3(0.06f, 0.09f, 0.36f), darkTactical);
            CreatePrimitivePart(PrimitiveType.Cylinder, "Rifle_Barrel", tpRifle.transform, new Vector3(0f, 0.015f, 0.28f), new Vector3(0.025f, 0.12f, 0.025f), bootsBlack, Quaternion.Euler(90f, 0f, 0f));
            CreatePrimitivePart(PrimitiveType.Cube, "Rifle_Mag", tpRifle.transform, new Vector3(0f, -0.08f, 0.05f), new Vector3(0.04f, 0.12f, 0.07f), bootsBlack, Quaternion.Euler(15f, 0f, 0f));

            // Start with body hidden (since starting in 1st person)
            bodyRoot.SetActive(false);
            Undo.RegisterCreatedObjectUndo(bodyRoot, "Create Tactical Character Body");

            // 5. Create or update TargetDummy_CombatTest (Centered in training room at 5m)
            GameObject dummy = GameObject.Find(DummyName);
            if (dummy == null)
            {
                dummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dummy.name = DummyName;
                dummy.transform.position = new Vector3(30f, 1f, 7.5f);
                dummy.transform.localScale = new Vector3(0.85f, 1.0f, 0.85f);

                Health health = dummy.AddComponent<Health>();
                TargetDummy dummyComponent = dummy.AddComponent<TargetDummy>();

                Renderer rend = dummy.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial.color = new Color(0.95f, 0.25f, 0.15f);

                Undo.RegisterCreatedObjectUndo(dummy, "Create Target Dummy");
                Debug.Log($"[CombatSetupUtility] Created '{DummyName}' at {dummy.transform.position}!");
            }
            else
            {
                dummy.transform.position = new Vector3(30f, 1f, 7.5f);
                dummy.transform.rotation = Quaternion.identity;
                if (dummy.GetComponent<Health>() == null) dummy.AddComponent<Health>();
                if (dummy.GetComponent<TargetDummy>() == null) dummy.AddComponent<TargetDummy>();
                dummy.GetComponent<TargetDummy>().ResetTarget();
            }

            // 6. Ensure Main scene in Build Settings
            EnsureMainSceneInBuildSettings();

            // 7. Save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[CombatSetupUtility] ✅ Complete Combat System setup and saved successfully!");
            EditorUtility.DisplayDialog("Combat System Setup", 
                "Tactical Combat System Ready!\n\n" +
                "• 3D Tactical Operative Model attached to Player\n" +
                "• 1st Person & 3rd Person camera switching (Press 'V')\n" +
                "• Tactical Minimap Radar in top-left\n" +
                "• 3D In-World Target Waypoint Marker\n" +
                "• Target Dummy centered at (30, 1, 7.5) with hit wobble & damage numbers\n" +
                "• Gunshot audio & glowing bullet tracers active\n" +
                "• Scene saved.\n\n" +
                "Press Play (▶️) to test!", "OK");
        }

        private static GameObject CreatePrimitivePart(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color, Quaternion? localRot = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (localRot.HasValue) part.transform.localRotation = localRot.Value;

            // Strip colliders from visual body parts so they don't block player controller or raycasts
            Collider col = part.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial.color = color;
            }

            return part;
        }

        [MenuItem("Tools/Combat/Remove Target Dummy", false, 11)]
        public static void RemoveTargetDummy()
        {
            GameObject dummy = GameObject.Find(DummyName);
            if (dummy != null)
            {
                Undo.DestroyObjectImmediate(dummy);
                Debug.Log($"[CombatSetupUtility] Removed '{DummyName}' from scene.");
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        [MenuItem("Tools/Combat/Ensure Main Scene In Build Settings", false, 12)]
        public static void EnsureMainSceneInBuildSettings()
        {
            const string mainScenePath = "Assets/_Project/Scenes/Main/Main.unity";
            var scenes = EditorBuildSettings.scenes;
            bool alreadyAdded = false;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == mainScenePath)
                {
                    alreadyAdded = true;
                    if (!scenes[i].enabled) scenes[i].enabled = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
                for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
                newScenes[scenes.Length] = new EditorBuildSettingsScene(mainScenePath, true);
                EditorBuildSettings.scenes = newScenes;
            }
        }
    }
}
