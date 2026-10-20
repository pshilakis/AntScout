using System;
using AntScout.Characters;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Match;
using AntScout.Pheromone;
using AntScout.Resources.Components;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using Pathfinding;
using PGS.Combat.Targeting;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// High-level behavioral controller and state machine for Soldier Ants (Majors).
    /// Inherits locomotion, pooling, vitals, primary action execution, and reactive enmity from SwarmAgent.
    /// Grounded in Single Responsibility (SRP) and Liskov Substitution (LSP) by specializing solely in:
    /// - Home nest yard perimeter guard & organic antenna pauses
    /// - High-urgency response to Alarm Pheromone Trails (focus-fire defense)
    /// - Highway escort & harvest site perimeter security for foraging worker columns
    /// - Tactical melee combat execution
    /// </summary>
    [RequireComponent(typeof(AntTrailFollower))]
    public class SoldierAntController : SwarmAgent
    {
        private float _scentCheckCooldown;
        private float _wanderTimer;
        private float _tipGuardTimer;
        private Vector3 _currentWanderTarget;

        private bool _isPausingAtNest;
        private float _nestPauseTimer;
        private float _nestPauseDuration;
        private float _baseFacingAngle;

        private bool _isFollowingAlarm;
        private bool _isEscortPatrol;
        private FoodSource _guardedFoodSource;
        private float _perimeterPaceTimer;
        private float _emptyHarvesterTimer;

        public bool IsFollowingAlarm => _isFollowingAlarm;
        public bool IsEscortPatrol => _isEscortPatrol;

        public override void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter)
        {
            base.Initialize(homeNest, trailEmitter);

            _scentCheckCooldown = 0f;
            _tipGuardTimer = 0f;
            _perimeterPaceTimer = 0f;
            _emptyHarvesterTimer = 0f;
            _isFollowingAlarm = false;
            _isEscortPatrol = false;
            _guardedFoodSource = null;

            StartWanderingAroundNest();
        }

        public override void OrderReturnHome()
        {
            if (_currentState == SwarmAgentState.ReturningHome) return;

            _guardedFoodSource = null;
            _isFollowingAlarm = false;
            _isEscortPatrol = false;

            if (_follower != null)
            {
                _follower.ReturnToNest(hasCargo: false);
            }

            base.OrderReturnHome();
        }

        protected override void EvaluateAgentState(float deltaTime)
        {
            switch (_currentState)
            {
                case SwarmAgentState.WanderingAroundNest:
                    EvaluateNestGuard(deltaTime);
                    break;

                case SwarmAgentState.FollowingTrailOutbound:
                    EvaluateOutboundPatrol(deltaTime);
                    break;

                case SwarmAgentState.AtTrailEnd:
                    EvaluateTrailTipVigil(deltaTime);
                    break;

                case SwarmAgentState.ReturningHome:
                    EvaluateReturningHome(deltaTime);
                    break;
            }
        }

        #region Alarm Mobilization API

        /// <summary>
        /// Scans for active alarm pheromone trails in sensory range and immediately mobilizes the soldier to battle.
        /// Grounded in Single Responsibility (SRP): Prioritizes military alarm response above civilian foraging or perimeter guard.
        /// </summary>
        private bool TryMobilizeToNearbyAlarmTrail()
        {
            if (_isFollowingAlarm) return false;

            PheromoneTrailEmitter emitter = TrailEmitter;
            if (emitter == null || emitter.ActiveNodeCount == 0) return false;

            float searchRadius = _config != null ? _config.TrailSamplingRadius * 1.5f : 18.0f;
            float alarmRadiusSqr = searchRadius * searchRadius;

            if (emitter.TryFindBestAlarmNode(transform.position, alarmRadiusSqr, out PheromoneTrailSegment alarmSeg, out int alarmIdx))
            {
                _isPausingAtNest = false;
                _isFollowingAlarm = true;
                _isEscortPatrol = false;
                _guardedFoodSource = null;
                _follower.FollowTrailOutbound(alarmSeg, alarmIdx);
                SetState(SwarmAgentState.FollowingTrailOutbound);
                return true;
            }

            return false;
        }

        #endregion

        #region Nest Yard Guard & Scent Evaluation

        private void StartWanderingAroundNest()
        {
            _isPausingAtNest = false;
            _nestPauseTimer = 0f;
            _isFollowingAlarm = false;
            _isEscortPatrol = false;
            _guardedFoodSource = null;
            SetState(SwarmAgentState.WanderingAroundNest);
            _wanderTimer = 0f;
            PickNewWanderTarget();
        }

        private void PickNewWanderTarget()
        {
            _isPausingAtNest = false;
            _nestPauseTimer = 0f;

            if (_homeNest == null) return;

            Vector2 randomDisk = UnityEngine.Random.insideUnitCircle * _config.NestWanderRadius;
            Vector3 rawTarget = _homeNest.SpawnPosition + new Vector3(randomDisk.x, 0f, randomDisk.y);

            if (AstarPath.active != null)
            {
                var nearest = AstarPath.active.GetNearest(rawTarget, NearestNodeConstraint.Walkable);
                if (nearest.node != null)
                {
                    _currentWanderTarget = nearest.position;
                    _follower.WanderTo(_currentWanderTarget);
                    return;
                }
            }

            _currentWanderTarget = rawTarget;
            _follower.WanderTo(_currentWanderTarget);
        }

        private void EvaluateNestGuard(float deltaTime)
        {
            // 1. Hostile intruder proximity detection (Expanded sensory vigilance for soldiers)
            float senseRadius = _characterDefinition != null && _characterDefinition.VisionRadius > 0.1f
                ? _characterDefinition.VisionRadius
                : _config.SwarmAggroRadius * 1.5f;

            if (TryDetectHostileIntruder(senseRadius, out IAttackable intruder))
            {
                _isPausingAtNest = false;
                EngageTarget(intruder);
                return;
            }

            if (_scentCheckCooldown > 0f)
            {
                _scentCheckCooldown -= deltaTime;
            }

            PheromoneTrailEmitter emitter = TrailEmitter;

            // 2. High-Priority Alarm Trail Scan: Immediate focus-fire mobilization
            if (_scentCheckCooldown <= 0f && emitter != null && emitter.ActiveNodeCount > 0)
            {
                if (TryMobilizeToNearbyAlarmTrail())
                {
                    return;
                }

                // 3. Highway Escort Patrol Scan: Check if designated to escort foraging trails
                bool canEscort = UnityEngine.Random.value < _config.SoldierTrailEscortRatio;
                if (canEscort)
                {
                    float recruitRadiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                    if (emitter.TryFindBestRecruitmentNode(transform.position, recruitRadiusSqr, out PheromoneTrailSegment recruitSeg, out int recruitIdx,
                        priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                    {
                        _isPausingAtNest = false;
                        _isFollowingAlarm = false;
                        _isEscortPatrol = true;
                        _follower.FollowTrailOutbound(recruitSeg, recruitIdx);
                        SetState(SwarmAgentState.FollowingTrailOutbound);
                        return;
                    }
                }

                _scentCheckCooldown = 0.5f;
            }

            // 4. Nest yard pause execution with atmospheric antenna sampling
            if (_isPausingAtNest)
            {
                _nestPauseTimer += deltaTime;
                if (_nestPauseTimer >= _nestPauseDuration)
                {
                    _isPausingAtNest = false;
                    PickNewWanderTarget();
                }
                else
                {
                    // Subtle organic head/antenna sweeping while halted on guard
                    float twitchOffset = Mathf.Sin(_nestPauseTimer * 10f) * 12f;
                    transform.rotation = Quaternion.Euler(0f, _baseFacingAngle + twitchOffset, 0f);
                }
                return;
            }

            // 5. Roam smoothly around the nest yard
            _wanderTimer += deltaTime;
            Vector3 myPos = transform.position;
            float dx = _currentWanderTarget.x - myPos.x;
            float dz = _currentWanderTarget.z - myPos.z;
            bool reachedTarget = (dx * dx + dz * dz) <= (_config.ArrivalThreshold * _config.ArrivalThreshold);

            if (_wanderTimer >= 4.0f || reachedTarget)
            {
                _wanderTimer = 0f;
                float pauseChance = _characterDefinition != null ? _characterDefinition.PauseChance : 0.6f;
                Vector2 durationRange = _characterDefinition != null ? _characterDefinition.PauseDurationRange : new Vector2(0.5f, 1.2f);

                if (UnityEngine.Random.value < pauseChance)
                {
                    _isPausingAtNest = true;
                    _nestPauseTimer = 0f;
                    _nestPauseDuration = UnityEngine.Random.Range(durationRange.x, durationRange.y);
                    _baseFacingAngle = transform.eulerAngles.y;
                    _follower.StopMovement();
                }
                else
                {
                    PickNewWanderTarget();
                }
            }
        }

        #endregion

        #region Outbound Patrol & Chaining

        private void EvaluateOutboundPatrol(float deltaTime)
        {
            // Continuous combat threat awareness while marching along the escort corridor
            float aggroDist = _config.SwarmAggroRadius;

            if (TryDetectHostileIntruder(aggroDist, out IAttackable intruder))
            {
                EngageTarget(intruder);
                return;
            }

            // High-urgency Alarm Trail response: break peaceful foraging escort if battle erupts nearby
            if (!_isFollowingAlarm && TryMobilizeToNearbyAlarmTrail())
            {
                return;
            }
        }

        /// <summary>
        /// Queries the home nest for the number of allied workers actively harvesting at the specified food source.
        /// Grounded in Single Responsibility (SRP): Informs soldier escort decisions without managing harvesting logic.
        /// </summary>
        private int CountNearbyWorkersAtHarvest(FoodSource food, float radius = 5.0f)
        {
            if (food == null || _homeNest == null) return 0;

            int count = 0;
            float radiusSqr = radius * radius;
            Vector3 foodPos = food.WorldPosition;

            var workers = _homeNest.ActiveWorkers;
            for (int i = 0; i < workers.Count; i++)
            {
                ISwarmAgent agent = workers[i];
                if (agent == null || agent.AgentTransform == null || !agent.AgentTransform.gameObject.activeInHierarchy) continue;

                if (agent.CurrentState == SwarmAgentState.Harvesting)
                {
                    Vector3 diff = agent.AgentTransform.position - foodPos;
                    diff.y = 0f;
                    if (diff.sqrMagnitude <= radiusSqr)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Projects a guard station on the outer perimeter surrounding the harvest site.
        /// Prevents clumping on top of the resource node and spaces soldiers evenly around gathering workers.
        /// </summary>
        private void RepositionPerimeterGuard()
        {
            if (_guardedFoodSource == null || _follower == null) return;

            // Offset outward to defensive perimeter (2.5m - 4.0m)
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2.5f, 4.0f);
            Vector3 guardPos = _guardedFoodSource.WorldPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            _follower.ApproachTarget(guardPos);
        }

        protected override void HandleReachedTrailTip()
        {
            _tipGuardTimer = 0f;
            _perimeterPaceTimer = 0f;
            _emptyHarvesterTimer = 0f;
            SetState(SwarmAgentState.AtTrailEnd);

            // Check if there is an active food source at this trail tip
            float searchRadius = _config.FoodDetectionRadius + 2.0f;
            _guardedFoodSource = FoodSource.FindClosest(transform.position, searchRadius);

            // If escorting a foraging trail and NO allied workers are at this site yet:
            // Do not idle in an empty clearing! Immediately turn around and patrol back down the trail to escort oncoming workers!
            if (_isEscortPatrol && (_guardedFoodSource == null || CountNearbyWorkersAtHarvest(_guardedFoodSource) == 0))
            {
                OrderReturnHome();
                return;
            }

            // Workers are present: step outward into an active perimeter guard station
            RepositionPerimeterGuard();
        }

        #endregion

        #region Trail Tip Vigil & Perimeter Defense

        private void EvaluateTrailTipVigil(float deltaTime)
        {
            // 1. Immediate threat response at the destination
            float aggroDist = _config.SwarmAggroRadius;

            if (TryDetectHostileIntruder(aggroDist, out IAttackable intruder))
            {
                EngageTarget(intruder);
                return;
            }

            // 2. High-urgency Alarm Trail response: break food guard if an attack trail is laid nearby
            if (!_isFollowingAlarm && TryMobilizeToNearbyAlarmTrail())
            {
                return;
            }

            _tipGuardTimer += deltaTime;

            // Scenario A: End of Alarm Trail (Area search)
            if (_isFollowingAlarm)
            {
                if (_tipGuardTimer >= _config.TrailEndSearchDuration)
                {
                    // Danger zone clear; return home to nest
                    OrderReturnHome();
                }
                return;
            }

            // Scenario B: Escorting Foraging Trail (Food perimeter guard vigil)
            float guardDuration = _config.SoldierHarvestGuardDuration;
            if (_tipGuardTimer >= guardDuration || (_guardedFoodSource != null && !_guardedFoodSource.CanHarvest))
            {
                // Shift finished or food depleted; march back along highway
                OrderReturnHome();
                return;
            }

            // Check worker presence: If all workers finished or left, don't abandon vigilance in an empty field
            if (_isEscortPatrol)
            {
                int activeHarvesters = CountNearbyWorkersAtHarvest(_guardedFoodSource);
                if (activeHarvesters == 0)
                {
                    _emptyHarvesterTimer += deltaTime;
                    if (_emptyHarvesterTimer >= 2.0f)
                    {
                        // Harvesters have packed up and departed; escort the column home
                        OrderReturnHome();
                        return;
                    }
                }
                else
                {
                    _emptyHarvesterTimer = 0f;
                }
            }

            // Active perimeter pacing: Periodically reposition around the outer perimeter arc
            _perimeterPaceTimer += deltaTime;
            if (_perimeterPaceTimer >= 2.5f)
            {
                _perimeterPaceTimer = 0f;
                RepositionPerimeterGuard();
            }

            // Face outwards defensively away from the food cache while watching for predators
            if (_guardedFoodSource != null)
            {
                Vector3 awayFromFood = transform.position - _guardedFoodSource.WorldPosition;
                awayFromFood.y = 0f;
                if (awayFromFood.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(awayFromFood.normalized),
                        180f * deltaTime);
                }
            }
        }

        #endregion

        #region Return & De-escalation

        private void EvaluateReturningHome(float deltaTime)
        {
            // Scan for hostiles while retracing steps
            float aggroDist = _config.SwarmAggroRadius;

            if (TryDetectHostileIntruder(aggroDist, out IAttackable intruder))
            {
                EngageTarget(intruder);
                return;
            }

            // High-urgency Alarm Trail response: turn around if battle erupts nearby
            if (TryMobilizeToNearbyAlarmTrail())
            {
                return;
            }
        }

        protected override void HandleReturnedToNest()
        {
            base.HandleReturnedToNest();
            StartWanderingAroundNest();
        }

        public override void DisengageCombat()
        {
            base.DisengageCombat();

            // 0. If an alarm trail is active nearby, continue military pursuit!
            if (TryMobilizeToNearbyAlarmTrail())
            {
                return;
            }

            // 1. If guarding an active food node at the trail tip, resume perimeter guard vigil
            if (_guardedFoodSource != null && _guardedFoodSource.CanHarvest)
            {
                SetState(SwarmAgentState.AtTrailEnd);
                return;
            }

            // 2. If escorting an active outbound recruitment trail, resume escort patrol
            if (_isEscortPatrol && _follower != null && _follower.TrackedSegment != null && _follower.TrackedSegment.NodeCount > 0)
            {
                _follower.FollowTrailOutbound();
                SetState(SwarmAgentState.FollowingTrailOutbound);
                return;
            }

            // 3. If within nest perimeter, resume nest yard guard
            if (_homeNest != null)
            {
                Vector3 toNest = _homeNest.SpawnPosition - transform.position;
                toNest.y = 0f;
                float distSqr = toNest.sqrMagnitude;
                float nestRadius = _config.NestWanderRadius + 2.0f;

                if (distSqr <= nestRadius * nestRadius)
                {
                    StartWanderingAroundNest();
                    return;
                }
            }

            // 4. Otherwise retrace steps home
            OrderReturnHome();
        }

        #endregion
    }
}
