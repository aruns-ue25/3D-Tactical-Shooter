using UnityEngine;
using UnityEngine.InputSystem;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Systems.Interaction
{
    [SelectionBase]
    public class AmmoPickup : MonoBehaviour, IInteractable
    {
        [Header("Ammo Settings")]
        [Tooltip("Number of 5.56mm rounds granted upon collection.")]
        [SerializeField] private int ammoAmount = 30;

        [Header("Pickup Radius")]
        [SerializeField] private float triggerRadius = 2.4f;
        [SerializeField] private float hoverAmplitude = 0.05f;
        [SerializeField] private float rotateSpeed = 28f;

        private Vector3 startPosition;
        private Transform playerTransform;
        private PlayerCombatController playerCombat;
        private bool isCollected = false;

        public string PromptText => $"Press E to collect +{ammoAmount} AMMO";
        public int AmmoAmount => ammoAmount;

        private void Start()
        {
            startPosition = transform.position;
            FindPlayer();
        }

        private void FindPlayer()
        {
            if (playerTransform != null && playerCombat != null) return;

            GameObject p = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
            if (p != null)
            {
                playerTransform = p.transform;
                playerCombat = p.GetComponent<PlayerCombatController>();
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
            if (playerTransform == null) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= triggerRadius)
            {
                CollectAmmo();
            }
        }

        public void Interact(GameObject interactor)
        {
            if (isCollected) return;
            CollectAmmo();
        }

        private void CollectAmmo()
        {
            if (isCollected) return;
            isCollected = true;

            FindPlayer();
            if (playerCombat != null)
            {
                playerCombat.AddAmmo(ammoAmount);
            }

            CombatEffects.PlayAmmoPickupSound(transform.position);
            Debug.Log($"[AmmoPickup] ★ Collected {ammoAmount} rounds of 5.56mm ammunition!");

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
                GUI.color = new Color(1.0f, 0.85f, 0.2f, alpha * 0.95f);

                GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };
                labelStyle.normal.textColor = new Color(1.0f, 0.9f, 0.3f, alpha);

                float w = 150f;
                float h = 24f;
                float x = screenPos.x - w * 0.5f;
                float y = Screen.height - screenPos.y - h * 0.5f;

                GUI.Label(new Rect(x, y, w, h), $"[ +{ammoAmount} AMMO ]", labelStyle);
                GUI.color = prevCol;
            }
        }
    }
}
