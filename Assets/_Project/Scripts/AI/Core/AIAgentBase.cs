using System;
using UnityEngine;
using TacticalShooter.AI.Perception;
using TacticalShooter.AI.Movement;
using TacticalShooter.Systems.Combat;

namespace TacticalShooter.AI.Core
{
    /// <summary>
    /// Shared base foundation class for all AI combat agents (Guard, Hunter, Flanker, Support).
    /// Owned by the Agent Controller architecture, extended by specific agent behaviors.
    /// Manages lifecycle, common perception, movement, health integration, and state events.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(AIPerception))]
    [RequireComponent(typeof(AIMovement))]
    public abstract class AIAgentBase : MonoBehaviour
    {
        [Header("Agent Identity")]
        [SerializeField] protected AgentRole agentRole = AgentRole.Guard;
        [SerializeField] protected string agentDisplayName = "Tactical Soldier (Guard)";

        [Header("State Tracking")]
        [SerializeField] protected AgentState currentState = AgentState.Idle;
        [SerializeField] protected AlertLevel alertLevel = AlertLevel.Unaware;

        protected Health health;
        protected AIPerception perception;
        protected AIMovement movement;

        public AgentRole Role => agentRole;
        public string DisplayName => agentDisplayName;
        public AgentState CurrentState => currentState;
        public AlertLevel CurrentAlertness => alertLevel;
        public Health HealthComponent
        {
            get
            {
                if (health == null)
                {
                    health = GetComponent<Health>();
                    if (health != null)
                    {
                        health.OnDamaged -= HandleDamaged;
                        health.OnDamaged += HandleDamaged;
                        health.OnDefeated -= HandleDefeated;
                        health.OnDefeated += HandleDefeated;
                    }
                }
                return health;
            }
        }

        public AIPerception Perception
        {
            get
            {
                if (perception == null) perception = GetComponent<AIPerception>();
                return perception;
            }
        }

        public AIMovement Movement
        {
            get
            {
                if (movement == null) movement = GetComponent<AIMovement>();
                return movement;
            }
        }

        public bool IsAlive => HealthComponent != null && HealthComponent.IsAlive;

        public event Action<AgentState, AgentState> OnStateChanged;
        public event Action<AlertLevel> OnAlertnessChanged;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            perception = GetComponent<AIPerception>();
            movement = GetComponent<AIMovement>();

            if (health != null)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDefeated += HandleDefeated;
            }
        }

        protected virtual void Start()
        {
            InitializeAgent();
        }

        protected virtual void Update()
        {
            if (!IsAlive) return;

            // 1. Refresh perception system
            if (perception != null)
            {
                perception.UpdatePerception();
            }

            // 2. Execute agent-specific behavioral decisions
            UpdateDecision();
        }

        protected virtual void OnDestroy()
        {
            if (health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDefeated -= HandleDefeated;
            }
        }

        /// <summary>
        /// Transitions agent to a new state and raises change events.
        /// </summary>
        public virtual void ChangeState(AgentState newState)
        {
            if (currentState == newState) return;

            AgentState prevState = currentState;
            currentState = newState;
            OnStateChanged?.Invoke(prevState, newState);
        }

        /// <summary>
        /// Updates the alertness level and raises change events.
        /// </summary>
        public virtual void ChangeAlertness(AlertLevel newAlert)
        {
            if (alertLevel == newAlert) return;

            alertLevel = newAlert;
            OnAlertnessChanged?.Invoke(newAlert);
        }

        private void HandleDamaged(DamageInfo damageInfo)
        {
            if (!IsAlive) return;
            OnDamaged(damageInfo);
        }

        private void HandleDefeated(DamageInfo damageInfo)
        {
            ChangeState(AgentState.Defeated);
            if (movement != null) movement.Stop();
            OnDefeated(damageInfo);
        }

        /// <summary>
        /// Subclass initialization hook for custom setups.
        /// </summary>
        protected abstract void InitializeAgent();

        /// <summary>
        /// Subclass per-frame state machine and decision evaluation.
        /// </summary>
        protected abstract void UpdateDecision();

        /// <summary>
        /// Subclass reaction when damaged by weapon fire.
        /// </summary>
        protected abstract void OnDamaged(DamageInfo damageInfo);

        /// <summary>
        /// Subclass reaction when health is depleted.
        /// </summary>
        protected abstract void OnDefeated(DamageInfo damageInfo);
    }
}
