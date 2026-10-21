using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Interactive target dummy with physical reaction:
    /// - Wobble / squash-and-stretch hit reaction
    /// - Floating floating damage text numbers (-25)
    /// - Knockdown collapse animation on defeat
    /// - Floating world-space health bar
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TargetDummy : MonoBehaviour
    {
        [Header("Visual Feedback")]
        [SerializeField] private Color damageFlashColor = Color.white;
        [SerializeField] private Color defaultTargetColor = new Color(0.95f, 0.25f, 0.15f); // Bright tactical red
        [SerializeField] private Color defeatedColor = new Color(0.3f, 0.3f, 0.3f);
        [SerializeField] private float flashDuration = 0.12f;

        [Header("World HUD")]
        [SerializeField] private bool showFloatingHealth = true;
        [SerializeField] private float hudVerticalOffset = 1.6f;

        private Health health;
        private Renderer targetRenderer;
        private Material targetMaterial;
        private Coroutine flashCoroutine;
        private Coroutine punchCoroutine;
        private Camera cachedCamera;
        private Vector3 defaultScale;
        private Quaternion defaultRotation;

        // Floating damage numbers list
        private class FloatingDamageText
        {
            public string text;
            public Vector3 worldPos;
            public float timeRemaining;
            public float maxTime;
        }
        private readonly List<FloatingDamageText> damageTexts = new List<FloatingDamageText>();

        public Health HealthComponent => health;

        private void Awake()
        {
            health = GetComponent<Health>();
            targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (targetRenderer != null)
            {
                targetMaterial = targetRenderer.material;
                targetMaterial.color = defaultTargetColor;
            }

            defaultScale = transform.localScale;
            defaultRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDefeated += HandleDefeated;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDefeated -= HandleDefeated;
            }
        }

        private void Update()
        {
            // Animate floating damage numbers upward
            for (int i = damageTexts.Count - 1; i >= 0; i--)
            {
                var dt = damageTexts[i];
                dt.worldPos += Vector3.up * (1.2f * Time.deltaTime);
                dt.timeRemaining -= Time.deltaTime;
                if (dt.timeRemaining <= 0f)
                {
                    damageTexts.RemoveAt(i);
                }
            }
        }

        private void HandleDamaged(DamageInfo damageInfo)
        {
            Debug.Log($"[TargetDummy] Hit for {damageInfo.Amount} damage! Remaining Health: {health.CurrentHealth}/{health.MaxHealth}");

            // Add floating damage popup
            damageTexts.Add(new FloatingDamageText
            {
                text = $"-{damageInfo.Amount:0}",
                worldPos = transform.position + Vector3.up * 1.8f + Random.insideUnitSphere * 0.2f,
                timeRemaining = 0.8f,
                maxTime = 0.8f
            });

            // Color Flash
            if (targetRenderer != null && health.IsAlive)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashRoutine());
            }

            // Punch Scale / Hit Reaction
            if (health.IsAlive)
            {
                if (punchCoroutine != null) StopCoroutine(punchCoroutine);
                punchCoroutine = StartCoroutine(PunchReactionRoutine());
            }
        }

        private void HandleDefeated(DamageInfo damageInfo)
        {
            Debug.Log($"[TargetDummy] DEFEATED by {damageInfo.Instigator?.name ?? "Unknown"}! Target collapsed.");

            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            if (punchCoroutine != null) StopCoroutine(punchCoroutine);

            CombatEffects.PlayDefeatSound(transform.position);

            if (targetMaterial != null)
            {
                targetMaterial.color = defeatedColor;
            }

            // Knockdown animation (falls backward onto ground)
            StartCoroutine(CollapseRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            if (targetMaterial != null)
            {
                targetMaterial.color = damageFlashColor;
                yield return new WaitForSeconds(flashDuration);
                if (health.IsAlive)
                {
                    targetMaterial.color = defaultTargetColor;
                }
            }
        }

        private IEnumerator PunchReactionRoutine()
        {
            Vector3 squashed = new Vector3(defaultScale.x * 1.15f, defaultScale.y * 0.82f, defaultScale.z * 1.15f);
            transform.localScale = squashed;

            float elapsed = 0f;
            float duration = 0.14f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(squashed, defaultScale, elapsed / duration);
                yield return null;
            }
            transform.localScale = defaultScale;
        }

        private IEnumerator CollapseRoutine()
        {
            Quaternion startRot = transform.localRotation;
            Quaternion targetRot = Quaternion.Euler(75f, startRot.eulerAngles.y, startRot.eulerAngles.z);

            float elapsed = 0f;
            float duration = 0.3f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
                yield return null;
            }
            transform.localRotation = targetRot;
        }

        public void ResetTarget()
        {
            if (health != null) health.ResetHealth();
            transform.localScale = defaultScale;
            transform.localRotation = defaultRotation;
            if (targetMaterial != null) targetMaterial.color = defaultTargetColor;
        }

        private Camera GetCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled) return cachedCamera;
            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                cachedCamera = FindFirstObjectByType<Camera>();
            }
            return cachedCamera;
        }

        private void OnGUI()
        {
            Camera cam = GetCamera();
            if (cam == null) return;

            // 1. Draw floating damage text popups
            foreach (var dt in damageTexts)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(dt.worldPos);
                if (screenPos.z > 0f)
                {
                    float alpha = dt.timeRemaining / dt.maxTime;
                    GUI.color = new Color(1f, 0.2f, 0.2f, alpha);
                    GUIStyle damageStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 20,
                        fontStyle = FontStyle.Bold
                    };
                    damageStyle.normal.textColor = new Color(1f, 0.2f, 0.2f, alpha);
                    GUI.Label(new Rect(screenPos.x - 40, Screen.height - screenPos.y - 15, 80, 30), dt.text, damageStyle);
                }
            }

            // 2. Draw Target Dummy Health Bar (Only when damaged and alive)
            if (showFloatingHealth && health != null && health.IsAlive && health.CurrentHealth < health.MaxHealth)
            {
                Vector3 worldPos = transform.position + Vector3.up * hudVerticalOffset;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenPos.z > 0f)
                {
                    float barWidth = 150f;
                    float barHeight = 20f;
                    float x = screenPos.x - barWidth * 0.5f;
                    float y = Screen.height - screenPos.y - barHeight;

                    Color prevColor = GUI.color;

                    // Background box
                    GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.9f);
                    GUI.DrawTexture(new Rect(x - 2, y - 2, barWidth + 4, barHeight + 4), Texture2D.whiteTexture);

                    // Health fill bar
                    float healthPercent = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
                    GUI.color = health.IsAlive ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.85f, 0.2f, 0.2f);
                    GUI.DrawTexture(new Rect(x, y, barWidth * healthPercent, barHeight), Texture2D.whiteTexture);

                    // Label text
                    GUI.color = Color.white;
                    GUIStyle style = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 11,
                        fontStyle = FontStyle.Bold
                    };
                    style.normal.textColor = Color.white;
                    string label = health.IsAlive ? $"TARGET: {health.CurrentHealth:0} / {health.MaxHealth:0}" : "TARGET DOWN";
                    GUI.Label(new Rect(x, y, barWidth, barHeight), label, style);

                    GUI.color = prevColor;
                }
            }
        }
    }
}
