using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalShooter.Systems.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Raycast & Detection")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float interactionDistance = 3.0f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        [Header("UI Prompt Reference")]
        [SerializeField] private InteractionPrompt interactionPrompt;

        private ObjectCarrier objectCarrier;
        private IInteractable currentInteractable;

        private void Awake()
        {
            objectCarrier = GetComponent<ObjectCarrier>();
            if (objectCarrier == null)
            {
                objectCarrier = GetComponentInParent<ObjectCarrier>();
            }

            if (playerCamera == null)
            {
                Camera mainCam = GetComponentInChildren<Camera>();
                if (mainCam != null)
                {
                    playerCamera = mainCam.transform;
                }
            }

            if (interactionPrompt == null)
            {
                interactionPrompt = GetComponent<InteractionPrompt>();
                if (interactionPrompt == null)
                {
                    interactionPrompt = gameObject.AddComponent<InteractionPrompt>();
                }
            }
        }

        private void Update()
        {
            // Coordinated Input Ownership: When an object is currently held, handle Drop input here and bypass raycasting
            if (objectCarrier != null && objectCarrier.IsHoldingObject)
            {
                if (currentInteractable != null)
                {
                    currentInteractable = null;
                }

                if (interactionPrompt != null)
                {
                    interactionPrompt.Show("Press E to drop | Mouse 1 to throw");
                }

                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    objectCarrier.ReleaseObject();
                    if (interactionPrompt != null)
                    {
                        interactionPrompt.Hide();
                    }
                }
                return;
            }

            DetectInteractable();
            HandleInteractionInput();
        }

        private void DetectInteractable()
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactableLayer))
            {
                // Searches hit object and parent hierarchy for IInteractable
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    if (currentInteractable != interactable)
                    {
                        currentInteractable = interactable;
                        if (interactionPrompt != null)
                        {
                            string prompt = string.IsNullOrEmpty(interactable.PromptText)
                                ? "Press E to interact"
                                : interactable.PromptText;
                            interactionPrompt.Show(prompt);
                        }
                    }
                    return;
                }
            }

            ClearCurrentTarget();
        }

        private void HandleInteractionInput()
        {
            if (currentInteractable == null) return;

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                currentInteractable.Interact(gameObject);
            }
        }

        private void ClearCurrentTarget()
        {
            if (currentInteractable != null)
            {
                currentInteractable = null;
                if (interactionPrompt != null)
                {
                    interactionPrompt.Hide();
                }
            }
        }
    }
}
