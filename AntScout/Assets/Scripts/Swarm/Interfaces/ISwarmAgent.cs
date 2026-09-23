using AntScout.Pheromone;
using AntScout.Swarm.Nest;
using UnityEngine;

namespace AntScout.Swarm.Interfaces
{
    /// <summary>
    /// Lifecycle and behavioral states for autonomous swarm agents.
    /// </summary>
    public enum SwarmAgentState
    {
        Idle = 0,
        SeekingTrail = 1,
        FollowingTrailOutbound = 2,
        AtTrailEnd = 3,
        ReturningHome = 4
    }

    /// <summary>
    /// Common contract for all colony swarm agents (Workers, Soldiers, etc.).
    /// Grounded in Liskov Substitution Principle (LSP) and Interface Segregation (ISP).
    /// </summary>
    public interface ISwarmAgent
    {
        SwarmAgentState CurrentState { get; }
        Transform AgentTransform { get; }
        ColonyNest HomeNest { get; }

        void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter);
        void OrderReturnHome();
    }
}
