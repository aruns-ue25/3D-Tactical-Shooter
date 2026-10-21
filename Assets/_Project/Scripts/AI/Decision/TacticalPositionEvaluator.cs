using System.Collections.Generic;
using UnityEngine;

namespace TacticalShooter.AI.Decision
{
    /// <summary>
    /// Evaluates defensive positions and cover points for tactical agents.
    /// Balances:
    /// 1. Defense zone constraint (stays within guard anchor radius).
    /// 2. Line of sight and engagement distance to target.
    /// 3. Physical obstacle cover to shield against incoming fire.
    /// </summary>
    public static class TacticalPositionEvaluator
    {
        /// <summary>
        /// Finds the best defensive repositioning point.
        /// </summary>
        /// <param name="agentPos">Current agent position.</param>
        /// <param name="targetPos">Player / target position.</param>
        /// <param name="guardAnchor">Guard post origin.</param>
        /// <param name="maxLeashRadius">Maximum allowed distance from guard anchor.</param>
        /// <param name="optimalCombatDistance">Preferred engagement distance.</param>
        /// <param name="obstacleMask">LayerMask for walls and cover blocks.</param>
        /// <returns>Evaluated defensive position.</returns>
        public static Vector3 FindDefensivePosition(
            Vector3 agentPos,
            Vector3 targetPos,
            Vector3 guardAnchor,
            float maxLeashRadius,
            float optimalCombatDistance,
            LayerMask obstacleMask)
        {
            Vector3 bestPos = agentPos;
            float bestScore = float.MinValue;

            // Generate candidate sample offsets around agent & anchor
            List<Vector3> candidates = new List<Vector3>();

            // Direct fallback: step toward anchor or laterally
            Vector3 toTarget = (targetPos - agentPos).normalized;
            Vector3 lateral = Vector3.Cross(toTarget, Vector3.up).normalized;

            candidates.Add(guardAnchor);
            candidates.Add(agentPos + lateral * 3.5f);
            candidates.Add(agentPos - lateral * 3.5f);
            candidates.Add(agentPos - toTarget * 3.0f); // Fall back slightly
            candidates.Add(agentPos + toTarget * 2.5f); // Move forward slightly

            // Scan nearby colliders for cover objects tagged or named "Cover" / "Crate"
            Collider[] nearbyHits = Physics.OverlapSphere(agentPos, 10f, obstacleMask, QueryTriggerInteraction.Ignore);
            foreach (Collider col in nearbyHits)
            {
                // Test positions behind the obstacle relative to target
                Vector3 colCenter = col.bounds.center;
                Vector3 awayFromTarget = (colCenter - targetPos).normalized;
                Vector3 coverSpot = colCenter + awayFromTarget * (col.bounds.extents.magnitude + 0.8f);
                coverSpot.y = agentPos.y;
                candidates.Add(coverSpot);
            }

            foreach (Vector3 cand in candidates)
            {
                // 1. Must stay within guard leash
                float distToAnchor = Vector3.Distance(cand, guardAnchor);
                if (distToAnchor > maxLeashRadius)
                {
                    continue;
                }

                // 2. Reject candidate if inside physical obstacle or wall collider
                if (Physics.CheckSphere(cand + Vector3.up * 0.9f, 0.42f, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // 3. Score candidate
                float distToTarget = Vector3.Distance(cand, targetPos);
                float distPenalty = -Mathf.Abs(distToTarget - optimalCombatDistance);

                // Check Line of Sight from candidate eye to target
                Vector3 eyeCand = cand + Vector3.up * 1.6f;
                Vector3 eyeTarget = targetPos + Vector3.up * 1.5f;
                bool hasLoS = !Physics.Linecast(eyeCand, eyeTarget, obstacleMask, QueryTriggerInteraction.Ignore);

                float score = distPenalty;
                if (hasLoS)
                {
                    // Sightline maintained is valued for tactical guard
                    score += 15f;
                }
                else
                {
                    // Complete cover is good for reloading/recovering
                    score += 5f;
                }

                // Prefer positions closer to current position (less unnecessary travel)
                score -= Vector3.Distance(agentPos, cand) * 0.8f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = cand;
                }
            }

            return bestPos;
        }
    }
}
