using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Systems.Mission;

namespace TacticalShooter.AssetPipeline.Editor
{
    public static class AssetIntegrationUtility
    {
        private const string RIFLE_FBX_PATH = "Assets/_Project/Art/Models/PlayerRifle_Custom.fbx";
        private const string TERMINAL_FBX_PATH = "Assets/_Project/Art/Models/ObjectiveTerminal_Custom.fbx";

        private const string RIFLE_MAT_PATH = "Assets/_Project/Art/Materials/M_PlayerRifle_Custom.mat";
        private const string TERMINAL_MAT_PATH = "Assets/_Project/Art/Materials/M_ObjectiveTerminal_Custom.mat";

        private const string RIFLE_PREFAB_PATH = "Assets/_Project/Prefabs/Weapons/PlayerRifle_Custom.prefab";
        private const string TERMINAL_PREFAB_PATH = "Assets/_Project/Prefabs/Environment/ObjectiveTerminal_Custom.prefab";

        [MenuItem("Tools/Assets/Setup Materials and Prefabs", false, 1)]
        public static void SetupMaterialsAndPrefabs()
        {
            Debug.Log("[AssetIntegrationUtility] Configuring Texture Import Settings...");
            ConfigureTextureImporter("Assets/_Project/Art/Textures/T_PlayerRifle_Normal.png", true);
            ConfigureTextureImporter("Assets/_Project/Art/Textures/T_ObjectiveTerminal_Normal.png", true);
            ConfigureTextureImporter("Assets/_Project/Art/Textures/T_PlayerRifle_MetallicSmoothness.png", false, false);
            ConfigureTextureImporter("Assets/_Project/Art/Textures/T_ObjectiveTerminal_MetallicSmoothness.png", false, false);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("[AssetIntegrationUtility] Creating URP Lit Materials...");
            CreateRifleMaterial();
            CreateTerminalMaterial();

            Debug.Log("[AssetIntegrationUtility] Configuring Model Importers...");
            ConfigureModelImporter(RIFLE_FBX_PATH);
            ConfigureModelImporter(TERMINAL_FBX_PATH);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("[AssetIntegrationUtility] Creating Prefabs...");
            CreateRiflePrefab();
            CreateTerminalPrefab();

            Debug.Log("[AssetIntegrationUtility] ✅ Materials and Prefabs successfully created!");
        }

        private static void ConfigureTextureImporter(string path, bool isNormalMap, bool sRGB = true)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (isNormalMap && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
                if (!sRGB && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    dirty = true;
                }
                if (dirty)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[AssetIntegrationUtility] Reimported texture at '{path}' (Normal={isNormalMap}, sRGB={sRGB})");
                }
            }
        }

        private static void ConfigureModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
        }

        private static void CreateRifleMaterial()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(RIFLE_MAT_PATH);
            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, RIFLE_MAT_PATH);
            }
            else
            {
                mat.shader = litShader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_PlayerRifle_Albedo.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_PlayerRifle_Normal.png");
            Texture2D metSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_PlayerRifle_MetallicSmoothness.png");
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_PlayerRifle_Emission.png");

            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metSmooth != null)
            {
                mat.SetTexture("_MetallicGlossMap", metSmooth);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1.0f);
            }
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", Color.white * 2.5f);
                mat.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        private static void CreateTerminalMaterial()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TERMINAL_MAT_PATH);
            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, TERMINAL_MAT_PATH);
            }
            else
            {
                mat.shader = litShader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_ObjectiveTerminal_Albedo.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_ObjectiveTerminal_Normal.png");
            Texture2D metSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_ObjectiveTerminal_MetallicSmoothness.png");
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/T_ObjectiveTerminal_Emission.png");

            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metSmooth != null)
            {
                mat.SetTexture("_MetallicGlossMap", metSmooth);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1.0f);
            }
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", Color.white * 2.5f);
                mat.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        private static void CreateRiflePrefab()
        {
            GameObject fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(RIFLE_FBX_PATH);
            if (fbxObj == null)
            {
                Debug.LogError($"[AssetIntegrationUtility] Could not load FBX at {RIFLE_FBX_PATH}");
                return;
            }

            GameObject instance = Object.Instantiate(fbxObj);
            instance.name = "PlayerRifle_Custom";

            Material rifleMat = AssetDatabase.LoadAssetAtPath<Material>(RIFLE_MAT_PATH);
            foreach (var rend in instance.GetComponentsInChildren<Renderer>())
            {
                rend.sharedMaterial = rifleMat;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, RIFLE_PREFAB_PATH);
            Object.DestroyImmediate(instance);
            Debug.Log($"[AssetIntegrationUtility] Created Weapon Prefab at {RIFLE_PREFAB_PATH}");
        }

        private static void CreateTerminalPrefab()
        {
            GameObject fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(TERMINAL_FBX_PATH);
            if (fbxObj == null)
            {
                Debug.LogError($"[AssetIntegrationUtility] Could not load FBX at {TERMINAL_FBX_PATH}");
                return;
            }

            GameObject instance = Object.Instantiate(fbxObj);
            instance.name = "ObjectiveTerminal_Custom";

            Material terminalMat = AssetDatabase.LoadAssetAtPath<Material>(TERMINAL_MAT_PATH);
            foreach (var rend in instance.GetComponentsInChildren<Renderer>())
            {
                rend.sharedMaterial = terminalMat;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, TERMINAL_PREFAB_PATH);
            Object.DestroyImmediate(instance);
            Debug.Log($"[AssetIntegrationUtility] Created Terminal Prefab at {TERMINAL_PREFAB_PATH}");
        }

        [MenuItem("Tools/Assets/Integrate Assets Into Main Scene", false, 2)]
        public static void IntegrateAssetsIntoScene()
        {
            SetupMaterialsAndPrefabs();

            const string mainScenePath = "Assets/_Project/Scenes/Main/Main.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(mainScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Debug.Log($"[AssetIntegrationUtility] Integrating Assets into scene '{scene.name}' ({scene.path})...");

            // 1. Integrate Objective Terminal Visuals
            GameObject terminalObj = GameObject.Find("ObjectiveTerminal");
            if (terminalObj != null)
            {
                // Preserve existing collider & ObjectiveInteractable
                ObjectiveInteractable interactable = terminalObj.GetComponent<ObjectiveInteractable>();

                // Disable or remove old primitive cylinder visual
                MeshFilter oldMf = terminalObj.GetComponent<MeshFilter>();
                MeshRenderer oldMr = terminalObj.GetComponent<MeshRenderer>();
                if (oldMr != null) oldMr.enabled = false;

                // Remove existing custom visual child if any
                Transform oldCustom = terminalObj.transform.Find("ObjectiveTerminal_Visual");
                if (oldCustom != null)
                {
                    Undo.DestroyObjectImmediate(oldCustom.gameObject);
                }

                // Instantiate Terminal Prefab as child
                GameObject terminalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TERMINAL_PREFAB_PATH);
                if (terminalPrefab != null)
                {
                    GameObject visualChild = PrefabUtility.InstantiatePrefab(terminalPrefab, terminalObj.transform) as GameObject;
                    visualChild.name = "ObjectiveTerminal_Visual";
                    visualChild.transform.localPosition = new Vector3(0f, -0.5f, 0f); // Rest base on floor
                    visualChild.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f); // Screen faces room entrance / player approach
                    visualChild.transform.localScale = Vector3.one;

                    // Strip any stray colliders on visual child so only gameplay CapsuleCollider on parent handles physics
                    foreach (var col in visualChild.GetComponentsInChildren<Collider>())
                    {
                        Object.DestroyImmediate(col);
                    }

                    // Assign terminalRenderer reference on ObjectiveInteractable to the visual renderer
                    if (interactable != null)
                    {
                        var visualRend = visualChild.GetComponentInChildren<Renderer>();
                        if (visualRend != null)
                        {
                            SerializedObject so = new SerializedObject(interactable);
                            so.FindProperty("terminalRenderer").objectReferenceValue = visualRend;
                            so.ApplyModifiedProperties();
                        }
                    }

                    Undo.RegisterCreatedObjectUndo(visualChild, "Integrate Objective Terminal Visual");
                    Debug.Log("[AssetIntegrationUtility] ✅ Integrated ObjectiveTerminal_Visual child under ObjectiveTerminal!");
                }
            }
            else
            {
                Debug.LogWarning("[AssetIntegrationUtility] Could not locate 'ObjectiveTerminal' in scene!");
            }

            // 2. Integrate Weapon Viewmodel Visuals
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                Transform playerCam = player.transform.Find("PlayerCamera");
                if (playerCam == null)
                {
                    Camera cam = player.GetComponentInChildren<Camera>();
                    if (cam != null) playerCam = cam.transform;
                }

                if (playerCam != null)
                {
                    Transform vmRoot = playerCam.Find("WeaponViewModel");
                    if (vmRoot == null)
                    {
                        GameObject vmObj = new GameObject("WeaponViewModel");
                        vmObj.transform.SetParent(playerCam, false);
                        vmObj.transform.localPosition = new Vector3(0.24f, -0.22f, 0.45f);
                        vmObj.transform.localRotation = Quaternion.identity;
                        vmRoot = vmObj.transform;
                        Undo.RegisterCreatedObjectUndo(vmObj, "Create WeaponViewModel Root");
                    }

                    WeaponViewModel vmComp = vmRoot.GetComponent<WeaponViewModel>();
                    if (vmComp == null) vmComp = Undo.AddComponent<WeaponViewModel>(vmRoot.gameObject);

                    // Clear old primitive children (Receiver, Barrel, Magazine, Sight)
                    for (int i = vmRoot.childCount - 1; i >= 0; i--)
                    {
                        Undo.DestroyObjectImmediate(vmRoot.GetChild(i).gameObject);
                    }

                    // Instantiate Rifle Prefab under WeaponViewModel
                    GameObject riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RIFLE_PREFAB_PATH);
                    if (riflePrefab != null)
                    {
                        GameObject visualRifle = PrefabUtility.InstantiatePrefab(riflePrefab, vmRoot) as GameObject;
                        visualRifle.name = "PlayerRifle_Visual";
                        visualRifle.transform.localPosition = new Vector3(0f, 0f, 0f);
                        visualRifle.transform.localRotation = Quaternion.identity;
                        visualRifle.transform.localScale = Vector3.one;

                        // Remove colliders from viewmodel visual
                        foreach (var col in visualRifle.GetComponentsInChildren<Collider>())
                        {
                            Object.DestroyImmediate(col);
                        }

                        // Locate or create MuzzlePoint
                        Transform muzzlePoint = visualRifle.transform.Find("MuzzlePoint");
                        if (muzzlePoint == null)
                        {
                            GameObject m = new GameObject("MuzzlePoint");
                            m.transform.SetParent(visualRifle.transform, false);
                            m.transform.localPosition = new Vector3(0f, 0.04f, 0.52f);
                            muzzlePoint = m.transform;
                        }

                        // Link MuzzlePoint to WeaponViewModel and HitscanWeapon
                        SerializedObject vmSo = new SerializedObject(vmComp);
                        vmSo.FindProperty("muzzlePoint").objectReferenceValue = muzzlePoint;
                        vmSo.ApplyModifiedProperties();

                        HitscanWeapon hitscan = player.GetComponent<HitscanWeapon>();
                        if (hitscan != null)
                        {
                            hitscan.MuzzlePoint = muzzlePoint;
                        }

                        // Link weaponViewModel to PlayerCombatController
                        PlayerCombatController combatCtrl = player.GetComponent<PlayerCombatController>();
                        if (combatCtrl != null)
                        {
                            SerializedObject ccSo = new SerializedObject(combatCtrl);
                            ccSo.FindProperty("weaponViewModel").objectReferenceValue = vmComp;
                            // Set startInThirdPerson to false (0) so player starts in first person view directly
                            ccSo.FindProperty("startInThirdPerson").boolValue = false;
                            ccSo.ApplyModifiedProperties();
                        }

                        Undo.RegisterCreatedObjectUndo(visualRifle, "Integrate PlayerRifle_Visual");
                        Debug.Log("[AssetIntegrationUtility] ✅ Integrated PlayerRifle_Visual child under WeaponViewModel!");
                    }
                }
            }
            else
            {
                Debug.LogWarning("[AssetIntegrationUtility] Could not locate 'Player' in scene!");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("[AssetIntegrationUtility] ✅ Scene successfully updated and saved!");
        }
    }
}
