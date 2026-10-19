using System;
using AntScout.Characters;
using AntScout.Config;
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
    /// High-level behavioral controller and state machine for Worker Ants.
    /// Inherits universal locomotion, pooling, vitals, primary action execution, and reactive enmity from SwarmAgent.
    /// Grounded in Single Responsibility (SRP) and Liskov Substitution (LSP) by specializing solely in
    /// food source harvesting, cargo transport, and delivery to the home nest depot.
    /// </summary>
    [RequireComponent(typeof(AntTrailFollower))]
    public partial class WorkerAntController : SwarmAgent
    {
        [Header("Carried Cargo Visual")]
        [Tooltip("Visual representation of food morsel held in mandibles. Auto-created if unassigned.")]
        [SerializeField] private GameObject _carriedMorselVisual;

        private FoodSource _targetFood;

        private float _searchTimer;
        private float _harvestTimer;
        private float _wanderTimer;
        private float _tipWanderTimer;
        private float _scentCheckCooldown;
        private float _forageCheckTimer;
        private Vector3 _currentWanderTarget;
        private Vector3 _tipAnchorPosition;
        private PheromoneTrailSegment _lastExhaustedSegment;
        private bool _isAtHarvestNode;

        private bool _isPausingAtNest;
        private float _nestPauseTimer;
        private float _nestPauseDuration;
        private float _baseFacingAngle;

        private int _carriedAmount;
        private ResourceDefinition _carriedResource;

        public int CarriedAmount => _carriedAmount;
        public ResourceDefinition CarriedResource => _carriedResource;

        protected override void Awake()
        {
            base.Awake();
            EnsureCarriedVisual();
        }

        private void EnsureCarriedVisual()
        {
            if (_carriedMorselVisual == null)
            {
                _carriedMorselVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _carriedMorselVisual.name = "CarriedMorsel";
                _carriedMorselVisual.transform.SetParent(transform, false);
                _carriedMorselVisual.transform.localPosition = new Vector3(0f, 0.15f, 0.4f);
                _carriedMorselVisual.transform.localScale = Vector3.one * 0.25f;

                Collider col = _carriedMorselVisual.GetComponent<Collider>();
                if (col != null) Destroy(col);

                _carriedMorselVisual.SetActive(false);
            }
        }

        public override void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter)
        {
            base.Initialize(homeNest, trailEmitter);

            _carriedAmount = 0;
            _carriedResource = null;
            _scentCheckCooldown = 0f;
            _lastExhaustedSegment = null;
            _isAtHarvestNode = false;
            _targetFood = null;

            if (_carriedMorselVisual != null)
            {
                _carriedMorselVisual.SetActive(false);
            }

            // StarCraft-style start: Mill and wander around the nest yard
            StartWanderingAroundNest();
        }

        public override void OrderReturnHome()
        {
            if (_currentState == SwarmAgentState.ReturningHome) return;

            _targetFood = null;
            _isAtHarvestNode = false;
            bool hasCargo = _carriedAmount > 0;
            if (_follower != null)
            {
                _follower.ReturnToNest(hasCargo);
            }
            base.OrderReturnHome();
        }

        protected override void EvaluateAgentState(float deltaTime)
        {
            switch (_currentState)
            {
                case SwarmAgentState.WanderingAroundNest:
                    EvaluateNestWandering(deltaTime);
                    break;

                case SwarmAgentState.FollowingTrailOutbound:
                    EvaluateOutboundForaging(deltaTime);
                    break;

                case SwarmAgentState.AtTrailEnd:
                    EvaluateFoodSearch(deltaTime);
                    break;

                case SwarmAgentState.Harvesting:
                    ProcessHarvesting(deltaTime);
                    break;

                case SwarmAgentState.ReturningHome:
                    EvaluateReturningHome(deltaTime);
                    break;
            }
        }

        private void StartWanderingAroundNest()
        {
            _isPausingAtNest = false;
            _nestPauseTimer = 0f;
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

        private void EvaluateNestWandering(float deltaTime)
        {
            // 1. Hostile intruder proximity detection (Self-Defense)
            if (TryDetectHostileIntruder(_config.SwarmAggroRadius, out IAttackable intruder))
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

            // 2. Sniff for active recruitment pheromone nearby
            PheromoneTrailEmitter emitter = TrailEmitter;
            if (_scentCheckCooldown <= 0f && emitter != null && emitter.ActiveNodeCount > 0)
            {
                float recruitRadiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                if (emitter.TryFindBestRecruitmentNode(transform.position, recruitRadiusSqr, out PheromoneTrailSegment bestSeg, out int bestIdx,
                    priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    _isPausingAtNest = false;
                    _follower.FollowTrailOutbound(bestSeg, bestIdx);
                    SetState(SwarmAgentState.FollowingTrailOutbound);
                    return;
                }
            }

            // 3. Nest yard pause execution with atmospheric antenna sampling
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
                    // Subtle organic head/antenna sweeping while halted
                    float twitchOffset = Mathf.Sin(_nestPauseTimer * 10f) * 12f;
                    transform.rotation = Quaternion.Euler(0f, _baseFacingAngle + twitchOffset, 0f);
                }
                return;
            }

            // 4. Roam smoothly around the nest yard
            _wanderTimer += deltaTime;
            Vector3 myPos = transform.position;
            float dx = _currentWanderTarget.x - myPos.x;
            float dz = _currentWanderTarget.z - myPos.z;
            bool reachedTarget = (dx * dx + dz * dz) <= (_config.ArrivalThreshold * _config.ArrivalThreshold);

            if (_wanderTimer >= 3.5f || reachedTarget)
            {
                _wanderTimer = 0f;
                float pauseChance = _characterDefinition != null ? _characterDefinition.PauseChance : 0.5f;
                Vector2 durationRange = _characterDefinition != null ? _characterDefinition.PauseDurationRange : new Vector2(0.4f, 1.0f);

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

        private void EvaluateFoodSearch(float deltaTime)
        {
            // 1. Hostile intruder proximity detection
            if (TryDetectHostileIntruder(_config.SwarmAggroRadius, out IAttackable intruder))
            {
                EngageTarget(intruder);
                BroadcastAlarmToSisters(intruder);
                return;
            }

            // 2. Sniff for harvestable food sources
            float searchRadius = _config.FoodDetectionRadius;
            FoodSource closestFood = FoodSource.FindClosest(transform.position, searchRadius);

            if (closestFood != null && closestFood.CanHarvest)
            {
                StartHarvesting(closestFood);
                return;
            }

            // 3. Sniff for continuing or staggered pheromone trail extensions
            PheromoneTrailEmitter emitter = TrailEmitter;
            if (emitter != null && emitter.ActiveNodeCount > 0)
            {
                float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;
                if (emitter.TryFindBestRecruitmentNode(transform.position, samplingRadiusSqr, out PheromoneTrailSegment newSeg, out int newIdx,
                    excludeSegment: _lastExhaustedSegment, priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    if (newSeg.NodeCount > 0)
                    {
                        Vector3 nestPos = _homeNest != null ? _homeNest.SpawnPosition : transform.position;
                        Vector3 segFirst = newSeg.Nodes[0].Position;
                        Vector3 segLast = newSeg.Nodes[newSeg.Nodes.Count - 1].Position;
                        float distFirstSqr = (segFirst.x - nestPos.x) * (segFirst.x - nestPos.x) + (segFirst.z - nestPos.z) * (segFirst.z - nestPos.z);
                        float distLastSqr = (segLast.x - nestPos.x) * (segLast.x - nestPos.x) + (segLast.z - nestPos.z) * (segLast.z - nestPos.z);
                        float maxSegDistSqr = Mathf.Max(distFirstSqr, distLastSqr);

                        float dxAnchor = _tipAnchorPosition.x - nestPos.x;
                        float dzAnchor = _tipAnchorPosition.z - nestPos.z;
                        float anchorDistSqr = dxAnchor * dxAnchor + dzAnchor * dzAnchor;

                        // Accept continuation segment if it extends outward at least as far as where we stopped
                        // OR if the candidate trail leads to an active harvestable FoodSource (arterial branch highway)
                        bool extendsOutward = maxSegDistSqr >= anchorDistSqr - 2.0f;
                        bool leadsToFood = false;

                        if (!extendsOutward)
                        {
                            Vector3 myPos = transform.position;
                            float dFirst = (segFirst.x - myPos.x) * (segFirst.x - myPos.x) + (segFirst.z - myPos.z) * (segFirst.z - myPos.z);
                            float dLast = (segLast.x - myPos.x) * (segLast.x - myPos.x) + (segLast.z - myPos.z) * (segLast.z - myPos.z);
                            Vector3 farTip = dFirst > dLast ? segFirst : segLast;

                            FoodSource candidateFood = FoodSource.FindClosest(farTip, 4.0f);
                            leadsToFood = candidateFood != null && candidateFood.CanHarvest;
                        }

                        if (extendsOutward || leadsToFood)
                        {
                            _searchTimer = 0f;
                            _lastExhaustedSegment = null;
                            _follower.FollowTrailOutbound(newSeg, newIdx);
                            SetState(SwarmAgentState.FollowingTrailOutbound);
                            return;
                        }
                    }
                }
            }

            // 4. Actively wander around the trail tip area (Area-Restricted Search)
            _tipWanderTimer += deltaTime;
            if (_tipWanderTimer >= 2.0f)
            {
                _tipWanderTimer = 0f;
                PickNewTipWanderTarget();
            }

            // 5. Timeout -> Begin organic return toward home along the highway
            _searchTimer += deltaTime;
            float maxTipSearchDuration = Mathf.Min(1.5f, _config.TrailTipWanderDuration);
            if (_searchTimer >= maxTipSearchDuration)
            {
                _searchTimer = 0f;
                OrderReturnHome();
            }
        }

        private void EvaluateReturningHome(float deltaTime)
        {
            // If carrying food, deliver directly to nest without distraction
            if (_carriedAmount > 0) return;

            // If empty-handed, actively sniff for fresh or extended recruitment trails to resume foraging
            PheromoneTrailEmitter emitter = TrailEmitter;
            if (emitter != null && emitter.ActiveNodeCount > 0)
            {
                float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;
                if (emitter.TryFindBestRecruitmentNode(transform.position, samplingRadiusSqr, out PheromoneTrailSegment newSeg, out int newIdx,
                    excludeSegment: _lastExhaustedSegment, priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    if (newSeg.NodeCount > 0)
                    {
                        Vector3 nestPos = _homeNest != null ? _homeNest.SpawnPosition : transform.position;
                        Vector3 myPos = transform.position;

                        Vector3 segFirst = newSeg.Nodes[0].Position;
                        Vector3 segLast = newSeg.Nodes[newSeg.Nodes.Count - 1].Position;
                        float distFirstSqr = (segFirst.x - nestPos.x) * (segFirst.x - nestPos.x) + (segFirst.z - nestPos.z) * (segFirst.z - nestPos.z);
                        float distLastSqr = (segLast.x - nestPos.x) * (segLast.x - nestPos.x) + (segLast.z - nestPos.z) * (segLast.z - nestPos.z);
                        float maxSegDistSqr = Mathf.Max(distFirstSqr, distLastSqr);

                        float myDistSqr = (myPos.x - nestPos.x) * (myPos.x - nestPos.x) + (myPos.z - nestPos.z) * (myPos.z - nestPos.z);

                        // Only resume outbound foraging if this candidate trail extends substantially further outward than where we currently are
                        // OR if the candidate trail leads to an active harvestable FoodSource (arterial branch highway)
                        bool extendsOutward = maxSegDistSqr > myDistSqr + 3.0f;
                        bool leadsToFood = false;

                        if (!extendsOutward)
                        {
                            float dFirst = (segFirst.x - myPos.x) * (segFirst.x - myPos.x) + (segFirst.z - myPos.z) * (segFirst.z - myPos.z);
                            float dLast = (segLast.x - myPos.x) * (segLast.x - myPos.x) + (segLast.z - myPos.z) * (segLast.z - myPos.z);
                            Vector3 farTip = dFirst > dLast ? segFirst : segLast;

                            FoodSource candidateFood = FoodSource.FindClosest(farTip, 4.0f);
                            leadsToFood = candidateFood != null && candidateFood.CanHarvest;
                        }

                        if (extendsOutward || leadsToFood)
                        {
                            _lastExhaustedSegment = null;
                            _follower.FollowTrailOutbound(newSeg, newIdx);
                            SetState(SwarmAgentState.FollowingTrailOutbound);
                        }
                    }
                }
            }
        }

        private void PickNewTipWanderTarget()
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * _config.TrailTipWanderRadius;
            Vector3 target = _tipAnchorPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
            _follower.WanderTo(target);
        }

        private void EvaluateOutboundForaging(float deltaTime)
        {
            _forageCheckTimer += deltaTime;
            if (_forageCheckTimer < 0.25f) return;
            _forageCheckTimer = 0f;

            // Check if our outbound trail passed close enough to food to begin harvesting
            float searchRadius = _config.FoodDetectionRadius;
            FoodSource closestFood = FoodSource.FindClosest(transform.position, searchRadius);
            if (closestFood != null && closestFood.CanHarvest)
            {
                StartHarvesting(closestFood);
            }
        }

        private void StartHarvesting(FoodSource food)
        {
            _targetFood = food;
            _harvestTimer = 0f;
            _isAtHarvestNode = false;
            SetState(SwarmAgentState.Harvesting);
        }

        private void ProcessHarvesting(float deltaTime)
        {
            if (_targetFood == null || !_targetFood.CanHarvest)
            {
                _isAtHarvestNode = false;
                OrderReturnHome();
                return;
            }

            Vector3 foodPos = _targetFood.WorldPosition;
            Vector3 myPos = transform.position;

            // 1. Move to contact distance of the food object if not yet locked at the perimeter ring
            if (!_isAtHarvestNode)
            {
                float dx = foodPos.x - myPos.x;
                float dz = foodPos.z - myPos.z;
                float distSqr = dx * dx + dz * dz;
                float harvestRange = Mathf.Max(_config.ArrivalThreshold * 3.0f, 1.8f);

                if (distSqr > harvestRange * harvestRange)
                {
                    _follower.ApproachTarget(foodPos);
                    return;
                }

                // Contact established: lock in place at the perimeter ring
                _isAtHarvestNode = true;
                _follower.StopMovement();
            }

            // 2. In contact range: smoothly face the food source
            Vector3 lookDir = foodPos - myPos;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    360f * deltaTime);
            }

            // 3. Harvest extraction timer
            _harvestTimer += deltaTime;
            float durationNeeded = _targetFood.HarvestDuration;
            ResourceDefinition foodDef = _targetFood.Definition;

            if (_harvestTimer >= durationNeeded)
            {
                _isAtHarvestNode = false;

                if (_targetFood.TryHarvest(1, out int harvested) && harvested > 0)
                {
                    _carriedAmount = harvested;
                    _carriedResource = foodDef;

                    if (_carriedMorselVisual != null)
                    {
                        _carriedMorselVisual.SetActive(true);
                    }
                }

                OrderReturnHome();
            }
        }

        #region Enmity Overrides

        /// <summary>
        /// Enforces cargo preservation: Workers laden with harvested cargo ignore calls to arms
        /// to prioritize safely delivering food to the depot.
        /// </summary>
        public override void OnAlarmReceived(IAttackable hostileTarget)
        {
            if (_carriedAmount > 0)
            {
                return;
            }

            base.OnAlarmReceived(hostileTarget);
        }

        /// <summary>
        /// Resumes normal caste routine when combat terminates.
        /// </summary>
        protected override void OnCombatDisengaged()
        {
            if (_currentState == SwarmAgentState.ReturningHome)
            {
                return;
            }

            if (_carriedAmount > 0)
            {
                OrderReturnHome();
            }
            else if (_follower != null && _follower.TrackedSegment != null && _follower.TrackedSegment.NodeCount > 0)
            {
                _follower.FollowTrailOutbound();
                SetState(SwarmAgentState.FollowingTrailOutbound);
            }
            else
            {
                StartWanderingAroundNest();
            }
        }

        #endregion

        #region Locomotion & Nest Event Handlers

        protected override void HandleReachedTrailTip()
        {
            if (_currentState == SwarmAgentState.FollowingTrailOutbound)
            {
                _searchTimer = 0f;
                _tipWanderTimer = 0f;
                _tipAnchorPosition = transform.position;
                _lastExhaustedSegment = _follower.TrackedSegment;
                SetState(SwarmAgentState.AtTrailEnd);
                PickNewTipWanderTarget();
            }
        }

        protected override void HandleTrailResumed()
        {
            if (_currentState == SwarmAgentState.AtTrailEnd)
            {
                _searchTimer = 0f;
                _lastExhaustedSegment = null;
                _follower.FollowTrailOutbound();
                SetState(SwarmAgentState.FollowingTrailOutbound);
            }
        }

        protected override void HandleTrailLost()
        {
            if (_currentState == SwarmAgentState.FollowingTrailOutbound || _currentState == SwarmAgentState.AtTrailEnd)
            {
                OrderReturnHome();
            }
        }

        protected override void HandleReturnedToNest()
        {
            _lastExhaustedSegment = null;
            bool broughtFood = _carriedAmount > 0 && _carriedResource != null;
            if (broughtFood && _homeNest != null)
            {
                _homeNest.DepositResource(_carriedResource, _carriedAmount);
                _carriedAmount = 0;
                _carriedResource = null;
            }

            if (_carriedMorselVisual != null)
            {
                _carriedMorselVisual.SetActive(false);
            }

            if (_homeNest != null)
            {
                _homeNest.NotifyWorkerReturned(this);
            }

            // StarCraft-style mining trip repetition:
            // If the worker brought food and the recruitment trail is still connected to the nest, head back out immediately!
            if (broughtFood && _homeNest != null && _homeNest.IsTrailConnected)
            {
                PheromoneTrailEmitter emitter = TrailEmitter;
                float recruitRadiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                if (emitter != null && emitter.TryFindBestRecruitmentNode(transform.position, recruitRadiusSqr, out PheromoneTrailSegment bestSeg, out int bestIdx,
                    priorityDuration: _config.NewTrailPriorityDuration, priorityMultiplier: _config.NewTrailPriorityMultiplier))
                {
                    _follower.FollowTrailOutbound(bestSeg, bestIdx);
                }
                else
                {
                    _follower.FollowTrailOutbound();
                }
                SetState(SwarmAgentState.FollowingTrailOutbound);
            }
            else
            {
                // Returned empty-handed (food depleted or trail lost) -> wander with a cooldown to prevent thrashing
                if (!broughtFood)
                {
                    _scentCheckCooldown = 3.0f;
                }
                StartWanderingAroundNest();
            }
        }

        #endregion

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            // Cyan wire sphere for sensory trail detection bubble
            Gizmos.color = new Color(0f, 0.9f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _config.TrailSamplingRadius);

            // Yellow wire sphere for tip wander area (when actively milling at trail tip)
            if (_currentState == SwarmAgentState.AtTrailEnd)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
                Gizmos.DrawWireSphere(_tipAnchorPosition, _config.TrailTipWanderRadius);
            }
        }

        #region IPoolable Implementation

        public override void OnSpawnedFromPool()
        {
            base.OnSpawnedFromPool();
            _carriedAmount = 0;
            _carriedResource = null;
            _targetFood = null;
            _scentCheckCooldown = 0f;
            _lastExhaustedSegment = null;
            _isAtHarvestNode = false;
            _isPausingAtNest = false;
            _nestPauseTimer = 0f;

            if (_carriedMorselVisual != null)
            {
                _carriedMorselVisual.SetActive(false);
            }
        }

        public override void OnDespawnedToPool()
        {
            base.OnDespawnedToPool();
            _carriedAmount = 0;
            _carriedResource = null;
            _targetFood = null;
            _isAtHarvestNode = false;

            if (_carriedMorselVisual != null)
            {
                _carriedMorselVisual.SetActive(false);
            }
        }

        #endregion
    }
}
