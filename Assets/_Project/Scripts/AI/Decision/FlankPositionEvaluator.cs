using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalShooter.AI.Decision
{
    /// <summary>
    /// Evaluates and scores flanking positions around a target or Last Known Position (LKP).
    /// Used by the Stealth / Flanker autonomous agent to select tactically advantageous
    /// approach routes (sides, rear, perpendicular angles) while avoiding direct frontal approaches.
    ///
    /// Scoring factors:
    /// 1. Angle of Approach (Flank angle): Favors lateral (90 deg) and rear (180 deg) angles relative to target facing or line-of-approach.
    /// 2. Line of Fire / Attack Advantage: Favors points with clean line of sight/fire to target.
    /// 3. Exposure to Target: Penalizes points or route segments excessively open to the player's view before reaching the flank.
    /// 4. Path / Travel Cost: Prefers shorter, reachable detours; penalizes extreme travel distances.
    /// 5. Reachability / Physical Clearance: Rejects positions embedded in walls or outside navigation boundaries.
    /// </summary>
    public static class FlankPositionEvaluator
    {
        public struct FlankCandidateResult
        {
            public Vector3 Position;
            public float TotalScore;
            public float AngleScore;
            public float LineOfSightScore;
            public float ExposurePenalty;
            public float DistanceScore;
            public float PathCost;
            public bool IsReachable;
            public float FlankAngleDegrees;
        }

        /// <summary>
        /// Generates and scores candidate flank positions around the target / LKP.
        /// </summary>
        /// <param name="agentPos">Current position of the flanker agent.</param>
        /// <param name="targetPos">Current or last known position of the target.</param>
        /// <param name="targetForward">Forward facing vector of the target (or vector from agent to target if facing unknown).</param>
        /// <param name="minFlankDist">Minimum desired combat standoff distance from target.</param>
        /// <param name="maxFlankDist">Maximum desired combat standoff distance from target.</param>
        /// <param name="obstacleMask">LayerMask for walls, props, and cover obstacles.</param>
        /// <param name="candidateAngleSteps">Number of angular samples around target (e.g. 12 = every 30 deg).</param>
        /// <param name="distanceRings">Number of distance rings (e.g. 2 = min and max distances).</param>
        /// <param name="preferLeftOrRight">Optional bias (-1 for left, +1 for right, 0 for neutral).</param>
        /// <returns>Scored list of candidates sorted by TotalScore descending.</returns>
        public static List<FlankCandidateResult> EvaluateFlankCandidates(
            Vector3 agentPos,
            Vector3 targetPos,
            Vector3 targetForward,
            float minFlankDist,
            float maxFlankDist,
            LayerMask obstacleMask,
            int candidateAngleSteps = 12,
            int distanceRings = 2,
            float preferLeftOrRight = 0f)
        {
            List<FlankCandidateResult> results = new List<FlankCandidateResult>();

            if (candidateAngleSteps < 4) candidateAngleSteps = 8;
            if (targetForward.sqrMagnitude < 0.01f)
            {
                targetForward = (targetPos - agentPos).normalized;
                if (targetForward.sqrMagnitude < 0.01f) targetForward = Vector3.forward;
            }
            else
            {
                targetForward.y = 0f;
                targetForward.Normalize();
            }

            Vector3 agentToTarget = (targetPos - agentPos);
            agentToTarget.y = 0f;
            Vector3 approachDir = agentToTarget.normalized;
            Vector3 approachLateral = Vector3.Cross(approachDir, Vector3.up).normalized;

            float angleStep = 360f / candidateAngleSteps;

            for (int r = 0; r < distanceRings; r++)
            {
                float t = distanceRings > 1 ? (float)r / (distanceRings - 1) : 0.5f;
                float ringDist = Mathf.Lerp(minFlankDist, maxFlankDist, t);

                for (int i = 0; i < candidateAngleSteps; i++)
                {
                    float angleDeg = i * angleStep;
                    // Angles relative to target forward vector: 0 = in front of target, 90 = right flank, 180 = rear, 270 = left flank
                    Quaternion rot = Quaternion.Euler(0f, angleDeg, 0f);
                    Vector3 offsetDir = rot * targetForward;
                    Vector3 candidatePos = targetPos + offsetDir * ringDist;
                    candidatePos.y = agentPos.y; // Keep level with ground

                    // --- Reachability & Clearance Check ---
                    // 1. Check if candidate point is inside a wall or obstacle collider
                    if (Physics.CheckSphere(candidatePos + Vector3.up * 0.9f, 0.42f, obstacleMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    // 2. Validate with NavMesh if baked/available
                    float navPathCost = 0f;
                    bool isNavMeshValid = true;
                    if (NavMesh.SamplePosition(candidatePos, out NavMeshHit navHit, 1.2f, NavMesh.AllAreas))
                    {
                        candidatePos = navHit.position;
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(agentPos, candidatePos, NavMesh.AllAreas, path))
                        {
                            if (path.status == NavMeshPathStatus.PathComplete)
                            {
                                // Calculate total path distance
                                float totalLen = 0f;
                                for (int p = 1; p < path.corners.Length; p++)
                                {
                                    totalLen += Vector3.Distance(path.corners[p - 1], path.corners[p]);
                                }
                                navPathCost = totalLen;
                            }
                            else if (path.status == NavMeshPathStatus.PathPartial)
                            {
                                // Partial path: penalize heavily but allow if no complete path
                                navPathCost = Vector3.Distance(agentPos, candidatePos) * 2.5f;
                            }
                            else
                            {
                                isNavMeshValid = false;
                            }
                        }
                    }
                    else
                    {
                        // NavMesh not baked or sampled: estimate path cost via straight Euclidean distance with obstacle checks
                        navPathCost = Vector3.Distance(agentPos, candidatePos);
                    }

                    if (!isNavMeshValid) continue;

                    // --- Scoring Factors ---

                    // 1. Approach Angle Score (Avoid frontal approach)
                    // Angle relative to direct frontal vector from agent to target
                    Vector3 agentToCandidate = (candidatePos - agentPos);
                    agentToCandidate.y = 0f;
                    Vector3 candDir = agentToCandidate.normalized;
                    float dotDirectApproach = Vector3.Dot(candDir, approachDir); // 1 = straight ahead directly into target
                    
                    // Angle relative to target's view (targetForward)
                    // If candidate is behind player (dot < 0), flank bonus is maximized.
                    float dotTargetForward = Vector3.Dot(offsetDir, targetForward); // 1 = in front of target, -1 = behind target
                    
                    // Angle score: strongly penalize directly approaching from the front (dotDirectApproach > 0.85)
                    // Highly reward rear (-1) and sides (dot ~ 0)
                    float angleScore = 0f;
                    if (dotTargetForward < -0.2f)
                    {
                        // Behind target (rear flank)
                        angleScore += 35f * (-dotTargetForward);
                    }
                    else if (Mathf.Abs(dotTargetForward) <= 0.6f)
                    {
                        // Lateral / 90 degree sides
                        angleScore += 25f;
                    }
                    else
                    {
                        // In front of target: heavy penalty
                        angleScore -= 30f * dotTargetForward;
                    }

                    // Penalize straight frontal charge down current corridor
                    if (dotDirectApproach > 0.85f)
                    {
                        angleScore -= 25f;
                    }

                    // 2. Lateral / side preference bias (hysteresis helper)
                    if (Mathf.Abs(preferLeftOrRight) > 0.01f)
                    {
                        float sideDot = Vector3.Dot(offsetDir, approachLateral); // >0 right, <0 left
                        if (Mathf.Sign(sideDot) == Mathf.Sign(preferLeftOrRight))
                        {
                            angleScore += 6.0f; // Small bias to prevent ping-pong oscillation
                        }
                    }

                    // 3. Line of Fire & Attack Advantage from candidate to target
                    Vector3 eyeCand = candidatePos + Vector3.up * 1.5f;
                    Vector3 eyeTarget = targetPos + Vector3.up * 1.3f;
                    bool hasLoS = !Physics.Linecast(eyeCand, eyeTarget, obstacleMask, QueryTriggerInteraction.Ignore);
                    float losScore = hasLoS ? 25f : -15f; // Must be able to shoot upon reaching destination

                    // 4. Exposure Penalty during travel
                    // Does a direct line between agent and candidate pass right in front of the target?
                    Vector3 midPoint = (agentPos + candidatePos) * 0.5f;
                    float distMidToTarget = Vector3.Distance(midPoint, targetPos);
                    float exposurePenalty = 0f;
                    if (distMidToTarget < minFlankDist * 0.65f)
                    {
                        exposurePenalty = -15f; // Route cuts dangerously close across the target's face
                    }

                    // 5. Travel Distance / Path Cost
                    // Penalize extreme detours that take too long
                    float travelCost = (navPathCost > 0f ? navPathCost : Vector3.Distance(agentPos, candidatePos)) * 0.75f;

                    float totalScore = angleScore + losScore + exposurePenalty - travelCost;

                    FlankCandidateResult result = new FlankCandidateResult
                    {
                        Position = candidatePos,
                        TotalScore = totalScore,
                        AngleScore = angleScore,
                        LineOfSightScore = losScore,
                        ExposurePenalty = exposurePenalty,
                        DistanceScore = -travelCost,
                        PathCost = navPathCost,
                        IsReachable = true,
                        FlankAngleDegrees = angleDeg
                    };

                    results.Add(result);
                }
            }

            // Sort by TotalScore descending
            results.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));
            return results;
        }
    }
}
