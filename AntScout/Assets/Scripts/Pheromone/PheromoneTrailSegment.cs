using System.Collections.Generic;
using AntScout.Core.Enums;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Represents an independent, contiguous stroke of pheromone scent nodes deposited during a single keypress.
    /// Tracks its own chemical classification, active node chain, and sealed/decay lifecycle.
    /// </summary>
    public class PheromoneTrailSegment
    {
        private readonly List<PheromoneNode> _nodes = new List<PheromoneNode>();
        private readonly PheromoneType _type;
        private bool _isSealed;

        public PheromoneType Type => _type;
        public bool IsSealed => _isSealed;
        public IReadOnlyList<PheromoneNode> Nodes => _nodes;
        public int NodeCount => _nodes.Count;
        public bool IsEmpty => _nodes.Count == 0;

        public PheromoneNode FirstNode => _nodes.Count > 0 ? _nodes[0] : default;
        public PheromoneNode LastNode => _nodes.Count > 0 ? _nodes[_nodes.Count - 1] : default;

        public PheromoneTrailSegment(PheromoneType type)
        {
            _type = type;
            _isSealed = false;
        }

        /// <summary>
        /// Appends a new scent node to this segment.
        /// </summary>
        public void AddNode(Vector3 position, float timeStamp)
        {
            if (_isSealed) return;
            _nodes.Add(new PheromoneNode(position, timeStamp, _type));
        }

        /// <summary>
        /// Seals this segment, preventing further nodes from being appended (triggered on mouse/shift release).
        /// </summary>
        public void Seal()
        {
            _isSealed = true;
        }

        /// <summary>
        /// Removes expired nodes from the head of this segment as time elapses.
        /// </summary>
        public int PruneExpired(float currentTime, float lifetime)
        {
            if (_nodes.Count == 0) return 0;

            int removedCount = 0;
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].GetCurrentIntensity(currentTime, lifetime) <= 0f)
                {
                    removedCount++;
                }
                else
                {
                    break;
                }
            }

            if (removedCount > 0)
            {
                _nodes.RemoveRange(0, removedCount);
            }

            return removedCount;
        }
    }
}
