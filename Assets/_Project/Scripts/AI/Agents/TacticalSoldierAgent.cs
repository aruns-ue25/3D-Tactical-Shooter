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
    /// Tactical Soldier (Guard) - Autonomous Area-Defending Combat Agent.
    /// Fulfills the Intelligent Systems (IS) assignment responsibility for the Systems Engineer.
    ///
    /// Distinct Guard Behavior Loop:
    /// 1. Hold assigned area: Patrols waypoint route or holds defensive post (e.g. corridor junction / security room).
    /// 2. Perception-driven detection: Reacts to FOV, LoS raycasts, and noise rather than omniscient knowledge.
    /// 3. Situation assessment: Evaluates distance to intruder, LoS, and engagement threshold.
    /// 4. Attack vs Reposition: Attacks with rifle bursts when within engagement range (<= 12m).
    ///    Otherwise repositions to a defensive cover position while keeping intruder in check.
    /// 5. Handle lost contact: Searches Last-Known-Position (LKP) for a brief duration, then returns to guard post.
    /// </summary>
    public class TacticalSoldierAgent : AIAgentBase
    {
        [Header("Guard Area & Perimeter")]
        [SerializeField] private Vector3 guardPostPosition;
        [SerializeField] private float guardLeashRadius = 35.0f; // Can move past initial post across their floor
        [SerializeField] private Vector3[] patrolWaypoints;
        [SerializeField] private float waypointWaitTime = 1.0f;

        [Header("Combat & Engagement")]
        [SerializeField] private float attackRange = 13.0f; // Short range shooting
        [SerializeField] private float fireRate = 0.15f; // Seconds per bullet in burst
        [SerializeField] private int burstCount = 3;
        [SerializeField] private float burstCooldown = 2.0f;
        [SerializeField] private float damagePerShot = 5f;
        [SerializeField] private float searchDuration = 6.0f;

        [Header("Agent Visuals & HUD")]
        [SerializeField] private bool showOverheadHUD = true;
        [SerializeField] private float overheadHudHeight = 2.3f;
        [SerializeField] private Color armorColor = new Color(0.2f, 0.28f, 0.24f); // Tactical military olive/slate

        // Runtime states
        private int currentWaypointIndex = 0;
        private float waypointTimer = 0f;
        private float nextBurstTime = 0f;
        private bool isBurstFiring = false;
        private float searchTimer = 0f;
        private float searchTransitTimer = 0f;
        private bool isSearchingAtPoint = false;
        private float searchScanAngle = 0f;
        private Vector3 targetSearchPoint;
        private Vector3 repositionTarget;
        private float lostContactGraceTimer = 0f;
        private float flankTimer = 0f;

        // Visual components
        private RoyalGuardVisuals guardVisuals;
        private Transform weaponMuzzle;
        private AudioSource audioSource;
        private LineRenderer tracerLine;

        // HUD Textures
        private Texture2D hudBarBgTex;
        private Texture2D hudBarFillTex;

        public Vector3 GuardPost => guardPostPosition;
        public float AttackRange => attackRange;

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

        protected override void InitializeAgent()
        {
            agentRole = AgentRole.Guard;
            agentDisplayName = "Tactical Soldier (Guard)";

            // Default guard post to spawn position if not configured
            if (guardPostPosition == Vector3.zero)
            {
                guardPostPosition = transform.position;
            }

            // Create default patrol waypoints around post if empty
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                patrolWaypoints = new Vector3[]
                {
                    guardPostPosition + new Vector3(0f, 0f, 2.5f),
                    guardPostPosition + new Vector3(4.5f, 0f, 2.5f),
                    guardPostPosition + new Vector3(4.5f, 0f, -2.5f),
                    guardPostPosition + new Vector3(0f, 0f, -2.5f)
                };
            }

            SetupVisualsAndWeapon();
            SetupAudioAndTracer();

            // Subscribe to perception events: gunshot sounds and intruder spotting
            if (perception != null)
            {
                perception.OnNoiseHeard += HandleNoiseHeard;
                perception.OnTargetSpotted += HandleTargetSpotted;
            }

            // Initial state: Start patrolling designated sector
            ChangeAlertness(AlertLevel.Unaware);
            ChangeState(AgentState.Patrol);
            movement.MoveTo(patrolWaypoints[currentWaypointIndex], 2.2f);
        }

        public void ConfigureDeployment(Vector3 post, float leash, Vector3[] waypoints)
        {
            guardPostPosition = post;
            guardLeashRadius = leash;
            if (waypoints != null && waypoints.Length > 0)
            {
                patrolWaypoints = waypoints;
            }
        }

        protected override void UpdateDecision()
        {
            switch (currentState)
            {
                case AgentState.Idle:
                case AgentState.Patrol:
                    HandlePatrolDecision();
                    break;

                case AgentState.Engage:
                    HandleEngageDecision();
                    break;

                case AgentState.Reposition:
                    HandleRepositionDecision();
                    break;

                case AgentState.Investigate:
                    HandleInvestigateDecision();
                    break;

                case AgentState.ReturnToPost:
                    HandleReturnToPostDecision();
                    break;

                case AgentState.Defeated:
                    // Agent defeated; no active decisions
                    break;
            }
        }

        #region State Decision Implementations

        /// <summary>
        /// Loop 1: Patrol or hold assigned area while monitoring perception.
        /// </summary>
        private void HandlePatrolDecision()
        {
            // Perception check: Did we detect the player?
            if (perception.IsTargetDetected && perception.CanSeeTarget)
            {
                AssessTargetSituation();
                return;
            }

            // Waypoint navigation
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                if (movement.HasReachedDestination(1.2f))
                {
                    waypointTimer += Time.deltaTime;
                    if (waypointTimer >= waypointWaitTime)
                    {
                        waypointTimer = 0f;
                        currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                        movement.MoveTo(patrolWaypoints[currentWaypointIndex], 2.2f);
                    }
                }
            }
        }

        /// <summary>
        /// Loop 3: Assess the situation to decide between Attack (Engage) or Defensive Reposition.
        /// </summary>
        private void AssessTargetSituation()
        {
            float distToTarget = perception.DistanceToTarget;
            float distToAnchor = Vector3.Distance(transform.position, guardPostPosition);

            // Vertical floor check: Guard stays on its assigned floor and does not follow upstairs or downstairs
            Transform target = TargetTransform;
            float verticalDiff = target != null ? Mathf.Abs(target.position.y - guardPostPosition.y) : (perception != null ? Mathf.Abs(perception.LastKnownPosition.y - guardPostPosition.y) : 0f);
            if (verticalDiff > 2.0f)
            {
                ChangeAlertness(AlertLevel.Alert);
                ChangeState(AgentState.Reposition);
                repositionTarget = guardPostPosition;
                movement.MoveTo(repositionTarget, 2.5f);
                return;
            }

            // Perimeter leash check: Guard defends ground; does not chase across entire map
            if (distToAnchor > guardLeashRadius)
            {
                // Reached perimeter boundary; do not chase farther, hold position or reposition back
                ChangeAlertness(AlertLevel.Alert);
                ChangeState(AgentState.Reposition);
                repositionTarget = guardPostPosition;
                movement.MoveTo(repositionTarget, 3.2f);
                return;
            }

            // Inside engagement range and clear line of sight
            if (perception.CanSeeTarget && distToTarget <= attackRange)
            {
                ChangeAlertness(AlertLevel.Combat);
                ChangeState(AgentState.Engage);
                movement.Stop();
            }
            else
            {
                // Target is further away or partially occluded: move toward defensive position
                ChangeAlertness(AlertLevel.Alert);
                ChangeState(AgentState.Reposition);
                repositionTarget = TacticalPositionEvaluator.FindDefensivePosition(
                    transform.position,
                    perception.Target != null ? perception.Target.position : perception.LastKnownPosition,
                    guardPostPosition,
                    guardLeashRadius,
                    attackRange * 0.75f,
                    LayerMask.GetMask("Default", "Environment")
                );

                if (!movement.TryMoveTo(repositionTarget, 3.5f))
                {
                    // If calculated cover is unreachable, fallback to holding post or moving to guard post
                    if (!movement.TryMoveTo(guardPostPosition, 3.0f))
                    {
                        movement.Stop();
                    }
                }
            }
        }

        /// <summary>
        /// Loop 4a: Attack when player is inside engagement range and targeted.
        /// Evaluates exposed target points when player is near walls/cover and flanks to cut angles if obstructed.
        /// </summary>
        private void HandleEngageDecision()
        {
            Transform target = TargetTransform;
            if (target == null) return;

            Vector3 aimPos = target.position + Vector3.up * 1.2f;

            // 1. Line of sight check with hysteresis grace timer
            if (!perception.CanSeeTarget)
            {
                lostContactGraceTimer += Time.deltaTime;
                // Hold aim on the corner / last known position during grace period
                Vector3 holdAimPos = (perception.LastKnownPosition != Vector3.zero) ? (perception.LastKnownPosition + Vector3.up * 1.2f) : aimPos;
                movement.LookAt(holdAimPos, 14.0f);

                if (lostContactGraceTimer >= 1.5f)
                {
                    lostContactGraceTimer = 0f;
                    InitiateLostContactSearch();
                }
                return;
            }
            else
            {
                lostContactGraceTimer = 0f;
            }

            // 2. Multi-point Line of Fire check to see if an exposed part of player can be shot
            bool hasClearLoF = GetBestLineOfFire(out Vector3 targetAimPoint, out Vector3 muzzleOrigin);

            // Turn body & weapon aim directly onto exposed target point
            Vector3 aimTarget = hasClearLoF ? targetAimPoint : aimPos;
            movement.LookAt(aimTarget, 22.0f);
            guardVisuals?.SetAimTarget(aimTarget);

            float distToTarget = Vector3.Distance(transform.position, target.position);

            // 3. Dynamic combat positioning & Cover Flanking
            if (!hasClearLoF)
            {
                // Line of Fire is blocked by wall/cover obstacle:
                // Maneuver / flank laterally to cut the angle instead of standing still shooting walls!
                flankTimer += Time.deltaTime;
                if (flankTimer >= 0.5f || !movement.IsMoving)
                {
                    flankTimer = 0f;
                    Vector3 flankPos = FindFlankingPosition(transform.position, target.position, guardPostPosition, guardLeashRadius);
                    if (!movement.TryMoveTo(flankPos, 3.4f))
                    {
                        movement.Stop();
                    }
                }
            }
            else
            {
                // Clear shot available at exposed player!
                flankTimer = 0f;

                if (distToTarget > 10.5f)
                {
                    // Advance toward player smoothly while maintaining aim only if on the same floor
                    if (Mathf.Abs(target.position.y - guardPostPosition.y) <= 2.0f)
                    {
                        movement.MoveTo(target.position, 2.4f);
                    }
                    else
                    {
                        movement.Stop();
                    }
                }
                else if (distToTarget < 3.0f)
                {
                    // Back up slightly if player is too close
                    Vector3 backStep = transform.position - transform.forward * 2.5f;
                    movement.MoveTo(backStep, 2.0f);
                }
                else
                {
                    // In optimal combat pocket with clear line of fire: hold ground
                    movement.Stop();
                }

                // 4. Fire tactical bursts at the exposed target point!
                if (Time.time >= nextBurstTime && !isBurstFiring)
                {
                    StartCoroutine(FireBurstRoutine());
                }
            }
        }

        /// <summary>
        /// Evaluates multiple candidate target points on the player (head, chest, exposed flanks when peeking).
        /// Returns true and the exposed target point if a clean line of fire from weapon muzzle exists.
        /// </summary>
        /// <summary>
        /// Evaluates multiple candidate target points strictly on the player's body.
        /// Returns true ONLY if a ray from the weapon muzzle actually strikes the player without any wall/obstacle in between.
        /// </summary>
        public bool GetBestLineOfFire(out Vector3 bestTargetPoint, out Vector3 muzzleOrigin)
        {
            muzzleOrigin = weaponMuzzle != null ? weaponMuzzle.position : (transform.position + Vector3.up * 1.35f + transform.forward * 0.4f);
            bestTargetPoint = Vector3.zero;

            Transform target = TargetTransform;
            if (target == null) return false;

            // Candidate points on the player's physical body (not extending into empty air outside cover)
            Vector3[] candidates = new Vector3[]
            {
                target.position + Vector3.up * 1.15f,                                  // Center chest
                target.position + Vector3.up * 1.60f,                                  // Head
                target.position + Vector3.up * 1.15f + target.right * 0.22f,           // Right shoulder
                target.position + Vector3.up * 1.15f - target.right * 0.22f,           // Left shoulder
                target.position + Vector3.up * 1.35f,                                  // Upper chest
                target.position + Vector3.up * 0.60f                                   // Hip / Upper legs
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 cand = candidates[i];
                Vector3 toCand = cand - muzzleOrigin;
                float dist = toCand.magnitude;
                if (dist < 0.1f) continue;

                Vector3 dir = toCand / dist;

                // Raycast from weapon muzzle to candidate point (small 0.05m offset to clear muzzle tip without penetrating obstacles)
                RaycastHit[] hits = Physics.RaycastAll(muzzleOrigin + dir * 0.05f, dir, dist + 0.6f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                for (int h = 0; h < hits.Length; h++)
                {
                    Collider col = hits[h].collider;
                    if (col == null || col.gameObject == gameObject || col.transform.IsChildOf(transform) || col.transform.root == transform.root) continue;
                    if (col.isTrigger) continue;

                    // If the FIRST solid surface hit along this ray is the player -> clean line of fire!
                    if (col.transform == target || col.transform.IsChildOf(target) || col.transform.root == target.root)
                    {
                        bestTargetPoint = hits[h].point;
                        return true;
                    }

                    // First solid object hit was a wall, door, crate, or cover barrier -> occluded!
                    break;
                }
            }

            bestTargetPoint = target.position + Vector3.up * 1.15f;
            return false;
        }

        private Vector3 FindFlankingPosition(Vector3 currentPos, Vector3 targetPos, Vector3 anchorPos, float leashRadius)
        {
            Vector3 toTarget = (targetPos - currentPos).normalized;
            Vector3 lateralRight = Vector3.Cross(Vector3.up, toTarget).normalized;
            Vector3 lateralLeft = -lateralRight;

            Vector3[] candidates = new Vector3[]
            {
                currentPos + lateralRight * 3.8f,
                currentPos + lateralLeft * 3.8f,
                currentPos + (toTarget + lateralRight).normalized * 4.2f,
                currentPos + (toTarget + lateralLeft).normalized * 4.2f,
                targetPos - toTarget * 6.5f + lateralRight * 3.2f,
                targetPos - toTarget * 6.5f + lateralLeft * 3.2f
            };

            Vector3 bestPos = currentPos;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Length; i++)
            {
                Vector3 cand = candidates[i];

                if (Vector3.Distance(cand, anchorPos) > leashRadius) continue;
                if (Physics.CheckSphere(cand + Vector3.up * 0.9f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;

                Vector3 candEye = cand + Vector3.up * 1.35f;
                Vector3 targetChest = targetPos + Vector3.up * 1.15f;
                Vector3 toTgt = (targetChest - candEye);
                float dist = toTgt.magnitude;

                bool clearShot = false;
                if (dist > 0.1f)
                {
                    Vector3 dir = toTgt / dist;
                    if (Physics.Raycast(candEye, dir, out RaycastHit hit, dist + 0.5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.transform == TargetTransform || hit.transform.IsChildOf(TargetTransform))
                        {
                            clearShot = true;
                        }
                    }
                }

                float score = 0f;
                if (clearShot) score += 50f;

                float distToTgt = Vector3.Distance(cand, targetPos);
                score -= Mathf.Abs(distToTgt - 7.0f) * 2f;
                score -= Vector3.Distance(currentPos, cand) * 0.6f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = cand;
                }
            }

            return bestPos;
        }

        /// <summary>
        /// Loop 4b: Move toward a useful defensive position while keeping player in view if possible.
        /// </summary>
        private void HandleRepositionDecision()
        {
            // Target re-entered close attack range with direct LoS
            if (perception.CanSeeTarget && perception.DistanceToTarget <= attackRange)
            {
                ChangeAlertness(AlertLevel.Combat);
                ChangeState(AgentState.Engage);
                return;
            }

            // If line of sight lost during repositioning
            if (!perception.CanSeeTarget && perception.TimeSinceLastSeen > 2.0f)
            {
                InitiateLostContactSearch();
                return;
            }

            // Keep moving toward defensive vantage point
            if (movement.HasReachedDestination(1.0f))
            {
                // Once at position, turn to face target or LKP
                if (perception.Target != null)
                {
                    movement.LookAt(perception.Target.position, 1.5f);
                }
                else
                {
                    movement.LookAt(perception.LastKnownPosition, 1.5f);
                }

                // If target is in LoS now, engage
                if (perception.CanSeeTarget)
                {
                    ChangeAlertness(AlertLevel.Combat);
                    ChangeState(AgentState.Engage);
                }
            }
        }

        /// <summary>
        /// Loop 5: Handle lost contact - Search Last-Known-Position directly, then resume guard area.
        /// </summary>
        private void InitiateLostContactSearch()
        {
            ChangeAlertness(AlertLevel.Suspicious);
            ChangeState(AgentState.Investigate);

            // Move directly to where player was actually standing
            targetSearchPoint = perception.LastKnownPosition;

            // Restrict search to Guard's assigned floor and leash
            float verticalDiff = Mathf.Abs(targetSearchPoint.y - guardPostPosition.y);
            float distFromPost = Vector3.Distance(targetSearchPoint, guardPostPosition);
            if (verticalDiff > 2.0f || distFromPost > guardLeashRadius)
            {
                // Target is on another floor or outside leash! Stay on assigned floor and scan post
                isSearchingAtPoint = true;
                movement.MoveTo(guardPostPosition, 2.5f);
                return;
            }

            searchTimer = 0f;
            searchTransitTimer = 0f;
            isSearchingAtPoint = false;

            if (!movement.TryMoveTo(targetSearchPoint, 3.2f))
            {
                // LKP unreachable; initiate 360 search from current position
                isSearchingAtPoint = true;
                movement.Stop();
                searchScanAngle = transform.eulerAngles.y;
            }
        }

        private void HandleInvestigateDecision()
        {
            // If target spotted again during search -> immediate re-engagement!
            if (perception.CanSeeTarget)
            {
                AssessTargetSituation();
                return;
            }

            searchTransitTimer += Time.deltaTime;

            if (!isSearchingAtPoint)
            {
                if (movement.HasReachedDestination(1.6f) || searchTransitTimer > 6.0f || movement.IsStuck)
                {
                    isSearchingAtPoint = true;
                    movement.Stop();
                    searchScanAngle = transform.eulerAngles.y;
                }
            }

            if (isSearchingAtPoint)
            {
                searchTimer += Time.deltaTime;

                // 360 degree room sweep: smoothly rotate looking around the entire room & corridor
                searchScanAngle += Time.deltaTime * 90f;
                Vector3 sweepDir = Quaternion.Euler(0f, searchScanAngle, 0f) * Vector3.forward;
                movement.LookAt(transform.position + sweepDir * 5f, 2.5f);

                // If search duration expired without detecting target -> Return to Post
                if (searchTimer >= searchDuration)
                {
                    ChangeAlertness(AlertLevel.Unaware);
                    ChangeState(AgentState.ReturnToPost);
                    if (!movement.TryMoveTo(guardPostPosition, 2.4f))
                    {
                        if (movement.IsDestinationReachable(guardPostPosition, out Vector3 reachablePost))
                        {
                            movement.MoveTo(reachablePost, 2.4f);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns to assigned guard post and resumes normal patrol loop.
        /// </summary>
        private void HandleReturnToPostDecision()
        {
            if (perception.CanSeeTarget)
            {
                AssessTargetSituation();
                return;
            }

            if (movement.HasReachedDestination(1.2f) || (movement.IsStuck && Vector3.Distance(transform.position, guardPostPosition) < 2.5f))
            {
                ChangeAlertness(AlertLevel.Unaware);
                ChangeState(AgentState.Patrol);
                if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                {
                    movement.TryMoveTo(patrolWaypoints[currentWaypointIndex], 2.2f);
                }
                else
                {
                    movement.Stop();
                }
            }
        }

        #endregion

        #region Weapon Combat Execution

        private IEnumerator FireBurstRoutine()
        {
            isBurstFiring = true;

            for (int i = 0; i < burstCount; i++)
            {
                if (!IsAlive || currentState != AgentState.Engage) break;

                // Stop burst immediately if target breaks line of fire / moves behind cover
                if (!GetBestLineOfFire(out _, out _)) break;

                ExecuteSingleShot();
                yield return new WaitForSeconds(fireRate);
            }

            nextBurstTime = Time.time + burstCooldown;
            isBurstFiring = false;
        }

        public void ExecuteSingleShot()
        {
            Transform target = TargetTransform;
            if (target == null) return;

            // 1. Determine best line of fire and exact exposed target point.
            // If the target is fully behind cover, abort immediately - do not fire phantom bullets.
            if (!GetBestLineOfFire(out Vector3 targetPoint, out Vector3 origin))
            {
                return;
            }

            // Minor combat spread tightly constrained
            Vector3 spread = UnityEngine.Random.insideUnitSphere * 0.025f;
            Vector3 aimSpot = targetPoint + spread;
            Vector3 shootDir = (aimSpot - origin).normalized;
            float shootDist = 45f;

            // Audio pop
            if (audioSource != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                audioSource.Play();
            }

            // Standard physical bullet hitscan:
            // Cast ray along shootDir and stop unconditionally at the FIRST solid collider struck.
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

                // First physical solid surface struck by bullet (wall, cover, prop, or player)
                hitPoint = hits[i].point;

                // Check if this collider belongs to the target or damageable entity
                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable != (IDamageable)health)
                {
                    DamageInfo damageInfo = new DamageInfo(damagePerShot, gameObject, hits[i].point, shootDir);
                    damageable.TakeDamage(damageInfo);
                }

                // Bullets terminate on the first solid impact - absolute zero penetration through walls
                break;
            }

            // Trigger weapon recoil and muzzle flash
            guardVisuals?.TriggerRecoil();

            // Visual Tracer
            StartCoroutine(ShowTracerRoutine(origin, hitPoint));
        }

        private IEnumerator ShowTracerRoutine(Vector3 start, Vector3 end)
        {
            if (tracerLine != null)
            {
                tracerLine.enabled = true;
                tracerLine.SetPosition(0, start);
                tracerLine.SetPosition(1, end);
                yield return new WaitForSeconds(0.06f);
                tracerLine.enabled = false;
            }
        }

        #endregion

        #region Damage and Defeat Reactions

        protected override void OnDamaged(DamageInfo damageInfo)
        {
            // Flash red on hit
            guardVisuals?.FlashDamage(Color.red);

            // If hit by attacker outside current vision cone, immediately react and face incoming fire
            if (damageInfo.Instigator != null)
            {
                Vector3 attackerPos = damageInfo.Instigator.transform.position;
                perception.ForceTargetDetection(attackerPos);

                // Turn to face attacker immediately
                movement.LookAt(attackerPos, 4f);

                if (currentState == AgentState.Patrol || currentState == AgentState.Idle || currentState == AgentState.ReturnToPost)
                {
                    AssessTargetSituation();
                }
            }
        }

        protected override void OnDefeated(DamageInfo damageInfo)
        {
            StopAllCoroutines();
            if (tracerLine != null) tracerLine.enabled = false;

            if (movement != null)
            {
                movement.Stop();
                movement.enabled = false;
            }

            if (perception != null)
            {
                perception.enabled = false;
            }

            // Disable character collision to allow walking over
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Trigger visual defeat knockdown animation
            guardVisuals?.PlayDefeatKnockdown();
        }

        #endregion

        #region Perception Stimuli & Noise Reactions

        private void HandleNoiseHeard(Vector3 noisePosition)
        {
            if (!IsAlive) return;

            // Immediately turn toward the gunshot noise
            movement.LookAt(noisePosition, 8.0f);

            // If already in active combat engagement, keep tracking target
            if (currentState == AgentState.Engage)
            {
                return;
            }

            // Immediately elevate alertness to Alert!
            ChangeAlertness(AlertLevel.Alert);

            // If intruder is in line of sight, immediately assess and attack!
            if (perception.CanSeeTarget)
            {
                AssessTargetSituation();
                return;
            }

            // Otherwise, rush towards the gunshot origin to hunt the intruder!
            ChangeState(AgentState.Investigate);
            targetSearchPoint = noisePosition;
            searchTimer = 0f;
            searchTransitTimer = 0f;
            isSearchingAtPoint = false;
            movement.MoveTo(targetSearchPoint, 3.8f);
        }

        private void HandleTargetSpotted(Transform target)
        {
            if (!IsAlive) return;
            AssessTargetSituation();
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

        #endregion

        #region Procedural 3D Soldier Visuals & Audio Setup

        private void SetupVisualsAndWeapon()
        {
            guardVisuals = GetComponent<RoyalGuardVisuals>();
            if (guardVisuals == null)
            {
                guardVisuals = gameObject.AddComponent<RoyalGuardVisuals>();
            }

            weaponMuzzle = guardVisuals.MuzzlePoint;
        }

        private void SetupAudioAndTracer()
        {
            // Audio source for gunfire
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.maxDistance = 35f;
            audioSource.volume = 0.5f;

            // Generate procedural crisp gunshot sound
            audioSource.clip = CreateGunshotAudioClip();

            // LineRenderer for bullet tracer
            tracerLine = GetComponent<LineRenderer>();
            if (tracerLine == null)
            {
                tracerLine = gameObject.AddComponent<LineRenderer>();
            }
            tracerLine.startWidth = 0.04f;
            tracerLine.endWidth = 0.02f;
            tracerLine.material = new Material(Shader.Find("Sprites/Default"));
            tracerLine.startColor = new Color(1.0f, 0.4f, 0.1f, 0.9f); // Amber-orange bullet trace
            tracerLine.endColor = new Color(1.0f, 0.8f, 0.2f, 0.5f);
            tracerLine.enabled = false;
        }

        private AudioClip CreateGunshotAudioClip()
        {
            int sampleRate = 44100;
            int lengthSamples = (int)(sampleRate * 0.18f);
            AudioClip clip = AudioClip.Create("GuardGunshot", lengthSamples, 1, sampleRate, false);
            float[] samples = new float[lengthSamples];

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 35f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.7f;
                float tone = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.3f;
                samples[i] = (noise + tone) * envelope;
            }

            clip.SetData(samples, 0);
            return clip;
        }

        #endregion

        #region Overhead World-Space Tactical HUD

        private void OnGUI()
        {
            if (!showOverheadHUD || Camera.main == null) return;

            // Don't draw if camera is facing away or too far
            Vector3 worldPos = transform.position + Vector3.up * overheadHudHeight;
            Vector3 camToAgent = worldPos - Camera.main.transform.position;
            if (Vector3.Dot(Camera.main.transform.forward, camToAgent) <= 0f) return;

            float distToCam = camToAgent.magnitude;
            if (distToCam > 30f) return;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) return;

            // Texture caching
            if (hudBarBgTex == null)
            {
                hudBarBgTex = new Texture2D(1, 1);
                hudBarBgTex.SetPixel(0, 0, new Color(0.08f, 0.1f, 0.12f, 0.85f));
                hudBarBgTex.Apply();
            }
            if (hudBarFillTex == null)
            {
                hudBarFillTex = new Texture2D(1, 1);
                hudBarFillTex.SetPixel(0, 0, Color.white);
                hudBarFillTex.Apply();
            }

            float hudW = 120f;
            float hudH = 26f;
            float x = screenPos.x - hudW * 0.5f;
            float y = Screen.height - screenPos.y - hudH * 0.5f;

            Color prevColor = GUI.color;

            // Background
            GUI.color = new Color(0.05f, 0.08f, 0.1f, 0.88f);
            GUI.DrawTexture(new Rect(x, y, hudW, hudH), hudBarBgTex);

            // State title & color
            string stateLabel = currentState switch
            {
                AgentState.Patrol => "[GUARD: SENTINEL] PATROL",
                AgentState.Idle => "[GUARD: SENTINEL] READY",
                AgentState.Engage => "[!] GUARD ENGAGING",
                AgentState.Reposition => "[<>] GUARD REPOSITION",
                AgentState.Investigate => "[?] GUARD SEARCHING",
                AgentState.ReturnToPost => "[<-] GUARD RETURNING",
                AgentState.Defeated => "[X] GUARD DOWN",
                _ => "[GUARD: SENTINEL]"
            };

            Color stateColor = currentState switch
            {
                AgentState.Engage => new Color(1.0f, 0.25f, 0.25f),
                AgentState.Reposition => new Color(1.0f, 0.65f, 0.1f),
                AgentState.Investigate => new Color(1.0f, 0.85f, 0.2f),
                AgentState.Defeated => new Color(0.5f, 0.5f, 0.5f),
                _ => new Color(0.35f, 0.85f, 0.45f)
            };

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = stateColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 1, hudW, 13), stateLabel, labelStyle);

            // Health bar
            if (health != null && IsAlive)
            {
                float hpPct = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
                float barW = hudW - 8f;
                float barH = 5f;
                float barX = x + 4f;
                float barY = y + 16f;

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), hudBarFillTex);

                Color hpColor = hpPct > 0.5f ? new Color(0.2f, 0.85f, 0.3f) : (hpPct > 0.25f ? Color.yellow : Color.red);
                GUI.color = hpColor;
                GUI.DrawTexture(new Rect(barX, barY, barW * hpPct, barH), hudBarFillTex);
            }

            GUI.color = prevColor;
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            // Guard anchor and leash circle
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(guardPostPosition, guardLeashRadius);

            // Attack range circle
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Patrol waypoints
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    Gizmos.DrawSphere(patrolWaypoints[i], 0.3f);
                    Vector3 next = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
                    Gizmos.DrawLine(patrolWaypoints[i], next);
                }
            }
        }
    }
}
