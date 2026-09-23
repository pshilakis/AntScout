using AntScout.Core.Enums;
using UnityEngine;

namespace AntScout.Core.Interfaces
{
    /// <summary>
    /// Contract for emitting chemical pheromone nodes into the world.
    /// Supports the Open/Closed Principle (OCP) by decoupling chemical depositor components from trail storage/rendering.
    /// </summary>
    public interface IPheromoneEmitter
    {
        /// <summary>
        /// Deposits a pheromone node at the specified 3D world coordinate.
        /// </summary>
        /// <param name="worldPosition">World coordinates for the node.</param>
        /// <param name="type">The chemical classification of scent.</param>
        void EmitNode(Vector3 worldPosition, PheromoneType type);

        /// <summary>
        /// The number of active, non-expired pheromone nodes currently tracked.
        /// </summary>
        int ActiveNodeCount { get; }
    }
}
