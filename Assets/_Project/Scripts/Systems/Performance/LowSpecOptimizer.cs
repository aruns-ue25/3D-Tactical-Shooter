using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TacticalShooter.Systems.Performance
{
    public class LowSpecOptimizer : MonoBehaviour
    {
        [Header("Frame Rate Limiter")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool enableVSync = true;

        [Header("Memory & Texture Optimization")]
        [SerializeField] private int textureMipmapLimit = 1;

        [Header("Render Scale")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float targetRenderScale = 0.85f;

        private static LowSpecOptimizer instance;
        public static LowSpecOptimizer Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[LowSpec_Performance_Manager]");
            instance = go.AddComponent<LowSpecOptimizer>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyLowSpecOptimizations();
        }

        public void ApplyLowSpecOptimizations()
        {
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = enableVSync ? 1 : 0;
            QualitySettings.globalTextureMipmapLimit = textureMipmapLimit;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                urpAsset.renderScale = targetRenderScale;
            }

            Time.fixedDeltaTime = 0.02f;
            Debug.Log($"[LowSpecOptimizer] Applied LowSpec Optimizations: FPS={targetFrameRate}, RenderScale={targetRenderScale}");
        }
    }
}
