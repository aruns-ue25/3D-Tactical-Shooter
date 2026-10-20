using System;
using UnityEngine;

namespace TacticalShooter.AI.Core
{
    /// <summary>
    /// Lifecycle and tactical decision states for AI agents.
    /// </summary>
    public enum AgentState
    {
        /// <summary>Standing guard or stationary observation.</summary>
        Idle,

        /// <summary>Walking assigned patrol route around designated anchor post.</summary>
        Patrol,

        /// <summary>Target in line of sight and within engagement range: firing bursts.</summary>
        Engage,

        /// <summary>Moving toward tactical cover or advantageous vantage point while maintaining defensive control.</summary>
        Reposition,

        /// <summary>Line of sight lost: investigating the target's Last Known Position (LKP).</summary>
        Investigate,

        /// <summary>Investigation concluded without re-establishing contact: returning to guard anchor post.</summary>
        ReturnToPost,

        /// <summary>Incapacitated or defeated.</summary>
        Defeated,

        /// <summary>Directly pursuing a target using fresh visual or acoustic evidence.</summary>
        Pursue
    }

    /// <summary>
    /// Distinguishable AI agent roles as defined in the game plan.
    /// </summary>
    public enum AgentRole
    {
        /// <summary>Area-defending combat sentinel (Tactical Soldier).</summary>
        Guard,

        /// <summary>Aggressive tracking combat agent.</summary>
        Hunter,

        /// <summary>Flanking and encirclement combat agent.</summary>
        Flanker,

        /// <summary>Tactical assistance and reinforcement agent.</summary>
        Support
    }

    /// <summary>
    /// Alertness level driven by perception and stimuli.
    /// </summary>
    public enum AlertLevel
    {
        /// <summary>Normal routine patrol / guard duties.</summary>
        Unaware,

        /// <summary>Heard audio cue or caught fleeting peripheral movement.</summary>
        Suspicious,

        /// <summary>Target identified or searching Last Known Position.</summary>
        Alert,

        /// <summary>Actively engaged in direct combat.</summary>
        Combat
    }
}
