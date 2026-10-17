using System;
using System.Collections.Generic;
using AntScout.Pheromone;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class containing outbound trail following, lookahead projection,
    /// lateral corridor wave dispersion, defection evaluation, and tip arrival detection.
    /// Strictly adheres to Single Responsibility (SRP).
    /// </summary>
    public partial class AntTrailFollower
    {
        public void FollowTrailOutbound(PheromoneTrailSegment initialSegment = null, int initialNodeIndex = -1)
        {
            _mode = FollowerMode.OutboundTrail;
            _hasCargo = false;
            _trackedSegment = initialSegment;
            _trackedNodeIndex = initialNodeIndex;
            _lostTrailTimer = 0f;
            _freshnessCheckTimer = 0f;
            ResumeMovement(MoveSpeed);
        }

        private void NavigateOutbound(float deltaTime)
        {
            PheromoneTrailEmitter emitter = Emitter;
            if (emitter == null || emitter.ActiveNodeCount == 0)
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
            if (_trackedSegment != null && _trackedNodeIndex >= 0 && emitter.IsSegmentActive(_trackedSegment))
            {
                if (emitter.TryFindNearestNodeLocal(_trackedSegment, _trackedNodeIndex, _config.LocalSearchNodeWindow, myPos, samplingRadiusSqr, out closestIndex))
                {
                    segment = _trackedSegment;
                }
            }

            // 2. Periodic freshness evaluation: if a newer or significantly more efficient segment was drawn nearby, switch to it!
            _freshnessCheckTimer += deltaTime;
            if (_freshnessCheckTimer >= 0.5f)
            {
                _freshnessCheckTimer = 0f;
                if (emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out PheromoneTrailSegment freshSeg, out int freshIdx,
                    excludeSegment: segment, priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    if (freshSeg != null && freshSeg.NodeCount > 0)
                    {
                        // Defection Criterion 1: Candidate segment was drawn by player more recently than our current segment and within priority duration
                        float segAge = PgsTime.ElapsedTime - freshSeg.CreatedAt;
                        bool isNewerPlayerTrail = freshSeg.CreatedAt > (segment != null ? segment.CreatedAt : 0f) && segAge < _config.NewTrailPriorityDuration;

                        // Defection Criterion 2: Candidate segment is significantly more direct/efficient
                        float currentEff = segment != null ? segment.CurrentEfficiency : 0f;
                        bool isMoreEfficient = freshSeg.CurrentEfficiency >= currentEff + _config.TrailSwitchEfficiencyAdvantage;

                        if (isNewerPlayerTrail || isMoreEfficient)
                        {
                            segment = freshSeg;
                            closestIndex = freshIdx;
                            _trackedSegment = segment;
                            _trackedNodeIndex = closestIndex;
                            _lostTrailTimer = 0f;
                        }
                    }
                }
            }

            // 3. Fall back to best recruitment node if local window lost scent
            if (segment == null)
            {
                if (!emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out segment, out closestIndex,
                    priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
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

            // Outbound foragers follow the scent gradient but do NOT reinforce it (biology standard)

            IReadOnlyList<PheromoneNode> nodes = segment.Nodes;
            if (nodes == null || nodes.Count == 0) return;

            Vector3 nestPos = _homeNest != null ? _homeNest.SpawnPosition : transform.position;
            Vector3 firstNodePos = nodes[0].Position;
            Vector3 lastNodePos = nodes[nodes.Count - 1].Position;

            float distFirstSqr = (firstNodePos.x - nestPos.x) * (firstNodePos.x - nestPos.x) + (firstNodePos.z - nestPos.z) * (firstNodePos.z - nestPos.z);
            float distLastSqr = (lastNodePos.x - nestPos.x) * (lastNodePos.x - nestPos.x) + (lastNodePos.z - nestPos.z) * (lastNodePos.z - nestPos.z);

            // Determine traversal direction: Outbound always steps toward the end furthest from the nest
            bool stepForward = distLastSqr >= distFirstSqr;
            int stepDir = stepForward ? 1 : -1;
            int tipIndex = stepForward ? (nodes.Count - 1) : 0;

            float lookaheadDist = _config.TrailLookaheadDistance;
            float lookaheadSqr = lookaheadDist * lookaheadDist;
            int lookAheadCount = Mathf.Max(1, Mathf.RoundToInt(lookaheadDist / _config.ArrivalThreshold));
            int targetIndex = closestIndex;

            // Euclidean Shortcut: Scan ahead along the trail toward the tip end for the furthest node within lookahead radius
            if (stepForward)
            {
                for (int i = nodes.Count - 1; i > closestIndex; i--)
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
                for (int i = 0; i < closestIndex; i++)
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

            // Fallback if no forward node was within Euclidean radius
            if (targetIndex == closestIndex)
            {
                targetIndex = Mathf.Clamp(closestIndex + (stepDir * lookAheadCount), 0, nodes.Count - 1);
            }

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

                // Combine persistent unique lane offset with organic tropotaxis wander across the corridor
                float wander = Mathf.Sin(PgsTime.ElapsedTime * 2.5f + _lateralOffsetSeed * 15f);
                float totalOffset = (_lateralOffsetSeed * 0.7f + wander * 0.3f) * _config.TrailWanderMaxDeviation;
                targetWaypoint = baseWaypoint + normal * totalOffset;
            }

            // Directly assign destination: lookahead point remains ~1.2m ahead, preventing slowdown/stop stuttering
            _ai.destination = targetWaypoint;

            // Check if we are approaching the outer tip of the active stroke
            bool isNearTip = (targetIndex == tipIndex) || (Mathf.Abs(closestIndex - tipIndex) <= lookAheadCount);
            if (isNearTip)
            {
                // Staggered Trail Chaining: Check if another segment is available before declaring trail end!
                if (emitter.TryFindBestRecruitmentNode(myPos, samplingRadiusSqr, out PheromoneTrailSegment candidateSeg, out int candidateIdx,
                    excludeSegment: segment, priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
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
                float dxTarget = targetWaypoint.x - myPos.x;
                float dzTarget = targetWaypoint.z - myPos.z;
                float distTargetSqr = dxTarget * dxTarget + dzTarget * dzTarget;

                float dxBase = baseWaypoint.x - myPos.x;
                float dzBase = baseWaypoint.z - myPos.z;
                float distBaseSqr = dxBase * dxBase + dzBase * dzBase;

                float tipArrivalThreshold = Mathf.Max(_config.ArrivalThreshold * 3.0f, 2.0f);
                float tipArrivalSqr = tipArrivalThreshold * tipArrivalThreshold;

                bool reachedTip = distTargetSqr <= tipArrivalSqr ||
                                  distBaseSqr <= tipArrivalSqr ||
                                  (_ai != null && (_ai.reachedDestination || _ai.reachedEndOfPath));

                if (reachedTip)
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
    }
}
