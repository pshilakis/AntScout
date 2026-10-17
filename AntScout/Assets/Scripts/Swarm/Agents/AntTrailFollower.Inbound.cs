using System.Collections.Generic;
using AntScout.Pheromone;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class containing inbound nest retracing, highway lookahead shortcuts,
    /// and food cargo stigmergy trail reinforcement/migration.
    /// Strictly adheres to Single Responsibility (SRP).
    /// </summary>
    public partial class AntTrailFollower
    {
        public void ReturnToNest(bool hasCargo)
        {
            _hasCargo = hasCargo;
            _mode = FollowerMode.InboundNest;
            ResumeMovement(MoveSpeed);

            PheromoneTrailEmitter emitter = Emitter;
            float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;

            // If already tracking an active segment, latch onto the closest physical node.
            // Cargo ants reinforce on departure; empty ants follow the highway without reinforcing so dead trails naturally decay.
            if (_trackedSegment != null && emitter != null && emitter.IsSegmentActive(_trackedSegment) && _trackedSegment.NodeCount > 0)
            {
                int closestOnSegment = _trackedSegment.FindClosestNodeIndex(transform.position, samplingRadiusSqr);
                if (closestOnSegment >= 0)
                {
                    _trackedNodeIndex = closestOnSegment;
                    if (_hasCargo)
                    {
                        _trackedSegment.ReinforceNode(_trackedNodeIndex, PgsTime.ElapsedTime);
                    }
                }
            }
            else if (emitter != null && emitter.ActiveNodeCount > 0)
            {
                // Fallback: Sniff for the best recruitment trail nearby
                if (emitter.TryFindBestRecruitmentNode(transform.position, samplingRadiusSqr, out PheromoneTrailSegment bestSeg, out int bestIdx,
                    priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    _trackedSegment = bestSeg;
                    _trackedNodeIndex = bestIdx;
                    if (_hasCargo)
                    {
                        _trackedSegment.ReinforceNode(_trackedNodeIndex, PgsTime.ElapsedTime);
                    }
                }
            }

            // If off-trail or trail expired, pathfind directly back to the home nest depot
            if (_trackedSegment == null && _homeNest != null && _ai != null)
            {
                _ai.destination = _homeNest.SpawnPosition;
            }
        }

        public void ReturnToNest()
        {
            ReturnToNest(false);
        }

        private void NavigateInbound(float deltaTime)
        {
            if (_homeNest == null) return;

            Vector3 nestPos = _homeNest.SpawnPosition;
            Vector3 myPos = transform.position;
            float dx = nestPos.x - myPos.x;
            float dz = nestPos.z - myPos.z;
            float distSqr = dx * dx + dz * dz;

            // Arrival check at the home nest depot
            if (distSqr <= (_config.NestArrivalThreshold * _config.NestArrivalThreshold))
            {
                OnReturnedToNest?.Invoke();
                return;
            }

            PheromoneTrailEmitter emitter = Emitter;

            // If we lost our segment or harvested food a few meters off the trail, re-acquire the nearest recruitment trail segment
            if ((_trackedSegment == null || !emitter.IsSegmentActive(_trackedSegment)) && emitter != null && emitter.ActiveNodeCount > 0)
            {
                float reattachRadiusSqr = (_config.TrailSamplingRadius * 2.5f) * (_config.TrailSamplingRadius * 2.5f);
                if (emitter.TryFindBestRecruitmentNode(myPos, reattachRadiusSqr, out PheromoneTrailSegment foundSeg, out int foundIdx,
                    priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    _trackedSegment = foundSeg;
                    _trackedNodeIndex = foundIdx;
                }
            }

            // Periodic defection check for returning cargo carriers: if a newer or straighter trail exists nearby, switch to it!
            _freshnessCheckTimer += deltaTime;
            if (_freshnessCheckTimer >= 0.5f)
            {
                _freshnessCheckTimer = 0f;
                float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;
                if (emitter != null && emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out PheromoneTrailSegment freshSeg, out int freshIdx,
                    excludeSegment: _trackedSegment, priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    if (freshSeg != null && freshSeg.NodeCount > 0)
                    {
                        float segAge = PgsTime.ElapsedTime - freshSeg.CreatedAt;
                        bool isNewerPlayerTrail = freshSeg.CreatedAt > (_trackedSegment != null ? _trackedSegment.CreatedAt : 0f) && segAge < _config.NewTrailPriorityDuration;

                        float currentEff = _trackedSegment != null ? _trackedSegment.CurrentEfficiency : 0f;
                        bool isMoreEfficient = freshSeg.CurrentEfficiency >= currentEff + _config.TrailSwitchEfficiencyAdvantage;

                        if (isNewerPlayerTrail || isMoreEfficient)
                        {
                            _trackedSegment = freshSeg;
                            _trackedNodeIndex = freshIdx;
                        }
                    }
                }
            }

            // If we have an active tracked segment, retrace it inbound toward the nest
            if (_trackedSegment != null && emitter != null && emitter.IsSegmentActive(_trackedSegment) && _trackedSegment.NodeCount > 0)
            {
                IReadOnlyList<PheromoneNode> nodes = _trackedSegment.Nodes;

                // Local window search to track current node position along the ribbon
                float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;
                if (emitter.TryFindNearestNodeLocal(_trackedSegment, _trackedNodeIndex, _config.LocalSearchNodeWindow, myPos, samplingRadiusSqr, out int closestIndex))
                {
                    _trackedNodeIndex = closestIndex;
                }
                else
                {
                    int segmentClosest = _trackedSegment.FindClosestNodeIndex(myPos, samplingRadiusSqr);
                    if (segmentClosest >= 0)
                    {
                        _trackedNodeIndex = segmentClosest;
                    }
                }

                // Only successful foragers carrying food cargo reinforce and optimize the trail (Stigmergy)
                if (_hasCargo)
                {
                    _trackedSegment.ReinforceAndMigrateRibbon(_trackedNodeIndex, _config.TrailOptimizationNormalized, deltaTime, PgsTime.ElapsedTime);
                }

                Vector3 firstNodePos = nodes[0].Position;
                Vector3 lastNodePos = nodes[nodes.Count - 1].Position;
                float distFirstSqr = (firstNodePos.x - nestPos.x) * (firstNodePos.x - nestPos.x) + (firstNodePos.z - nestPos.z) * (firstNodePos.z - nestPos.z);
                float distLastSqr = (lastNodePos.x - nestPos.x) * (lastNodePos.x - nestPos.x) + (lastNodePos.z - nestPos.z) * (lastNodePos.z - nestPos.z);

                // Inbound direction steps towards whichever end of the segment is closer to the nest
                bool stepForward = distFirstSqr >= distLastSqr;
                int stepDir = stepForward ? 1 : -1;
                int nestEndIndex = stepForward ? (nodes.Count - 1) : 0;

                // If reached the end of the ribbon closest to the nest, navigate final stretch directly into depot
                if (_trackedNodeIndex == nestEndIndex)
                {
                    _ai.destination = nestPos;
                    return;
                }

                float lookaheadDist = _config.TrailLookaheadDistance;
                float lookaheadSqr = lookaheadDist * lookaheadDist;

                // Direct shortcut to nest depot if already within lookahead radius
                if (distSqr <= lookaheadSqr)
                {
                    _ai.destination = nestPos;
                    return;
                }

                // Euclidean Shortcut: Scan ahead along the trail toward the nest end for the furthest node within lookahead radius
                int targetIndex = _trackedNodeIndex;
                if (stepForward)
                {
                    for (int i = nodes.Count - 1; i > _trackedNodeIndex; i--)
                    {
                        Vector3 p = nodes[i].Position;
                        float dxNode = p.x - myPos.x;
                        float dzNode = p.z - myPos.z;
                        if ((dxNode * dxNode + dzNode * dzNode) <= lookaheadSqr)
                        {
                            targetIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < _trackedNodeIndex; i++)
                    {
                        Vector3 p = nodes[i].Position;
                        float dxNode = p.x - myPos.x;
                        float dzNode = p.z - myPos.z;
                        if ((dxNode * dxNode + dzNode * dzNode) <= lookaheadSqr)
                        {
                            targetIndex = i;
                            break;
                        }
                    }
                }

                // If no shortcut node was within Euclidean radius, fallback to standard incremental step along the ribbon
                if (targetIndex == _trackedNodeIndex)
                {
                    int lookAheadCount = Mathf.Max(1, Mathf.RoundToInt(lookaheadDist / _config.ArrivalThreshold));
                    targetIndex = Mathf.Clamp(_trackedNodeIndex + (stepDir * lookAheadCount), 0, nodes.Count - 1);
                }

                Vector3 baseWaypoint = nodes[targetIndex].Position;

                // Lateral offset for two-way traffic separation and organic wander
                int priorIndex = Mathf.Clamp(targetIndex - stepDir, 0, nodes.Count - 1);
                Vector3 forwardDir = baseWaypoint - nodes[priorIndex].Position;
                forwardDir.y = 0f;

                Vector3 targetWaypoint = baseWaypoint;
                if (forwardDir.sqrMagnitude > 0.0001f)
                {
                    Vector3 tangent = forwardDir.normalized;
                    Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
                    float wander = Mathf.Sin(PgsTime.ElapsedTime * 2.5f + _lateralOffsetSeed * 15f);
                    float lateralOffset = (_lateralOffsetSeed * 0.7f + wander * 0.3f) * _config.TrailWanderMaxDeviation;
                    targetWaypoint = baseWaypoint + normal * lateralOffset;
                }

                _ai.destination = targetWaypoint;
            }
            else
            {
                // Fallback to direct pathfinding if trail expired or was lost
                _ai.destination = nestPos;
            }
        }
    }
}
