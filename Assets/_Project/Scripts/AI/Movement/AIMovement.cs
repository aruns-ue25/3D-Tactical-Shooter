using System;
using UnityEngine;
using UnityEngine.AI;
using TacticalShooter.Environment;
using TacticalShooter.Systems.Interaction;

namespace TacticalShooter.AI.Movement
{
    /// <summary>
    /// Shared navigation and locomotion controller for AI agents.
    /// Provides hybrid navigation:
    /// 1. Uses UnityEngine.AI.NavMeshAgent if NavMesh is baked and active.
    /// 2. Provides autonomous kinematic raycast steering fallback if NavMesh is unbaked.
    /// 3. Collaborates with GV door systems to automatically open doors in transit.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AIMovement : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float jogSpeed = 3.8f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float stoppingDistance = 1.0f;

        [Header("Obstacle Avoidance (Kinematic Fallback)")]
        [SerializeField] private float obstacleCheckDistance = 1.8f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Door Interaction")]
        [SerializeField] private bool autoOpenDoors = true;
        [SerializeField] private float doorCheckDistance = 2.0f;

        private CharacterController characterController;
        private NavMeshAgent navMeshAgent;

        private Vector3 currentDestination;
        private bool isMoving = false;
        private float currentMoveSpeed = 2.2f;
        private Vector3 currentVelocity = Vector3.zero;
        private float doorCheckCooldown = 0f;

        // Anti-stuck tracking
        private Vector3 lastSampledPosition;
        private float stuckSampleTimer = 0f;
        private float stuckTimer = 0f;
        private bool isStuck = false;

        // Physical collision contact tracking
        private Vector3 lastContactNormal = Vector3.zero;
        private float lastContactTime = -10f;
        private float unstickSideSign = 1f;

        // Dynamic combat aim & focus lock
        private Vector3? forcedLookTarget = null;
        private float forcedLookTimer = 0f;
        private float forcedLookSpeed = 1f;

        public bool IsMoving => isMoving;
        public bool IsStuck => isStuck;
        public Vector3 Destination => currentDestination;
        public float StoppingDistance => stoppingDistance;
        public float WalkSpeed => walkSpeed;
        public float JogSpeed => jogSpeed;

        public float RemainingDistance
        {
            get
            {
                if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
                {
                    return navMeshAgent.remainingDistance;
                }
                Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatDest = new Vector3(currentDestination.x, 0f, currentDestination.z);
                return Vector3.Distance(flatPos, flatDest);
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            navMeshAgent = GetComponent<NavMeshAgent>();

            // Setup CharacterController sensible defaults if newly added
            if (characterController != null)
            {
                characterController.height = 1.9f;
                characterController.center = new Vector3(0f, 0.95f, 0f);
                characterController.radius = 0.38f;
                characterController.stepOffset = 0.4f;
            }

            // Configure NavMeshAgent if present
            if (navMeshAgent != null)
            {
                navMeshAgent.stoppingDistance = stoppingDistance;
                navMeshAgent.speed = walkSpeed;
                navMeshAgent.angularSpeed = rotationSpeed * 30f;
                navMeshAgent.acceleration = 12f;
            }

            currentDestination = transform.position;
        }

        private void Update()
        {
            // Continuous combat focus rotation
            if (forcedLookTimer > 0f)
            {
                forcedLookTimer -= Time.deltaTime;
                if (forcedLookTarget.HasValue)
                {
                    Vector3 lookDir = forcedLookTarget.Value - transform.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * forcedLookSpeed);
                    }
                }
            }

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.updateRotation = (forcedLookTimer <= 0f);
            }

            if (autoOpenDoors && isMoving)
            {
                CheckForDoorAhead();
            }

            // Fallback kinematic locomotion if NavMesh is not handling it
            if (isMoving && (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh))
            {
                UpdateKinematicMovement();
            }
        }

        public void MoveTo(Vector3 targetPosition, float speed)
        {
            currentDestination = targetPosition;
            currentMoveSpeed = speed;
            isMoving = true;

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = speed;
                navMeshAgent.SetDestination(targetPosition);
            }
        }

        public void Stop()
        {
            isMoving = false;
            currentVelocity = Vector3.zero;

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
            }
        }

        public bool HasReachedDestination(float threshold = 0f)
        {
            if (!isMoving) return true;

            // If physically stuck against a corner or obstacle for > 0.8s, consider destination reached / unstick
            if (isStuck && stuckTimer > 0.8f)
            {
                isMoving = false;
                stuckTimer = 0f;
                isStuck = false;
                return true;
            }

            float tolerance = threshold > 0f ? threshold : stoppingDistance;

            if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= tolerance)
                {
                    return true;
                }
                return false;
            }

            Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatDest = new Vector3(currentDestination.x, 0f, currentDestination.z);
            return Vector3.Distance(flatPos, flatDest) <= tolerance;
        }

        public void LookAt(Vector3 worldPoint, float turnSpeedMultiplier = 1f)
        {
            forcedLookTarget = worldPoint;
            forcedLookSpeed = turnSpeedMultiplier;
            forcedLookTimer = 0.35f;

            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed * turnSpeedMultiplier);
            }
        }

        private void UpdateKinematicMovement()
        {
            Vector3 toDest = currentDestination - transform.position;
            toDest.y = 0f;
            float dist = toDest.magnitude;

            if (dist <= stoppingDistance)
            {
                isMoving = false;
                stuckTimer = 0f;
                isStuck = false;
                return;
            }

            // 1. Stuck tracking over 0.25s interval
            stuckSampleTimer += Time.deltaTime;
            if (stuckSampleTimer >= 0.25f)
            {
                stuckSampleTimer = 0f;
                Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatLast = new Vector3(lastSampledPosition.x, 0f, lastSampledPosition.z);
                float distMoved = Vector3.Distance(flatPos, flatLast);
                lastSampledPosition = transform.position;

                if (isMoving && distMoved < 0.08f)
                {
                    stuckTimer += 0.25f;
                    if (stuckTimer >= 0.5f)
                    {
                        isStuck = true;
                        unstickSideSign = (UnityEngine.Random.value > 0.5f) ? 1f : -1f;
                    }
                }
                else
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - 0.3f);
                    if (stuckTimer <= 0.1f)
                    {
                        isStuck = false;
                    }
                }
            }

            Vector3 moveDir = toDest.normalized;

            // 2. Proactive Obstacle Check (checks ahead for crates, barriers, and walls)
            if (CheckObstacleInDirection(moveDir, obstacleCheckDistance, out RaycastHit centerHit))
            {
                // Test left whisker (-35 deg) and right whisker (+35 deg)
                Vector3 leftDir = Quaternion.Euler(0f, -35f, 0f) * moveDir;
                Vector3 rightDir = Quaternion.Euler(0f, 35f, 0f) * moveDir;

                bool leftBlocked = CheckObstacleInDirection(leftDir, obstacleCheckDistance * 1.1f, out RaycastHit leftHit);
                bool rightBlocked = CheckObstacleInDirection(rightDir, obstacleCheckDistance * 1.1f, out RaycastHit rightHit);

                float leftClearance = leftBlocked ? leftHit.distance : (obstacleCheckDistance * 1.5f);
                float rightClearance = rightBlocked ? rightHit.distance : (obstacleCheckDistance * 1.5f);

                Vector3 steerDir = (leftClearance >= rightClearance) ? leftDir : rightDir;

                // Also calculate surface deflection from obstacle normal
                Vector3 normalTangent = Vector3.ProjectOnPlane(moveDir, centerHit.normal).normalized;
                if (normalTangent.sqrMagnitude < 0.1f)
                {
                    normalTangent = (leftClearance >= rightClearance) ? leftDir : rightDir;
                }

                float steerWeight = Mathf.Clamp01(1f - (centerHit.distance / obstacleCheckDistance));
                moveDir = Vector3.Slerp(moveDir, Vector3.Slerp(steerDir, normalTangent, 0.5f).normalized, Mathf.Lerp(0.5f, 0.85f, steerWeight)).normalized;
            }

            // 3. Unstick routine if blocked against corner
            if (isStuck && stuckTimer > 0.5f)
            {
                moveDir = (-transform.forward * 0.4f + transform.right * unstickSideSign * 0.7f).normalized;
            }

            // Ensure motion is strictly horizontal
            moveDir = new Vector3(moveDir.x, 0f, moveDir.z).normalized;

            // 4. Smooth rotation towards movement direction (only when not locked on combat focus target)
            if (forcedLookTimer <= 0f && moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            // 5. Apply smooth accelerated movement through CharacterController
            Vector3 targetVelocity = moveDir * currentMoveSpeed;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, 14f * Time.deltaTime);

            Vector3 motion = currentVelocity * Time.deltaTime;
            motion.y = -9.81f * Time.deltaTime; // keep grounded
            characterController.Move(motion);
        }

        private bool CheckObstacleInDirection(Vector3 dir, float distance, out RaycastHit hitInfo)
        {
            hitInfo = default;
            float capsuleRadius = characterController != null ? characterController.radius : 0.38f;
            // Probe origin starts outside self capsule at mid-height (0.65m) to clear floor while detecting 1m crates and walls
            Vector3 probeOrigin = transform.position + Vector3.up * 0.65f + dir * (capsuleRadius + 0.12f);

            RaycastHit[] hits = Physics.SphereCastAll(probeOrigin, 0.22f, dir, distance, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            float nearestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;
                // Ignore self
                if (h.collider == characterController || h.collider.transform.root == transform.root) continue;
                // Ignore triggers
                if (h.collider.isTrigger) continue;
                // Ignore Player
                if (h.collider.CompareTag("Player") || h.collider.name == "Player") continue;
                // Ignore ground/floor (horizontal surfaces)
                if (h.normal.y > 0.65f) continue;

                if (h.distance < nearestDist)
                {
                    nearestDist = h.distance;
                    hitInfo = h;
                    found = true;
                }
            }

            return found;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.65f) return;
            lastContactNormal = hit.normal;
            lastContactTime = Time.time;
        }

        private void CheckForDoorAhead()
        {
            doorCheckCooldown -= Time.deltaTime;
            if (doorCheckCooldown > 0f) return;

            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
            if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, doorCheckDistance, ~0, QueryTriggerInteraction.Collide))
            {
                // Check for DoorController
                DoorController doorController = hit.collider.GetComponentInParent<DoorController>();
                if (doorController != null && !doorController.IsOpen)
                {
                    doorController.ToggleDoor();
                    doorCheckCooldown = 1.5f;
                    return;
                }

                // Check for DoorInteractable
                DoorInteractable doorInteractable = hit.collider.GetComponentInParent<DoorInteractable>();
                if (doorInteractable != null && !doorInteractable.IsOpen)
                {
                    doorInteractable.Interact(gameObject);
                    doorCheckCooldown = 1.5f;
                    return;
                }
            }
        }
    }
}
