using AntScout.FogOfWar.Core;
using UnityEngine;

namespace AntScout.FogOfWar.Interfaces
{
    /// <summary>
    /// Service contract (DIP) providing global access to Fog of War state queries and revealer lifecycle management.
    /// </summary>
    public interface IFogOfWarService
    {
        /// <summary>
        /// Registers a vision revealer into active tracking.
        /// </summary>
        void RegisterRevealer(IVisionRevealer revealer);

        /// <summary>
        /// Unregisters a vision revealer from active tracking.
        /// </summary>
        void UnregisterRevealer(IVisionRevealer revealer);

        /// <summary>
        /// Evaluates the discrete visibility state (Unexplored, Explored, Visible) of a world position for a given colony team.
        /// </summary>
        FogVisibilityState GetVisibilityState(Vector3 worldPosition, int colonyTeamId);

        /// <summary>
        /// Fast check if a world position is currently inside active line-of-sight for a given colony team.
        /// </summary>
        bool IsVisible(Vector3 worldPosition, int colonyTeamId);

        /// <summary>
        /// Fast check if a world position has ever been explored by a given colony team.
        /// </summary>
        bool IsExplored(Vector3 worldPosition, int colonyTeamId);

        /// <summary>
        /// The active GPU texture encoding Explored (R) and Visible (G) channels for rendering.
        /// </summary>
        Texture2D FogTexture { get; }
    }
}
