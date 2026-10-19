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

        public bool IsFollowingAlarm => _isFollowingAlarm;
        public bool IsEscortPatrol => _isEscortPatrol;

        public override void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter)
        {
            base.Initialize(homeNest, trailEmitter);

            _scentCheckCooldown = 0f;
            _tipGuardTimer = 0f;
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
                BroadcastAlarmToSisters(intruder);
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
                float alarmRadiusSqr = (_config.NestRecruitmentRadius * 1.5f) * (_config.NestRecruitmentRadius * 1.5f);
                if (emitter.TryFindBestAlarmNode(transform.position, alarmRadiusSqr, out PheromoneTrailSegment alarmSeg, out int alarmIdx))
                {
                    _isPausingAtNest = false;
                    _isFollowingAlarm = true;
                    _isEscortPatrol = false;
                    _follower.FollowTrailOutbound(alarmSeg, alarmIdx);
                    SetState(SwarmAgentState.FollowingTrailOutbound);
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
            // Continuous combat threat awareness while marching
            float aggroDist = _characterDefinition != null && _characterDefinition.VisionRadius > 0.1f
                ? _characterDefinition.VisionRadius
                : _config.SwarmAggroRadius * 1.5f;

            if (TryDetectHostileIntruder(aggroDist, out IAttackable intruder))
            {
                EngageTarget(intruder);
                BroadcastAlarmToSisters(intruder);
            }
        }

        protected override void HandleReachedTrailTip()
        {
            _tipGuardTimer = 0f;
            SetState(SwarmAgentState.AtTrailEnd);

            // Check if there is an active food source at this trail tip
            float searchRadius = _config.FoodDetectionRadius + 2.0f;
            _guardedFoodSource = FoodSource.FindClosest(transform.position, searchRadius);
        }

        #endregion

        #region Trail Tip Vigil & Perimeter Defense

        private void EvaluateTrailTipVigil(float deltaTime)
        {
            // 1. Immediate threat response at the destination
            float aggroDist = _characterDefinition != null && _characterDefinition.VisionRadius > 0.1f
                ? _characterDefinition.VisionRadius
                : _config.SwarmAggroRadius * 1.5f;

            if (TryDetectHostileIntruder(aggroDist, out IAttackable intruder))
            {
                EngageTarget(intruder);
                BroadcastAlarmToSisters(intruder);
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

            // Face outwards defensively while standing guard
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
            float aggroDist = _characterDefinition != null && _characterDefinition.VisionRadius > 0.1f
                ? _characterDefinition.VisionRadius
                : _config.SwarmAggroRadius;

            if (TryDetectHostileIntruder(aggroDist, out IAttackable intruder))
            {
                EngageTarget(intruder);
                BroadcastAlarmToSisters(intruder);
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

            OrderReturnHome();
        }

        #endregion
    }
}
