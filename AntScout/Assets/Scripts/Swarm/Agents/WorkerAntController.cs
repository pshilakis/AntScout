using System;
using AntScout.Config;
using AntScout.Pheromone;
using AntScout.Resources.Components;
using AntScout.Resources.Enums;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// High-level behavioral controller and state machine for Worker Ants.
    /// Implements ISwarmAgent and IUpdatable to allow polymorphic management and deterministic time stepping on TimeChannel.World.
    /// Coordinates outbound trail following, food source harvesting, and inbound delivery to the home nest.
    /// </summary>
    [RequireComponent(typeof(AntTrailFollower))]
    public class WorkerAntController : MonoBehaviour, ISwarmAgent, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for worker balance parameters.")]
        [SerializeField] private SwarmConfigSO _config;

        [Tooltip("Configuration for resource harvesting timings and biomass values.")]
        [SerializeField] private ResourceConfigSO _resourceConfig;

        [Header("Carried Cargo Visual")]
        [Tooltip("Visual representation of food morsel held in mandibles. Auto-created if unassigned.")]
        [SerializeField] private GameObject _carriedMorselVisual;

        private AntTrailFollower _follower;
        private ColonyNest _homeNest;
        private PheromoneTrailEmitter _trailEmitter;
        private SwarmAgentState _currentState = SwarmAgentState.Idle;
        private FoodSource _targetFood;

        private float _searchTimer;
        private float _harvestTimer;
        private float _wanderTimer;
        private float _tipWanderTimer;
        private float _scentCheckCooldown;
        private Vector3 _currentWanderTarget;
        private Vector3 _tipAnchorPosition;
        private PheromoneTrailSegment _lastExhaustedSegment;

        private int _carriedAmount;
        private ResourceType _carriedType;

        public SwarmAgentState CurrentState => _currentState;
        public Transform AgentTransform => transform;
        public ColonyNest HomeNest => _homeNest;
        public int CarriedAmount => _carriedAmount;
        public ResourceType CarriedType => _carriedType;

        public event Action<SwarmAgentState> OnStateChanged;

        private void Awake()
        {
            _follower = GetComponent<AntTrailFollower>();

            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required SwarmConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid SwarmConfigSO in the Inspector.");
            }

            if (_follower == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required AntTrailFollower on GameObject '{gameObject.name}'.");
            }

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

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
            _follower.OnReachedTrailTip += HandleReachedTrailTip;
            _follower.OnTrailResumed += HandleTrailResumed;
            _follower.OnTrailLost += HandleTrailLost;
            _follower.OnReturnedToNest += HandleReturnedToNest;
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
            _follower.OnReachedTrailTip -= HandleReachedTrailTip;
            _follower.OnTrailResumed -= HandleTrailResumed;
            _follower.OnTrailLost -= HandleTrailLost;
            _follower.OnReturnedToNest -= HandleReturnedToNest;
        }

        public void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter)
        {
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));
            _trailEmitter = trailEmitter ?? throw new ArgumentNullException(nameof(trailEmitter));
            _follower.Initialize(homeNest, trailEmitter, _config);
            _carriedAmount = 0;
            _scentCheckCooldown = 0f;
            _lastExhaustedSegment = null;

            if (_carriedMorselVisual != null)
            {
                _carriedMorselVisual.SetActive(false);
            }

            // StarCraft-style start: Mill and wander around the nest yard
            StartWanderingAroundNest();
        }

        public void OrderReturnHome()
        {
            _targetFood = null;
            bool hasCargo = _carriedAmount > 0;
            _follower.ReturnToNest(hasCargo);
            SetState(SwarmAgentState.ReturningHome);
        }

        public void OnUpdate(float deltaTime)
        {
            switch (_currentState)
            {
                case SwarmAgentState.WanderingAroundNest:
                    EvaluateNestWandering(deltaTime);
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
            SetState(SwarmAgentState.WanderingAroundNest);
            _wanderTimer = 0f;
            PickNewWanderTarget();
        }

        private void PickNewWanderTarget()
        {
            if (_homeNest == null) return;

            Vector2 randomDisk = UnityEngine.Random.insideUnitCircle * _config.NestWanderRadius;
            _currentWanderTarget = _homeNest.DepotPosition + new Vector3(randomDisk.x, 0f, randomDisk.y);
            _follower.WanderTo(_currentWanderTarget);
        }

        private void EvaluateNestWandering(float deltaTime)
        {
            if (_scentCheckCooldown > 0f)
            {
                _scentCheckCooldown -= deltaTime;
            }

            // 1. Sniff for active recruitment pheromone nearby
            if (_scentCheckCooldown <= 0f && _trailEmitter != null && _trailEmitter.ActiveNodeCount > 0)
            {
                float recruitRadiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                if (_trailEmitter.HasRecruitmentNear(transform.position, recruitRadiusSqr))
                {
                    _follower.FollowTrailOutbound();
                    SetState(SwarmAgentState.FollowingTrailOutbound);
                    return;
                }
            }

            // 2. Roam smoothly around the nest yard
            _wanderTimer += deltaTime;
            Vector3 myPos = transform.position;
            float dx = _currentWanderTarget.x - myPos.x;
            float dz = _currentWanderTarget.z - myPos.z;
            bool reachedTarget = (dx * dx + dz * dz) <= (_config.ArrivalThreshold * _config.ArrivalThreshold);

            if (_wanderTimer >= 3.5f || reachedTarget)
            {
                _wanderTimer = 0f;
                PickNewWanderTarget();
            }
        }

        private void EvaluateFoodSearch(float deltaTime)
        {
            // 1. Sniff for harvestable food sources
            float searchRadius = _resourceConfig != null ? _resourceConfig.FoodDetectionRadius : 3.5f;
            FoodSource closestFood = FoodSource.FindClosest(transform.position, searchRadius);

            if (closestFood != null && closestFood.CanHarvest)
            {
                StartHarvesting(closestFood);
                return;
            }

            // 2. Sniff for continuing or staggered pheromone trail extensions
            if (_trailEmitter != null && _trailEmitter.ActiveNodeCount > 0)
            {
                float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;
                if (_trailEmitter.TryFindBestRecruitmentNode(transform.position, samplingRadiusSqr, out PheromoneTrailSegment newSeg, out int newIdx, excludeSegment: _lastExhaustedSegment))
                {
                    if (newSeg.NodeCount > 0)
                    {
                        Vector3 nestPos = _homeNest != null ? _homeNest.DepotPosition : transform.position;
                        Vector3 segFirst = newSeg.Nodes[0].Position;
                        Vector3 segLast = newSeg.Nodes[newSeg.Nodes.Count - 1].Position;
                        float distFirstSqr = (segFirst.x - nestPos.x) * (segFirst.x - nestPos.x) + (segFirst.z - nestPos.z) * (segFirst.z - nestPos.z);
                        float distLastSqr = (segLast.x - nestPos.x) * (segLast.x - nestPos.x) + (segLast.z - nestPos.z) * (segLast.z - nestPos.z);
                        float maxSegDistSqr = Mathf.Max(distFirstSqr, distLastSqr);

                        float dxAnchor = _tipAnchorPosition.x - nestPos.x;
                        float dzAnchor = _tipAnchorPosition.z - nestPos.z;
                        float anchorDistSqr = dxAnchor * dxAnchor + dzAnchor * dzAnchor;

                        // Accept continuation segment if it extends outward at least as far as where we stopped
                        if (maxSegDistSqr >= anchorDistSqr - 2.0f)
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

            // 3. Actively wander around the trail tip area (Area-Restricted Search)
            _tipWanderTimer += deltaTime;
            if (_tipWanderTimer >= 2.0f)
            {
                _tipWanderTimer = 0f;
                PickNewTipWanderTarget();
            }

            // 4. Timeout -> Begin slow, organic meander toward home
            _searchTimer += deltaTime;
            if (_searchTimer >= _config.TrailTipWanderDuration)
            {
                _searchTimer = 0f;
                OrderReturnHome();
            }
        }

        private void EvaluateReturningHome(float deltaTime)
        {
            // If carrying food, deliver directly to nest without distraction
            if (_carriedAmount > 0) return;

            // If empty-handed, actively sniff for fresh or extended recruitment trails to resume foraging!
            if (_trailEmitter != null && _trailEmitter.ActiveNodeCount > 0)
            {
                float samplingRadiusSqr = _config.TrailSamplingRadius * _config.TrailSamplingRadius;
                if (_trailEmitter.TryFindBestRecruitmentNode(transform.position, samplingRadiusSqr, out PheromoneTrailSegment newSeg, out int newIdx, excludeSegment: _lastExhaustedSegment))
                {
                    if (newSeg.NodeCount > 0)
                    {
                        Vector3 nestPos = _homeNest != null ? _homeNest.DepotPosition : transform.position;
                        Vector3 myPos = transform.position;

                        Vector3 segFirst = newSeg.Nodes[0].Position;
                        Vector3 segLast = newSeg.Nodes[newSeg.Nodes.Count - 1].Position;
                        float distFirstSqr = (segFirst.x - nestPos.x) * (segFirst.x - nestPos.x) + (segFirst.z - nestPos.z) * (segFirst.z - nestPos.z);
                        float distLastSqr = (segLast.x - nestPos.x) * (segLast.x - nestPos.x) + (segLast.z - nestPos.z) * (segLast.z - nestPos.z);
                        float maxSegDistSqr = Mathf.Max(distFirstSqr, distLastSqr);

                        float myDistSqr = (myPos.x - nestPos.x) * (myPos.x - nestPos.x) + (myPos.z - nestPos.z) * (myPos.z - nestPos.z);

                        // Only resume outbound foraging if this trail extends further out than where we currently are
                        if (maxSegDistSqr > myDistSqr)
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
            _follower.WanderTo(target, _config.WorkerMoveSpeed);
        }

        private void StartHarvesting(FoodSource food)
        {
            _targetFood = food;
            _harvestTimer = 0f;
            _follower.StopMovement();
            SetState(SwarmAgentState.Harvesting);
        }

        private void ProcessHarvesting(float deltaTime)
        {
            if (_targetFood == null || !_targetFood.CanHarvest)
            {
                OrderReturnHome();
                return;
            }

            _harvestTimer += deltaTime;
            float durationNeeded = _resourceConfig != null ? _resourceConfig.GetHarvestDuration(_targetFood.Type) : 1.5f;

            if (_harvestTimer >= durationNeeded)
            {
                if (_targetFood.TryHarvest(1, out int harvested) && harvested > 0)
                {
                    _carriedAmount = harvested;
                    _carriedType = _targetFood.Type;

                    if (_carriedMorselVisual != null)
                    {
                        _carriedMorselVisual.SetActive(true);
                    }
                }

                OrderReturnHome();
            }
        }

        private void HandleReachedTrailTip()
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

        private void HandleTrailResumed()
        {
            if (_currentState == SwarmAgentState.AtTrailEnd)
            {
                _searchTimer = 0f;
                _lastExhaustedSegment = null;
                _follower.FollowTrailOutbound();
                SetState(SwarmAgentState.FollowingTrailOutbound);
            }
        }

        private void HandleTrailLost()
        {
            if (_currentState == SwarmAgentState.FollowingTrailOutbound || _currentState == SwarmAgentState.AtTrailEnd)
            {
                OrderReturnHome();
            }
        }

        private void HandleReturnedToNest()
        {
            _lastExhaustedSegment = null;
            bool broughtFood = _carriedAmount > 0;
            if (broughtFood && _homeNest != null)
            {
                int biomassValue = _resourceConfig != null ? _resourceConfig.GetBiomassValue(_carriedType) : 10;
                _homeNest.DepositResource(_carriedType, _carriedAmount, biomassValue);
                _carriedAmount = 0;
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
                _follower.FollowTrailOutbound();
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

        private void SetState(SwarmAgentState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            OnStateChanged?.Invoke(_currentState);
        }

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
    }
}
