using AntScout.FogOfWar.Core;
using UnityEngine;

namespace AntScout.FogOfWar.Interfaces
{
    /// <summary>
    /// Contract for entities whose rendering and selection are dynamically governed by the Fog of War.
    /// Dynamic units (Ants) hide in Explored and Unexplored fog.
    /// Static structures (Nests) and resources remain visible once Explored.
    /// </summary>
    public interface IFogCullable
    {
        /// <summary>World position used to sample fog visibility.</summary>
        Vector3 CullPosition { get; }

        /// <summary>Colony/team identifier this entity belongs to (0 = Player, 1+ = Enemy, -1 = Neutral).</summary>
        int ColonyTeamId { get; }

        /// <summary>Whether this entity is currently active and eligible for culling evaluations.</summary>
        bool IsCullableActive { get; }

        /// <summary>
        /// Callback invoked whenever fog visibility is evaluated for this entity from the local player's perspective.
        /// </summary>
        /// <param name="state">The evaluated visibility state (Unexplored, Explored, or Visible).</param>
        void OnFogVisibilityEvaluated(FogVisibilityState state);
    }
}
