using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using AntScout.Swarm.Nest;
using Pathfinding;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Bridges the pheromone trail simulation with A* Pathfinding Project Pro (IAstarAI).
    /// Samples scent gradients from PheromoneTrailEmitter and projects navigation destinations to A*.
    /// </summary>
    public class AntTrailFollower : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for worker movement speed, sensory radii, and waypoint lookahead.")]
        [SerializeField] private SwarmConfigSO _config;

        private IAstarAI _ai;
        private PheromoneTrailEmitter _emitter;
        private ColonyNest _homeNest;

        private bool _isOutbound = true;
        private bool _isInitialized;

        public event Action OnReachedTrailTip;
        public event Action OnTrailLost;
        public event Action OnReturnedToNest;

        public bool IsOutbound => _isOutbound;

        private void Awake()
        {
            _ai = GetComponent<IAstarAI>();

            if (_ai == null)
            {
                throw new InvalidOperationException(
                    $"[AntTrailFollower] Missing required IAstarAI movement component (e.g., AIPath, RichAI) " +
                    $"on GameObject '{gameObject.name}'.");
            }
        }

        public void Initialize(ColonyNest homeNest, PheromoneTrailEmitter emitter, SwarmConfigSO config)
        {
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _ai.maxSpeed = _config.WorkerMoveSpeed;
            _ai.canSearch = true;
            _ai.isStopped = false;

            if (_ai is AIPath aiPath)
            {
                aiPath.slowWhenNotFacingTarget = false;
                aiPath.slowdownDistance = 0.2f;
                aiPath.rotationSpeed = 720f;
            }

            _isOutbound = true;
            _isInitialized = true;
        }

        public void SetOutbound(bool outbound)
        {
            _isOutbound = outbound;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (_isOutbound)
            {
                NavigateOutbound();
            }
            else
            {
                NavigateInbound();
            }
        }

        private float _lostTrailTimer;
        public event Action OnTrailResumed;

        private void NavigateOutbound()
        {
            if (_emitter == null || _emitter.ActiveNodeCount == 0)
            {
                HandleTrailAbsence();
                return;
            }

            IReadOnlyList<PheromoneNode> nodes = _emitter.ActiveNodes;
            Vector3 myPos = transform.position;
            float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;

            int closestIndex = -1;
            float closestDistSqr = float.MaxValue;

            // Find closest active recruitment node
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type != PheromoneType.Recruitment) continue;

                Vector3 nodePos = nodes[i].Position;
                float dx = nodePos.x - myPos.x;
                float dz = nodePos.z - myPos.z;
                float distSqr = dx * dx + dz * dz;

                if (distSqr <= samplingRadiusSqr && distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closestIndex = i;
                }
            }

            if (closestIndex == -1)
            {
                HandleTrailAbsence();
                return;
            }

            // Scent found, reset lost timer
            _lostTrailTimer = 0f;
            OnTrailResumed?.Invoke();

            // Project forward along the trail chain toward newer nodes (higher index)
            int lookAheadCount = Mathf.Max(1, Mathf.RoundToInt(_config.WaypointLookAheadDistance / _config.ArrivalThreshold));
            int targetIndex = Mathf.Min(closestIndex + lookAheadCount, nodes.Count - 1);
            Vector3 targetWaypoint = nodes[targetIndex].Position;

            _ai.destination = targetWaypoint;

            // Check if we are near the very tip of the active trail
            if (targetIndex == nodes.Count - 1)
            {
                float dxTip = targetWaypoint.x - myPos.x;
                float dzTip = targetWaypoint.z - myPos.z;
                if ((dxTip * dxTip + dzTip * dzTip) <= (_config.ArrivalThreshold * _config.ArrivalThreshold))
                {
                    OnReachedTrailTip?.Invoke();
                }
            }
        }

        private void HandleTrailAbsence()
        {
            _lostTrailTimer += Time.deltaTime;
            if (_lostTrailTimer >= _config.TrailEndSearchDuration)
            {
                OnTrailLost?.Invoke();
            }
        }

        private void NavigateInbound()
        {
            if (_homeNest == null) return;

            Vector3 nestPos = _homeNest.DepotPosition;
            _ai.destination = nestPos;

            Vector3 myPos = transform.position;
            float dx = nestPos.x - myPos.x;
            float dz = nestPos.z - myPos.z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr <= (_config.NestArrivalThreshold * _config.NestArrivalThreshold))
            {
                OnReturnedToNest?.Invoke();
            }
        }
    }
}
