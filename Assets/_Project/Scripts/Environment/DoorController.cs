using System.Collections;
using UnityEngine;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Environment
{
    [RequireComponent(typeof(BoxCollider))]
    public class DoorController : MonoBehaviour, IDamageable
    {
        [Header("Door State & Settings")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float openSpeed = 4f;
        [SerializeField] private float interactionDistance = 3.5f;

        [Header("Interaction Prompt")]
        [SerializeField] private bool showPrompt = true;

        [Header("Destructible Door Settings")]
        [Tooltip("If true, the door can be broken by shooting it multiple times.")]
        [SerializeField] private bool isDestructible = true;
        [Tooltip("Health of the door. 70 HP means 2-3 shots with carbine (35 dmg) breaks it!")]
        [SerializeField] private float maxHealth = 70f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool isDestroyed = false;

        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private Coroutine activeMotion;
        private Transform playerTransform;
        private bool isPlayerNearby = false;

        public bool IsOpen => isOpen;
        public bool IsAlive => !isDestroyed && currentHealth > 0f;
        public bool CanTakeDamage => isDestructible && IsAlive;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            targetRotation = closedRotation;
            currentHealth = maxHealth;
            isDestroyed = false;
        }

        private void Start()
        {
            FindPlayer();
        }

        private void Update()
        {
            if (isDestroyed) return;

            CheckPlayerProximity();

            // Toggle door on 'E' key press when nearby
            if (isPlayerNearby && (Input.GetKeyDown(KeyCode.E) || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)))
            {
                ToggleDoor();
            }
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!CanTakeDamage) return;

            currentHealth = Mathf.Max(0f, currentHealth - damageInfo.Amount);

            // Audiovisual impact
            CombatEffects.PlayHitImpact(damageInfo.HitPoint);
            StartCoroutine(HitJoltRoutine(damageInfo.HitDirection));

            Debug.Log($"[DoorController] Door hit for {damageInfo.Amount} dmg! Remaining HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0f && !isDestroyed)
            {
                BreakDoor(damageInfo);
            }
        }

        private IEnumerator HitJoltRoutine(Vector3 bulletDir)
        {
            Vector3 origPos = transform.localPosition;
            Vector3 jolt = transform.InverseTransformDirection(bulletDir.normalized * 0.04f);
            transform.localPosition += jolt;
            yield return new WaitForSeconds(0.07f);
            if (!isDestroyed)
            {
                transform.localPosition = origPos;
            }
        }

        private void BreakDoor(DamageInfo damageInfo)
        {
            isDestroyed = true;
            if (activeMotion != null) StopCoroutine(activeMotion);

            // 1. Play wood shatter sound
            CombatEffects.PlayDoorBreakSound(transform.position);

            // 2. Spawn physics wood splinters / debris chunks
            SpawnWoodSplinters(damageInfo);

            // 3. Disable all colliders and renderers on the door
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                rend.enabled = false;
            }

            Debug.Log($"[DoorController] ★ DOOR BREACHED & DESTROYED! ({name}) ★");
        }

        private void SpawnWoodSplinters(DamageInfo damageInfo)
        {
            Vector3 center = transform.position;
            Color woodColor = new Color(0.48f, 0.30f, 0.17f);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material woodMat = new Material(shader) { color = woodColor };

            int splinterCount = 9;
            for (int i = 0; i < splinterCount; i++)
            {
                GameObject splinter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                splinter.name = "Door_WoodSplinter";

                float w = Random.Range(0.12f, 0.28f);
                float h = Random.Range(0.25f, 0.70f);
                float d = Random.Range(0.04f, 0.12f);
                splinter.transform.localScale = new Vector3(w, h, d);

                Vector3 spawnOffset = new Vector3(
                    Random.Range(-0.6f, 0.6f),
                    Random.Range(-0.8f, 0.8f),
                    Random.Range(-0.1f, 0.1f)
                );
                splinter.transform.position = center + spawnOffset;
                splinter.transform.rotation = Random.rotation;

                Renderer rend = splinter.GetComponent<Renderer>();
                if (rend != null) rend.material = woodMat;

                Rigidbody rb = splinter.AddComponent<Rigidbody>();
                rb.mass = Random.Range(1.5f, 3.5f);

                Vector3 blastDir = damageInfo.HitDirection.normalized * Random.Range(5f, 9.5f) +
                                   Random.insideUnitSphere * 3.5f +
                                   Vector3.up * Random.Range(1.5f, 4f);
                rb.AddForce(blastDir, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 35f, ForceMode.Impulse);

                Destroy(splinter, Random.Range(4.5f, 6.0f));
            }
        }

        private void CheckPlayerProximity()
        {
            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            isPlayerNearby = dist <= interactionDistance;
        }

        private void FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }

        public void ToggleDoor()
        {
            SetDoorOpen(!isOpen);
        }

        public void SetDoorOpen(bool open)
        {
            isOpen = open;

            // Swing outward based on player position relative to door
            float swingDirection = 1f;
            if (playerTransform != null)
            {
                Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, toPlayer) < 0f)
                {
                    swingDirection = -1f;
                }
            }

            targetRotation = isOpen ? closedRotation * Quaternion.Euler(0f, openAngle * swingDirection, 0f) : closedRotation;

            if (activeMotion != null) StopCoroutine(activeMotion);
            activeMotion = StartCoroutine(AnimateDoor(targetRotation));
        }

        private IEnumerator AnimateDoor(Quaternion target)
        {
            while (Quaternion.Angle(transform.localRotation, target) > 0.5f)
            {
                transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * openSpeed);
                yield return null;
            }
            transform.localRotation = target;
            activeMotion = null;
        }

        private void OnGUI()
        {
            if (isDestroyed || !showPrompt || !isPlayerNearby) return;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;

            string actionText = isOpen ? "[E] Close Door" : "[E] Open Door";
            float boxWidth = 220f;
            float boxHeight = 44f;
            float posX = (Screen.width - boxWidth) * 0.5f;
            float posY = Screen.height * 0.68f;

            // Background banner
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(new Rect(posX, posY, boxWidth, boxHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Border outline
            GUI.color = new Color(0.2f, 0.6f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(posX, posY, boxWidth, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(posX, posY, boxWidth, boxHeight), actionText, style);
        }
    }
}
