using UnityEngine;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// Manages the 3D tactical soldier character model for the player.
    /// In 1st Person: Hides body to prevent camera clipping.
    /// In 3rd Person: Renders the full military operative holding the rifle in tactical ready stance.
    /// Adds walking movement bob and torso aiming alignment.
    /// </summary>
    public class PlayerCharacterVisuals : MonoBehaviour
    {
        [Header("Character Body Root")]
        [SerializeField] private GameObject bodyRoot;
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform thirdPersonRifle;

        private PlayerCombatController combatController;
        private CharacterController characterController;
        private Transform playerCamera;
        private Vector3 defaultUpperBodyLocalPos;

        private void Awake()
        {
            combatController = GetComponent<PlayerCombatController>();
            characterController = GetComponent<CharacterController>();

            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;

            if (bodyRoot == null)
            {
                Transform found = transform.Find("CharacterBody");
                if (found != null) bodyRoot = found.gameObject;
            }

            if (upperBody == null && bodyRoot != null)
            {
                upperBody = bodyRoot.transform.Find("Torso");
            }

            if (upperBody != null)
            {
                defaultUpperBodyLocalPos = upperBody.localPosition;
            }
        }

        private void LateUpdate()
        {
            UpdateVisibility();
            AnimateMovement();
        }

        private void UpdateVisibility()
        {
            if (bodyRoot == null) return;

            bool isThirdPerson = combatController != null && combatController.IsThirdPerson;

            // Only show 3D body when in Third-Person mode
            if (bodyRoot.activeSelf != isThirdPerson)
            {
                bodyRoot.SetActive(isThirdPerson);
            }

            // Align upper body pitch with camera in 3rd person
            if (isThirdPerson && upperBody != null && playerCamera != null)
            {
                float pitch = playerCamera.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                pitch = Mathf.Clamp(pitch, -45f, 45f);
                upperBody.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void AnimateMovement()
        {
            if (bodyRoot == null || !bodyRoot.activeSelf || upperBody == null) return;

            // Subtle breathing and walking bob
            float speed = characterController != null ? characterController.velocity.magnitude : 0f;
            float bobSpeed = speed > 0.5f ? 10f : 2f;
            float bobAmount = speed > 0.5f ? 0.03f : 0.008f;

            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            upperBody.localPosition = defaultUpperBodyLocalPos + new Vector3(0f, bobOffset, 0f);
        }
    }
}
