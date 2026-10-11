using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalShooter.Systems.Combat
{
    /// <summary>
    /// First-person weapon viewmodel controller.
    /// Provides smooth mouse look sway and punchy recoil animation.
    /// </summary>
    public class WeaponViewModel : MonoBehaviour
    {
        [Header("Muzzle & Visuals")]
        [SerializeField] private Transform muzzlePoint;

        [Header("Sway Settings")]
        [SerializeField] private float swayAmount = 0.02f;
        [SerializeField] private float maxSwayAmount = 0.05f;
        [SerializeField] private float swaySmooth = 8f;

        [Header("Recoil Settings")]
        [SerializeField] private float recoilKickBack = 0.06f;
        [SerializeField] private float recoilKickUp = 0.02f;
        [SerializeField] private float recoilDuration = 0.09f;

        private Vector3 defaultLocalPosition;
        private Quaternion defaultLocalRotation;
        private Coroutine recoilRoutine;
        private CharacterController characterController;
        private float walkBobTimer = 0f;

        public Transform MuzzlePoint => muzzlePoint != null ? muzzlePoint : transform;

        private void Awake()
        {
            defaultLocalPosition = transform.localPosition;
            defaultLocalRotation = transform.localRotation;
            characterController = GetComponentInParent<CharacterController>();

            if (muzzlePoint == null)
            {
                Transform m = transform.Find("MuzzlePoint");
                if (m != null) muzzlePoint = m;
            }
        }

        private void Update()
        {
            HandleSway();
        }

        private void HandleSway()
        {
            float moveX = 0f;
            float moveY = 0f;

            if (Mouse.current != null)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                moveX = Mathf.Clamp(-mouseDelta.x * swayAmount, -maxSwayAmount, maxSwayAmount);
                moveY = Mathf.Clamp(-mouseDelta.y * swayAmount, -maxSwayAmount, maxSwayAmount);
            }

            // Tactical weapon bob while walking or sprinting
            float walkBobX = 0f;
            float walkBobY = 0f;
            if (characterController != null)
            {
                float speed = characterController.velocity.magnitude;
                if (speed > 0.2f && characterController.isGrounded)
                {
                    bool isSprinting = speed > 6f;
                    float bobSpeed = isSprinting ? 15f : 9.5f;
                    walkBobTimer += Time.deltaTime * bobSpeed;

                    walkBobX = Mathf.Cos(walkBobTimer * 0.5f) * (isSprinting ? 0.02f : 0.008f);
                    walkBobY = Mathf.Sin(walkBobTimer) * (isSprinting ? 0.022f : 0.01f);
                }
                else
                {
                    walkBobTimer = 0f;
                }
            }

            Vector3 finalPosition = new Vector3(
                defaultLocalPosition.x + moveX + walkBobX,
                defaultLocalPosition.y + moveY + walkBobY,
                defaultLocalPosition.z
            );

            transform.localPosition = Vector3.Lerp(transform.localPosition, finalPosition, Time.deltaTime * swaySmooth);
        }

        public void PlayRecoil()
        {
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            recoilRoutine = StartCoroutine(RecoilRoutine());
        }

        private IEnumerator RecoilRoutine()
        {
            Vector3 kickPos = defaultLocalPosition - Vector3.forward * recoilKickBack + Vector3.up * recoilKickUp;
            transform.localPosition = kickPos;

            float elapsed = 0f;
            while (elapsed < recoilDuration)
            {
                elapsed += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(kickPos, defaultLocalPosition, elapsed / recoilDuration);
                yield return null;
            }

            transform.localPosition = defaultLocalPosition;
        }
    }
}
