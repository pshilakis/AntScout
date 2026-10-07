using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using AntScout.Swarm.Nest;
using Pathfinding;
using Pathfinding.RVO;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Operating modes for the AntTrailFollower steering layer.
    /// </summary>
    public enum FollowerMode
    {
        Inactive = 0,
        OutboundTrail = 1,
        InboundNest = 2,
        Wander = 3,
        MeanderHome = 4
    }

    /// <summary>
    /// Bridges the pheromone trail simulation with A* Pathfinding Project Pro (IAstarAI).
    /// Samples scent gradients from PheromoneTrailEmitter and projects navigation destinations to A*.
    /// Implements IUpdatable to receive deterministic simulation updates on TimeChannel.World.
    /// </summary>
    public class AntTrailFollower : MonoBehaviour, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for worker movement speed, sensory radii, and waypoint lookahead.")]
        [SerializeField] private SwarmConfigSO _config;

        private IAstarAI _ai;
        private RVOController _rvoController;
        private PheromoneTrailEmitter _emitter;
        private ColonyNest _homeNest;

        private PheromoneTrailSegment _trackedSegment;
        private int _trackedNodeIndex = -1;
        private float _lateralOffsetSeed;
        private float _lostTrailTimer;
        private float _freshnessCheckTimer;

        private float _meanderTimer;
        private Vector3 _currentMeanderWaypoint;

        private FollowerMode _mode = FollowerMode.Inactive;
        private bool _isInitialized;

        public event Action OnReachedTrailTip;
        public event Action OnTrailLost;
        public event Action OnReturnedToNest;
        public event Action OnTrailResumed;

        public FollowerMode CurrentMode => _mode;
        public bool IsOutbound => _mode == FollowerMode.OutboundTrail;
        public PheromoneTrailSegment TrackedSegment => _trackedSegment;

        private void Awake()
        {
            _ai = GetComponent<IAstarAI>();
            _rvoController = GetComponent<RVOController>();

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
                aiPath.rvoDensityBehavior.enabled = false;
            }

            if (_rvoController != null)
            {
                // Flow following set to 0 to restore natural lateral swarm separation across the scent corridor
                _rvoController.flowFollowingStrength = 0.0f;
                _rvoController.maxNeighbours = 6;
                _rvoController.agentTimeHorizon = 1.0f;
                _rvoController.obstacleTimeHorizon = 0.4f;
            }

            // Assign each ant a unique lateral lane across the pheromone plume
            _lateralOffsetSeed = UnityEngine.Random.Range(-1.0f, 1.0f);
            _trackedSegment = null;
            _trackedNodeIndex = -1;
            _lostTrailTimer = 0f;
            _freshnessCheckTimer = 0f;
            _mode = FollowerMode.Inactive;
            _isInitialized = true;
        }

        public void FollowTrailOutbound(PheromoneTrailSegment initialSegment = null, int initialNodeIndex = -1)
        {
            _mode = FollowerMode.OutboundTrail;
            _trackedSegment = initialSegment;
            _trackedNodeIndex = initialNodeIndex;
            _lostTrailTimer = 0f;
            _freshnessCheckTimer = 0f;
            if (_ai != null)
            {
                _ai.isStopped = false;
                _ai.maxSpeed = _config.WorkerMoveSpeed;
            }
        }

        public void ReturnToNest(bool hasCargo)
        {
            _trackedSegment = null;
            _trackedNodeIndex = -1;

            if (_ai != null) _ai.isStopped = false;

            if (hasCargo)
            {
                // Delivering food: direct A* route at full speed
                _mode = FollowerMode.InboundNest;
                if (_ai != null)
                {
                    _ai.maxSpeed = _config.WorkerMoveSpeed;
                    if (_homeNest != null)
                    {
                        _ai.destination = _homeNest.DepotPosition;
                    }
                }
            }
            else
            {
                // Empty-handed: slow, organic biased meander toward nest
                _mode = FollowerMode.MeanderHome;
                _meanderTimer = 0f;
                _currentMeanderWaypoint = Vector3.zero;
                if (_ai != null)
                {
                    _ai.maxSpeed = _config.WorkerReturnWanderSpeed;
                }
                Vector3 nestPos = _homeNest != null ? _homeNest.DepotPosition : transform.position;
                PickNextMeanderWaypoint(transform.position, nestPos);
            }
        }

        public void ReturnToNest()
        {
            ReturnToNest(false);
        }

        public void WanderTo(Vector3 targetPosition, float speed = -1f)
        {
            _mode = FollowerMode.Wander;
            _trackedSegment = null;
            _trackedNodeIndex = -1;
            if (_ai != null)
            {
                _ai.isStopped = false;
                _ai.maxSpeed = speed > 0f ? speed : _config.WorkerMoveSpeed;
                _ai.destination = targetPosition;
            }
        }

        public void StopMovement()
        {
            _mode = FollowerMode.Inactive;
            if (_ai != null)
            {
                _ai.isStopped = true;
            }
        }

        public void SetOutbound(bool outbound)
        {
            if (outbound)
            {
                FollowTrailOutbound();
            }
            else
            {
                ReturnToNest();
            }
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_isInitialized) return;

            switch (_mode)
            {
                case FollowerMode.OutboundTrail:
                    NavigateOutbound(deltaTime);
                    break;

                case FollowerMode.InboundNest:
                    NavigateInbound();
                    break;

                case FollowerMode.MeanderHome:
                    NavigateMeanderHome(deltaTime);
                    break;

                case FollowerMode.Wander:
                case FollowerMode.Inactive:
                    break;
            }
        }

        private void NavigateOutbound(float deltaTime)
        {
            if (_emitter == null || _emitter.ActiveNodeCount == 0)
            {
                _trackedSegment = null;
                _trackedNodeIndex = -1;
                HandleTrailAbsence(deltaTime);
                return;
            }

            Vector3 myPos = transform.position;
            float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;

            PheromoneTrailSegment segment = null;
            int closestIndex = -1;

            // 1. Fast O(1) local window search if already tracking a valid segment
            if (_trackedSegment != null && _trackedNodeIndex >= 0 && _emitter.IsSegmentActive(_trackedSegment))
            {
                if (_emitter.TryFindNearestNodeLocal(_trackedSegment, _trackedNodeIndex, _config.LocalSearchNodeWindow, myPos, samplingRadiusSqr, out closestIndex))
                {
                    segment = _trackedSegment;
                }
            }

            // 2. Periodic freshness evaluation: if a much fresher segment was drawn nearby, switch to it!
            _freshnessCheckTimer += deltaTime;
            if (_freshnessCheckTimer >= 0.5f)
            {
                _freshnessCheckTimer = 0f;
                if (_emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out PheromoneTrailSegment freshSeg, out int freshIdx))
                {
                    if (freshSeg != segment && freshSeg.NodeCount > 0)
                    {
                        float freshIntensity = freshSeg.Nodes[freshIdx].GetCurrentIntensity(PgsTime.ElapsedTime, _emitter.NodeLifetime);
                        float currIntensity = (segment != null && closestIndex >= 0 && closestIndex < segment.NodeCount)
                            ? segment.Nodes[closestIndex].GetCurrentIntensity(PgsTime.ElapsedTime, _emitter.NodeLifetime)
                            : 0f;

                        if (freshIntensity > currIntensity + 0.35f)
                        {
                            segment = freshSeg;
                            closestIndex = freshIdx;
                        }
                    }
                }
            }

            // 3. Fall back to best recruitment node if local window lost scent
            if (segment == null)
            {
                if (!_emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out segment, out closestIndex))
                {
                    _trackedSegment = null;
                    _trackedNodeIndex = -1;
                    HandleTrailAbsence(deltaTime);
                    return;
                }
            }

            // Scent confirmed, update local tracking cache and reset lost timer
            _trackedSegment = segment;
            _trackedNodeIndex = closestIndex;
            _lostTrailTimer = 0f;
            OnTrailResumed?.Invoke();

            IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
            if (nodes == null || nodes.Count == 0) return;

            Vector3 nestPos = _homeNest != null ? _homeNest.DepotPosition : transform.position;
            Vector3 firstNodePos = nodes[0].Position;
            Vector3 lastNodePos = nodes[nodes.Count - 1].Position;

            float distFirstSqr = (firstNodePos.x - nestPos.x) * (firstNodePos.x - nestPos.x) + (firstNodePos.z - nestPos.z) * (firstNodePos.z - nestPos.z);
            float distLastSqr = (lastNodePos.x - nestPos.x) * (lastNodePos.x - nestPos.x) + (lastNodePos.z - nestPos.z) * (lastNodePos.z - nestPos.z);

            // Determine traversal direction: Outbound always steps toward the end furthest from the nest
            bool stepForward = distLastSqr >= distFirstSqr;
            int stepDir = stepForward ? 1 : -1;
            int tipIndex = stepForward ? (nodes.Count - 1) : 0;

            int lookAheadCount = Mathf.Max(1, Mathf.RoundToInt(_config.WaypointLookAheadDistance / _config.ArrivalThreshold));
            int targetIndex = Mathf.Clamp(closestIndex + (stepDir * lookAheadCount), 0, nodes.Count - 1);
            Vector3 baseWaypoint = nodes[targetIndex].Position;

            // Calculate forward direction along true travel trajectory
            int priorIndex = Mathf.Clamp(targetIndex - stepDir, 0, nodes.Count - 1);
            Vector3 forwardDir = baseWaypoint - nodes[priorIndex].Position;
            forwardDir.y = 0f;

            Vector3 targetWaypoint = baseWaypoint;
            if (forwardDir.sqrMagnitude > 0.0001f)
            {
                Vector3 tangent = forwardDir.normalized;
                Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);

                // Combine persistent unique lane offset with a gentle organic tropotaxis sine wobble
                float wander = Mathf.Sin(PgsTime.ElapsedTime * 3.0f + _lateralOffsetSeed * 10f) * 0.25f;
                float totalOffset = (_lateralOffsetSeed + wander) * _config.TrailSwarmDispersion;
                targetWaypoint = baseWaypoint + normal * totalOffset;
            }

            // Directly assign destination: lookahead point remains ~1.2m ahead, preventing slowdown/stop stuttering
            _ai.destination = targetWaypoint;

            // Check if we are approaching the outer tip of the active stroke
            bool isNearTip = (targetIndex == tipIndex) || (Mathf.Abs(closestIndex - tipIndex) <= lookAheadCount);
            if (isNearTip)
            {
                // Staggered Trail Chaining: Check if another segment is available before declaring trail end!
                if (_emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out PheromoneTrailSegment candidateSeg, out int candidateIdx, excludeSegment: segment))
                {
                    if (candidateSeg.NodeCount > 0)
                    {
                        Vector3 candFirst = candidateSeg.Nodes[0].Position;
                        Vector3 candLast = candidateSeg.Nodes[candidateSeg.NodeCount - 1].Position;
                        float candFirstDistSqr = (candFirst.x - nestPos.x) * (candFirst.x - nestPos.x) + (candFirst.z - nestPos.z) * (candFirst.z - nestPos.z);
                        float candLastDistSqr = (candLast.x - nestPos.x) * (candLast.x - nestPos.x) + (candLast.z - nestPos.z) * (candLast.z - nestPos.z);
                        float candTipDistSqr = Mathf.Max(candFirstDistSqr, candLastDistSqr);

                        float dxTipCoord = baseWaypoint.x - nestPos.x;
                        float dzTipCoord = baseWaypoint.z - nestPos.z;
                        float currentTipDistSqr = dxTipCoord * dxTipCoord + dzTipCoord * dzTipCoord;

                        // Accept candidate trail if it extends outward at least as far as the current stroke's tip
                        if (candTipDistSqr >= currentTipDistSqr - 2.0f)
                        {
                            _trackedSegment = candidateSeg;
                            _trackedNodeIndex = candidateIdx;
                            return;
                        }
                    }
                }
            }

            // Check if we reached the physical tip of the trail
            if (targetIndex == tipIndex)
            {
                float dxTip = baseWaypoint.x - myPos.x;
                float dzTip = baseWaypoint.z - myPos.z;
                float tipArrivalThreshold = Mathf.Max(_config.ArrivalThreshold * 2.5f, 1.5f);
                if ((dxTip * dxTip + dzTip * dzTip) <= (tipArrivalThreshold * tipArrivalThreshold))
                {
                    OnReachedTrailTip?.Invoke();
                }
            }
        }

        private void HandleTrailAbsence(float deltaTime)
        {
            _lostTrailTimer += deltaTime;
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

        private void NavigateMeanderHome(float deltaTime)
        {
            if (_homeNest == null) return;

            Vector3 nestPos = _homeNest.DepotPosition;
            Vector3 myPos = transform.position;

            float dxNest = nestPos.x - myPos.x;
            float dzNest = nestPos.z - myPos.z;
            float distToNestSqr = dxNest * dxNest + dzNest * dzNest;

            if (distToNestSqr <= (_config.NestArrivalThreshold * _config.NestArrivalThreshold))
            {
                OnReturnedToNest?.Invoke();
                return;
            }

            _meanderTimer += deltaTime;
            float dxWay = _currentMeanderWaypoint.x - myPos.x;
            float dzWay = _currentMeanderWaypoint.z - myPos.z;
            bool reachedWaypoint = (dxWay * dxWay + dzWay * dzWay) <= (_config.ArrivalThreshold * _config.ArrivalThreshold);

            if (_meanderTimer >= 2.0f || reachedWaypoint || _currentMeanderWaypoint == Vector3.zero)
            {
                _meanderTimer = 0f;
                PickNextMeanderWaypoint(myPos, nestPos);
            }
        }

        private void PickNextMeanderWaypoint(Vector3 myPos, Vector3 nestPos)
        {
            Vector3 dirToNest = nestPos - myPos;
            dirToNest.y = 0f;

            float distToNest = dirToNest.magnitude;
            if (distToNest <= 3.0f)
            {
                _currentMeanderWaypoint = nestPos;
                _ai.destination = nestPos;
                return;
            }

            dirToNest.Normalize();

            // Biased random walk: rotate base direction toward nest by a random jitter angle
            float jitterAngle = UnityEngine.Random.Range(-_config.MeanderJitterAngle, _config.MeanderJitterAngle);
            Quaternion rotation = Quaternion.Euler(0f, jitterAngle, 0f);
            Vector3 meanderDir = rotation * dirToNest;

            float stepDistance = Mathf.Min(3.0f, distToNest);
            _currentMeanderWaypoint = myPos + meanderDir * stepDistance;
            _ai.destination = _currentMeanderWaypoint;
        }
    }
}
