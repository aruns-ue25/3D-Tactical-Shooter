using System.Collections;
using UnityEngine;
using TacticalShooter.AI.Core;
using TacticalShooter.AI.Perception;
using TacticalShooter.AI.Movement;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Agents
{
    /// <summary>
    /// Support Agent - Heavy Defensive Combat Anchor.
    /// Provides suppressive fire, defensive area denial, and fortified chokepoint anchoring.
    /// </summary>
    public class SupportAgent : AIAgentBase
    {
        [Header("Defensive Anchor Settings (Objective Circling)")]
        [SerializeField] private Vector3 anchorPosition;
        [SerializeField] private float anchorLeashRadius = 7.0f;
        [SerializeField] private Vector3[] patrolWaypoints;
        [SerializeField] private float waypointWaitTime = 0.4f;
        [SerializeField] private float patrolSpeed = 2.4f;

        [Header("Suppressive Fire (Heavy SAW LMG)")]
        [SerializeField] private float attackRange = 28f;
        [SerializeField] private float fireRate = 0.15f; // Calibrated to ~6.6 rounds per second
        [SerializeField] private int burstCount = 5; // 5 rounds per shot burst
        [SerializeField] private float burstCooldown = 2.5f; // Tactical cooldown period
        [SerializeField] private float damagePerShot = 8.5f;

        [Header("HUD")]
        [SerializeField] private bool showOverheadHUD = true;
        [SerializeField] private float overheadHudHeight = 2.65f;

        private int currentWaypointIndex = 0;
        private float waypointTimer = 0f;
        private float stuckWatchdogTimer = 0f;
        private float nextBurstTime = 0f;
        private bool isBurstFiring = false;
        private Texture2D hudBorderTex;
        private Texture2D hudBgTex;
        private Texture2D hudFillTex;
        private RoyalGuardVisuals visuals;

        public Transform TargetTransform
        {
            get
            {
                if (perception != null && perception.Target != null) return perception.Target;
                var player = GameObject.Find("Player");
                if (player != null) return player.transform;
                return null;
            }
        }

        protected override void Awake()
        {
            agentRole = AgentRole.Support;
            agentDisplayName = "★ BOSS ★ Heavy Juggernaut";
            base.Awake();
        }

        protected override void InitializeAgent()
        {
            agentRole = AgentRole.Support; // Boss Archetype
            agentDisplayName = "★ BOSS ★ Heavy Juggernaut";

            // Only auto-bind if deployment waypoints were not already assigned
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
            {
                AutoBindObjectiveAnchor();
            }

            if (health != null)
            {
                health.SetMaxHealth(500f, true);
            }

            visuals = GetComponent<RoyalGuardVisuals>();
            GenerateHUDTextures();
            ChangeState(AgentState.Patrol);
        }

        private void AutoBindObjectiveAnchor()
        {
            // Discriminate between Level 2 Records (West Room), Level 2 Archive (East Room), and Level 3 Helipad
            if (gameObject.name.Contains("Records") || (transform.position.x < 35f && transform.position.y < 5.0f))
            {
                // Level 2 Classified Records Terminal (West Room at X = 13, Z = 44)
                var objRecords = GameObject.Find("ObjectivePoint_Records");
                if (objRecords != null) anchorPosition = new Vector3(objRecords.transform.position.x, 3.6f, objRecords.transform.position.z);
                else anchorPosition = new Vector3(13.0f, 3.6f, 44.0f);
                GenerateCircularWaypoints(anchorPosition, 2.8f);
            }
            else if (gameObject.name.Contains("Archive") || transform.position.y < 5.0f)
            {
                // Level 2 Secure Archive Terminal (East Room at X = 58, Z = 48)
                var objArchive = GameObject.Find("Objective Point") ?? GameObject.Find("ObjectiveTerminal");
                if (objArchive != null) anchorPosition = new Vector3(objArchive.transform.position.x, 3.6f, objArchive.transform.position.z);
                else anchorPosition = new Vector3(58.0f, 3.6f, 48.0f);
                GenerateCircularWaypoints(anchorPosition, 3.2f);
            }
            else
            {
                // Level 3 Helipad Extraction Zone (Roof Deck at X = 57, Z = 48)
                var extZone = GameObject.Find("ExtractionPoint") ?? GameObject.Find("ExtractionZone");
                if (extZone != null) anchorPosition = new Vector3(extZone.transform.position.x, 7.2f, extZone.transform.position.z);
                else anchorPosition = new Vector3(57.0f, 7.2f, 48.0f);
                GenerateCircularWaypoints(anchorPosition, 4.5f);
            }
        }

        private void GenerateCircularWaypoints(Vector3 center, float radius)
        {
            int count = 8;
            patrolWaypoints = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                float x = center.x + Mathf.Cos(angle) * radius;
                float z = center.z + Mathf.Sin(angle) * radius;
                Vector3 rawPos = new Vector3(x, center.y, z);
                if (UnityEngine.AI.NavMesh.SamplePosition(rawPos, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    patrolWaypoints[i] = hit.position;
                }
                else
                {
                    patrolWaypoints[i] = rawPos;
                }
            }
            anchorLeashRadius = radius + 2.5f;
            waypointWaitTime = 0.35f;
        }

        public void ConfigureDeployment(Vector3 post, float leash, Vector3[] waypoints)
        {
            anchorPosition = post;
            anchorLeashRadius = leash;
            if (waypoints != null && waypoints.Length > 0)
            {
                patrolWaypoints = waypoints;
            }
            else
            {
                GenerateCircularWaypoints(post, 3.2f);
            }
        }

        protected override void UpdateDecision()
        {
            if (!IsAlive) return;

            EvaluateBehavior();
        }

        private void EvaluateBehavior()
        {
            Transform target = TargetTransform;
            bool targetVisible = perception != null && perception.CanSeeTarget;
            float distToTarget = target != null ? Vector3.Distance(transform.position, target.position) : 999f;
            float distFromAnchor = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(anchorPosition.x, 0, anchorPosition.z));
            float verticalDist = target != null ? Mathf.Abs(target.position.y - anchorPosition.y) : (perception != null ? Mathf.Abs(perception.LastKnownPosition.y - anchorPosition.y) : 0f);

            // Floor tether check: Support Agent defends assigned floor post and never travels between floors
            if (verticalDist > 2.0f)
            {
                if (currentState != AgentState.Idle && currentState != AgentState.Patrol)
                {
                    ChangeState(AgentState.Patrol);
                }
                ExecutePatrol();
                return;
            }

            // Strict leash: If pushed or dragged away from objective, return to objective circle immediately
            if (distFromAnchor > anchorLeashRadius)
            {
                if (movement != null)
                {
                    movement.MoveTo(anchorPosition, patrolSpeed * 1.3f);
                }
                return;
            }

            if (targetVisible && distToTarget <= attackRange)
            {
                if (currentState != AgentState.Engage)
                {
                    ChangeState(AgentState.Engage);
                }
                ExecuteEngagement(target);
            }
            else if (targetVisible && distToTarget > attackRange)
            {
                // Reposition towards anchor while keeping aim
                if (currentState != AgentState.Reposition)
                {
                    ChangeState(AgentState.Reposition);
                }
                if (movement != null)
                {
                    movement.MoveTo(anchorPosition, patrolSpeed);
                }
            }
            else
            {
                // Patrol assigned circular waypoints orbiting anchor
                if (currentState != AgentState.Patrol && currentState != AgentState.Idle)
                {
                    ChangeState(AgentState.Patrol);
                }
                ExecutePatrol();
            }
        }

        private void ExecutePatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            Vector3 targetWp = patrolWaypoints[currentWaypointIndex];
            float dist = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(targetWp.x, 0, targetWp.z));

            if (dist <= 1.5f)
            {
                stuckWatchdogTimer = 0f;
                waypointTimer += Time.deltaTime;
                if (waypointTimer >= waypointWaitTime)
                {
                    waypointTimer = 0f;
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                }
            }
            else
            {
                stuckWatchdogTimer += Time.deltaTime;
                if (stuckWatchdogTimer >= 5.0f)
                {
                    // Unstuck watchdog: auto-advance to next waypoint if obstructed or path blocked
                    stuckWatchdogTimer = 0f;
                    currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Length;
                }

                if (movement != null)
                {
                    movement.MoveTo(targetWp, patrolSpeed);
                }
            }
        }

        private void ExecuteEngagement(Transform target)
        {
            if (target == null) return;

            // Aim towards target
            Vector3 lookDir = (target.position - transform.position);
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
            }

            // Continue actively circling the objective even during combat!
            ExecutePatrol();

            // Suppressive Burst firing (14 rounds from heavy SAW LMG)
            if (!isBurstFiring && Time.time >= nextBurstTime)
            {
                StartCoroutine(FireSuppressiveBurst(target));
            }
        }

        private IEnumerator FireSuppressiveBurst(Transform target)
        {
            isBurstFiring = true;

            for (int i = 0; i < burstCount; i++)
            {
                if (!IsAlive || target == null) break;

                // Fire bullet
                Vector3 muzzlePos = transform.position + Vector3.up * 1.3f + transform.forward * 0.6f;
                Vector3 shootDir = (target.position + Vector3.up * 1.0f - muzzlePos).normalized;

                CombatEffects.PlayGunshot(muzzlePos);

                if (Physics.Raycast(muzzlePos, shootDir, out RaycastHit hit, attackRange + 5f))
                {
                    CombatEffects.SpawnTracer(muzzlePos, hit.point, this);
                    CombatEffects.PlayHitImpact(hit.point);
                    CombatEffects.SpawnImpactSpark(hit.point, hit.normal, this);

                    var dmg = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmg != null && hit.collider.gameObject != gameObject)
                    {
                        dmg.TakeDamage(new DamageInfo(damagePerShot, gameObject, hit.point, shootDir));
                    }
                }
                else
                {
                    CombatEffects.SpawnTracer(muzzlePos, muzzlePos + shootDir * attackRange, this);
                }

                yield return new WaitForSeconds(fireRate);
            }

            nextBurstTime = Time.time + burstCooldown;
            isBurstFiring = false;
        }

        protected override void OnDamaged(DamageInfo damageInfo)
        {
            visuals?.FlashDamage(Color.red);

            if (damageInfo.Instigator != null && perception != null)
            {
                perception.ForceTargetDetection(damageInfo.Instigator.transform.position);
                movement?.LookAt(damageInfo.Instigator.transform.position, 5f);
            }
        }

        protected override void OnDefeated(DamageInfo damageInfo)
        {
            isBurstFiring = false;
            StopAllCoroutines();

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            visuals?.PlayDefeatKnockdown();
        }

        private void GenerateHUDTextures()
        {
            if (hudBorderTex == null)
            {
                hudBorderTex = new Texture2D(1, 1);
                hudBorderTex.SetPixel(0, 0, new Color(0.95f, 0.82f, 0.25f, 0.95f)); // Imperial Gilded Gold
                hudBorderTex.Apply();
            }
            if (hudBgTex == null)
            {
                hudBgTex = new Texture2D(1, 1);
                hudBgTex.SetPixel(0, 0, new Color(0.12f, 0.04f, 0.05f, 0.92f)); // Dark blood background
                hudBgTex.Apply();
            }
            if (hudFillTex == null)
            {
                hudFillTex = new Texture2D(1, 1);
                hudFillTex.SetPixel(0, 0, new Color(0.85f, 0.12f, 0.15f, 0.98f)); // Crimson Boss HP fill
                hudFillTex.Apply();
            }
        }

        private void OnGUI()
        {
            if (!showOverheadHUD || !IsAlive) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + Vector3.up * overheadHudHeight;
            Vector3 camToAgent = worldPos - cam.transform.position;
            if (Vector3.Dot(cam.transform.forward, camToAgent) <= 0f) return;

            float distToCam = camToAgent.magnitude;
            if (distToCam > 35f) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0) return;

            float barWidth = 140f;
            float barHeight = 10f;
            float x = screenPos.x - barWidth * 0.5f;
            float y = Screen.height - screenPos.y - barHeight * 0.5f;

            Color prevColor = GUI.color;

            // Gold Border
            if (hudBorderTex != null)
            {
                GUI.DrawTexture(new Rect(x - 2f, y - 2f, barWidth + 4f, barHeight + 4f), hudBorderTex);
            }

            // Dark inner background
            if (hudBgTex != null)
            {
                GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), hudBgTex);
            }

            // Crimson Boss Health Bar Fill
            if (hudFillTex != null && health != null)
            {
                float pct = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
                GUI.DrawTexture(new Rect(x + 1f, y + 1f, (barWidth - 2f) * pct, barHeight - 2f), hudFillTex);
            }

            // Big Bold Imperial Gold Title
            GUIStyle bossTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            bossTitleStyle.normal.textColor = new Color(1.0f, 0.88f, 0.35f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x - 30f, y - 18f, barWidth + 60f, 17f), "★ BOSS: HEAVY JUGGERNAUT ★", bossTitleStyle);

            GUI.color = prevColor;
        }
    }
}
