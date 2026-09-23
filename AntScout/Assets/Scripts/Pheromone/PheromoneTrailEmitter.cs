using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Core.Interfaces;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Manages the registration, spatial spacing, intensity decay, and expiration of 3D pheromone scent nodes.
    /// Implements IPheromoneEmitter to allow decoupled emission by player scouts, AI units, or stationary scent beacons.
    /// </summary>
    public class PheromoneTrailEmitter : MonoBehaviour, IPheromoneEmitter
    {
        [Header("Configuration")]
        [Tooltip("Tunable configuration defining node placement spacing and lifetime.")]
        [SerializeField] private ScoutConfigSO _config;

        private readonly List<PheromoneNode> _activeNodes = new List<PheromoneNode>();

        public event Action<PheromoneNode> OnNodeEmitted;
        public event Action OnTrailUpdated;

        public IReadOnlyList<PheromoneNode> ActiveNodes => _activeNodes;
        public int ActiveNodeCount => _activeNodes.Count;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[PheromoneTrailEmitter] Missing required ScoutConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ScoutConfigSO in the Inspector.");
            }
        }

        private void Update()
        {
            PruneExpiredNodes();
        }

        /// <summary>
        /// Attempts to deposit a new node. Enforces spatial spacing based on NodePlacementDistance to prevent excess nodes.
        /// </summary>
        public void EmitNode(Vector3 worldPosition, PheromoneType type)
        {
            if (_activeNodes.Count > 0)
            {
                PheromoneNode lastNode = _activeNodes[_activeNodes.Count - 1];
                float distanceSqr = (lastNode.Position - worldPosition).sqrMagnitude;
                float minSpacing = _config.NodePlacementDistance;

                if (distanceSqr < (minSpacing * minSpacing))
                {
                    return;
                }
            }

            PheromoneNode newNode = new PheromoneNode(worldPosition, Time.time, type);
            _activeNodes.Add(newNode);

            OnNodeEmitted?.Invoke(newNode);
            OnTrailUpdated?.Invoke();
        }

        /// <summary>
        /// Cleans up nodes whose lifetime has expired.
        /// </summary>
        private void PruneExpiredNodes()
        {
            if (_activeNodes.Count == 0) return;

            float currentTime = Time.time;
            float lifetime = _config.NodeLifetime;
            int removedCount = 0;

            // Nodes are stored chronologically; oldest nodes are at index 0
            for (int i = 0; i < _activeNodes.Count; i++)
            {
                if (_activeNodes[i].GetCurrentIntensity(currentTime, lifetime) <= 0f)
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
                _activeNodes.RemoveRange(0, removedCount);
                OnTrailUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Clears all active scent nodes immediately (e.g. on respawn or trail wipe).
        /// </summary>
        public void ClearTrail()
        {
            if (_activeNodes.Count > 0)
            {
                _activeNodes.Clear();
                OnTrailUpdated?.Invoke();
            }
        }
    }
}
