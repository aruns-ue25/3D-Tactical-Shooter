using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TacticalShooter.Environment;
using TacticalShooter.Environment.Config;
using TacticalShooter.AI.Editor;

namespace TacticalShooter.EditorTools
{
    public static class CampusBuildPipeline
    {
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";
        private const string CONFIG_PATH = "Assets/_Project/Data/CampusLayoutConfig.asset";

        private const string SCREENSHOT_OVERVIEW = "Assets/_Project/Art/SourceArt/Campus_Overview_Stepped_3Level.png";
        private const string SCREENSHOT_ARCHIVE = "Assets/_Project/Art/SourceArt/Level2_SecureArchive_Objective.png";
        private const string SCREENSHOT_STAIRS = "Assets/_Project/Art/SourceArt/StairCore_Continuous_Switchback.png";
        private const string SCREENSHOT_HELIPAD = "Assets/_Project/Art/SourceArt/Level3_Helipad_Extraction.png";

        [MenuItem("Tactical Shooter/Run Master Campus Build & Verification", false, 0)]
        public static void RunMasterBuildAndVerification()
        {
            Debug.Log("[CampusBuildPipeline] ================= STARTING MASTER BUILD =================");

            // 1. Ensure ScriptableObject Asset exists on disk
            CampusLayoutConfig config = AssetDatabase.LoadAssetAtPath<CampusLayoutConfig>(CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CampusLayoutConfig>();
                config.PopulateDefaultData();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CampusBuildPipeline] Created CampusLayoutConfig ScriptableObject asset at {CONFIG_PATH}");
            }
            else
            {
                config.PopulateDefaultData();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }

            // 2. Open Main Scene
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[CampusBuildPipeline] Could not open scene {SCENE_PATH}!");
                return;
            }

            // 3. Find or Create CampusGenerator and Generate Campus
            GameObject envObj = GameObject.Find("Environment");
            if (envObj == null) envObj = new GameObject("Environment");

            CampusGenerator generator = envObj.GetComponent<CampusGenerator>() ?? envObj.AddComponent<CampusGenerator>();
            generator.Config = config;
            generator.GenerateCampus();

            // 3b. Bake & Align All Enemy Archetypes and Visuals
            EnemyVisualsBakeUtility.BakeAllEnemyVisuals();

            // 4. Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CampusBuildPipeline] Scene geometry generated and saved.");

            // 5. Bake NavMesh & Validate Reachability
            NavMeshBakeUtility.BakeMainSceneNavMesh();

            // 6. Capture High-Resolution Verification Screenshots
            CaptureAllVerificationScreenshots();

            Debug.Log("[CampusBuildPipeline] ================= MASTER BUILD COMPLETE =================");
        }

        private static void CaptureAllVerificationScreenshots()
        {
            GameObject camObj = new GameObject("TempVerificationCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;

            // 1. Overview looking from Vehicle Entrance gate toward 3-Level stepped facility
            camObj.transform.position = new Vector3(35f, 2.5f, -24f);
            camObj.transform.LookAt(new Vector3(35f, 5.0f, 20f));
            CaptureCamera(cam, SCREENSHOT_OVERVIEW, 1280, 720);

            // 2. Continuous Switchback Stair Core (from Ground base looking up)
            camObj.transform.position = new Vector3(32f, 1.2f, 41.5f);
            camObj.transform.LookAt(new Vector3(32f, 3.8f, 51f));
            CaptureCamera(cam, SCREENSHOT_STAIRS, 1280, 720);

            // 3. Level 2 Secure Archive with Objective Terminal & Mainframe Aisles
            camObj.transform.position = new Vector3(48f, 4.8f, 42f);
            camObj.transform.LookAt(new Vector3(58f, 4.2f, 48f));
            CaptureCamera(cam, SCREENSHOT_ARCHIVE, 1280, 720);

            // 4. Level 3 Open-Air Helipad with Extraction Zone
            camObj.transform.position = new Vector3(46f, 8.4f, 38f);
            camObj.transform.LookAt(new Vector3(57f, 7.8f, 48f));
            CaptureCamera(cam, SCREENSHOT_HELIPAD, 1280, 720);

            Object.DestroyImmediate(camObj);
        }

        private static void CaptureCamera(Camera cam, string outputPath, int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());

            cam.targetTexture = prev;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            Debug.Log($"[CampusBuildPipeline] Saved verification screenshot: {outputPath}");
        }
    }
}
