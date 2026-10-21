using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.EditorTools
{
    public static class ConfigureSrikarsanCharacter
    {
        private const string TEXTURE_DIR = "Assets/_Project/Art/Textures";
        private const string MATERIAL_DIR = "Assets/_Project/Art/Materials";
        private const string SCENE_PATH = "Assets/_Project/Scenes/Main/Main.unity";
        private const string SCREENSHOT_PATH = "Assets/_Project/Art/SourceArt/Srikarsan_Game_Portrait.png";
        private const string GAMEPLAY_SCREENSHOT_PATH = "Assets/_Project/Art/SourceArt/Srikarsan_Game_ThirdPerson.png";

        [MenuItem("Tactical Shooter/Setup Srikarsan Character")]
        public static void SetupCharacterAndCapture()
        {
            Debug.Log("[ConfigureSrikarsanCharacter] Setting up Srikarsan materials and character...");

            // 1. Texture Importers
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Shirt_Albedo.png", isNormal: false, isLinear: false);
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Shirt_Normal.png", isNormal: true, isLinear: true);
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Shirt_MetallicSmoothness.png", isNormal: false, isLinear: true);

            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Face_Albedo.png", isNormal: false, isLinear: false);
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Face_Normal.png", isNormal: true, isLinear: true);
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Face_MetallicSmoothness.png", isNormal: false, isLinear: true);

            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Jeans_Albedo.png", isNormal: false, isLinear: false);
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Jeans_Normal.png", isNormal: true, isLinear: true);
            ConfigureTexture($"{TEXTURE_DIR}/T_Srikarsan_Jeans_MetallicSmoothness.png", isNormal: false, isLinear: true);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Create PBR Materials
            SetupPBRMat($"{MATERIAL_DIR}/M_Srikarsan_Shirt.mat",
                $"{TEXTURE_DIR}/T_Srikarsan_Shirt_Albedo.png",
                $"{TEXTURE_DIR}/T_Srikarsan_Shirt_Normal.png",
                $"{TEXTURE_DIR}/T_Srikarsan_Shirt_MetallicSmoothness.png",
                new Vector2(1f, 1f));

            SetupPBRMat($"{MATERIAL_DIR}/M_Srikarsan_Face.mat",
                $"{TEXTURE_DIR}/T_Srikarsan_Face_Albedo.png",
                $"{TEXTURE_DIR}/T_Srikarsan_Face_Normal.png",
                $"{TEXTURE_DIR}/T_Srikarsan_Face_MetallicSmoothness.png",
                new Vector2(1f, 1f));

            SetupPBRMat($"{MATERIAL_DIR}/M_Srikarsan_Jeans.mat",
                $"{TEXTURE_DIR}/T_Srikarsan_Jeans_Albedo.png",
                $"{TEXTURE_DIR}/T_Srikarsan_Jeans_Normal.png",
                $"{TEXTURE_DIR}/T_Srikarsan_Jeans_MetallicSmoothness.png",
                new Vector2(1f, 1f));

            SetupColorMat($"{MATERIAL_DIR}/M_Srikarsan_Skin.mat", new Color(0.70f, 0.52f, 0.40f), smoothness: 0.35f);
            SetupColorMat($"{MATERIAL_DIR}/M_Srikarsan_Hair.mat", new Color(0.08f, 0.08f, 0.09f), smoothness: 0.25f);
            SetupColorMat($"{MATERIAL_DIR}/M_Srikarsan_Shoes.mat", new Color(0.18f, 0.20f, 0.22f), smoothness: 0.40f);
            SetupColorMat($"{MATERIAL_DIR}/M_Srikarsan_Sole.mat", new Color(0.92f, 0.92f, 0.94f), smoothness: 0.30f);
            SetupColorMat($"{MATERIAL_DIR}/M_Srikarsan_Watch.mat", new Color(0.85f, 0.85f, 0.88f), metallic: 0.95f, smoothness: 0.85f);
            SetupColorMat($"{MATERIAL_DIR}/M_Srikarsan_Belt.mat", new Color(0.24f, 0.16f, 0.10f), smoothness: 0.45f);

            AssetDatabase.SaveAssets();

            // 3. Open Scene and Rebuild Player Character Visuals
            Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            
            // Delete the dummy target object from the scene
            GameObject dummy = GameObject.Find("TargetDummy_CombatTest");
            if (dummy != null)
            {
                Object.DestroyImmediate(dummy);
                Debug.Log("[ConfigureSrikarsanCharacter] Deleted TargetDummy_CombatTest from scene.");
            }

            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                PlayerCombatController combatCtrl = player.GetComponent<PlayerCombatController>();
                if (combatCtrl != null)
                {
                    SerializedObject so = new SerializedObject(combatCtrl);
                    so.FindProperty("startInThirdPerson").boolValue = true;
                    so.ApplyModifiedProperties();
                }

                PlayerCharacterVisuals visuals = player.GetComponent<PlayerCharacterVisuals>();
                if (visuals == null)
                {
                    visuals = player.AddComponent<PlayerCharacterVisuals>();
                }
                visuals.RebuildCharacterBody();

                // Hide First-Person WeaponViewModel during screenshots so it doesn't block the view
                WeaponViewModel vm = player.GetComponentInChildren<WeaponViewModel>(true);
                bool vmWasActive = false;
                if (vm != null)
                {
                    vmWasActive = vm.gameObject.activeSelf;
                    vm.gameObject.SetActive(false);
                }

                // 4. Capture Portrait Screenshot (front-facing camera looking at Srikarsan)
                Transform playerCamTransform = player.transform.Find("PlayerCamera");
                if (playerCamTransform == null)
                {
                    Camera c = player.GetComponentInChildren<Camera>();
                    if (c != null) playerCamTransform = c.transform;
                }

                Camera cam = playerCamTransform != null ? playerCamTransform.GetComponent<Camera>() : null;
                if (cam != null)
                {
                    // Portrait view: place camera in front of Srikarsan at chest/eye level looking at him
                    Vector3 origPos = playerCamTransform.localPosition;
                    Quaternion origRot = playerCamTransform.localRotation;

                    playerCamTransform.position = player.transform.position + new Vector3(0f, 1.35f, 1.6f);
                    playerCamTransform.LookAt(player.transform.position + new Vector3(0f, 1.25f, 0f));
                    CaptureCameraView(cam, SCREENSHOT_PATH, 960, 1280);

                    // Third-Person Over-The-Shoulder Gameplay View
                    playerCamTransform.position = player.transform.position + new Vector3(0.55f, 1.70f, -2.2f);
                    playerCamTransform.LookAt(player.transform.position + new Vector3(0.15f, 1.40f, 6.0f));
                    CaptureCameraView(cam, GAMEPLAY_SCREENSHOT_PATH, 1280, 720);

                    // Restore standard camera position
                    playerCamTransform.localPosition = origPos;
                    playerCamTransform.localRotation = origRot;
                }

                if (vm != null && vmWasActive)
                {
                    vm.gameObject.SetActive(true);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ConfigureSrikarsanCharacter] Successfully configured Srikarsan and captured screenshots!");
            }
        }

        private static void ConfigureTexture(string path, bool isNormal, bool isLinear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            bool dirty = false;

            if (isNormal)
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
            }
            else
            {
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    dirty = true;
                }
                if (importer.sRGBTexture == isLinear)
                {
                    importer.sRGBTexture = !isLinear;
                    dirty = true;
                }
            }

            if (dirty) importer.SaveAndReimport();
        }

        private static Material SetupPBRMat(string matPath, string alb, string nrm, string ms, Vector2 tiling)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = lit;
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);

            Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(alb);
            if (albedoTex != null)
            {
                mat.SetTexture("_BaseMap", albedoTex);
                mat.SetTexture("_MainTex", albedoTex);
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetTextureScale("_MainTex", tiling);
            }

            Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(nrm);
            if (normTex != null)
            {
                mat.SetTexture("_BumpMap", normTex);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.EnableKeyword("_NORMALMAP");
            }

            Texture2D msTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ms);
            if (msTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", msTex);
                mat.SetFloat("_Metallic", 1.0f);
                mat.SetFloat("_Smoothness", 1.0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SetupColorMat(string matPath, Color col, float metallic = 0f, float smoothness = 0.5f)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(lit);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = lit;
            }

            mat.SetColor("_BaseColor", col);
            mat.SetColor("_Color", col);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            EditorUtility.SetDirty(mat);
            return mat;
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

            Debug.Log($"[ConfigureSrikarsanCharacter] Saved screenshot to: {outputPath}");
        }
    }
}
