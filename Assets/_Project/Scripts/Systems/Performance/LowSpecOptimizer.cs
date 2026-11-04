using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TacticalShooter.Systems.Performance
{
    public class LowSpecOptimizer : MonoBehaviour
    {
        [Header("HUD Overlay")]
        [SerializeField] private bool showPerformanceHud = true;

        private float frameTimeAccumulator = 0f;
        private int frameCount = 0;
        private float currentFps = 60f;
        private float currentFrameTimeMs = 16.6f;
        private float lastFpsUpdate = 0f;
        private long totalAllocatedMemoryMB = 0;
        private long totalReservedMemoryMB = 0;
        private float lastMemoryUpdate = 0f;
        [Header("Frame Rate Limiter")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool enableVSync = true;

        [Header("Memory & Texture Optimization")]
        [SerializeField] private int textureMipmapLimit = 1;
        [SerializeField] private float shadowDistance = 28f;
        [SerializeField] private int shadowCascades = 2;

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
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowCascades = shadowCascades;

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                urpAsset.renderScale = targetRenderScale;
                urpAsset.shadowDistance = shadowDistance;
            }

            Time.fixedDeltaTime = 0.02f;
            Debug.Log($"[LowSpecOptimizer] Applied LowSpec Optimizations: FPS={targetFrameRate}, RenderScale={targetRenderScale}");
        }
    }

        private void Update()
        {
            frameTimeAccumulator += Time.unscaledDeltaTime;
            frameCount++;
            if (Time.unscaledTime - lastFpsUpdate >= 0.5f)
            {
                currentFps = frameCount / frameTimeAccumulator;
                currentFrameTimeMs = (frameTimeAccumulator / frameCount) * 1000f;
                frameTimeAccumulator = 0f;
                frameCount = 0;
                lastFpsUpdate = Time.unscaledTime;
            }

            if (Time.unscaledTime - lastMemoryUpdate >= 2f)
            {
                totalAllocatedMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                totalReservedMemoryMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
                lastMemoryUpdate = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (!showPerformanceHud) return;
            float boxWidth = 230f;
            float boxHeight = 78f;
            float boxX = Screen.width - boxWidth - 15f;
            float boxY = 15f;

            Color prevColor = GUI.color;
            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), Texture2D.whiteTexture);

            Color fpsColor = currentFps >= 50f ? new Color(0.2f, 1f, 0.4f) :
                            (currentFps >= 28f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f));
            GUI.color = fpsColor;
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, 2f), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = fpsColor;
            GUI.Label(new Rect(boxX + 8f, boxY + 4f, boxWidth - 16f, 18f), $"⚡ FPS: {currentFps:F0} ({currentFrameTimeMs:F1}ms) | {targetFrameRate} FPS", titleStyle);

            GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Normal };
            infoStyle.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            GUI.Label(new Rect(boxX + 8f, boxY + 24f, boxWidth - 16f, 16f), $"RAM: {totalAllocatedMemoryMB} MB / Heap {totalReservedMemoryMB} MB (8GB Mode)", infoStyle);
            GUI.Label(new Rect(boxX + 8f, boxY + 42f, boxWidth - 16f, 16f), $"Render Scale: {targetRenderScale:F2} | Shadows: {shadowCascades}x", infoStyle);
            GUI.color = prevColor;
        }
    }
}