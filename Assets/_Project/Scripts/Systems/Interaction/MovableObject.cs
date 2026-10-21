using UnityEngine;

namespace TacticalShooter.Systems.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovableObject : MonoBehaviour, IInteractable
    {
        [Header("Movable Object Settings")]
        [SerializeField] private bool isPickupable = true;
        [SerializeField] private float objectMass = 10f;
        [SerializeField] private float throwForceMultiplier = 1.0f;
        [SerializeField] private string promptMessage = "Press E to pick up";

        private Rigidbody rb;

        public bool IsPickupable => isPickupable;
        public float ThrowForceMultiplier => throwForceMultiplier;
        public Rigidbody Rb => rb;
        public string PromptText => promptMessage;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = objectMass;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!isPickupable || interactor == null) return;

            ObjectCarrier carrier = interactor.GetComponent<ObjectCarrier>();
            if (carrier == null)
            {
                carrier = interactor.GetComponentInParent<ObjectCarrier>();
            }

            if (carrier != null && !carrier.IsHoldingObject)
            {
                carrier.PickUp(this);
            }
        }
    }
}
