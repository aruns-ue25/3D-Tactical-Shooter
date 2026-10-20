using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Movement;
using TacticalShooter.AI.Perception;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    /// <summary>
    /// Persistent tracker that pursues fresh evidence, searches the last known position,
    /// and returns to its patrol when the trail is cold or outside its assigned boundary.
    /// Unlike the Guard it does not defend a fixed combat post; unlike the Flanker it does
    /// not score alternate attack angles.
    /// </summary>
    [DisallowMultipleComponent]
    public class HunterAgent : AIAgentBase
    {
        public enum HunterState
        {
            Patrol,
            Pursue,
            Engage,
            Search,
            Return,
            Defeated
        }

        [Header("Hunter Patrol")]
        [SerializeField] private Vector3 homePosition;
        [SerializeField] private Vector3[] patrolWaypoints;
        [SerializeField] private float waypointPause = 0.8f;
        [SerializeField] private float patrolSpeed = 2.35f;

        [Header("Tracking")]
        [SerializeField] private float pursuitSpeed = 4.1f;
        [SerializeField] private float searchSpeed = 3.0f;
        [SerializeField] private float maximumPursuitRadius = 55f;
        [SerializeField] private float searchDuration = 10f;
        [SerializeField] private float searchPointRadius = 3.5f;
        [SerializeField] private float pursuitRepathInterval = 0.65f;
        [SerializeField] private float pursuitRepathDistance = 1.25f;
        [SerializeField] private float lostSightPursuitTimeout = 7.0f;

        [Header("Combat")]
        [SerializeField] private float attackRange = 22f;
        [SerializeField] private float preferredCombatDistance = 11f;
        [SerializeField] private float fireRate = 0.18f;
        [SerializeField] private int shotsPerBurst = 3;
        [SerializeField] private float burstCooldown = 1.8f;
        [SerializeField] private float damagePerShot = 4f;
        [SerializeField] private float weaponRange = 50f;

        [Header("Presentation")]
        [SerializeField] private bool showHunterHUD = true;
        [SerializeField] private float hudHeight = 3.0f;
        [SerializeField] private Color hunterAccent = new Color(1f, 0.36f, 0.06f);

        [SerializeField] private HunterState hunterState = HunterState.Patrol;

        private int waypointIndex;
        private float waypointPauseTimer;
        private float searchElapsed;
        private float searchLegStarted;
        private float lostSightPursuitTimer;
        private float nextPursuitRepath;
        private float nextBurstTime;
        private bool isFiringBurst;
        private bool isSearchingAtPoint;
        private int searchPointIndex;
        private Vector3 searchCenter;
        private Vector3 lastPursuitDestination;
        private bool hasPursuitDestination;
        private bool homePositionConfigured;
        private bool warnedAboutUnreachableHome;

        private Transform weaponMuzzle;
        private RoyalGuardVisuals visuals;
        private Material hunterBadgeMaterial;

        private static readonly Vector3[] SearchOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 1f),
            new Vector3(-1f, 0f, 1f),
            new Vector3(-1f, 0f, -1f),
            new Vector3(1f, 0f, -1f)
        };

        public HunterState CurrentHunterState => hunterState;
        public Vector3 HomePosition => homePosition;
        public Transform TargetTransform => perception != null ? perception.Target : null;

        protected override void Awake()
        {
            agentRole = AgentRole.Hunter;
            agentDisplayName = "Hunter (Persistent Tracker)";
            base.Awake();
        }

        /// <summary>Used by the scene bootstrap before Start initializes the agent.</summary>
        public void ConfigureDeployment(Vector3 home, Vector3[] waypoints)
        {
            homePosition = home;
            homePositionConfigured = true;
            patrolWaypoints = waypoints;
        }

        protected override void InitializeAgent()
        {
            agentRole = AgentRole.Hunter;
            agentDisplayName = "Hunter (Persistent Tracker)";

            if (!homePositionConfigured && homePosition == Vector3.zero)
            {
                homePosition = transform.position;
            }

            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                patrolWaypoints = new[]
                {
                    homePosition + new Vector3(-2.5f, 0f, 0f),
                    homePosition + new Vector3(0f, 0f, 2.5f),
                    homePosition + new Vector3(2.5f, 0f, 0f),
                    homePosition + new Vector3(0f, 0f, -2.5f)
                };
            }

            visuals = GetComponent<RoyalGuardVisuals>();
            weaponMuzzle = visuals != null ? visuals.MuzzlePoint : null;
            AddHunterVisualIdentity();

            if (perception != null)
            {
                perception.OnTargetSpotted += HandleTargetSpotted;
                perception.OnNoiseHeard += HandleNoiseHeard;
            }

            waypointIndex = 0;
            lostSightPursuitTimer = 0f;
            ChangeAlertness(AlertLevel.Unaware);
            SetHunterState(HunterState.Patrol);
            TryMoveTo(patrolWaypoints[waypointIndex], patrolSpeed);
        }

        protected override void UpdateDecision()
        {
            if (!IsAlive)
            {
                SetHunterState(HunterState.Defeated);
                return;
            }

            switch (hunterState)
            {
                case HunterState.Patrol:
                    UpdatePatrol();
                    break;
                case HunterState.Pursue:
                    UpdatePursuit();
                    break;
                case HunterState.Engage:
                    UpdateEngagement();
                    break;
                case HunterState.Search:
                    UpdateSearch();
                    break;
                case HunterState.Return:
                    UpdateReturn();
                    break;
                case HunterState.Defeated:
                    break;
            }
        }

        private void UpdatePatrol()
        {
            if (perception != null && perception.CanSeeTarget)
            {
                BeginPursuit(perception.LastKnownPosition);
                return;
            }

            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                movement.Stop();
                return;
            }

            if (!movement.HasReachedDestination(1.0f)) return;

            waypointPauseTimer += Time.deltaTime;
            if (waypointPauseTimer < waypointPause) return;

            waypointPauseTimer = 0f;
            waypointIndex = (waypointIndex + 1) % patrolWaypoints.Length;
            TryMoveTo(patrolWaypoints[waypointIndex], patrolSpeed);
        }

        private void UpdatePursuit()
        {
            if (perception == null) return;

            if (!IsWithinPursuitBoundary(perception.LastKnownPosition))
            {
                BeginReturn();
                return;
            }

            if (perception.CanSeeTarget && perception.Target != null)
            {
                lostSightPursuitTimer = 0f;
                Vector3 targetPosition = perception.Target.position;
                float distance = Vector3.Distance(transform.position, targetPosition);

                if (distance <= attackRange && HasLineOfFire(out _, out _))
                {
                    SetHunterState(HunterState.Engage);
                    return;
                }

                // The live transform is used only while the player is currently visible.
                // Once sight is lost, movement below is restricted to LastKnownPosition.
                if (distance > preferredCombatDistance || !HasLineOfFire(out _, out _))
                {
                    RequestPursuitMove(targetPosition);
                }
                else
                {
                    movement.Stop();
                    movement.LookAt(targetPosition, 1.4f);
                }
                return;
            }

            // Sight has been lost: pursue strictly to Last Known Position with bounded duration,
            // stuck mitigation, and unreachable path recovery.
            lostSightPursuitTimer += Time.deltaTime;

            if (!RequestPursuitMove(perception.LastKnownPosition))
            {
                // Destination is unreachable on NavMesh; begin local search from current position.
                BeginSearch(transform.position);
                return;
            }

            if (movement.HasReachedDestination(1.1f))
            {
                BeginSearch(perception.LastKnownPosition);
                return;
            }

            if (movement != null && movement.IsStuck && lostSightPursuitTimer > 1.0f)
            {
                // Movement is physically blocked; begin local search from current reachable area.
                BeginSearch(transform.position);
                return;
            }

            if (lostSightPursuitTimer >= lostSightPursuitTimeout)
            {
                // Bounded pursuit timeout: trail went cold without reacquiring visual contact.
                BeginSearch(perception.LastKnownPosition != Vector3.zero ? perception.LastKnownPosition : transform.position);
            }
        }

        private void UpdateEngagement()
        {
            if (perception == null || !perception.CanSeeTarget || perception.Target == null)
            {
                if (perception != null && perception.TimeSinceLastSeen >= 1.0f)
                {
                    BeginPursuit(perception.LastKnownPosition);
                }
                return;
            }

            Vector3 targetPosition = perception.Target.position;
            if (!IsWithinPursuitBoundary(targetPosition))
            {
                BeginReturn();
                return;
            }

            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > attackRange || !HasLineOfFire(out _, out _))
            {
                BeginPursuit(targetPosition);
                return;
            }

            movement.LookAt(targetPosition + Vector3.up * 1.15f, 2f);

            if (distance > preferredCombatDistance + 2f)
            {
                RequestPursuitMove(targetPosition);
            }
            else if (distance < preferredCombatDistance - 3f)
            {
                Vector3 retreat = transform.position - (targetPosition - transform.position).normalized * 2f;
                TryMoveTo(retreat, patrolSpeed);
            }
            else
            {
                movement.Stop();
            }

            if (Time.time >= nextBurstTime && !isFiringBurst)
            {
                StartCoroutine(FireBurst());
            }
        }

        private void BeginSearch(Vector3 lastKnownPosition)
        {
            lostSightPursuitTimer = 0f;
            searchCenter = lastKnownPosition;
            searchElapsed = 0f;
            searchPointIndex = 0;
            isSearchingAtPoint = false;
            searchLegStarted = Time.time;
            ChangeAlertness(AlertLevel.Suspicious);
            SetHunterState(HunterState.Search);
            MoveToSearchPoint();
        }

        private void UpdateSearch()
        {
            if (perception != null && perception.CanSeeTarget)
            {
                BeginPursuit(perception.LastKnownPosition);
                return;
            }

            searchElapsed += Time.deltaTime;
            if (searchElapsed >= searchDuration)
            {
                BeginReturn();
                return;
            }

            if (isSearchingAtPoint)
            {
                float scan = Mathf.Sin(searchElapsed * 2.8f) * 70f;
                Vector3 scanDirection = Quaternion.Euler(0f, scan, 0f) * transform.forward;
                movement.LookAt(transform.position + scanDirection * 4f, 2f);

                if (Time.time - searchLegStarted >= 0.8f)
                {
                    searchPointIndex++;
                    isSearchingAtPoint = false;
                    searchLegStarted = Time.time;
                    MoveToSearchPoint();
                }
                return;
            }

            if (movement.HasReachedDestination(1f) || Time.time - searchLegStarted >= 2.5f)
            {
                movement.Stop();
                isSearchingAtPoint = true;
                searchLegStarted = Time.time;
            }
        }

        private void MoveToSearchPoint()
        {
            if (searchPointIndex >= SearchOffsets.Length)
            {
                searchPointIndex = 0;
            }

            Vector3 offset = SearchOffsets[searchPointIndex] * searchPointRadius;
            Vector3 target = searchCenter + offset;
            if (!TryMoveTo(target, searchSpeed))
            {
                // If search point offset is unreachable, advance search index and scan current position
                searchPointIndex++;
                isSearchingAtPoint = true;
                movement?.Stop();
            }
        }

        private void BeginReturn()
        {
            isFiringBurst = false;
            StopAllCoroutines();
            lostSightPursuitTimer = 0f;
            ChangeAlertness(AlertLevel.Unaware);
            SetHunterState(HunterState.Return);
            warnedAboutUnreachableHome = false;
            TryMoveTo(homePosition, pursuitSpeed);
        }

        private void UpdateReturn()
        {
            if (perception != null && perception.CanSeeTarget && perception.Target != null &&
                IsWithinPursuitBoundary(perception.Target.position))
            {
                BeginPursuit(perception.LastKnownPosition);
                return;
            }

            if (movement != null && (movement.HasReachedDestination(1.25f) || (movement.IsStuck && Vector3.Distance(transform.position, homePosition) < 2.5f)))
            {
                float distanceFromHome = Vector3.Distance(transform.position, homePosition);
                if (distanceFromHome > 2.5f && !warnedAboutUnreachableHome)
                {
                    warnedAboutUnreachableHome = true;
                    Debug.LogWarning($"[{DisplayName}] Could not reach its patrol anchor; resuming patrol from the nearest reachable point.");
                }

                waypointIndex = 0;
                ChangeAlertness(AlertLevel.Unaware);
                SetHunterState(HunterState.Patrol);
                if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                {
                    TryMoveTo(patrolWaypoints[waypointIndex], patrolSpeed);
                }
            }
        }

        private void BeginPursuit(Vector3 evidencePosition, bool resetTimeout = true)
        {
            if (!IsWithinPursuitBoundary(evidencePosition))
            {
                BeginReturn();
                return;
            }

            ChangeAlertness(AlertLevel.Alert);
            SetHunterState(HunterState.Pursue);
            hasPursuitDestination = false;
            nextPursuitRepath = 0f;
            if (resetTimeout)
            {
                lostSightPursuitTimer = 0f;
            }
            RequestPursuitMove(evidencePosition);
        }

        private bool RequestPursuitMove(Vector3 destination)
        {
            bool targetMovedEnough = !hasPursuitDestination ||
                Vector3.Distance(destination, lastPursuitDestination) >= pursuitRepathDistance;
            if (Time.time < nextPursuitRepath && !targetMovedEnough) return true;

            lastPursuitDestination = destination;
            hasPursuitDestination = true;
            nextPursuitRepath = Time.time + pursuitRepathInterval;
            return TryMoveTo(destination, pursuitSpeed);
        }

        /// <summary>
        /// Delegates to AIMovement's unified hybrid reachability and movement system.
        /// </summary>
        private bool TryMoveTo(Vector3 destination, float speed)
        {
            if (movement == null) return false;
            return movement.TryMoveTo(destination, speed);
        }

        private bool IsWithinPursuitBoundary(Vector3 evidencePosition)
        {
            return Vector3.Distance(homePosition, evidencePosition) <= maximumPursuitRadius;
        }

        private bool HasLineOfFire(out Vector3 targetPoint, out Vector3 muzzleOrigin)
        {
            targetPoint = Vector3.zero;
            muzzleOrigin = weaponMuzzle != null
                ? weaponMuzzle.position
                : transform.position + Vector3.up * 1.35f + transform.forward * 0.4f;

            Transform target = perception != null ? perception.Target : null;
            if (target == null) return false;

            Vector3[] targetPoints =
            {
                target.position + Vector3.up * 1.15f,
                target.position + Vector3.up * 1.55f,
                target.position + Vector3.up * 0.65f
            };

            foreach (Vector3 candidate in targetPoints)
            {
                Vector3 direction = candidate - muzzleOrigin;
                float distance = direction.magnitude;
                if (distance < 0.1f || distance > attackRange) continue;
                direction /= distance;

                RaycastHit[] hits = Physics.RaycastAll(
                    muzzleOrigin + direction * 0.05f,
                    direction,
                    distance + 0.5f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (RaycastHit hit in hits)
                {
                    Collider collider = hit.collider;
                    if (collider == null || collider.transform.root == transform.root || collider.isTrigger) continue;

                    if (collider.transform == target || collider.transform.IsChildOf(target) || collider.transform.root == target.root)
                    {
                        targetPoint = hit.point;
                        return true;
                    }

                    // The first solid object is cover; do not claim a shot through it.
                    break;
                }
            }

            return false;
        }

        private IEnumerator FireBurst()
        {
            isFiringBurst = true;
            for (int shot = 0; shot < shotsPerBurst; shot++)
            {
                if (!IsAlive || hunterState != HunterState.Engage || !perception.CanSeeTarget) break;
                if (!FireOneShot()) break;
                yield return new WaitForSeconds(fireRate);
            }

            nextBurstTime = Time.time + burstCooldown;
            isFiringBurst = false;
        }

        private bool FireOneShot()
        {
            if (!HasLineOfFire(out Vector3 targetPoint, out Vector3 muzzleOrigin)) return false;
            visuals?.TriggerRecoil();

            Vector3 direction = (targetPoint + Random.insideUnitSphere * 0.025f - muzzleOrigin).normalized;
            Vector3 rayOrigin = muzzleOrigin + direction * 0.05f;
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, direction, weaponRange, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 hitPoint = muzzleOrigin + direction * weaponRange;
            bool fired = false;
            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null || collider.transform.root == transform.root || collider.isTrigger) continue;

                hitPoint = hit.point;
                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.CanTakeDamage)
                {
                    damageable.TakeDamage(new DamageInfo(damagePerShot, gameObject, hit.point, direction));
                }
                fired = true;
                break;
            }

            if (fired)
            {
                CombatEffects.PlayGunshot(muzzleOrigin);
                CombatEffects.SpawnTracer(muzzleOrigin, hitPoint, this);
            }
            return fired;
        }

        private void HandleTargetSpotted(Transform target)
        {
            if (target != null)
            {
                BeginPursuit(target.position);
            }
        }

        private void HandleNoiseHeard(Vector3 noisePosition)
        {
            if (!IsAlive) return;
            // Gunfire is useful evidence when the player is hidden. Preserve an active
            // engagement when the player is already visible instead of restarting pursuit.
            if (perception != null && perception.CanSeeTarget) return;

            // Update pursuit destination to fresh noise evidence without resetting
            // the lost-sight timeout indefinitely if already pursuing a cold trail.
            bool alreadyPursuingCold = (hunterState == HunterState.Pursue);
            BeginPursuit(noisePosition, resetTimeout: !alreadyPursuingCold);
        }

        protected override void OnDamaged(DamageInfo damageInfo)
        {
            visuals?.FlashDamage(Color.red);
            if (damageInfo.Instigator == null || perception == null) return;

            Vector3 attackerPosition = damageInfo.Instigator.transform.position;
            perception.ForceTargetDetection(attackerPosition);

            // Preserve an active combat engagement when the target is currently visible,
            // preventing taking damage from canceling active firing bursts or resetting to Pursue.
            if (hunterState == HunterState.Engage && perception.CanSeeTarget)
            {
                movement?.LookAt(attackerPosition + Vector3.up * 1.15f, 4f);
                return;
            }

            // If taking damage while already pursuing without sight, update heading
            // without resetting the bounded lost-sight timer indefinitely.
            bool alreadyPursuingCold = (hunterState == HunterState.Pursue && !perception.CanSeeTarget);
            BeginPursuit(attackerPosition, resetTimeout: !alreadyPursuingCold);
        }

        protected override void OnDefeated(DamageInfo damageInfo)
        {
            isFiringBurst = false;
            StopAllCoroutines();
            movement?.Stop();

            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            Collider collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            SetHunterState(HunterState.Defeated);
            visuals?.PlayDefeatKnockdown();
        }

        private void SetHunterState(HunterState next)
        {
            hunterState = next;
            switch (next)
            {
                case HunterState.Patrol: ChangeState(AgentState.Patrol); break;
                case HunterState.Pursue: ChangeState(AgentState.Pursue); break;
                case HunterState.Engage:
                    ChangeAlertness(AlertLevel.Combat);
                    ChangeState(AgentState.Engage);
                    break;
                case HunterState.Search: ChangeState(AgentState.Investigate); break;
                case HunterState.Return: ChangeState(AgentState.ReturnToPost); break;
                case HunterState.Defeated: ChangeState(AgentState.Defeated); break;
            }
        }

        private void AddHunterVisualIdentity()
        {
            Transform torso = transform.Find("Visuals/Torso");
            if (torso == null || torso.Find("Hunter_AmberTrackerBadge") != null) return;

            GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            badge.name = "Hunter_AmberTrackerBadge";
            badge.transform.SetParent(torso, false);
            badge.transform.localPosition = new Vector3(0f, 0.075f, 0.202f);
            badge.transform.localScale = new Vector3(0.22f, 0.11f, 0.035f);

            Collider badgeCollider = badge.GetComponent<Collider>();
            if (badgeCollider != null) Destroy(badgeCollider);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            hunterBadgeMaterial = new Material(shader)
            {
                name = "Hunter_AmberTrackerBadge_Material",
                color = hunterAccent
            };
            if (hunterBadgeMaterial.HasProperty("_EmissionColor"))
            {
                hunterBadgeMaterial.EnableKeyword("_EMISSION");
                hunterBadgeMaterial.SetColor("_EmissionColor", hunterAccent * 0.65f);
            }

            Renderer renderer = badge.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = hunterBadgeMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void OnGUI()
        {
            if (!showHunterHUD || !IsAlive) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * hudHeight);
            if (screen.z <= 0f) return;

            float y = Screen.height - screen.y;
            float width = 104f;
            float x = screen.x - width * 0.5f;
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.82f);
            GUI.DrawTexture(new Rect(x, y, width, 8f), Texture2D.whiteTexture);

            GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, width - 2f, 6f), Texture2D.whiteTexture);
            float healthFraction = HealthComponent != null ? HealthComponent.CurrentHealth / HealthComponent.MaxHealth : 1f;
            GUI.color = hunterAccent;
            GUI.DrawTexture(new Rect(x + 1f, y + 1f, (width - 2f) * Mathf.Clamp01(healthFraction), 6f), Texture2D.whiteTexture);

            string stateText;
            switch (hunterState)
            {
                case HunterState.Patrol: stateText = "PATROL"; break;
                case HunterState.Pursue: stateText = "PURSUING"; break;
                case HunterState.Engage: stateText = "ENGAGING"; break;
                case HunterState.Search: stateText = "SEARCHING"; break;
                case HunterState.Return: stateText = "RETURNING"; break;
                default: stateText = "DOWN"; break;
            }

            GUI.color = Color.white;
            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            label.normal.textColor = hunterAccent;
            GUI.Label(new Rect(x - 16f, y - 18f, width + 32f, 17f), $"HUNTER | {stateText}", label);
            GUI.color = Color.white;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (perception != null)
            {
                perception.OnTargetSpotted -= HandleTargetSpotted;
                perception.OnNoiseHeard -= HandleNoiseHeard;
            }

            if (hunterBadgeMaterial != null)
            {
                if (Application.isPlaying) Destroy(hunterBadgeMaterial);
                else DestroyImmediate(hunterBadgeMaterial);
            }
        }
    }
}
