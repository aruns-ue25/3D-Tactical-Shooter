using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalShooter.Systems.Interaction
{
    public class ObjectCarrier : MonoBehaviour
    {
        [Header("Hold Settings")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float holdDistance = 2.0f;
        [SerializeField] private float followSpeed = 15.0f;

        [Header("Throw Settings")]
        [SerializeField] private float throwForce = 12.0f;

        private MovableObject currentHeldObject;
        private Rigidbody heldRb;
        private float originalDrag;
        private float originalAngularDrag;
        private bool originalUseGravity;
        private CollisionDetectionMode originalCollisionMode;

        public bool IsHoldingObject => currentHeldObject != null;
        public MovableObject CurrentHeldObject => currentHeldObject;

        private void Awake()
        {
            if (playerCamera == null)
            {
                Camera mainCam = GetComponentInChildren<Camera>();
                if (mainCam != null)
                {
                    playerCamera = mainCam.transform;
                }
            }
        }

        private void Update()
        {
            if (!IsHoldingObject) return;

            // Throw Input (Left Mouse Button)
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                ThrowObject();
            }
        }

        private void FixedUpdate()
        {
            if (!IsHoldingObject || heldRb == null || playerCamera == null) return;

            Vector3 targetPosition = playerCamera.position + playerCamera.forward * holdDistance;
            Vector3 direction = targetPosition - heldRb.position;
            heldRb.linearVelocity = direction * followSpeed;
        }

        public void PickUp(MovableObject target)
        {
            if (target == null || target.Rb == null || IsHoldingObject) return;

            currentHeldObject = target;
            heldRb = target.Rb;

            originalUseGravity = heldRb.useGravity;
            originalDrag = heldRb.linearDamping;
            originalAngularDrag = heldRb.angularDamping;
            originalCollisionMode = heldRb.collisionDetectionMode;

            heldRb.useGravity = false;
            heldRb.linearDamping = 10f;
            heldRb.angularDamping = 10f;
            heldRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        public void ReleaseObject()
        {
            if (!IsHoldingObject || heldRb == null) return;

            heldRb.useGravity = originalUseGravity;
            heldRb.linearDamping = originalDrag;
            heldRb.angularDamping = originalAngularDrag;
            heldRb.collisionDetectionMode = originalCollisionMode;

            heldRb = null;
            currentHeldObject = null;
        }

        public void ThrowObject()
        {
            if (!IsHoldingObject || heldRb == null || playerCamera == null) return;

            MovableObject objToThrow = currentHeldObject;
            Rigidbody rbToThrow = heldRb;

            ReleaseObject();

            float forceMultiplier = objToThrow.ThrowForceMultiplier;
            rbToThrow.AddForce(playerCamera.forward * (throwForce * forceMultiplier), ForceMode.Impulse);
        }
    }
}
