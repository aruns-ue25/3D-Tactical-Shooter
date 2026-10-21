using UnityEngine;
using UnityEngine.InputSystem;
using TacticalShooter.Systems.Combat;
using TacticalShooter.Systems.Mission;

namespace TacticalShooter.Systems.Interaction
{
    [SelectionBase]
    public class HealthPickup : MonoBehaviour, IInteractable
    {
        [Header("Health Settings")]
        [Tooltip("Amount of HP restored to the operator upon collection.")]
        [SerializeField] private float healAmount = 100f;

        [Header("Pickup Radius")]
        [SerializeField] private float triggerRadius = 2.4f;
        [SerializeField] private float hoverAmplitude = 0.05f;
        [SerializeField] private float rotateSpeed = 28f;

        private Vector3 startPosition;
        private Transform playerTransform;
        private Health playerHealth;
        private bool isCollected = false;

        public string PromptText => (playerHealth != null && playerHealth.CurrentHealth >= 200f)
            ? "Trauma Medkit reserved [Vitals >= 200 HP]"
            : $"Press E to use Trauma Medkit (+{healAmount} HP)";
        public float HealAmount => healAmount;

        public void SetHealAmount(float amount)
        {
            healAmount = amount;
        }

        private void Awake()
        {
            if (healAmount < 100f)
            {
                healAmount = 100f;
            }
        }

        private void Start()
        {
            startPosition = transform.position;
            FindPlayer();
        }

        private void FindPlayer()
        {
            if (playerTransform != null && playerHealth != null) return;

            GameObject p = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerHealth = p.GetComponent<Health>();
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // Gentle tactical hovering & rotation
            float yOffset = Mathf.Sin(Time.time * 2.2f) * hoverAmplitude;
            transform.position = startPosition + new Vector3(0f, yOffset, 0f);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

            FindPlayer();
            if (playerTransform == null || playerHealth == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= triggerRadius)
            {
                // Only collect if player's current health is strictly below 200 HP threshold
                if (playerHealth.CurrentHealth < 200f)
                {
                    CollectHealth();
                }
            }
        }

        public void Interact(GameObject interactor)
        {
            if (isCollected) return;
            FindPlayer();
            if (playerHealth != null && playerHealth.CurrentHealth < 200f)
            {
                CollectHealth();
            }
        }

        private void CollectHealth()
        {
            if (isCollected) return;

            FindPlayer();
            if (playerHealth == null) return;

            bool healed = playerHealth.Heal(healAmount);
            if (!healed) return;

            isCollected = true;
            CombatEffects.PlayMedkitPickupSound(transform.position);

            if (MissionManager.Instance != null)
            {
                MissionManager.Instance.ShowSubtitle($"MEDIC: +{healAmount} HP Restored! [Vitals: {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)} HP]", 3.5f, new Color(0.25f, 1.0f, 0.45f));
            }

            Debug.Log($"[HealthPickup] ★ Operator restored +{healAmount} HP! Current HP: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");
            Destroy(gameObject, 0.05f);
        }

        private void OnGUI()
        {
            if (isCollected || playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist > 6.0f) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * 0.45f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z > 0f)
            {
                float alpha = Mathf.Clamp01((6.0f - dist) / 3.0f);
                Color prevCol = GUI.color;
                GUI.color = new Color(0.2f, 0.95f, 0.45f, alpha * 0.95f);

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                labelStyle.normal.textColor = new Color(0.35f, 1.0f, 0.55f, alpha);

                float w = 220f;
                float h = 24f;
                float x = screenPos.x - w * 0.5f;
                float y = Screen.height - screenPos.y - h * 0.5f;

                string label = (playerHealth != null && playerHealth.CurrentHealth >= 200f)
                    ? "[ MEDKIT (REQUIRES < 200 HP) ]"
                    : $"[ MEDKIT (+{healAmount} HP) ]";

                GUI.Label(new Rect(x, y, w, h), label, labelStyle);
                GUI.color = prevCol;
            }
        }
    }
}
