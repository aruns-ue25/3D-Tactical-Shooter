using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 9.5f;

    [Header("Acceleration & Ground Physics")]
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float deceleration = 12f;
    [SerializeField] private float airControl = 3f;

    [Header("Jump & Gravity")]
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -18f;

    [Header("Look")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Head Bob & Locomotion Dynamics")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField] private float walkBobSpeed = 9.5f;
    [SerializeField] private float sprintBobSpeed = 15f;
    [SerializeField] private float walkBobAmountY = 0.035f;
    [SerializeField] private float walkBobAmountX = 0.02f;
    [SerializeField] private float sprintBobAmountY = 0.065f;
    [SerializeField] private float sprintBobAmountX = 0.035f;
    [SerializeField] private float bobRollAngle = 1.2f;

    private CharacterController characterController;
    private Vector3 verticalVelocity;
    private Vector3 currentPlanarVelocity;
    private float cameraPitch = 0f;

    // Head bob state
    private float bobTimer = 0f;
    private Vector3 bobOffset = Vector3.zero;
    private float bobRoll = 0f;
    private float lastStepPhase = 0f;

    private TacticalShooter.Systems.Combat.Health playerHealth;
    private Vector3 initialSpawnPosition;
    private Quaternion initialSpawnRotation;
    private Vector3 initialCamLocalPos = new Vector3(0f, 1.6f, 0f);

    public Vector3 HeadBobOffset => bobOffset;
    public float HeadBobRoll => bobRoll;
    public float CurrentSpeed => currentPlanarVelocity.magnitude;
    public bool IsGrounded => characterController != null && characterController.isGrounded;

    private void Awake()
    {
        initialSpawnPosition = transform.position;
        initialSpawnRotation = transform.rotation;

        characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            if (characterController.radius < 0.45f)
            {
                characterController.radius = 0.45f;
            }
            characterController.center = new Vector3(0f, 0.95f, 0f);
            characterController.height = 1.9f;
            characterController.skinWidth = 0.05f;
        }

        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) playerCamera = cam.transform;
            else if (Camera.main != null) playerCamera = Camera.main.transform;
        }

        if (playerCamera != null)
        {
            initialCamLocalPos = playerCamera.localPosition;
        }

        playerHealth = GetComponent<TacticalShooter.Systems.Combat.Health>();

        if (GetComponent<TacticalShooter.Systems.Combat.PlayerCombatController>() == null)
        {
            gameObject.AddComponent<TacticalShooter.Systems.Combat.PlayerCombatController>();
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<TacticalShooter.Systems.Combat.Health>();
        }

        if (playerHealth != null && !playerHealth.IsAlive)
        {
            HandleDefeatedState();
            return;
        }

        HandleJump();
        HandleMovement();
        HandleGravity();
        HandleHeadBob();
        HandleLook();
    }

    private void HandleDefeatedState()
    {
        verticalVelocity = Vector3.zero;
        currentPlanarVelocity = Vector3.zero;

        // Smoothly sink camera toward floor and tilt down to simulate collapsing
        if (playerCamera != null)
        {
            Vector3 floorCamPos = new Vector3(initialCamLocalPos.x, 0.28f, initialCamLocalPos.z);
            playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, floorCamPos, Time.deltaTime * 3.5f);
            playerCamera.localRotation = Quaternion.Slerp(playerCamera.localRotation, Quaternion.Euler(38f, 0f, 15f), Time.deltaTime * 3.0f);
        }

        // Press 'R' to restart when operator is eliminated (Permadeath)
        if (playerHealth != null && !playerHealth.IsAlive && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Respawn();
        }

        // Press 'F11' to toggle full screen
        if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

    public void Respawn()
    {
        // Hard Permadeath: Full mission reload from beginning
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleJump()
    {
        if (characterController == null) return;

        // Jump when spacebar is pressed while on the ground
        if (characterController.isGrounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // Calculate jump impulse velocity: v = sqrt(2 * h * -g)
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            TacticalShooter.Systems.Combat.CombatEffects.PlayFootstep(transform.position, true);
        }
    }

    private void HandleMovement()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;
            if (Keyboard.current.wKey.isPressed) vertical += 1f;

            input = new Vector2(horizontal, vertical);
        }

        input = Vector2.ClampMagnitude(input, 1f);

        bool isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && input.sqrMagnitude > 0.05f;
        float targetSpeed = isSprinting ? sprintSpeed : moveSpeed;

        Vector3 targetDirection = (transform.right * input.x + transform.forward * input.y).normalized;
        Vector3 targetVelocity = targetDirection * (input.magnitude * targetSpeed);

        // Ground acceleration and deceleration with physical inertia
        bool isGrounded = characterController != null && characterController.isGrounded;
        float accelRate = isGrounded ? (targetVelocity.sqrMagnitude > 0.01f ? acceleration : deceleration) : airControl;
        currentPlanarVelocity = Vector3.MoveTowards(currentPlanarVelocity, targetVelocity, accelRate * Time.deltaTime);

        characterController.Move((currentPlanarVelocity + verticalVelocity) * Time.deltaTime);
    }

    private void HandleGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = -2f;
        }

        verticalVelocity.y += gravity * Time.deltaTime;
    }

    private void HandleHeadBob()
    {
        if (!enableHeadBob || playerCamera == null) return;

        float speed = currentPlanarVelocity.magnitude;
        bool isMoving = speed > 0.2f && characterController.isGrounded;
        bool isSprinting = speed > (moveSpeed + 0.5f);

        if (isMoving)
        {
            float bobSpeed = isSprinting ? sprintBobSpeed : walkBobSpeed;
            bobTimer += Time.deltaTime * bobSpeed;

            float curBobY = isSprinting ? sprintBobAmountY : walkBobAmountY;
            float curBobX = isSprinting ? sprintBobAmountX : walkBobAmountX;

            // Vertical bob dips twice per stride (once per footstep)
            float sinCycle = Mathf.Sin(bobTimer);
            float targetY = Mathf.Sin(bobTimer * 2f) * curBobY;
            float targetX = Mathf.Cos(bobTimer) * curBobX;
            float targetRoll = Mathf.Cos(bobTimer) * (isSprinting ? bobRollAngle * 1.4f : bobRollAngle);

            bobOffset = Vector3.Lerp(bobOffset, new Vector3(targetX, targetY, 0f), Time.deltaTime * 12f);
            bobRoll = Mathf.Lerp(bobRoll, targetRoll, Time.deltaTime * 12f);

            // Audio footstep trigger when foot plants into the ground
            if (lastStepPhase > 0f && sinCycle <= 0f)
            {
                TacticalShooter.Systems.Combat.CombatEffects.PlayFootstep(transform.position, isSprinting);
            }
            lastStepPhase = sinCycle;
        }
        else
        {
            // Smoothly settle back to resting eye level
            bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, Time.deltaTime * 8f);
            bobRoll = Mathf.Lerp(bobRoll, 0f, Time.deltaTime * 8f);
            bobTimer = 0f;
        }
    }

    private void HandleLook()
    {
        if (Mouse.current == null || playerCamera == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        // Horizontal rotation: rotate the player body
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation: rotate only the camera, factoring in head bob roll
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

        playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, bobRoll);
    }
}