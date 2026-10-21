using System;
using UnityEngine;

namespace TacticalShooter.AI.Perception
{
    /// <summary>
    /// Shared perception system for all AI agents.
    /// Handles:
    /// - Vision cone (field of view angle + sight distance)
    /// - Physical line of sight raycasting (occlusion checks against walls and obstacles)
    /// - Audio / hearing detection for footfalls and weapon discharges
    /// - Last Known Position (LKP) timestamping and memory retention
    /// </summary>
    public class AIPerception : MonoBehaviour
    {
        [Header("Vision Parameters")]
        [SerializeField] private float viewDistance = 35f;
        [SerializeField] [Range(30f, 180f)] private float fieldOfViewAngle = 125f;
        [SerializeField] private float eyeHeight = 1.6f;
        [SerializeField] private LayerMask obstacleMask = ~0; // All layers except player/ignore raycast by default
        [SerializeField] private string targetTag = "Player";

        [Header("Hearing & Proximity Parameters")]
        [SerializeField] private float hearingRadius = 35f;
        [SerializeField] private float proximityDetectionRadius = 5.0f;

        [Header("Detection Memory")]
        [SerializeField] private float memoryDuration = 8f;

        [Header("Runtime Status")]
        [SerializeField] private bool canSeeTarget;
        [SerializeField] private bool isTargetDetected;
        [SerializeField] private Vector3 lastKnownPosition;
        [SerializeField] private float timeSinceLastSeen = float.MaxValue;
        [SerializeField] private float distanceToTarget = float.MaxValue;
        [SerializeField] private Vector3 directionToTarget = Vector3.forward;

        private Transform targetTransform;

        public bool CanSeeTarget => canSeeTarget;
        public bool IsTargetDetected => isTargetDetected;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public float TimeSinceLastSeen => timeSinceLastSeen;
        public float DistanceToTarget => distanceToTarget;
        public Vector3 DirectionToTarget => directionToTarget;
        public Transform Target => targetTransform;
        public Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        public event Action<Transform> OnTargetSpotted;
        public event Action<Vector3> OnTargetLost;
        public event Action<Vector3> OnNoiseHeard;

        private void Awake()
        {
            // Auto locate player if not assigned
            LocateTarget();

            // Default obstacle mask excludes Ignore Raycast (layer 2)
            if (obstacleMask == ~0 || obstacleMask == 0)
            {
                obstacleMask = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            }
        }

        private void LocateTarget()
        {
            if (targetTransform == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    targetTransform = pc.transform;
                    return;
                }

                var pcc = FindFirstObjectByType<TacticalShooter.Systems.Combat.PlayerCombatController>();
                if (pcc != null)
                {
                    targetTransform = pcc.transform;
                    return;
                }

                GameObject player = GameObject.FindWithTag(targetTag);
                if (player == null)
                {
                    player = GameObject.Find("Player");
                }
                if (player != null)
                {
                    targetTransform = player.transform;
                }
            }
        }

        public void SetTarget(Transform target)
        {
            targetTransform = target;
        }

        public void UpdatePerception()
        {
            if (targetTransform == null)
            {
                LocateTarget();
                if (targetTransform == null)
                {
                    canSeeTarget = false;
                    isTargetDetected = false;
                    return;
                }
            }

            Vector3 eyePos = EyePosition;
            Vector3 targetCenter = targetTransform.position + Vector3.up * 1.2f;
            Vector3 toTarget = targetCenter - eyePos;
            distanceToTarget = toTarget.magnitude;

            if (distanceToTarget > 0.001f)
            {
                directionToTarget = toTarget / distanceToTarget;
            }

            bool wasSeeingTarget = canSeeTarget;
            canSeeTarget = EvaluateVision(eyePos, targetCenter, toTarget, distanceToTarget);

            if (canSeeTarget)
            {
                lastKnownPosition = targetTransform.position;
                timeSinceLastSeen = 0f;
                isTargetDetected = true;

                if (!wasSeeingTarget)
                {
                    OnTargetSpotted?.Invoke(targetTransform);
                }
            }
            else
            {
                timeSinceLastSeen += Time.deltaTime;

                if (wasSeeingTarget)
                {
                    OnTargetLost?.Invoke(lastKnownPosition);
                }

                // If target has been hidden longer than memory duration, lose active detection
                if (timeSinceLastSeen > memoryDuration)
                {
                    isTargetDetected = false;
                }
            }
        }

        private bool EvaluateVision(Vector3 eyePos, Vector3 targetCenter, Vector3 toTarget, float dist)
        {
            // 1. Distance check
            if (dist > viewDistance)
            {
                return false;
            }

            // Close proximity presence check (detects player within proximity radius even from behind if unobstructed)
            bool isCloseProximity = dist <= Mathf.Max(proximityDetectionRadius, 6.0f);

            // 2. Field of view angle check (required if beyond close proximity)
            if (!isCloseProximity)
            {
                // In active alert or combat engagement, AI has 170° combat peripheral awareness
                float effectiveFov = (isTargetDetected || canSeeTarget) ? Mathf.Max(fieldOfViewAngle, 170f) : fieldOfViewAngle;
                float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
                if (angleToTarget > effectiveFov * 0.5f)
                {
                    // Target is outside horizontal vision cone
                    return false;
                }
            }

            // 3. Multi-point Line of Sight Check (Head, Chest, Right Shoulder, Left Shoulder, Hip)
            // Kept strictly within player collider dimensions (0.22m max width) so rays never shoot into empty air outside walls
            Vector3[] targetPoints = new Vector3[]
            {
                targetTransform.position + Vector3.up * 1.60f,                                  // Head
                targetTransform.position + Vector3.up * 1.15f,                                  // Chest center
                targetTransform.position + Vector3.up * 1.15f + targetTransform.right * 0.22f, // Right shoulder
                targetTransform.position + Vector3.up * 1.15f - targetTransform.right * 0.22f, // Left shoulder
                targetTransform.position + Vector3.up * 0.60f                                   // Hip / Legs
            };

            for (int i = 0; i < targetPoints.Length; i++)
            {
                Vector3 pt = targetPoints[i];
                Vector3 toPt = pt - eyePos;
                float ptDist = toPt.magnitude;
                if (ptDist < 0.001f) continue;
                Vector3 ptDir = toPt / ptDist;

                // Start ray just past eye position (avoiding self penetration without skipping close obstacles)
                Vector3 rayStart = eyePos + ptDir * 0.05f;
                float checkDist = ptDist + 0.5f;

                RaycastHit[] hits = Physics.RaycastAll(rayStart, ptDir, checkDist, obstacleMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int h = 0; h < hits.Length; h++)
                {
                    RaycastHit hit = hits[h];
                    if (hit.collider == null) continue;
                    // Ignore self
                    if (hit.collider.transform.root == transform.root) continue;
                    // Ignore triggers
                    if (hit.collider.isTrigger) continue;

                    // If the FIRST solid surface hit is the player -> target is genuinely visible!
                    if (hit.transform == targetTransform || hit.transform.IsChildOf(targetTransform) || hit.transform.root == targetTransform.root)
                    {
                        return true;
                    }

                    // First solid object hit was a wall, door, or cover barrier -> this line is blocked!
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// External stimuli notification: gunshot, footstep, or impact.
        /// </summary>
        public void ReportNoise(Vector3 noiseOrigin, float loudness)
        {
            float dist = Vector3.Distance(transform.position, noiseOrigin);
            if (dist <= hearingRadius * loudness)
            {
                lastKnownPosition = noiseOrigin;
                timeSinceLastSeen = 0f;
                isTargetDetected = true;
                OnNoiseHeard?.Invoke(noiseOrigin);
            }
        }

        /// <summary>
        /// Forces detection (e.g. when attacked from behind).
        /// </summary>
        public void ForceTargetDetection(Vector3 position)
        {
            lastKnownPosition = position;
            timeSinceLastSeen = 0f;
            isTargetDetected = true;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eyePos = Application.isPlaying ? EyePosition : transform.position + Vector3.up * eyeHeight;

            // Draw view distance sphere
            Gizmos.color = canSeeTarget ? new Color(1f, 0.2f, 0.2f, 0.25f) : new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(eyePos, viewDistance);

            // Draw FOV cone lines
            Vector3 fwd = transform.forward;
            Quaternion leftRayRot = Quaternion.AngleAxis(-fieldOfViewAngle * 0.5f, Vector3.up);
            Quaternion rightRayRot = Quaternion.AngleAxis(fieldOfViewAngle * 0.5f, Vector3.up);
            Vector3 leftRay = leftRayRot * fwd;
            Vector3 rightRay = rightRayRot * fwd;

            Gizmos.color = canSeeTarget ? Color.red : Color.cyan;
            Gizmos.DrawRay(eyePos, leftRay * viewDistance);
            Gizmos.DrawRay(eyePos, rightRay * viewDistance);

            if (isTargetDetected)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(lastKnownPosition, 0.8f);
            }
        }
    }
}
