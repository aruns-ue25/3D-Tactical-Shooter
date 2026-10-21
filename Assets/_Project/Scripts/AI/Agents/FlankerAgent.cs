using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Perception;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Decision;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    /// <summary>
    /// Stealth / Flanker Autonomous Combat Agent.
    /// Fulfills the Intelligent Systems (IS) assignment requirement.
    ///
    /// Signature Flanker Behavior Loop:
    /// 1. Patrol or Hold: Maintains quiet surveillance around assigned perimeter/patrol route.
    /// 2. Detect & Assess: When perception detects player or receives gunshot noise, assesses target position and facing.
    /// 3. Select Flank: Uses FlankPositionEvaluator to generate multi-angle candidate approach positions
    ///    (sides, rear, perpendicular vectors) and utility-scores them against approach angle, line of fire,
    ///    exposure, and path cost. Rejects direct frontal paths.
    /// 4. Move to Flank: Travels to the chosen flank position while suppressing frontal charge.
    /// 5. Engage: When reaching flank position with clear line of fire, faces and attacks player with tactical rifle fire.
    /// 6. Replan: Evaluates conditions periodically; replans if route blocked, player moved significantly,
    ///    or line of sight is broken. Employs hysteresis threshold to avoid side-to-side flip-flop.
    /// 7. Investigate: If line of sight lost, travels strictly to Last Known Position (LKP) and conducts search;
    ///    never cheats with live player transform.
    /// 8. Return to Post: Returns to patrol route if search concludes without reacquisition.
    /// 9. Defeated: Ceases locomotion and firing upon health depletion.
    /// </summary>
    public class FlankerAgent : AIAgentBase
    {
        public FlankerAgent()
        {
            agentRole = AgentRole.Flanker;
            agentDisplayName = "Stealth / Flanker Agent";
        }

        [Header("Flanker Tactical Settings")]
        [SerializeField] private Vector3 homeBasePosition;
        [SerializeField] private Vector3[] patrolWaypoints;
        [SerializeField] private float waypointWaitTime = 1.5f;

        [Header("Flanking Evaluation")]
        [SerializeField] private float minFlankDistance = 8.0f;
        [SerializeField] private float maxFlankDistance = 35.0f; // Can move past assigned locations to flank anywhere on floor
        [SerializeField] private int candidateAngleSamples = 12; // 30-degree increments
        [SerializeField] private float replanInterval = 1.8f;     // Avoid per-frame re-pathing
        [SerializeField] private float replanMovementThreshold = 3.5f; // Player must move > 3.5m to force replan
        [SerializeField] private float scoreHysteresis = 8.0f;    // Minimum score improvement to switch flank sides
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Combat & Engagement (Sniper / Marksman)")]
        [SerializeField] private float attackRange = 42.0f; // Long ranged shooting
        [SerializeField] private float fireRate = 0.2f;
        [SerializeField] private int burstCount = 1; // Strictly single-shot sniper rifle
        [SerializeField] private float burstCooldown = 3.2f; // Deliberate bolt-action cooldown, strictly NO rapid fire
        [SerializeField] private float damagePerShot = 45.0f; // High damage sniper DMR (45 HP per shot)
        [SerializeField] private float searchDuration = 5.5f;

        [Header("Movement Speeds")]
        [SerializeField] private float patrolSpeed = 2.0f;
        [SerializeField] private float flankSpeed = 3.6f;
        [SerializeField] private float sprintSpeed = 4.4f;

        [Header("Visuals & Presentation")]
        [SerializeField] private Color stealthArmorColor = new Color(0.18f, 0.20f, 0.26f); // Midnight shadow blue/slate
        [SerializeField] private bool showOverheadHUD = true;
        [SerializeField] private float overheadHudHeight = 2.3f;

        // Flanker private phase state
        public enum FlankerPhase
        {
            Patrol,
            MovingToFlank,
            EngagingAtFlank,
            SearchingLKP,
            ReturningHome
        }

        [SerializeField] private FlankerPhase currentPhase = FlankerPhase.Patrol;

        // Runtime Tracking
        private int currentWaypointIndex = 0;
        private float waypointTimer = 0f;
        private Vector3 selectedFlankDestination;
        private float lastReplanTime = -10f;
        private Vector3 targetPosAtLastPlan = Vector3.zero;
        private float currentPlanScore = float.MinValue;
        private float sideBias = 0f; // -1 for left, +1 for right, prevents oscillating
        private float searchTimer = 0f;
        private Vector3 currentSearchPoint;
        private bool isSearchingAtPoint = false;

        // Weapon & Combat
        private float nextBurstTime = 0f;
        private bool isBurstFiring = false;
        private Transform weaponMuzzle;
        private AudioSource audioSource;
        private LineRenderer tracerLine;
        private RoyalGuardVisuals visuals;

        // HUD Textures
        private Texture2D hudBarBgTex;
        private Texture2D hudBarFillTex;

        public FlankerPhase CurrentPhase => currentPhase;
        public Vector3 SelectedFlankDestination => selectedFlankDestination;
        public Vector3 HomeBase => homeBasePosition;

        public Transform TargetTransform
        {
            get
            {
                if (perception != null && perception.Target != null) return perception.Target;
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc != null) return pc.transform;
                var pcc = FindFirstObjectByType<TacticalShooter.Systems.Combat.PlayerCombatController>();
                if (pcc != null) return pcc.transform;
                return null;
            }
        }

        protected override void Awake()
        {
            agentRole = AgentRole.Flanker;
            agentDisplayName = "Flanker (Ghost Marksman)";
            damagePerShot = 45.0f;
            burstCount = 1;
            burstCooldown = 3.2f;
            base.Awake();
        }

        protected override void InitializeAgent()
        {
            agentRole = AgentRole.Flanker;
            agentDisplayName = "Flanker (Ghost Marksman)";
            damagePerShot = 45.0f;
            burstCount = 1;
            burstCooldown = 3.2f;

            if (homeBasePosition == Vector3.zero)
            {
                homeBasePosition = transform.position;
            }

            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                patrolWaypoints = new Vector3[]
                {
                    homeBasePosition + new Vector3(0f, 0f, 3.5f),
                    homeBasePosition + new Vector3(4.0f, 0f, 3.5f),
                    homeBasePosition + new Vector3(4.0f, 0f, -3.5f),
                    homeBasePosition + new Vector3(0f, 0f, -3.5f)
                };
            }

            if (obstacleMask == ~0 || obstacleMask == 0)
            {
                obstacleMask = ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
            }

            SetupVisualsAndWeapon();
            SetupAudioAndTracer();

            if (perception != null)
            {
                perception.OnNoiseHeard += HandleNoiseHeard;
                perception.OnTargetSpotted += HandleTargetSpotted;
            }

            ChangeAlertness(AlertLevel.Unaware);
            ChangeState(AgentState.Patrol);
            currentPhase = FlankerPhase.Patrol;

            if (patrolWaypoints.Length > 0 && movement != null)
            {
                movement.MoveTo(patrolWaypoints[currentWaypointIndex], patrolSpeed);
            }
        }

        protected override void UpdateDecision()
        {
            if (!IsAlive) return;

            switch (currentState)
            {
                case AgentState.Idle:
                case AgentState.Patrol:
                    HandlePatrolDecision();
                    break;

                case AgentState.Reposition:
                    HandleFlankMovementDecision();
                    break;

                case AgentState.Engage:
                    HandleEngageDecision();
                    break;

                case AgentState.Investigate:
                    HandleInvestigateDecision();
                    break;

                case AgentState.ReturnToPost:
                    HandleReturnHomeDecision();
                    break;

                case AgentState.Defeated:
                    break;
            }
        }

        #region Phase 1: Patrol / Hold

        private void HandlePatrolDecision()
        {
            // If perception spots player, immediately initiate flank maneuver!
            if (perception != null && perception.CanSeeTarget)
            {
                ChangeAlertness(AlertLevel.Alert);
                InitiateFlankSelection(perception.LastKnownPosition, GetTargetForward());
                return;
            }

            currentPhase = FlankerPhase.Patrol;

            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                movement.Stop();
                return;
            }

            if (movement.HasReachedDestination(1.2f))
            {
                waypointTimer += Time.deltaTime;
                if (waypointTimer >= waypointWaitTime)
                {
                    waypointTimer = 0f;
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                    movement.MoveTo(patrolWaypoints[currentWaypointIndex], patrolSpeed);
                }
            }
        }

        #endregion

        #region Phase 2 & 3: Flank Selection & Movement

        /// <summary>
        /// Selects an advantageous flank approach position and begins transit.
        /// </summary>
        public bool InitiateFlankSelection(Vector3 targetCenter, Vector3 targetFacing, bool forceReplan = false)
        {
            // Check replan rate limit
            if (!forceReplan && Time.time - lastReplanTime < replanInterval)
            {
                return false;
            }

            // Floor tether check: Flanker operates strictly on its assigned floor level
            float verticalTargetDiff = Mathf.Abs(targetCenter.y - homeBasePosition.y);
            if (verticalTargetDiff > 1.8f)
            {
                selectedFlankDestination = homeBasePosition;
                if (movement != null) movement.MoveTo(homeBasePosition, patrolSpeed);
                return false;
            }

            List<FlankPositionEvaluator.FlankCandidateResult> candidates = FlankPositionEvaluator.EvaluateFlankCandidates(
                transform.position,
                targetCenter,
                targetFacing,
                minFlankDistance,
                maxFlankDistance,
                obstacleMask,
                candidateAngleSamples,
                distanceRings: 2,
                preferLeftOrRight: sideBias
            );

            bool foundReachableCandidate = false;
            FlankPositionEvaluator.FlankCandidateResult selectedCandidate = default;
            Vector3 targetReachablePos = Vector3.zero;

            if (candidates != null && candidates.Count > 0)
            {
                // Iterate through multi-angle candidates ordered by tactical score and select the highest ranked reachable point
                for (int i = 0; i < candidates.Count; i++)
                {
                    FlankPositionEvaluator.FlankCandidateResult cand = candidates[i];
                    // Restrict flank candidates strictly to the Flanker's assigned floor level
                    if (Mathf.Abs(cand.Position.y - homeBasePosition.y) > 1.8f) continue;
                    if (movement != null && movement.IsDestinationReachable(cand.Position, out Vector3 reachablePoint))
                    {
                        selectedCandidate = cand;
                        targetReachablePos = reachablePoint;
                        foundReachableCandidate = true;
                        break;
                    }
                }
            }

            if (!foundReachableCandidate)
            {
                // Fallback: If no candidate has a complete route, test lateral deflection or LKP
                Vector3 toTarget = (targetCenter - transform.position).normalized;
                Vector3 lateral = Vector3.Cross(toTarget, Vector3.up).normalized * (sideBias >= 0 ? 1f : -1f);
                Vector3 fallbackDest = transform.position + lateral * 5f;
                if (movement != null && movement.IsDestinationReachable(fallbackDest, out Vector3 reachableFallback))
                {
                    selectedFlankDestination = reachableFallback;
                }
                else
                {
                    selectedFlankDestination = targetCenter;
                }
                currentPlanScore = float.MinValue;
            }
            else
            {
                // Check hysteresis: Only switch if the new score exceeds current plan by threshold
                if (!forceReplan && currentPlanScore > float.MinValue && (selectedCandidate.TotalScore - currentPlanScore) < scoreHysteresis)
                {
                    // Existing route remains satisfactory; keep pursuing current destination
                    return false;
                }

                selectedFlankDestination = targetReachablePos;
                currentPlanScore = selectedCandidate.TotalScore;

                // Update side bias (-1 or +1) based on candidate direction relative to target approach
                Vector3 toCand = (selectedFlankDestination - transform.position).normalized;
                Vector3 toTarg = (targetCenter - transform.position).normalized;
                Vector3 lat = Vector3.Cross(toTarg, Vector3.up);
                sideBias = Vector3.Dot(toCand, lat) >= 0 ? 1f : -1f;

                Debug.Log($"[{DisplayName}] Selected Reachable Flank Destination at {selectedFlankDestination} (Score: {selectedCandidate.TotalScore:F1}, AngleScore: {selectedCandidate.AngleScore:F1}, LoS: {selectedCandidate.LineOfSightScore:F1})");
            }

            lastReplanTime = Time.time;
            targetPosAtLastPlan = targetCenter;

            ChangeAlertness(AlertLevel.Combat);
            ChangeState(AgentState.Reposition);
            currentPhase = FlankerPhase.MovingToFlank;
            if (movement != null)
            {
                if (!movement.TryMoveTo(selectedFlankDestination, flankSpeed))
                {
                    // Destination rejected by path check, investigate LKP directly
                    StartInvestigatingLKP();
                    return false;
                }
            }
            return true;
        }

        private void HandleFlankMovementDecision()
        {
            currentPhase = FlankerPhase.MovingToFlank;

            // Check if player is visible during flank transit
            if (perception != null && perception.CanSeeTarget)
            {
                Vector3 playerPos = perception.LastKnownPosition;
                float distMoved = Vector3.Distance(playerPos, targetPosAtLastPlan);

                // If player moved significantly (> 3.5m), trigger informed replan
                if (distMoved >= replanMovementThreshold)
                {
                    InitiateFlankSelection(playerPos, GetTargetForward());
                    return;
                }

                // If we have arrived in attack range with a clear shot at the player from the side/rear, engage!
                if (HasLineOfFireToTarget(out Vector3 targetPoint, out _))
                {
                    float distToTarget = Vector3.Distance(transform.position, playerPos);
                    if (distToTarget <= attackRange && (movement.HasReachedDestination(1.6f) || distToTarget <= minFlankDistance * 1.25f))
                    {
                        ChangeState(AgentState.Engage);
                        currentPhase = FlankerPhase.EngagingAtFlank;
                        movement.Stop();
                        return;
                    }
                }
            }
            else
            {
                // Lost sight of target while flanking: if we reached the flank point without seeing player, investigate LKP
                if (movement.HasReachedDestination(1.4f))
                {
                    StartInvestigatingLKP();
                    return;
                }
            }

            // Periodic replan check if stuck or making no progress
            if (movement != null && movement.IsStuck)
            {
                if (!InitiateFlankSelection(perception != null ? perception.LastKnownPosition : targetPosAtLastPlan, GetTargetForward(), forceReplan: true))
                {
                    StartInvestigatingLKP();
                }
            }
        }

        #endregion

        #region Phase 4: Combat Engagement

        private void HandleEngageDecision()
        {
            currentPhase = FlankerPhase.EngagingAtFlank;

            // If perception lost sight, transition to investigate last known position
            if (perception != null && !perception.CanSeeTarget)
            {
                if (perception.TimeSinceLastSeen > 1.2f)
                {
                    StartInvestigatingLKP();
                    return;
                }
            }

            Transform target = TargetTransform;
            if (target == null)
            {
                StartInvestigatingLKP();
                return;
            }

            // Continuously face target
            movement.LookAt(target.position, 6.0f);

            // Check line of fire
            if (HasLineOfFireToTarget(out Vector3 targetSpot, out Vector3 muzzleOrigin))
            {
                float dist = Vector3.Distance(transform.position, target.position);

                // If player is closing in directly, reposition to maintain lateral standoff
                if (dist < minFlankDistance * 0.7f)
                {
                    InitiateFlankSelection(target.position, GetTargetForward(), forceReplan: true);
                    return;
                }

                // In advantageous position: fire tactical bursts!
                if (Time.time >= nextBurstTime && !isBurstFiring)
                {
                    StartCoroutine(FireBurstRoutine());
                }
            }
            else
            {
                // Line of fire blocked by cover/wall: replan flank position to regain attack line
                InitiateFlankSelection(perception.LastKnownPosition, GetTargetForward(), forceReplan: true);
            }
        }

        private IEnumerator FireBurstRoutine()
        {
            isBurstFiring = true;

            // Aim acquisition / steadying breath before trigger pull
            yield return new WaitForSeconds(0.35f);

            for (int i = 0; i < burstCount; i++)
            {
                if (!IsAlive || currentState != AgentState.Engage) break;
                if (!HasLineOfFireToTarget(out _, out _)) break;

                ExecuteSingleShot();
                if (burstCount > 1) yield return new WaitForSeconds(fireRate);
            }

            nextBurstTime = Time.time + burstCooldown;
            isBurstFiring = false;
        }

        private void ExecuteSingleShot()
        {
            Transform target = TargetTransform;
            if (target == null) return;

            if (!HasLineOfFireToTarget(out Vector3 targetPoint, out Vector3 origin))
            {
                return;
            }

            // Sniper rifle precision: minimal spread
            Vector3 spread = UnityEngine.Random.insideUnitSphere * 0.012f;
            Vector3 shootDir = ((targetPoint + spread) - origin).normalized;
            float shootDist = 65f;

            if (audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.82f, 0.90f); // Heavy sniper report
                audioSource.Play();
            }
            CombatEffects.PlayGunshot(origin);

            Vector3 hitPoint = origin + shootDir * shootDist;
            Vector3 castOrigin = origin + shootDir * 0.05f;

            RaycastHit[] hits = Physics.RaycastAll(castOrigin, shootDir, shootDist, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.transform.root == transform.root) continue;
                if (col.isTrigger) continue;

                hitPoint = hits[i].point;

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    DamageInfo damageInfo = new DamageInfo(damagePerShot, gameObject, hitPoint, shootDir);
                    damageable.TakeDamage(damageInfo);
                }
                break;
            }

            if (tracerLine != null)
            {
                StartCoroutine(ShowTracerRoutine(origin, hitPoint));
            }
            CombatEffects.SpawnTracer(origin, hitPoint, this);
        }

        private IEnumerator ShowTracerRoutine(Vector3 start, Vector3 end)
        {
            tracerLine.SetPosition(0, start);
            tracerLine.SetPosition(1, end);
            tracerLine.enabled = true;
            yield return new WaitForSeconds(0.045f);
            tracerLine.enabled = false;
        }

        public bool HasLineOfFireToTarget(out Vector3 bestTargetPoint, out Vector3 muzzleOrigin)
        {
            muzzleOrigin = weaponMuzzle != null ? weaponMuzzle.position : (transform.position + Vector3.up * 1.35f + transform.forward * 0.4f);
            bestTargetPoint = Vector3.zero;

            Transform target = TargetTransform;
            if (target == null) return false;

            Vector3[] candidates = new Vector3[]
            {
                target.position + Vector3.up * 1.15f,
                target.position + Vector3.up * 1.55f,
                target.position + Vector3.up * 1.15f + target.right * 0.25f,
                target.position + Vector3.up * 1.15f - target.right * 0.25f,
                target.position + Vector3.up * 0.65f
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 cand = candidates[i];
                Vector3 toCand = cand - muzzleOrigin;
                float dist = toCand.magnitude;
                if (dist < 0.1f) continue;

                Vector3 dir = toCand / dist;
                RaycastHit[] hits = Physics.RaycastAll(muzzleOrigin + dir * 0.05f, dir, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                bool clear = true;
                for (int h = 0; h < hits.Length; h++)
                {
                    Collider col = hits[h].collider;
                    if (col == null) continue;
                    if (col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.transform.root == transform.root) continue;
                    if (col.isTrigger) continue;

                    if (col.transform == target || col.transform.IsChildOf(target) || col.transform.root == target.root)
                    {
                        bestTargetPoint = hits[h].point;
                        return true;
                    }

                    // Obstructed by obstacle
                    clear = false;
                    break;
                }

                if (clear)
                {
                    bestTargetPoint = cand;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Phase 5: Investigate Last Known Position (LKP)

        private void StartInvestigatingLKP()
        {
            ChangeAlertness(AlertLevel.Suspicious);
            ChangeState(AgentState.Investigate);
            currentPhase = FlankerPhase.SearchingLKP;
            currentSearchPoint = perception != null && perception.LastKnownPosition != Vector3.zero ? perception.LastKnownPosition : targetPosAtLastPlan;

            // Restrict LKP investigation to the Flanker's assigned floor level
            float verticalDiff = Mathf.Abs(currentSearchPoint.y - homeBasePosition.y);
            if (verticalDiff > 1.8f)
            {
                currentSearchPoint = homeBasePosition;
            }

            searchTimer = 0f;
            isSearchingAtPoint = false;

            if (movement != null)
            {
                if (!movement.TryMoveTo(currentSearchPoint, flankSpeed))
                {
                    // LKP unreachable via complete NavMesh route; initiate 360 search immediately from current position
                    isSearchingAtPoint = true;
                    movement.Stop();
                }
            }
            Debug.Log($"[{DisplayName}] Lost contact; moving to investigate LKP at {currentSearchPoint}");
        }

        private void HandleInvestigateDecision()
        {
            currentPhase = FlankerPhase.SearchingLKP;

            // If player spotted during search, immediately initiate flank!
            if (perception != null && perception.CanSeeTarget)
            {
                ChangeAlertness(AlertLevel.Alert);
                InitiateFlankSelection(perception.LastKnownPosition, GetTargetForward());
                return;
            }

            if (!isSearchingAtPoint)
            {
                if (movement.HasReachedDestination(1.5f) || movement.IsStuck)
                {
                    isSearchingAtPoint = true;
                    searchTimer = 0f;
                    movement.Stop();
                }
            }
            else
            {
                searchTimer += Time.deltaTime;

                // Look around suspiciously at the search point
                float scanAngle = Mathf.Sin(searchTimer * 2.5f) * 65f;
                Vector3 lookDir = Quaternion.Euler(0f, scanAngle, 0f) * transform.forward;
                movement.LookAt(transform.position + lookDir * 4f, 3.0f);

                if (searchTimer >= searchDuration)
                {
                    // Investigation concluded without contact: return to home base / patrol
                    ChangeAlertness(AlertLevel.Unaware);
                    ChangeState(AgentState.ReturnToPost);
                    currentPhase = FlankerPhase.ReturningHome;
                    if (!movement.TryMoveTo(homeBasePosition, patrolSpeed))
                    {
                        if (movement.IsDestinationReachable(homeBasePosition, out Vector3 reachableHome))
                        {
                            movement.MoveTo(reachableHome, patrolSpeed);
                        }
                    }
                    Debug.Log($"[{DisplayName}] Search concluded. Returning to post at {homeBasePosition}");
                }
            }
        }

        private void HandleReturnHomeDecision()
        {
            currentPhase = FlankerPhase.ReturningHome;

            if (perception != null && perception.CanSeeTarget)
            {
                ChangeAlertness(AlertLevel.Alert);
                InitiateFlankSelection(perception.LastKnownPosition, GetTargetForward());
                return;
            }

            if (movement != null && (movement.HasReachedDestination(1.5f) || (movement.IsStuck && Vector3.Distance(transform.position, homeBasePosition) < 3.0f)))
            {
                ChangeAlertness(AlertLevel.Unaware);
                ChangeState(AgentState.Patrol);
                currentPhase = FlankerPhase.Patrol;
                if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                {
                    currentWaypointIndex = 0;
                    movement.TryMoveTo(patrolWaypoints[0], patrolSpeed);
                }
            }
        }

        #endregion

        #region Sensory & Perception Stimuli

        private void HandleNoiseHeard(Vector3 noisePosition)
        {
            if (!IsAlive) return;

            // Elevate alertness
            ChangeAlertness(AlertLevel.Alert);

            // If already engaging or moving to a flank, ignore minor noises
            if (currentState == AgentState.Engage) return;

            // Plan flank approach against noise position instead of charging directly at it!
            Vector3 estimatedFacing = (noisePosition - transform.position).normalized;
            InitiateFlankSelection(noisePosition, estimatedFacing, forceReplan: true);
        }

        private void HandleTargetSpotted(Transform target)
        {
            if (!IsAlive) return;
            ChangeAlertness(AlertLevel.Alert);
            InitiateFlankSelection(target.position, target.forward, forceReplan: true);
        }

        protected override void OnDamaged(DamageInfo damageInfo)
        {
            if (!IsAlive) return;
            ChangeAlertness(AlertLevel.Alert);

            if (damageInfo.Instigator != null)
            {
                // Force an immediate replan to an alternate flank route!
                InitiateFlankSelection(damageInfo.Instigator.transform.position, damageInfo.Instigator.transform.forward, forceReplan: true);
            }
        }

        protected override void OnDefeated(DamageInfo damageInfo)
        {
            if (movement != null) movement.Stop();
            if (tracerLine != null) tracerLine.enabled = false;
            isBurstFiring = false;
            StopAllCoroutines();

            // Disable character collision to allow walking over and prevent standing collider
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Trigger visual defeat knockdown animation (fall down flat like soldier)
            visuals?.PlayDefeatKnockdown();

            Debug.Log($"[{DisplayName}] Defeated by {damageInfo.Instigator?.name ?? "unknown"}! Falling down.");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (perception != null)
            {
                perception.OnNoiseHeard -= HandleNoiseHeard;
                perception.OnTargetSpotted -= HandleTargetSpotted;
            }
        }

        private Vector3 GetTargetForward()
        {
            Transform t = TargetTransform;
            if (t != null) return t.forward;
            return (transform.position - homeBasePosition).normalized;
        }

        #endregion

        #region Visuals, Audio & HUD

        private void SetupVisualsAndWeapon()
        {
            visuals = GetComponent<RoyalGuardVisuals>();
            if (visuals == null)
            {
                visuals = gameObject.AddComponent<RoyalGuardVisuals>();
            }

            weaponMuzzle = visuals.MuzzlePoint;
        }

        private void SetupAudioAndTracer()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.maxDistance = 35f;
            audioSource.volume = 0.45f;
            audioSource.clip = CreateGunshotClip();

            tracerLine = GetComponent<LineRenderer>();
            if (tracerLine == null)
            {
                tracerLine = gameObject.AddComponent<LineRenderer>();
            }
            tracerLine.startWidth = 0.035f;
            tracerLine.endWidth = 0.015f;
            tracerLine.material = new Material(Shader.Find("Sprites/Default"));
            tracerLine.startColor = new Color(0.2f, 0.8f, 1.0f, 0.9f); // Electric cyan tracer for Flanker
            tracerLine.endColor = new Color(0.1f, 0.4f, 0.9f, 0.4f);
            tracerLine.enabled = false;
        }

        private AudioClip CreateGunshotClip()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.16f);
            AudioClip clip = AudioClip.Create("FlankerGunshot", lengthSamples, 1, sampleRate, false);
            float[] samples = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 40f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.75f;
                float tone = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.25f;
                samples[i] = (noise + tone) * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        private void OnGUI()
        {
            if (!showOverheadHUD || !IsAlive) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * overheadHudHeight;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            if (screenPos.z <= 0f) return;

            float screenY = Screen.height - screenPos.y;
            float barWidth = 90f;
            float barHeight = 8f;
            float x = screenPos.x - barWidth * 0.5f;

            if (hudBarBgTex == null)
            {
                hudBarBgTex = new Texture2D(1, 1);
                hudBarBgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                hudBarBgTex.Apply();
            }

            if (hudBarFillTex == null)
            {
                hudBarFillTex = new Texture2D(1, 1);
                hudBarFillTex.SetPixel(0, 0, new Color(0.2f, 0.7f, 1.0f, 0.9f)); // Cyan fill
                hudBarFillTex.Apply();
            }

            // Health bar
            GUI.DrawTexture(new Rect(x, screenY, barWidth, barHeight), hudBarBgTex);
            float pct = health != null ? (health.CurrentHealth / health.MaxHealth) : 1f;
            GUI.DrawTexture(new Rect(x, screenY, barWidth * pct, barHeight), hudBarFillTex);

            // Phase Label
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(x - 30f, screenY - 18f, barWidth + 60f, 16f), $"FLANKER [MARKSMAN] | {currentPhase}", labelStyle);
        }

        #endregion
    }
}
