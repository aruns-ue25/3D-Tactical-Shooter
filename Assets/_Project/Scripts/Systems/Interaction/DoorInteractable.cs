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
        [SerializeField] private float duration = 0.8f;

        [Header("Interaction Prompts")]
        [SerializeField] private string openPrompt = "Press E to open door";
        [SerializeField] private string closePrompt = "Press E to close door";

        [Header("Destructible Door Settings")]
        [Tooltip("If true, the door can be broken by shooting it multiple times.")]
        [SerializeField] private bool isDestructible = true;
        [Tooltip("Health of the door. 70 HP means 2-3 shots with carbine (35 dmg) breaks it!")]
        [SerializeField] private float maxHealth = 70f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool isDestroyed = false;

        private bool isOpen = false;
        private bool isAnimating = false;
        private Quaternion closedRotation;
        private Quaternion openRotation;

        public bool IsAlive => !isDestroyed && currentHealth > 0f;
        public bool CanTakeDamage => isDestructible && IsAlive;
        public bool IsOpen => isOpen;
        public string PromptText => isDestroyed ? "" : (isOpen ? closePrompt : openPrompt);

        private void Awake()
        {
            currentHealth = maxHealth;
            isDestroyed = false;

            if (doorLeaf == null)
            {
                doorLeaf = transform.Find("HingePivot") ?? transform;
            }

            closedRotation = doorLeaf.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        public void Interact(GameObject interactor)
        {
            if (isDestroyed || isAnimating) return;

            isOpen = !isOpen;
            StartCoroutine(AnimateDoor(isOpen ? openRotation : closedRotation));
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!CanTakeDamage) return;

            currentHealth = Mathf.Max(0f, currentHealth - damageInfo.Amount);

            // Audiovisual impact
            CombatEffects.PlayHitImpact(damageInfo.HitPoint);
            StartCoroutine(HitJoltRoutine(damageInfo.HitDirection));

            Debug.Log($"[DoorInteractable] Door hit for {damageInfo.Amount} dmg! Remaining HP: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0f && !isDestroyed)
            {
                BreakDoor(damageInfo);
            }
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
            isAnimating = false;
        }
    }
}
