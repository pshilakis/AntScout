using UnityEngine;

namespace AntScout.FogOfWar.Interfaces
{
    /// <summary>
    /// Lean interface (ISP) implemented by any entity that projects line-of-sight in the Fog of War.
    /// Supports Heroes, Swarm Agents, Colony Nests, and sensor deployables.
    /// </summary>
    public interface IVisionRevealer
    {
        /// <summary>
        /// Current world position of the vision origin.
        /// </summary>
        Vector3 RevealerPosition { get; }

        /// <summary>
        /// Radius of vision in world units (meters).
        /// </summary>
        float VisionRadius { get; }

        /// <summary>
        /// The colony team identifier this revealer belongs to (e.g. 0 = Player Colony, 1 = Enemy Colony).
        /// </summary>
        int ColonyTeamId { get; }

        /// <summary>
        /// Whether this revealer is currently active, alive, and projecting sight.
        /// </summary>
        bool IsRevealerActive { get; }
    }
}
