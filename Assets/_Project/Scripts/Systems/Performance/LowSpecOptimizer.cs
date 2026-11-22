using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TacticalShooter.Systems.Performance
{
    public enum PerformanceHudPosition
    {
        BottomRight,
        BottomLeft,
        TopLeft,
        TopRight
    }

    /// <summary>
    /// LowSpecOptimizer - Automatic performance and memory optimizer for 8GB RAM laptops & low-spec GPUs.
    /// 
    /// Features:
    /// 1. Caps frame rate (default 60 FPS) to prevent 100% CPU/GPU thermal throttling and laptop fan noise.
    /// 2. Cuts texture memory usage in RAM/VRAM via globalTextureMipmapLimit.
    /// 3. Optimizes URP render scale and shadow cascade overhead.
    /// 4. Provides in-game HUD (F9 to toggle, F8 to reposition) and hotkey (F10) to switch between 60/45/30 FPS caps.
    /// </summary>
    public class LowSpecOptimizer : MonoBehaviour
    {
        [Header("Frame Rate Limiter")]
        [Tooltip("Target frame rate limit. Default is 60 FPS for smooth performance without overheating 8GB laptops.")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool enableVSync = true;

        [Header("Memory & Texture Optimization")]
        [Tooltip("Mipmap limit level: 1 halves texture resolution, saving ~75% VRAM and shared system RAM.")]
        [SerializeField] private int textureMipmapLimit = 1;
        [SerializeField] private float shadowDistance = 28f;
        [SerializeField] private int shadowCascades = 2;

        [Header("Render Scale")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float targetRenderScale = 0.85f;

        [Header("HUD Overlay")]
        [SerializeField] private bool showPerformanceHud = true;
        [SerializeField] private PerformanceHudPosition hudPosition = PerformanceHudPosition.BottomRight;

        // Frame timing measurements
        private float frameTimeAccumulator = 0f;
        private int frameCount = 0;
        private float currentFps = 60f;
        private float currentFrameTimeMs = 16.6f;
        private float lastFpsUpdate = 0f;

        // Memory stats
        private long totalAllocatedMemoryMB = 0;
        private long totalReservedMemoryMB = 0;
        private float lastMemoryUpdate = 0f;

        // FPS modes for hotkey toggling
        private readonly int[] fpsPresets = new int[] { 60, 45, 30, -1 };
        private int currentFpsPresetIndex = 0;

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
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            ApplyLowSpecOptimizations();
        }

        private void Start()
        {
            ApplyLowSpecOptimizations();
        }

        /// <summary>
        /// Applies essential graphics and memory optimizations tailored for 8GB RAM laptops.
        /// </summary>
        public void ApplyLowSpecOptimizations()
        {
            // 1. Cap frame rate to prevent GPU/CPU 100% saturation
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = enableVSync ? 1 : 0;

            // 2. Texture RAM/VRAM optimization:
            // 8GB laptops with integrated graphics share system RAM with GPU.
            // Level 1 mipmap saves up to 75% texture RAM footprint!
            QualitySettings.globalTextureMipmapLimit = textureMipmapLimit;

            // 3. Shadow optimization:
            // Lower shadow distance and cascades to avoid multi-pass shadow rendering
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowCascades = shadowCascades;
            QualitySettings.particleRaycastBudget = 64;
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // 4. Adjust URP Render Pipeline if available
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
            {
                urpAsset.renderScale = targetRenderScale;
                urpAsset.shadowDistance = shadowDistance;
            }

            // 5. Strip realtime shadows from all punctual lights (indoor spotlights/pointlights)
            // Having 200+ indoor spotlights casting realtime shadows tanks low-spec GPUs down to 0 FPS!
            StripPunctualLightShadows();

            // 6. Ensure physics tick rate is standardized (50Hz = 0.02s) to prevent CPU spikes
            Time.fixedDeltaTime = 0.02f;

            Debug.Log($"[LowSpecOptimizer] Applied 8GB Laptop Optimizations: FPS Cap={targetFrameRate}, TextureMipLimit={textureMipmapLimit}, RenderScale={targetRenderScale:F2}, VSync={enableVSync}");
        }

        /// <summary>
        /// Disables realtime shadows on all non-directional lights (spotlights, pointlights).
        /// Directional sunlight retains crisp shadows while eliminating 200+ shadow maps.
        /// </summary>
        public static void StripPunctualLightShadows()
        {
            Light[] allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            int stripped = 0;
            foreach (var l in allLights)
            {
                if (l != null && l.type != LightType.Directional && l.shadows != LightShadows.None)
                {
                    l.shadows = LightShadows.None;
                    stripped++;
                }
            }
            if (stripped > 0)
            {
                Debug.Log($"[LowSpecOptimizer] Stripped realtime shadows from {stripped} punctual lights to restore 60 FPS!");
            }
        }

        private void Update()
        {
            // FPS calculation
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

            // Memory stats update and light shadow sanitization every 2 seconds
            if (Time.unscaledTime - lastMemoryUpdate >= 2f)
            {
                totalAllocatedMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                totalReservedMemoryMB = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
                StripPunctualLightShadows();
                lastMemoryUpdate = Time.unscaledTime;
            }

            // Hotkeys via New Input System
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                // Hotkey F10: Cycle FPS Cap (60 -> 45 -> 30 -> Uncapped -> 60)
                if (kb.f10Key.wasPressedThisFrame)
                {
                    CycleFpsPreset();
                }

                // Hotkey F9: Toggle Performance HUD
                if (kb.f9Key.wasPressedThisFrame)
                {
                    showPerformanceHud = !showPerformanceHud;
                }

                // Hotkey F8: Cycle HUD Position (Bottom-Right -> Bottom-Left -> Top-Right)
                if (kb.f8Key.wasPressedThisFrame)
                {
                    CycleHudPosition();
                }
            }
        }

        private void CycleHudPosition()
        {
            hudPosition = hudPosition switch
            {
                PerformanceHudPosition.BottomRight => PerformanceHudPosition.BottomLeft,
                PerformanceHudPosition.BottomLeft => PerformanceHudPosition.TopRight,
                _ => PerformanceHudPosition.BottomRight
            };
            Debug.Log($"[LowSpecOptimizer] Performance HUD position switched to: {hudPosition}");
        }

        private void CycleFpsPreset()
        {
            currentFpsPresetIndex = (currentFpsPresetIndex + 1) % fpsPresets.Length;
            targetFrameRate = fpsPresets[currentFpsPresetIndex];

            if (targetFrameRate > 0)
            {
                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = (targetFrameRate == 60 && enableVSync) ? 1 : 0;
            }
            else
            {
                Application.targetFrameRate = -1;
                QualitySettings.vSyncCount = 0;
            }

            Debug.Log($"[LowSpecOptimizer] FPS Limit switched to: {(targetFrameRate > 0 ? targetFrameRate.ToString() + " FPS" : "Uncapped")}");
        }

        private void OnGUI()
        {
            if (!showPerformanceHud) return;

            // Draw compact, tactical performance indicator
            float boxWidth = 230f;
            float boxHeight = 78f;
            float margin = 20f;
            float boxX = Screen.width - boxWidth - margin;
            float boxY = Screen.height - boxHeight - margin; // Default Bottom-Right

            switch (hudPosition)
            {
                case PerformanceHudPosition.BottomRight:
                    boxX = Screen.width - boxWidth - margin;
                    boxY = Screen.height - boxHeight - margin;
                    break;
                case PerformanceHudPosition.BottomLeft:
                    // Positioned cleanly beside Operator Vitals HUD (which ends at X = 300)
                    boxX = 310f;
                    boxY = Screen.height - boxHeight - margin;
                    break;
                case PerformanceHudPosition.TopLeft:
                    boxX = margin;
                    boxY = 190f; // Below Radar
                    break;
                case PerformanceHudPosition.TopRight:
                default:
                    boxX = Screen.width - boxWidth - margin;
                    boxY = 105f; // Placed below Weapon HUD so it never overlaps!
                    break;
            }

            Color prevColor = GUI.color;

            // Semi-transparent dark background
            GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, boxHeight), Texture2D.whiteTexture);

            // Border line
            Color fpsColor = currentFps >= 50f ? new Color(0.2f, 1f, 0.4f) :
                            (currentFps >= 28f ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.3f, 0.3f));
            GUI.color = fpsColor;
            GUI.DrawTexture(new Rect(boxX, boxY, boxWidth, 2f), Texture2D.whiteTexture);

            // Text style
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = fpsColor;

            string fpsModeText = targetFrameRate > 0 ? $"{targetFrameRate} FPS Cap" : "Uncapped";
            GUI.Label(new Rect(boxX + 8f, boxY + 4f, boxWidth - 16f, 18f), $"⚡ FPS: {currentFps:F0} ({currentFrameTimeMs:F1}ms) | {fpsModeText}", titleStyle);

            GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            infoStyle.normal.textColor = new Color(0.85f, 0.9f, 0.95f);

            GUI.Label(new Rect(boxX + 8f, boxY + 24f, boxWidth - 16f, 16f), $"RAM: {totalAllocatedMemoryMB} MB / Heap {totalReservedMemoryMB} MB (8GB Mode)", infoStyle);
            GUI.Label(new Rect(boxX + 8f, boxY + 42f, boxWidth - 16f, 16f), $"Render Scale: {targetRenderScale:F2} | Shadows: {shadowCascades}x", infoStyle);

            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Italic
            };
            hintStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
            GUI.Label(new Rect(boxX + 8f, boxY + 58f, boxWidth - 16f, 16f), "[F10] FPS | [F9] Hide | [F8] Move", hintStyle);

            GUI.color = prevColor;
        }
    }
}
