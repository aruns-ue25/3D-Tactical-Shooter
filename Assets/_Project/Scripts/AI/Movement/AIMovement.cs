using System;
using UnityEngine;
using UnityEngine.AI;
using TacticalShooter.Environment;
using TacticalShooter.Systems.Interaction;

namespace TacticalShooter.AI.Movement
{
    /// <summary>
    /// Shared navigation and locomotion controller for all AI agents (Guard, Hunter, Flanker).
    /// Provides unified hybrid navigation:
    /// 1. Uses UnityEngine.AI.NavMeshAgent for pathfinding & steering when NavMesh is active.
    /// 2. Executes all physical motion exclusively through CharacterController.Move() to avoid transform fighting.
    /// 3. Maintains simulation position synchronization and drift recovery (warp).
    /// 4. Provides autonomous kinematic raycast steering fallback when NavMesh data is unavailable.
    /// 5. Collaborates with door systems to automatically open doors in transit.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class AIMovement : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 2.2f;
        [SerializeField] private float jogSpeed = 3.8f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float stoppingDistance = 1.0f;

        [Header("Gravity & Grounding")]
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float groundedGravity = 1.5f;
        [SerializeField] private float terminalVelocity = 20.0f;

        [Header("NavMesh Synchronization")]
        [SerializeField] private float warpRecoveryThreshold = 1.25f;
        [SerializeField] private float navMeshSamplingRadius = 2.5f;

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
        private float verticalVelocity = 0f;
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
        public NavMeshAgent Agent => navMeshAgent;
        public bool IsOnNavMesh => navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh;

        public float RemainingDistance
        {
            get
            {
                if (IsOnNavMesh)
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
            if (characterController != null)
            {
                characterController.height = 1.9f;
                characterController.center = new Vector3(0f, 0.95f, 0f);
                characterController.radius = 0.38f;
                characterController.stepOffset = 0.4f;
            }

            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
            }
            ConfigureNavMeshAgent();

            currentDestination = transform.position;
            lastSampledPosition = transform.position;
        }

        /// <summary>
        /// Applies consistent NavMeshAgent settings across all agents.
        /// Disables automatic position and rotation driving to let CharacterController
        /// be the single source of truth for motion and physics.
        /// </summary>
        public void ConfigureNavMeshAgent()
        {
            if (navMeshAgent == null) return;

            float r = characterController != null ? characterController.radius : 0.38f;
            float h = characterController != null ? characterController.height : 1.9f;

            navMeshAgent.radius = r;
            navMeshAgent.height = h;
            navMeshAgent.baseOffset = 0f;
            navMeshAgent.stoppingDistance = stoppingDistance;
            navMeshAgent.speed = walkSpeed;
            navMeshAgent.angularSpeed = rotationSpeed * 30f;
            navMeshAgent.acceleration = 14f;
            navMeshAgent.autoRepath = true;
            navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // CharacterController executes translation exclusively
            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;
            navMeshAgent.updateUpAxis = false;
        }

        private void Update()
        {
            // 1. Vertical velocity tracking (ground-clamped to prevent unbounded accumulation)
            if (characterController != null && characterController.isGrounded)
            {
                verticalVelocity = -groundedGravity;
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity - gravity * Time.deltaTime, -terminalVelocity);
            }

            // 2. Dynamic combat look target (overrides movement facing during aiming)
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

            // 3. Proactive door opening while in transit
            if (autoOpenDoors && isMoving)
            {
                CheckForDoorAhead();
            }

            // 4. Locomotion execution
            if (isMoving)
            {
                if (IsOnNavMesh)
                {
                    UpdateNavMeshLocomotion();
                }
                else
                {
                    UpdateKinematicMovement();
                }
            }
            else
            {
                // Apply gravity while stationary
                if (characterController != null && !characterController.isGrounded)
                {
                    characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
                }
                currentVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Hybrid NavMesh locomotion: reads desiredVelocity from NavMeshAgent,
        /// moves through CharacterController.Move, and synchronizes agent.nextPosition.
        /// </summary>
        private void UpdateNavMeshLocomotion()
        {
            if (navMeshAgent == null || !navMeshAgent.isOnNavMesh) return;

            // Check if destination reached
            if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= stoppingDistance)
            {
                Stop();
                return;
            }

            // Anti-stuck tracking in NavMesh mode
            stuckSampleTimer += Time.deltaTime;
            if (stuckSampleTimer >= 0.25f)
            {
                stuckSampleTimer = 0f;
                Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
                Vector3 flatLast = new Vector3(lastSampledPosition.x, 0f, lastSampledPosition.z);
                float distMoved = Vector3.Distance(flatPos, flatLast);
                lastSampledPosition = transform.position;

                if (distMoved < 0.06f && navMeshAgent.desiredVelocity.sqrMagnitude > 0.1f)
                {
                    stuckTimer += 0.25f;
                    if (stuckTimer >= 0.5f) isStuck = true;
                }
                else
                {
                    stuckTimer = Mathf.Max(0f, stuckTimer - 0.3f);
                    if (stuckTimer <= 0.1f) isStuck = false;
                }
            }

            Vector3 desired = navMeshAgent.desiredVelocity;
            desired.y = 0f;

            // Accelerate smoothly towards desired velocity
            currentVelocity = Vector3.MoveTowards(currentVelocity, desired, 16f * Time.deltaTime);

            // Execute translation through CharacterController
            Vector3 motion = currentVelocity * Time.deltaTime;
            motion.y = verticalVelocity * Time.deltaTime;
            characterController.Move(motion);

            // Smooth rotation towards movement direction when not locked on combat aim
            if (forcedLookTimer <= 0f && currentVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(currentVelocity.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            // Synchronize NavMeshAgent simulation position with CharacterController
            float drift = Vector3.Distance(transform.position, navMeshAgent.nextPosition);
            if (drift > warpRecoveryThreshold)
            {
                navMeshAgent.Warp(transform.position);
            }
            else
            {
                navMeshAgent.nextPosition = transform.position;
            }
        }

        public void MoveTo(Vector3 targetPosition, float speed)
        {
            currentDestination = targetPosition;
            currentMoveSpeed = speed;
            isMoving = true;
            stuckTimer = 0f;
            isStuck = false;

            EnsureAgentOnNavMesh();

            if (IsOnNavMesh)
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = speed;
                navMeshAgent.SetDestination(targetPosition);
            }
        }

        /// <summary>
        /// Validates reachability and begins movement if a valid destination is found.
        /// Returns true if a path is established, or false if unreachable.
        /// </summary>
        public bool TryMoveTo(Vector3 targetPosition, float speed)
        {
            if (IsDestinationReachable(targetPosition, out Vector3 reachablePoint))
            {
                MoveTo(reachablePoint, speed);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Mode-aware path reachability check:
        /// On NavMesh: requires a complete, valid route (status == PathComplete).
        /// Off NavMesh: validates straight line clearance via kinematic probe.
        /// </summary>
        public bool IsDestinationReachable(Vector3 targetPosition, out Vector3 reachablePoint)
        {
            reachablePoint = targetPosition;
            EnsureAgentOnNavMesh();

            if (IsOnNavMesh)
            {
                if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 3.0f, navMeshAgent.areaMask))
                {
                    return false;
                }

                Vector3 startPos = transform.position;
                if (NavMesh.SamplePosition(startPos, out NavMeshHit startHit, 3.0f, navMeshAgent.areaMask))
                {
                    startPos = startHit.position;
                }

                NavMeshPath path = new NavMeshPath();
                if (!NavMesh.CalculatePath(startPos, hit.position, navMeshAgent.areaMask, path))
                {
                    return false;
                }

                if (path.status != NavMeshPathStatus.PathComplete || path.corners == null || path.corners.Length == 0)
                {
                    if (path.corners != null && path.corners.Length > 0)
                    {
                        reachablePoint = path.corners[path.corners.Length - 1];
                    }
                    return false;
                }

                reachablePoint = hit.position;
                return true;
            }

            // Kinematic fallback mode: test straight-line clearance against static obstacles
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist < 0.1f) return true;
            Vector3 dir = toTarget / dist;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.65f, 0.35f, dir, out RaycastHit hitInfo, dist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hitInfo.collider != null && !hitInfo.collider.isTrigger && hitInfo.collider.transform.root != transform.root)
                {
                    reachablePoint = hitInfo.point - dir * 0.5f;
                    return false;
                }
            }
            return true;
        }

        public void Stop()
        {
            isMoving = false;
            currentVelocity = Vector3.zero;

            if (IsOnNavMesh)
            {
                navMeshAgent.isStopped = true;
                navMeshAgent.ResetPath();
            }
        }

        public bool HasReachedDestination(float threshold = 0f)
        {
            if (!isMoving) return true;

            // If physically stuck for > 0.8s, consider destination reached to enable state recovery
            if (isStuck && stuckTimer > 0.8f)
            {
                isMoving = false;
                stuckTimer = 0f;
                isStuck = false;
                return true;
            }

            float tolerance = threshold > 0f ? threshold : stoppingDistance;

            if (IsOnNavMesh)
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

        /// <summary>
        /// Warps the NavMeshAgent onto the nearest active NavMesh surface if currently unplaced.
        /// </summary>
        public bool EnsureAgentOnNavMesh(float searchRadius = 2.5f)
        {
            if (navMeshAgent == null || !navMeshAgent.enabled) return false;
            if (navMeshAgent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                return navMeshAgent.Warp(hit.position);
            }
            return false;
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

            // 2. Obstacle checking & steering
            if (CheckObstacleInDirection(moveDir, obstacleCheckDistance, out RaycastHit centerHit))
            {
                Vector3 leftDir = Quaternion.Euler(0f, -35f, 0f) * moveDir;
                Vector3 rightDir = Quaternion.Euler(0f, 35f, 0f) * moveDir;

                bool leftBlocked = CheckObstacleInDirection(leftDir, obstacleCheckDistance * 1.1f, out RaycastHit leftHit);
                bool rightBlocked = CheckObstacleInDirection(rightDir, obstacleCheckDistance * 1.1f, out RaycastHit rightHit);

                float leftClearance = leftBlocked ? leftHit.distance : (obstacleCheckDistance * 1.5f);
                float rightClearance = rightBlocked ? rightHit.distance : (obstacleCheckDistance * 1.5f);

                Vector3 steerDir = (leftClearance >= rightClearance) ? leftDir : rightDir;
                Vector3 normalTangent = Vector3.ProjectOnPlane(moveDir, centerHit.normal).normalized;
                if (normalTangent.sqrMagnitude < 0.1f)
                {
                    normalTangent = (leftClearance >= rightClearance) ? leftDir : rightDir;
                }

                float steerWeight = Mathf.Clamp01(1f - (centerHit.distance / obstacleCheckDistance));
                moveDir = Vector3.Slerp(moveDir, Vector3.Slerp(steerDir, normalTangent, 0.5f).normalized, Mathf.Lerp(0.5f, 0.85f, steerWeight)).normalized;
            }

            // 3. Unstick routine
            if (isStuck && stuckTimer > 0.5f)
            {
                moveDir = (-transform.forward * 0.4f + transform.right * unstickSideSign * 0.7f).normalized;
            }

            moveDir = new Vector3(moveDir.x, 0f, moveDir.z).normalized;

            // 4. Smooth rotation
            if (forcedLookTimer <= 0f && moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            // 5. Apply smooth movement with grounded vertical velocity
            Vector3 targetVelocity = moveDir * currentMoveSpeed;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, 14f * Time.deltaTime);

            Vector3 motion = currentVelocity * Time.deltaTime;
            motion.y = verticalVelocity * Time.deltaTime;
            characterController.Move(motion);
        }

        private bool CheckObstacleInDirection(Vector3 dir, float distance, out RaycastHit hitInfo)
        {
            hitInfo = default;
            float capsuleRadius = characterController != null ? characterController.radius : 0.38f;
            Vector3 probeOrigin = transform.position + Vector3.up * 0.65f + dir * (capsuleRadius + 0.12f);

            RaycastHit[] hits = Physics.SphereCastAll(probeOrigin, 0.22f, dir, distance, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            float nearestDist = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null) continue;
                if (h.collider == characterController || h.collider.transform.root == transform.root) continue;
                if (h.collider.isTrigger) continue;
                if (h.collider.CompareTag("Player") || h.collider.name == "Player") continue;
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
                DoorController doorController = hit.collider.GetComponentInParent<DoorController>();
                if (doorController != null && !doorController.IsOpen)
                {
                    doorController.ToggleDoor();
                    doorCheckCooldown = 1.5f;
                    return;
                }

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
