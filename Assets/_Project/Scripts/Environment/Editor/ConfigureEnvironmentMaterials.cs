using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TacticalShooter.Environment;

namespace TacticalShooter.EditorTools
{
    public static class ConfigureEnvironmentMaterials
    {
        private const string TEXTURE_DIR = "Assets/_Project/Art/Textures";
        private const string MATERIAL_DIR = "Assets/_Project/Art/Materials";
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";
        private const string DOOR_PREFAB_PATH = "Assets/_Project/Prefabs/Interactables/Door.prefab";
        private const string SCREENSHOT_OVERVIEW_PATH = "Assets/_Project/Art/SourceArt/Environment_PBR_Screenshot.png";
        private const string SCREENSHOT_DOOR_PATH = "Assets/_Project/Art/SourceArt/Environment_Door_Screenshot.png";

        [MenuItem("Tactical Shooter/Apply Environment PBR Textures")]
        public static void ApplyTexturesAndMaterials()
        {
            Debug.Log("[ConfigureEnvironmentMaterials] Starting configuration of PBR environment materials...");

            // 1. Configure Texture Importer Settings
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Floor_Planks_Albedo.png", isNormal: false, isLinear: false);
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Floor_Planks_Normal.png", isNormal: true, isLinear: true);
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Floor_Planks_MetallicSmoothness.png", isNormal: false, isLinear: true);

            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Wall_Plaster_Albedo.png", isNormal: false, isLinear: false);
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Wall_Plaster_Normal.png", isNormal: true, isLinear: true);
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Wall_Plaster_MetallicSmoothness.png", isNormal: false, isLinear: true);

            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Door_Metal_Albedo.png", isNormal: false, isLinear: false);
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Door_Metal_Normal.png", isNormal: true, isLinear: true);
            ConfigureTextureImporter($"{TEXTURE_DIR}/T_Door_Metal_MetallicSmoothness.png", isNormal: false, isLinear: true);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Setup Floor Material (Seamless Wood Planks)
            Material floorMat = SetupPBRMaterial(
                $"{MATERIAL_DIR}/FloorMaterial.mat",
                albedoPath: $"{TEXTURE_DIR}/T_Floor_Planks_Albedo.png",
                normalPath: $"{TEXTURE_DIR}/T_Floor_Planks_Normal.png",
                metSmoothPath: $"{TEXTURE_DIR}/T_Floor_Planks_MetallicSmoothness.png",
                tiling: new Vector2(2.0f, 2.0f) // 2x2 tiling on 5m floor tiles gives natural plank width ~25cm
            );

            // 3. Setup Wall Material (Seamless Concrete Plaster)
            Material wallMat = SetupPBRMaterial(
                $"{MATERIAL_DIR}/WallMaterial.mat",
                albedoPath: $"{TEXTURE_DIR}/T_Wall_Plaster_Albedo.png",
                normalPath: $"{TEXTURE_DIR}/T_Wall_Plaster_Normal.png",
                metSmoothPath: $"{TEXTURE_DIR}/T_Wall_Plaster_MetallicSmoothness.png",
                tiling: new Vector2(1.0f, 1.0f)
            );

            // 4. Setup Door Material (Industrial Painted Steel)
            Material doorMat = SetupPBRMaterial(
                $"{MATERIAL_DIR}/DoorMaterial.mat",
                albedoPath: $"{TEXTURE_DIR}/T_Door_Metal_Albedo.png",
                normalPath: $"{TEXTURE_DIR}/T_Door_Metal_Normal.png",
                metSmoothPath: $"{TEXTURE_DIR}/T_Door_Metal_MetallicSmoothness.png",
                tiling: new Vector2(1.0f, 1.0f)
            );

            // 5. Assign Door Material to Door Prefab
            AssignMaterialToDoorPrefab(doorMat);

            // 6. Update Scene Objects (Main.unity)
            UpdateMainScene(floorMat, wallMat, doorMat);

            AssetDatabase.SaveAssets();
            Debug.Log("[ConfigureEnvironmentMaterials] All environment materials and scene objects successfully updated!");
        }

        [MenuItem("Tactical Shooter/Apply and Capture Environment Verification")]
        public static void ApplyAndCaptureEnvironmentVerification()
        {
            ApplyTexturesAndMaterials();

            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("[ConfigureEnvironmentMaterials] Player GameObject not found in Main scene!");
                return;
            }

            Transform playerCamTransform = player.transform.Find("PlayerCamera");
            if (playerCamTransform == null)
            {
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null) playerCamTransform = cam.transform;
            }

            Camera playerCamera = playerCamTransform != null ? playerCamTransform.GetComponent<Camera>() : null;
            if (playerCamera == null)
            {
                Debug.LogError("[ConfigureEnvironmentMaterials] Player Camera not found!");
                return;
            }

            // Capture Overview from spawn looking down the hallway with floor, walls, and guard
            player.transform.position = new Vector3(30f, 1.0f, 2.5f);
            playerCamTransform.localPosition = new Vector3(0f, 1.6f, 0f);
            playerCamTransform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            CaptureCameraView(playerCamera, SCREENSHOT_OVERVIEW_PATH, 1280, 720);

            // Capture close-up facing Door (2) at (15, 0, 15)
            player.transform.position = new Vector3(15f, 1.0f, 12.5f);
            playerCamTransform.position = new Vector3(15f, 1.6f, 12.5f);
            playerCamTransform.LookAt(new Vector3(15f, 1.6f, 15.0f));
            CaptureCameraView(playerCamera, SCREENSHOT_DOOR_PATH, 1280, 720);

            // Capture view in Laboratory room showing terminal, walls, and floor
            player.transform.position = new Vector3(41f, 1.0f, 35.5f);
            playerCamTransform.position = new Vector3(41f, 1.6f, 35.5f);
            playerCamTransform.LookAt(new Vector3(45f, 1.4f, 37.5f));
            CaptureCameraView(playerCamera, "Assets/_Project/Art/SourceArt/Environment_Lab_Screenshot.png", 1280, 720);

            // Restore player view
            player.transform.position = new Vector3(30f, 1.0f, 2.5f);
            playerCamTransform.localPosition = new Vector3(0f, 1.6f, 0f);
            playerCamTransform.localRotation = Quaternion.identity;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ConfigureEnvironmentMaterials] Verification screenshots successfully captured!");
        }

        private static void ConfigureTextureImporter(string assetPath, bool isNormal, bool isLinear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ConfigureEnvironmentMaterials] TextureImporter not found for {assetPath}");
                return;
            }

            bool changed = false;

            if (isNormal)
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
            }
            else
            {
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }
                if (importer.sRGBTexture == isLinear)
                {
                    importer.sRGBTexture = !isLinear;
                    changed = true;
                }
            }

            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"[ConfigureEnvironmentMaterials] Reimported {assetPath} (Normal: {isNormal}, Linear: {isLinear})");
            }
        }

        private static Material SetupPBRMaterial(string matPath, string albedoPath, string normalPath, string metSmoothPath, Vector2 tiling)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log($"[ConfigureEnvironmentMaterials] Created new material: {matPath}");
            }
            else
            {
                mat.shader = litShader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D metSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>(metSmoothPath);

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetTextureScale("_MainTex", tiling);
            }

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metSmooth != null)
            {
                mat.SetTexture("_MetallicGlossMap", metSmooth);
                mat.SetTextureScale("_MetallicGlossMap", tiling);
                mat.SetFloat("_Metallic", 1.0f);
                mat.SetFloat("_Smoothness", 1.0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void AssignMaterialToDoorPrefab(Material doorMat)
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(DOOR_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError($"[ConfigureEnvironmentMaterials] Could not load Door prefab at {DOOR_PREFAB_PATH}");
                return;
            }

            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in renderers)
            {
                mr.sharedMaterial = doorMat;
                Debug.Log($"[ConfigureEnvironmentMaterials] Assigned DoorMaterial to {mr.gameObject.name} in Door prefab");
            }

            PrefabUtility.SaveAsPrefabAsset(prefab, DOOR_PREFAB_PATH);
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        private static void UpdateMainScene(Material floorMat, Material wallMat, Material doorMat)
        {
            bool sceneOpened = false;
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != SCENE_PATH)
            {
                scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
                sceneOpened = true;
            }

            // 1. Update all floor tiles
            GameObject floorsRoot = GameObject.Find("Generated_Facility/Floors");
            if (floorsRoot != null)
            {
                MeshRenderer[] floorRenderers = floorsRoot.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in floorRenderers)
                {
                    mr.sharedMaterial = floorMat;
                }
                Debug.Log($"[ConfigureEnvironmentMaterials] Updated {floorRenderers.Length} floor tiles with FloorMaterial.");
            }

            // 2. Update and adjust UVs for all wall segments
            GameObject wallsRoot = GameObject.Find("Generated_Facility/Walls");
            if (wallsRoot != null)
            {
                MeshFilter[] wallFilters = wallsRoot.GetComponentsInChildren<MeshFilter>(true);
                int uvCount = 0;
                foreach (var mf in wallFilters)
                {
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = wallMat;

                    ApplyScaleProportionalUVs(mf, tileMeters: 3.0f);
                    uvCount++;
                }
                Debug.Log($"[ConfigureEnvironmentMaterials] Updated {uvCount} wall segments with WallMaterial and scale-proportional UVs.");
            }

            // 3. Update cover objects if any
            GameObject coverRoot = GameObject.Find("Generated_Facility/Cover_Obstacles");
            if (coverRoot != null)
            {
                MeshFilter[] coverFilters = coverRoot.GetComponentsInChildren<MeshFilter>(true);
                foreach (var cf in coverFilters)
                {
                    MeshRenderer mr = cf.GetComponent<MeshRenderer>();
                    if (mr != null) mr.sharedMaterial = wallMat;
                    ApplyScaleProportionalUVs(cf, tileMeters: 2.0f);
                }
                Debug.Log($"[ConfigureEnvironmentMaterials] Updated {coverFilters.Length} cover obstacles with WallMaterial.");
            }

            // 4. Update all door instances in scene
            DoorController[] doorControllers = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
            foreach (var dc in doorControllers)
            {
                MeshRenderer[] renderers = dc.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var mr in renderers)
                {
                    mr.sharedMaterial = doorMat;
                }
            }
            Debug.Log($"[ConfigureEnvironmentMaterials] Updated {doorControllers.Length} DoorController instances in scene.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void ApplyScaleProportionalUVs(MeshFilter filter, float tileMeters)
        {
            if (filter == null) return;
            Vector3 scale = filter.transform.lossyScale;

            Mesh originalMesh = filter.sharedMesh;
            if (originalMesh == null) return;

            Mesh newMesh = Object.Instantiate(originalMesh);
            newMesh.name = $"{filter.gameObject.name}_UVAdjusted";

            Vector3[] vertices = newMesh.vertices;
            Vector3[] normals = newMesh.normals;
            Vector2[] uvs = new Vector2[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals[i];
                Vector3 v = vertices[i];

                if (Mathf.Abs(n.z) > 0.5f)
                {
                    uvs[i] = new Vector2((v.x + 0.5f) * (scale.x / tileMeters), (v.y + 0.5f) * (scale.y / tileMeters));
                }
                else if (Mathf.Abs(n.x) > 0.5f)
                {
                    uvs[i] = new Vector2((v.z + 0.5f) * (scale.z / tileMeters), (v.y + 0.5f) * (scale.y / tileMeters));
                }
                else
                {
                    uvs[i] = new Vector2((v.x + 0.5f) * (scale.x / tileMeters), (v.z + 0.5f) * (scale.z / tileMeters));
                }
            }

            newMesh.uv = uvs;
            filter.sharedMesh = newMesh;
        }

        private static void CaptureCameraView(Camera cam, string outputPath, int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRt = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(outputPath, bytes);

            cam.targetTexture = prevRt;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            Debug.Log($"[ConfigureEnvironmentMaterials] Saved screenshot to: {outputPath}");
        }
    }
}
