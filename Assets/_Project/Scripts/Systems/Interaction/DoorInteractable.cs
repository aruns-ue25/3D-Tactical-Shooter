using System.Collections;
using UnityEngine;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.Systems.Interaction
{
    public class DoorInteractable : MonoBehaviour, IInteractable, IDamageable
    {
        [Header("Door Configuration")]
        [SerializeField] private Transform doorLeaf;
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float duration = 0.55f;

        [Header("Interaction Prompts")]
        [SerializeField] private string openPrompt = "Press E to open door";
        [SerializeField] private string closePrompt = "Press E to close door";

        [Header("Security Door Settings")]
        [Tooltip("Security doors are solid structural obstacles that cannot be broken by gunfire.")]
        [SerializeField] private bool isDestructible = false;
        [SerializeField] private float maxHealth = 9999f;
        [SerializeField] private float currentHealth = 9999f;
        [SerializeField] private bool isDestroyed = false;

        private bool isOpen = false;
        private bool isAnimating = false;
        private Quaternion closedRotation;
        private Quaternion openRotation;
        private Transform playerTransform;
        private bool isPlayerNearby = false;
        private Coroutine activeAnimRoutine;
        private float lastInteractTime = -1f;

        public bool IsAlive => true;
        public bool CanTakeDamage => false; // Indestructible security door
        public bool IsOpen => isOpen;
        public string PromptText => isOpen ? closePrompt : openPrompt;

        private void Awake()
        {
            isDestructible = false;
            currentHealth = maxHealth;
            isDestroyed = false;

            doorLeaf = transform.Find("HingePivot");
            if (doorLeaf == null)
            {
                doorLeaf = transform;
            }

            closedRotation = Quaternion.identity;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        private void Start()
        {
            FindPlayer();
            doorLeaf = transform.Find("HingePivot") ?? transform;
            if (!isOpen)
            {
                doorLeaf.localRotation = Quaternion.identity;
            }
        }

        private void FindPlayer()
        {
            if (playerTransform != null) return;
            GameObject p = GameObject.Find("Player") ?? GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
            else
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) playerTransform = pc.transform;
            }
        }

        private void Update()
        {
            FindPlayer();
            if (playerTransform != null)
            {
                // Measure both direct horizontal XZ distance and vertical elevation relative to door portal floor
                Vector3 doorwayCenter = transform.position;
                float distXZ = Vector2.Distance(new Vector2(doorwayCenter.x, doorwayCenter.z), new Vector2(playerTransform.position.x, playerTransform.position.z));
                float distY = Mathf.Abs(playerTransform.position.y - doorwayCenter.y);

                float minDistToColliders = float.MaxValue;
                var colliders = GetComponentsInChildren<Collider>();
                if (colliders != null && colliders.Length > 0)
                {
                    foreach (var c in colliders)
                    {
                        if (c != null && c.enabled)
                        {
                            float d = Vector3.Distance(c.bounds.ClosestPoint(playerTransform.position), playerTransform.position);
                            if (d < minDistToColliders) minDistToColliders = d;
                        }
                    }
                }

                // Strict vertical elevation guard: player must be on the same floor level (prevents bleed through floors)
                if (distY > 2.0f)
                {
                    isPlayerNearby = false;
                }
                else
                {
                    isPlayerNearby = (distXZ <= 5.0f) || (minDistToColliders <= 3.8f);
                }
            }
            else
            {
                isPlayerNearby = false;
            }

            // Watchdog auto-timeout if animation ever got stuck
            if (isAnimating && Time.time - lastInteractTime > 2.0f)
            {
                isAnimating = false;
            }

            if (isPlayerNearby && !isAnimating)
            {
                bool ePressed = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
                if (ePressed)
                {
                    Interact(playerTransform != null ? playerTransform.gameObject : gameObject);
                }
            }
        }

        public void Interact(GameObject interactor)
        {
            // Input debouncing to prevent multiple callers in the same frame from double-toggling
            if (Time.time - lastInteractTime < 0.25f) return;
            lastInteractTime = Time.time;

            if (isAnimating) return;

            doorLeaf = transform.Find("HingePivot") ?? transform;

            isOpen = !isOpen;

            if (isOpen)
            {
                CombatEffects.PlayDoorOpenSound(transform.position);
            }
            else
            {
                CombatEffects.PlayDoorCloseSound(transform.position);
            }

            // Swing outward based on player position relative to door
            float swingDirection = 1f;
            if (interactor != null)
            {
                Vector3 toPlayer = (interactor.transform.position - transform.position).normalized;
                if (Vector3.Dot(transform.forward, toPlayer) < 0f)
                {
                    swingDirection = -1f;
                }
            }

            Quaternion targetRotation = isOpen ? Quaternion.Euler(0f, openAngle * swingDirection, 0f) : Quaternion.identity;
            if (activeAnimRoutine != null) StopCoroutine(activeAnimRoutine);
            activeAnimRoutine = StartCoroutine(AnimateDoor(targetRotation));
        }

        private void OnGUI()
        {
            if (!isPlayerNearby) return;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;

            string actionText = isOpen ? "[E] CLOSE SECURITY DOOR" : "[E] OPEN SECURITY DOOR";
            float boxWidth = 260f;
            float boxHeight = 40f;
            float posX = (Screen.width - boxWidth) * 0.5f;
            float posY = Screen.height * 0.70f;

            Color prev = GUI.color;
            GUI.color = new Color(0.04f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(new Rect(posX, posY, boxWidth, boxHeight), Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.75f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(posX, posY, boxWidth, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(posX, posY, boxWidth, boxHeight), actionText, style);
            GUI.color = prev;
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            // Indestructible reinforced security door: absorb impact with sparks and jolt, never break
            CombatEffects.PlayHitImpact(damageInfo.HitPoint);
            StartCoroutine(HitJoltRoutine(damageInfo.HitDirection));
        }

        private IEnumerator HitJoltRoutine(Vector3 bulletDir)
        {
            if (doorLeaf == null) yield break;
            Vector3 origPos = doorLeaf.localPosition;
            Vector3 jolt = doorLeaf.InverseTransformDirection(bulletDir.normalized * 0.04f);
            doorLeaf.localPosition += jolt;
            yield return new WaitForSeconds(0.07f);
            if (!isDestroyed && doorLeaf != null)
            {
                doorLeaf.localPosition = origPos;
            }
        }

        private void BreakDoor(DamageInfo damageInfo)
        {
            isDestroyed = true;
            StopAllCoroutines();

            // 1. Play wood breach crash sound
            CombatEffects.PlayDoorBreakSound(transform.position);

            // 2. Spawn physics wood splinters / debris chunks
            SpawnWoodSplinters(damageInfo);

            // 3. Disable all colliders and renderers on the door so player can walk freely through
            if (doorLeaf != null)
            {
                foreach (var col in doorLeaf.GetComponentsInChildren<Collider>())
                {
                    col.enabled = false;
                }
                foreach (var rend in doorLeaf.GetComponentsInChildren<Renderer>())
                {
                    rend.enabled = false;
                }
            }

            foreach (var col in GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                rend.enabled = false;
            }

            Debug.Log($"[DoorInteractable] ★ DOOR BREACHED & DESTROYED! ({name}) ★");
        }

        private void SpawnWoodSplinters(DamageInfo damageInfo)
        {
            Vector3 center = doorLeaf != null ? doorLeaf.position : transform.position;
            Color woodColor = new Color(0.48f, 0.30f, 0.17f); // Wood brown

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material woodMat = new Material(shader) { color = woodColor };

            int splinterCount = 9;
            for (int i = 0; i < splinterCount; i++)
            {
                GameObject splinter = GameObject.CreatePrimitive(PrimitiveType.Cube);
                splinter.name = "Door_WoodSplinter";

                // Varied splinter plank shapes
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

                // Blast outwards in bullet direction + explosive scatter
                Vector3 blastDir = damageInfo.HitDirection.normalized * Random.Range(5f, 9.5f) +
                                   Random.insideUnitSphere * 3.5f +
                                   Vector3.up * Random.Range(1.5f, 4f);
                rb.AddForce(blastDir, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 35f, ForceMode.Impulse);

                // Auto-cleanup splinter debris after a few seconds
                Destroy(splinter, Random.Range(4.5f, 6.0f));
            }
        }

        private IEnumerator AnimateDoor(Quaternion targetRotation)
        {
            isAnimating = true;
            try
            {
                if (doorLeaf == null) doorLeaf = transform.Find("HingePivot") ?? transform;
                Quaternion startRotation = doorLeaf.localRotation;
                float elapsedTime = 0f;

                while (elapsedTime < duration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / duration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    doorLeaf.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
                    yield return null;
                }

                doorLeaf.localRotation = targetRotation;
            }
            finally
            {
                isAnimating = false;
            }
        }
    }
}
