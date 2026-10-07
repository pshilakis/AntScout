using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Core.Interfaces;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Manages independent pheromone trail strokes (segments), spacing, and expiration pruning.
    /// Implements IPheromoneEmitter and IUpdatable to allow deterministic per-stroke segmented emission.
    /// </summary>
    public class PheromoneTrailEmitter : MonoBehaviour, IPheromoneEmitter, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Tunable configuration defining node placement spacing and lifetime.")]
        [SerializeField] private ScoutConfigSO _config;

        private readonly List<PheromoneTrailSegment> _segments = new List<PheromoneTrailSegment>();
        private PheromoneTrailSegment _currentSegment;

        public event Action<PheromoneTrailSegment> OnSegmentCreated;
        public event Action<PheromoneTrailSegment> OnSegmentDestroyed;
        public event Action<PheromoneNode> OnNodeEmitted;
        public event Action OnTrailUpdated;

        public IReadOnlyList<PheromoneTrailSegment> ActiveSegments => _segments;
        public float NodeLifetime => _config != null ? _config.NodeLifetime : 30f;
        public bool IsSegmentActive(PheromoneTrailSegment segment) => segment != null && _segments.Contains(segment);

        public int ActiveNodeCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _segments.Count; i++)
                {
                    count += _segments[i].NodeCount;
                }
                return count;
            }
        }

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[PheromoneTrailEmitter] Missing required ScoutConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ScoutConfigSO in the Inspector.");
            }
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Continuous, TimeChannel.World);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            PruneExpiredSegments();
        }

        public void StartNewSegment(PheromoneType type)
        {
            if (_currentSegment != null && !_currentSegment.IsSealed)
            {
                EndCurrentSegment();
            }

            _currentSegment = new PheromoneTrailSegment(type);
            _segments.Add(_currentSegment);
            OnSegmentCreated?.Invoke(_currentSegment);
        }

        public void EndCurrentSegment()
        {
            if (_currentSegment != null)
            {
                _currentSegment.Seal();
                _currentSegment = null;
            }
        }

        public void EmitNode(Vector3 worldPosition, PheromoneType type)
        {
            if (_currentSegment == null || _currentSegment.Type != type)
            {
                StartNewSegment(type);
            }

            if (_currentSegment.NodeCount > 0)
            {
                Vector3 lastPos = _currentSegment.LastNode.Position;
                float dx = lastPos.x - worldPosition.x;
                float dz = lastPos.z - worldPosition.z;
                float distanceSqr = dx * dx + dz * dz;
                float minSpacing = _config.NodePlacementDistance;

                if (distanceSqr < (minSpacing * minSpacing))
                {
                    return;
                }
            }

            _currentSegment.AddNode(worldPosition, PgsTime.ElapsedTime);
            OnNodeEmitted?.Invoke(_currentSegment.LastNode);
            OnTrailUpdated?.Invoke();
        }

        private void PruneExpiredSegments()
        {
            if (_segments.Count == 0) return;

            float currentTime = PgsTime.ElapsedTime;
            float lifetime = _config.NodeLifetime;
            bool updated = false;

            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                PheromoneTrailSegment segment = _segments[i];
                int pruned = segment.PruneExpired(currentTime, lifetime);
                if (pruned > 0) updated = true;

                // If segment has expired and is sealed, remove it
                if (segment.IsEmpty && segment != _currentSegment)
                {
                    _segments.RemoveAt(i);
                    OnSegmentDestroyed?.Invoke(segment);
                    updated = true;
                }
            }

            if (updated)
            {
                OnTrailUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Checks if any active recruitment trail segment has a node within the specified radius of a point.
        /// </summary>
        public bool HasRecruitmentNear(Vector3 position, float radiusSqr)
        {
            for (int s = 0; s < _segments.Count; s++)
            {
                PheromoneTrailSegment seg = _segments[s];
                if (seg.Type != PheromoneType.Recruitment) continue;

                IReadOnlyList<PheromoneNode> nodes = seg.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    Vector3 p = nodes[i].Position;
                    float dx = p.x - position.x;
                    float dz = p.z - position.z;
                    if ((dx * dx + dz * dz) <= radiusSqr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the closest recruitment node among all active strokes to guide worker ant steering.
        /// </summary>
        public bool TryFindNearestRecruitmentNode(Vector3 position, float maxRadiusSqr, out PheromoneTrailSegment foundSegment, out int foundNodeIndex)
        {
            foundSegment = null;
            foundNodeIndex = -1;
            float closestDistSqr = float.MaxValue;

            for (int s = 0; s < _segments.Count; s++)
            {
                PheromoneTrailSegment seg = _segments[s];
                if (seg.Type != PheromoneType.Recruitment) continue;

                IReadOnlyList<PheromoneNode> nodes = seg.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    Vector3 p = nodes[i].Position;
                    float dx = p.x - position.x;
                    float dz = p.z - position.z;
                    float distSqr = dx * dx + dz * dz;

                    if (distSqr <= maxRadiusSqr && distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        foundSegment = seg;
                        foundNodeIndex = i;
                    }
                }
            }

            return foundSegment != null;
        }

        /// <summary>
        /// Finds the best recruitment node based on chemical scent freshness (intensity) and spatial proximity.
        /// Favors newly laid, potent trails over decaying or old strokes.
        /// </summary>
        public bool TryFindBestRecruitmentNode(
            Vector3 position,
            float maxRadiusSqr,
            out PheromoneTrailSegment bestSegment,
            out int bestNodeIndex,
            PheromoneTrailSegment excludeSegment = null)
        {
            bestSegment = null;
            bestNodeIndex = -1;
            float bestScore = float.MinValue;
            float currentTime = PgsTime.ElapsedTime;
            float lifetime = _config.NodeLifetime;

            for (int s = 0; s < _segments.Count; s++)
            {
                PheromoneTrailSegment seg = _segments[s];
                if (seg.Type != PheromoneType.Recruitment) continue;
                if (excludeSegment != null && seg == excludeSegment) continue;

                IReadOnlyList<PheromoneNode> nodes = seg.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    Vector3 p = nodes[i].Position;
                    float dx = p.x - position.x;
                    float dz = p.z - position.z;
                    float distSqr = dx * dx + dz * dz;

                    if (distSqr <= maxRadiusSqr)
                    {
                        float intensity = nodes[i].GetCurrentIntensity(currentTime, lifetime);
                        if (intensity <= 0.01f) continue;

                        // Potency Score: Intensity / (1.0 + distance)
                        float score = intensity / (1.0f + Mathf.Sqrt(distSqr));

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestSegment = seg;
                            bestNodeIndex = i;
                        }
                    }
                }
            }

            return bestSegment != null;
        }

        /// <summary>
        /// Searches a local window of nodes around a known index within a segment for rapid O(1) trail tracking.
        /// Returns true if a recruitment node within maxRadiusSqr is found in the window.
        /// </summary>
        public bool TryFindNearestNodeLocal(
            PheromoneTrailSegment segment,
            int estimatedIndex,
            int searchRadiusNodes,
            Vector3 position,
            float maxRadiusSqr,
            out int foundNodeIndex)
        {
            foundNodeIndex = -1;
            if (segment == null || segment.IsEmpty || segment.Type != PheromoneType.Recruitment || !_segments.Contains(segment))
            {
                return false;
            }

            IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
            int total = nodes.Count;

            int minIdx = Mathf.Max(0, estimatedIndex - searchRadiusNodes);
            int maxIdx = Mathf.Min(total - 1, estimatedIndex + searchRadiusNodes);
            float closestDistSqr = float.MaxValue;

            for (int i = minIdx; i <= maxIdx; i++)
            {
                Vector3 p = nodes[i].Position;
                float dx = p.x - position.x;
                float dz = p.z - position.z;
                float distSqr = dx * dx + dz * dz;

                if (distSqr <= maxRadiusSqr && distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    foundNodeIndex = i;
                }
            }

            return foundNodeIndex != -1;
        }

        public void ClearTrail()
        {
            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                OnSegmentDestroyed?.Invoke(_segments[i]);
            }
            _segments.Clear();
            _currentSegment = null;
            OnTrailUpdated?.Invoke();
        }
    }
}
