#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TacticalShooter.EditorTools
{
    public static class ConfigurePickupsUtility
    {
        private const string TEXTURE_DIR = "Assets/_Project/Art/Textures";
        private const string MATERIAL_DIR = "Assets/_Project/Art/Materials";

        [MenuItem("Tactical Shooter/Configure Pickups & Materials")]
        public static void ConfigurePickups()
        {
            // 1. Texture Importers
            SetTextureSettings($"{TEXTURE_DIR}/T_AmmoBox_Albedo.png", isNormal: false, isLinear: false);
            SetTextureSettings($"{TEXTURE_DIR}/T_AmmoBox_Normal.png", isNormal: true, isLinear: true);
            SetTextureSettings($"{TEXTURE_DIR}/T_AmmoBox_MetallicSmoothness.png", isNormal: false, isLinear: true);
            SetTextureSettings($"{TEXTURE_DIR}/T_AmmoBox_Emission.png", isNormal: false, isLinear: false);

            SetTextureSettings($"{TEXTURE_DIR}/T_Medkit_Albedo.png", isNormal: false, isLinear: false);
            SetTextureSettings($"{TEXTURE_DIR}/T_Medkit_Normal.png", isNormal: true, isLinear: true);
            SetTextureSettings($"{TEXTURE_DIR}/T_Medkit_MetallicSmoothness.png", isNormal: false, isLinear: true);
            SetTextureSettings($"{TEXTURE_DIR}/T_Medkit_Emission.png", isNormal: false, isLinear: false);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 2. Ammo Box Material
            CreatePBRMaterial(
                $"{MATERIAL_DIR}/M_AmmoBox_Custom.mat",
                $"{TEXTURE_DIR}/T_AmmoBox_Albedo.png",
                $"{TEXTURE_DIR}/T_AmmoBox_Normal.png",
                $"{TEXTURE_DIR}/T_AmmoBox_MetallicSmoothness.png",
                $"{TEXTURE_DIR}/T_AmmoBox_Emission.png",
                new Color(1.0f, 0.7f, 0.1f)
            );

            // 3. Medkit Material
            CreatePBRMaterial(
                $"{MATERIAL_DIR}/M_Medkit_Custom.mat",
                $"{TEXTURE_DIR}/T_Medkit_Albedo.png",
                $"{TEXTURE_DIR}/T_Medkit_Normal.png",
                $"{TEXTURE_DIR}/T_Medkit_MetallicSmoothness.png",
                $"{TEXTURE_DIR}/T_Medkit_Emission.png",
                new Color(0.2f, 1.0f, 0.45f)
            );

            AssetDatabase.SaveAssets();
            Debug.Log("[ConfigurePickupsUtility] ✅ AmmoBox & Medkit materials configured successfully!");
        }

        private static void SetTextureSettings(string path, bool isNormal, bool isLinear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool modified = false;
            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                modified = true;
            }
            if (isLinear && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                modified = true;
            }
            if (importer.maxTextureSize != 1024)
            {
                importer.maxTextureSize = 1024;
                modified = true;
            }

            if (modified)
            {
                importer.SaveAndReimport();
            }
        }

        private static void CreatePBRMaterial(string matPath, string albPath, string normPath, string metPath, string emPath, Color emCol)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normPath);
            Texture2D metSmooth = AssetDatabase.LoadAssetAtPath<Texture2D>(metPath);
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(emPath);

            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metSmooth != null)
            {
                mat.SetTexture("_MetallicGlossMap", metSmooth);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.SetColor("_EmissionColor", emCol * 1.5f);
                mat.EnableKeyword("_EMISSION");
            }

            mat.SetFloat("_Smoothness", 0.65f);
            mat.SetFloat("_Metallic", 0.85f);
            EditorUtility.SetDirty(mat);
        }
    }
}
#endif
