using System;
using UnityEngine;

namespace TacticalShooter.Systems.Performance
{
    public class LowSpecOptimizer : MonoBehaviour
    {
        [Header("Frame Rate Limiter")]
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool enableVSync = true;

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
            Time.fixedDeltaTime = 0.02f;
            Debug.Log($"[LowSpecOptimizer] FPS Limiter initialized: {targetFrameRate} FPS (VSync={enableVSync})");
        }
    }
}
