using System.Collections.Generic;
using AntScout.Core.Enums;
using PGS.Core.Time;
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
        private readonly List<Vector3> _originalPositions = new List<Vector3>();
        private readonly PheromoneType _type;
        private readonly float _createdAt;
        private bool _isSealed;

        /// <summary>
        /// Base physical scaling factor mapping normalized (0 to 1) optimization intensity
        /// to frame-rate independent ribbon relaxation.
        /// </summary>
        private const float BaseRelaxationScale = 0.5f;

        public PheromoneType Type => _type;
        public bool IsSealed => _isSealed;
        public float CreatedAt => _createdAt;
        public IReadOnlyList<PheromoneNode> Nodes => _nodes;
        public IReadOnlyList<Vector3> OriginalPositions => _originalPositions;
        public int NodeCount => _nodes.Count;
        public bool IsEmpty => _nodes.Count == 0;

        public PheromoneNode FirstNode => _nodes.Count > 0 ? _nodes[0] : default;
        public PheromoneNode LastNode => _nodes.Count > 0 ? _nodes[_nodes.Count - 1] : default;

        /// <summary>
        /// Direct Euclidean distance between the start (Nest) and end (Food) nodes.
        /// </summary>
        public float DirectDistance => (_nodes.Count >= 2)
            ? Vector3.Distance(_nodes[0].Position, _nodes[_nodes.Count - 1].Position)
            : 0f;

        /// <summary>
        /// Total traveled path length along the live polyline ribbon.
        /// </summary>
        public float CurrentLength
        {
            get
            {
                if (_nodes.Count < 2) return 0f;
                float len = 0f;
                for (int i = 0; i < _nodes.Count - 1; i++)
                {
                    len += Vector3.Distance(_nodes[i].Position, _nodes[i + 1].Position);
                }
                return len;
            }
        }

        /// <summary>
        /// Total initial path length as originally drawn by the player before ant optimization.
        /// </summary>
        public float OriginalLength
        {
            get
            {
                if (_originalPositions.Count < 2) return 0f;
                float len = 0f;
                for (int i = 0; i < _originalPositions.Count - 1; i++)
                {
                    len += Vector3.Distance(_originalPositions[i], _originalPositions[i + 1]);
                }
                return len;
            }
        }

        /// <summary>
        /// Live path efficiency ratio (0.0 to 1.0 = Direct Distance / Current Length). 1.0 is a perfectly straight highway.
        /// </summary>
        public float CurrentEfficiency => CurrentLength > 0.05f ? Mathf.Clamp01(DirectDistance / CurrentLength) : 1f;

        /// <summary>
        /// Baseline path efficiency ratio as originally drawn by the player (0.0 to 1.0).
        /// </summary>
        public float OriginalEfficiency => OriginalLength > 0.05f ? Mathf.Clamp01(DirectDistance / OriginalLength) : 1f;

        public PheromoneTrailSegment(PheromoneType type)
        {
            _type = type;
            _isSealed = false;
            _createdAt = PgsTime.ElapsedTime;
        }

        /// <summary>
        /// Appends a new scent node to this segment and snapshots its initial position.
        /// </summary>
        public void AddNode(Vector3 position, float timeStamp)
        {
            if (_isSealed) return;
            _nodes.Add(new PheromoneNode(position, timeStamp, _type));
            _originalPositions.Add(position);
        }

        /// <summary>
        /// Returns distance in meters this node has migrated from its original drawn position.
        /// </summary>
        public float GetDisplacement(int index)
        {
            if (index < 0 || index >= _nodes.Count || index >= _originalPositions.Count) return 0f;
            return Vector3.Distance(_nodes[index].Position, _originalPositions[index]);
        }

        /// <summary>
        /// Seals this segment, preventing further nodes from being appended (triggered on mouse/shift release).
        /// </summary>
        public void Seal()
        {
            _isSealed = true;
        }

        /// <summary>
        /// O(1) direct node reinforcement using local array index.
        /// Refreshes timestamp and boosts traffic strength.
        /// </summary>
        public void ReinforceNode(int index, float currentTime, float strengthBoost = 0.5f)
        {
            if (index < 0 || index >= _nodes.Count) return;
            _nodes[index] = _nodes[index].WithReinforcement(currentTime, strengthBoost);
        }

        /// <summary>
        /// Finds the index of the closest node within this segment to a given position within maxRadiusSqr.
        /// </summary>
        public int FindClosestNodeIndex(Vector3 position, float maxRadiusSqr)
        {
            int closestIdx = -1;
            float closestDistSqr = float.MaxValue;
            for (int i = 0; i < _nodes.Count; i++)
            {
                Vector3 p = _nodes[i].Position;
                float dx = p.x - position.x;
                float dz = p.z - position.z;
                float distSqr = dx * dx + dz * dz;
                if (distSqr <= maxRadiusSqr && distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closestIdx = i;
                }
            }
            return closestIdx;
        }

        /// <summary>
        /// Direct node reinforcement and whole-ribbon biological path optimization (string tension).
        /// When workers haul food cargo along this trail, the entire intermediate ribbon of nodes
        /// relaxes inward toward the direct baseline chord connecting Nest (index 0) and Food (last index).
        /// Endpoints remain permanently anchored in place.
        /// Prevents single-node tearing, eliminates bypassed abandoned loops, and smoothly pulls
        /// the entire drawn stroke into an optimal straight highway.
        /// </summary>
        public void ReinforceAndMigrateRibbon(int trackedIndex, float migrationRate, float deltaTime, float currentTime, float strengthBoost = 0.5f)
        {
            if (_nodes.Count < 3)
            {
                if (trackedIndex >= 0 && trackedIndex < _nodes.Count)
                {
                    ReinforceNode(trackedIndex, currentTime, strengthBoost);
                }
                return;
            }

            // Always reinforce the node closest to the active worker
            if (trackedIndex >= 0 && trackedIndex < _nodes.Count)
            {
                _nodes[trackedIndex] = _nodes[trackedIndex].WithReinforcement(currentTime, strengthBoost);
            }

            // Biological Endpoint Anchoring:
            // When workers are hauling cargo near the food tip (within 2 nodes of the end),
            // explicitly reinforce the tip endpoint (_nodes.Count - 1) so the highway never burns backward.
            if (trackedIndex >= _nodes.Count - 2 && _nodes.Count > 0)
            {
                int tipIdx = _nodes.Count - 1;
                _nodes[tipIdx] = _nodes[tipIdx].WithReinforcement(currentTime, strengthBoost);
            }

            // When workers are near the nest entrance (within 2 nodes of index 0), reinforce the nest endpoint
            if (trackedIndex <= 1 && _nodes.Count > 0)
            {
                _nodes[0] = _nodes[0].WithReinforcement(currentTime, strengthBoost);
            }

            if (migrationRate <= 0.0001f) return;

            // Global chord baseline connecting the trail's endpoints (Nest <-> Food)
            Vector3 endpointA = _nodes[0].Position;
            Vector3 endpointB = _nodes[_nodes.Count - 1].Position;
            Vector3 chord = endpointB - endpointA;
            chord.y = 0f;
            float chordSqr = chord.sqrMagnitude;
            if (chordSqr <= 0.01f) return;

            float t = Mathf.Clamp01(migrationRate * BaseRelaxationScale * deltaTime);

            // Relax all intermediate nodes toward their chord projection and neighbor midpoints
            for (int i = 1; i < _nodes.Count - 1; i++)
            {
                Vector3 currentPos = _nodes[i].Position;

                // 1. Direct chord projection
                Vector3 toCurrent = currentPos - endpointA;
                toCurrent.y = 0f;
                float tProj = Mathf.Clamp01(Vector3.Dot(toCurrent, chord) / chordSqr);
                Vector3 chordTarget = endpointA + chord * tProj;
                chordTarget.y = currentPos.y;

                // 2. Neighbor midpoint smoothing (maintains even chain tension along ribbon)
                Vector3 prevPos = _nodes[i - 1].Position;
                Vector3 nextPos = _nodes[i + 1].Position;
                Vector3 neighborMidpoint = (prevPos + nextPos) * 0.5f;
                neighborMidpoint.y = currentPos.y;

                // 3. Blend: 50% pull toward the direct highway chord, 50% neighbor smoothing
                Vector3 desiredPos = Vector3.Lerp(neighborMidpoint, chordTarget, 0.5f);
                Vector3 targetPos = Vector3.Lerp(currentPos, desiredPos, t);
                targetPos.y = currentPos.y;

                // Gently refresh timestamp so intermediate nodes don't decay into a gap while migrating
                _nodes[i] = _nodes[i].WithAdjustedPosition(targetPos, currentTime, strengthBoost * 0.1f);
            }
        }

        /// <summary>
        /// Backwards-compatible overload routing to <see cref="ReinforceAndMigrateRibbon"/>.
        /// </summary>
        public void ReinforceAndMigrateNode(int index, Vector3 antPosition, float migrationRate, float deltaTime, float currentTime, float strengthBoost = 0.5f)
        {
            ReinforceAndMigrateRibbon(index, migrationRate, deltaTime, currentTime, strengthBoost);
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
