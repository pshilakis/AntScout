using System;
using System.Collections.Generic;
using AntScout.Characters;
using AntScout.Config;
using PGS.FogOfWar.Core;
using AntScout.Core.Enums;
using AntScout.Match;
using AntScout.Pheromone;
using AntScout.Swarm.Agents;
using AntScout.Swarm.Interfaces;
using Pathfinding;
using PGS.Combat.Targeting;
using PGS.Core;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Represents the home anthill depot. Senses nearby pheromone trails and deploys worker ants.
    /// Strictly adheres to Single Responsibility Principle (SRP) by isolating nest macro state from agent steering.
    /// Split across partial classes:
    /// - ColonyNest.cs: Macro recruitment sensing, worker deployment, and biomass economy.
    /// - ColonyNest.Structure.cs: Colony team identity (IColonyFactionMember), targetability (IAttackable), and structure vitals (IDamageReceiver, IHealReceiver).
    /// Implements IUpdatable to receive deterministic updates on TimeChannel.World.
    /// </summary>
    public partial class ColonyNest : MonoBehaviour, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for nest recruitment radius, spawn timers, and population limits.")]
        [SerializeField] private SwarmDefinition _config;

        [Header("References")]
        [Tooltip("The pheromone trail emitter tracked by the colony.")]
        [SerializeField] private PheromoneTrailEmitter _trailEmitter;

        [Tooltip("Optional visual trail prefab spawned if no PheromoneTrailEmitter exists for this colony in the scene.")]
        [SerializeField] private GameObject _pheromoneTrailPrefab;

        [Tooltip("Prefab instantiated when deploying worker ants (must contain an ISwarmAgent component).")]
        [SerializeField] private GameObject _workerPrefab;

        [Tooltip("Optional prefab instantiated when deploying soldier ants (must contain an ISwarmAgent component).")]
        [SerializeField] private GameObject _soldierPrefab;

        [Tooltip("Spawn origin point for deployed ants. Defaults to Nest transform if unassigned.")]
        [SerializeField] private Transform _spawnPoint;

        private readonly List<ISwarmAgent> _activeWorkers = new List<ISwarmAgent>();
        private readonly List<ISwarmAgent> _activeSoldiers = new List<ISwarmAgent>();
        private bool _isTrailConnected;
        private float _spawnTimer;
        private float _soldierSpawnTimer;
        private float _emergencyIncubationTimer;

        [Header("Live Colony Telemetry")]
        [Tooltip("Live current and maximum biomass reserves for the colony nest.")]
        [SerializeField] private BoundedInt _biomass;

        public event Action<bool> OnTrailConnectionChanged;
        public event Action<ISwarmAgent> OnWorkerSpawned;
        public event Action<ISwarmAgent> OnSoldierSpawned;
        public event Action<BoundedInt> OnBiomassChanged;

        private float _underAttackExpiryTimestamp = -1000f;
        public bool IsUnderAttack => PgsTime.ElapsedTime < _underAttackExpiryTimestamp ||
                                     (_vitals != null && _config != null && _vitals.IsInCombat(_config.NestUnderAttackLingerDuration));

        public Vector3 SpawnPosition
        {
            get
            {
                if (_spawnPoint == null)
                {
                    throw new InvalidOperationException(
                        $"[{name}] ColonyNest is missing an assigned _spawnPoint! Please assign a dedicated SpawnPoint Transform in the Inspector.");
                }

                Vector3 origin = _spawnPoint.position;
                if (AstarPath.active != null)
                {
                    var nearest = AstarPath.active.GetNearest(origin, NearestNodeConstraint.Walkable);
                    if (nearest.node != null)
                    {
                        return (Vector3)nearest.node.position;
                    }
                }
                return origin;
            }
        }
        public bool IsTrailConnected => _isTrailConnected;
        public int ActiveWorkerCount => _activeWorkers.Count;
        public int MaxWorkers => _config != null ? _config.MaxActiveWorkers : 0;
        public int ActiveSoldierCount => _activeSoldiers.Count;
        public int MaxSoldiers => _config != null ? _config.MaxActiveSoldiers : 0;
        public IReadOnlyList<ISwarmAgent> ActiveWorkers => _activeWorkers;
        public IReadOnlyList<ISwarmAgent> ActiveSoldiers => _activeSoldiers;
        public BoundedInt Biomass => _biomass;
        public int TotalBiomass => _biomass.Current;
        public SwarmDefinition Config => _config;

        public PheromoneTrailEmitter TrailEmitter
        {
            get
            {
                if (_trailEmitter == null || !_trailEmitter.gameObject.scene.IsValid())
                {
                    ResolveTrailEmitter();
                }
                return _trailEmitter;
            }
        }

        private static readonly Dictionary<int, ColonyNest> s_nestsByTeam = new Dictionary<int, ColonyNest>();

        public static IReadOnlyCollection<ColonyNest> ActiveNests => s_nestsByTeam.Values;

        public static bool TryGetForTeam(int teamId, out ColonyNest nest)
        {
            if (s_nestsByTeam.TryGetValue(teamId, out nest) && nest != null)
            {
                return true;
            }
            nest = null;
            return false;
        }

        private void Awake()
        {
            ValidateNestConfiguration();
            if (_config != null)
            {
                _biomass = _config.InitialBiomass;
            }
            ResolveTrailEmitter();
            BootstrapStructure();
        }

        private void Start()
        {
            ResolveTrailEmitter();
            ValidateTrailEmitter();

            // Spawn initial StarCraft-style worker squad
            int initialCount = Mathf.Min(_config.InitialWorkerCount, _config.MaxActiveWorkers);
            for (int i = 0; i < initialCount; i++)
            {
                SpawnWorker();
            }

            // Spawn initial soldier defense squad
            if (_soldierPrefab != null && _config != null)
            {
                int initialSoldiers = Mathf.Min(_config.InitialSoldierCount, _config.MaxActiveSoldiers);
                for (int i = 0; i < initialSoldiers; i++)
                {
                    SpawnSoldier();
                }
            }
        }

        private void ResolveTrailEmitter()
        {
            // If assigned emitter is a prefab asset on disk, convert it to _pheromoneTrailPrefab template
            if (_trailEmitter != null && !_trailEmitter.gameObject.scene.IsValid())
            {
                if (_pheromoneTrailPrefab == null)
                {
                    _pheromoneTrailPrefab = _trailEmitter.gameObject;
                }
                _trailEmitter = null;
            }

            if (_trailEmitter != null && _trailEmitter.gameObject.scene.IsValid() && _trailEmitter.ColonyTeamId == _colonyTeamId)
            {
                return;
            }

            if (PheromoneTrailEmitter.TryGetForColony(_colonyTeamId, out PheromoneTrailEmitter liveEmitter) &&
                liveEmitter != null && liveEmitter.gameObject.scene.IsValid())
            {
                _trailEmitter = liveEmitter;
                return;
            }

            if (_pheromoneTrailPrefab != null)
            {
                GameObject instance = Instantiate(_pheromoneTrailPrefab, transform.position, Quaternion.identity);
                instance.name = $"PheromoneTrail_Colony{_colonyTeamId}";
                _trailEmitter = instance.GetComponent<PheromoneTrailEmitter>();
                if (_trailEmitter != null)
                {
                    _trailEmitter.SetColonyTeam(_colonyTeamId);
                }
            }
        }

        private void OnEnable()
        {
            if (!s_nestsByTeam.TryGetValue(_colonyTeamId, out ColonyNest existing) || existing == null || !existing.IsStructureAlive)
            {
                s_nestsByTeam[_colonyTeamId] = this;
            }
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
            FogOfWarManager.Register(this);
            FogOfWarManager.RegisterCullable(this);
        }

        private void OnDisable()
        {
            if (s_nestsByTeam.TryGetValue(_colonyTeamId, out ColonyNest registered) && registered == this)
            {
                s_nestsByTeam.Remove(_colonyTeamId);
            }
            PgsTime.Unregister(this);
            FogOfWarManager.Unregister(this);
            FogOfWarManager.UnregisterCullable(this);
        }

        private void OnDestroy()
        {
            CleanupStructure();
        }

        public void OnUpdate(float deltaTime)
        {
            if (!IsStructureAlive) return;
            CleanupInactiveWorkers();
            EvaluateTrailConnection();
            ProcessDeployment(deltaTime);
            ProcessAuras(deltaTime);
            EvaluateNestDefensePerimeter();
        }

        private static readonly Collider[] s_defensePerimeterBuffer = new Collider[16];

        private void EvaluateNestDefensePerimeter()
        {
            if (_config == null) return;

            float radius = _config.NestDefenseRadius;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, s_defensePerimeterBuffer, ~0, QueryTriggerInteraction.Collide);
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                Collider col = s_defensePerimeterBuffer[i];
                if (col == null) continue;

                IAttackable attackable = col.GetComponent<IAttackable>() ?? col.GetComponentInParent<IAttackable>();
                if (attackable == null || !attackable.IsAttackable || attackable == (this as IAttackable)) continue;

                IFactionMember faction = (attackable as IFactionMember) ?? col.GetComponentInParent<IFactionMember>();
                if (faction != null && GetAffiliationTowards(faction) == TargetAffiliation.Enemy)
                {
                    _underAttackExpiryTimestamp = PgsTime.ElapsedTime + _config.NestUnderAttackLingerDuration;
                    RallyNestDefenders(attackable);
                    break;
                }
            }
        }

        /// <summary>
        /// Rallies active defenders in this colony to converge on and attack a hostile intruder (Red Alert).
        /// Prioritizes armored soldier caste, followed by idle worker militia physically within the home nest yard.
        /// Workers deployed out on foraging trails remain dedicated to food logistics.
        /// </summary>
        public void RallyNestDefenders(IAttackable hostileTarget)
        {
            if (hostileTarget == null || !hostileTarget.IsAttackable) return;

            for (int i = 0; i < _activeSoldiers.Count; i++)
            {
                ISwarmAgent agent = _activeSoldiers[i];
                if (agent is SwarmAgent swarmAgent)
                {
                    swarmAgent.EngageTarget(hostileTarget);
                }
            }

            float nestRadiusSqr = (_config.NestWanderRadius + 1.0f) * (_config.NestWanderRadius + 1.0f);
            Vector3 nestPos = transform.position;

            for (int i = 0; i < _activeWorkers.Count; i++)
            {
                ISwarmAgent agent = _activeWorkers[i];
                if (agent is SwarmAgent swarmAgent && swarmAgent.AgentTransform != null)
                {
                    Vector3 diff = swarmAgent.AgentTransform.position - nestPos;
                    float distSqr = diff.x * diff.x + diff.z * diff.z;
                    if (distSqr <= nestRadiusSqr && swarmAgent.CurrentState == SwarmAgentState.WanderingAroundNest)
                    {
                        swarmAgent.EngageTarget(hostileTarget);
                    }
                }
            }
        }

        private void CleanupInactiveWorkers()
        {
            for (int i = _activeWorkers.Count - 1; i >= 0; i--)
            {
                if (_activeWorkers[i] == null || 
                    _activeWorkers[i].AgentTransform == null || 
                    !_activeWorkers[i].AgentTransform.gameObject.activeInHierarchy)
                {
                    _activeWorkers.RemoveAt(i);
                }
            }

            for (int i = _activeSoldiers.Count - 1; i >= 0; i--)
            {
                if (_activeSoldiers[i] == null || 
                    _activeSoldiers[i].AgentTransform == null || 
                    !_activeSoldiers[i].AgentTransform.gameObject.activeInHierarchy)
                {
                    _activeSoldiers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Explicitly unregisters an agent from active worker tracking (e.g. upon death or pool recycling).
        /// </summary>
        public void UnregisterWorker(ISwarmAgent agent)
        {
            if (agent != null)
            {
                _activeWorkers.Remove(agent);
                _activeSoldiers.Remove(agent);
            }
        }

        /// <summary>
        /// Explicitly unregisters an agent from active soldier tracking.
        /// </summary>
        public void UnregisterSoldier(ISwarmAgent agent)
        {
            if (agent != null)
            {
                _activeSoldiers.Remove(agent);
            }
        }

        /// <summary>
        /// Broadcasts an alarm signal to all active swarm agents within the specified radius of the origin.
        /// Zero heap allocation (iterates internal active agent lists).
        /// </summary>
        public void BroadcastSwarmAlarm(ISwarmAgent instigatorAgent, IAttackable hostileTarget, float radius)
        {
            if (hostileTarget == null || !hostileTarget.IsAttackable || instigatorAgent == null) return;
            Transform originTrans = instigatorAgent.AgentTransform;
            if (originTrans == null) return;

            Vector3 origin = originTrans.position;
            float radiusSqr = radius * radius;

            for (int i = 0; i < _activeWorkers.Count; i++)
            {
                ISwarmAgent agent = _activeWorkers[i];
                if (agent == null || agent == instigatorAgent) continue;

                Transform t = agent.AgentTransform;
                if (t == null) continue;

                Vector3 diff = t.position - origin;
                float dSqr = diff.x * diff.x + diff.z * diff.z;
                if (dSqr <= radiusSqr)
                {
                    if (agent is SwarmAgent swarmAgent)
                    {
                        swarmAgent.OnAlarmReceived(hostileTarget);
                    }
                }
            }

            // Soldiers have dedicated combat duty: alert soldiers across an expanded radius
            float soldierRadiusSqr = (radius * 1.5f) * (radius * 1.5f);
            for (int i = 0; i < _activeSoldiers.Count; i++)
            {
                ISwarmAgent agent = _activeSoldiers[i];
                if (agent == null || agent == instigatorAgent) continue;

                Transform t = agent.AgentTransform;
                if (t == null) continue;

                Vector3 diff = t.position - origin;
                float dSqr = diff.x * diff.x + diff.z * diff.z;
                if (dSqr <= soldierRadiusSqr)
                {
                    if (agent is SwarmAgent swarmAgent)
                    {
                        swarmAgent.OnAlarmReceived(hostileTarget);
                    }
                }
            }
        }

        private void EvaluateTrailConnection()
        {
            bool wasConnected = _isTrailConnected;
            _isTrailConnected = false;

            PheromoneTrailEmitter emitter = TrailEmitter;
            if (emitter != null && emitter.ActiveNodeCount > 0)
            {
                float radiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                _isTrailConnected = emitter.HasRecruitmentNear(transform.position, radiusSqr) ||
                                    (_spawnPoint != null && emitter.HasRecruitmentNear(_spawnPoint.position, radiusSqr));
            }

            if (wasConnected != _isTrailConnected)
            {
                OnTrailConnectionChanged?.Invoke(_isTrailConnected);
            }
        }

        private void ProcessDeployment(float deltaTime)
        {
            if (_config == null) return;

            // 0. Queen's Emergency Brood (Anti-Softlock Failsafe)
            // If active workers are wiped out and biomass is insufficient to hatch a replacement, incubate a rescue worker.
            if (_activeWorkers.Count == 0 && _biomass.Current < _config.WorkerBiomassCost && _config.EnableEmergencyBrood && _workerPrefab != null)
            {
                _emergencyIncubationTimer += deltaTime;
                if (_emergencyIncubationTimer >= _config.EmergencyWorkerIncubationTime)
                {
                    _emergencyIncubationTimer = 0f;
                    SpawnWorker();
                    Debug.Log($"[{name}] The Queen has completed emergency incubation of a rescue worker ant!");
                }
            }
            else
            {
                _emergencyIncubationTimer = 0f;
            }

            // 1. Evaluate Wartime Surge (Red Alert)
            bool isWartimeSurge = false;
            if (_config.EnableWartimeSurge)
            {
                PheromoneTrailEmitter emitter = TrailEmitter;
                float alertRadiusSqr = (_config.NestDefenseRadius + 4f) * (_config.NestDefenseRadius + 4f);
                if (emitter != null && emitter.HasAlarmNear(transform.position, alertRadiusSqr))
                {
                    isWartimeSurge = true;
                }

                if (!isWartimeSurge)
                {
                    int intruderCount = Physics.OverlapSphereNonAlloc(transform.position, _config.NestDefenseRadius, s_defensePerimeterBuffer, ~0, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < intruderCount; i++)
                    {
                        Collider col = s_defensePerimeterBuffer[i];
                        if (col == null) continue;
                        IAttackable atk = col.GetComponent<IAttackable>() ?? col.GetComponentInParent<IAttackable>();
                        if (atk != null && atk.IsAttackable && atk != (this as IAttackable))
                        {
                            IFactionMember faction = (atk as IFactionMember) ?? col.GetComponentInParent<IFactionMember>();
                            if (faction != null && GetAffiliationTowards(faction) == TargetAffiliation.Enemy)
                            {
                                isWartimeSurge = true;
                                break;
                            }
                        }
                    }
                }
            }

            // 2. Proportional Ratio Calculation (Golden Ratio: e.g. 1 soldier per 3 workers)
            // Grounded in Economic Supremacy (StarCraft/Age of Empires): If 0 workers exist, target soldiers MUST be 0!
            int ratio = Mathf.Max(1, _config.WorkersPerSoldierRatio);
            int targetSoldiers = _activeWorkers.Count == 0
                ? 0
                : Mathf.Min(Mathf.Max(1, _activeWorkers.Count / ratio), _config.MaxActiveSoldiers);

            bool isSoldierDeficit = _activeSoldiers.Count < targetSoldiers;
            bool canHatchSoldier = _soldierPrefab != null && _activeSoldiers.Count < _config.MaxActiveSoldiers;
            bool canHatchWorker = _workerPrefab != null && _activeWorkers.Count < _config.MaxActiveWorkers;

            // 3. Strategic Caste Prioritization:
            // - Economy First: If workers are zero, NEVER prioritize soldiers over workers!
            // - Anti-Stall Fallback: If soldiers are prioritized but biomass is insufficient for a soldier (< 75)
            //   while sufficient for a worker (>= 50), do NOT freeze production—hatch a worker!
            bool prioritizeSoldier = false;

            if (_activeWorkers.Count > 0 && canHatchSoldier && (isWartimeSurge || isSoldierDeficit || !canHatchWorker))
            {
                // Only lock into soldier production if we can actually afford one, OR if we can't afford a worker either.
                if (_biomass.Current >= _config.SoldierBiomassCost || _biomass.Current < _config.WorkerBiomassCost || !canHatchWorker)
                {
                    prioritizeSoldier = true;
                }
            }

            if (prioritizeSoldier)
            {
                _spawnTimer = 0f;

                if (_biomass.Current >= _config.SoldierBiomassCost)
                {
                    _soldierSpawnTimer += deltaTime;
                    if (_soldierSpawnTimer >= _config.SoldierSpawnInterval)
                    {
                        _soldierSpawnTimer = 0f;
                        _biomass -= _config.SoldierBiomassCost;
                        OnBiomassChanged?.Invoke(_biomass);
                        SpawnSoldier();
                    }
                }
                else
                {
                    _soldierSpawnTimer = 0f;
                }
            }
            else if (canHatchWorker)
            {
                _soldierSpawnTimer = 0f;

                if (_biomass.Current >= _config.WorkerBiomassCost)
                {
                    _spawnTimer += deltaTime;
                    if (_spawnTimer >= _config.WorkerSpawnInterval)
                    {
                        _spawnTimer = 0f;
                        _biomass -= _config.WorkerBiomassCost;
                        OnBiomassChanged?.Invoke(_biomass);
                        SpawnWorker();
                    }
                }
                else
                {
                    _spawnTimer = 0f;
                }
            }
            else
            {
                _spawnTimer = 0f;
                _soldierSpawnTimer = 0f;
            }
        }

        private void SpawnWorker()
        {
            GameObject workerObj = SpawnPool.Spawn(_workerPrefab, _spawnPoint.position, _spawnPoint.rotation);
            ISwarmAgent agent = workerObj.GetComponent<ISwarmAgent>();

            if (agent == null)
            {
                SpawnPool.Despawn(workerObj);
                throw new InvalidOperationException(
                    $"[ColonyNest] Assigned WorkerPrefab '{_workerPrefab.name}' is missing an ISwarmAgent component.");
            }

            agent.Initialize(this, TrailEmitter);
            _activeWorkers.Add(agent);
            OnWorkerSpawned?.Invoke(agent);
        }

        private void SpawnSoldier()
        {
            if (_soldierPrefab == null) return;

            GameObject soldierObj = SpawnPool.Spawn(_soldierPrefab, _spawnPoint.position, _spawnPoint.rotation);
            ISwarmAgent agent = soldierObj.GetComponent<ISwarmAgent>();

            if (agent == null)
            {
                SpawnPool.Despawn(soldierObj);
                throw new InvalidOperationException(
                    $"[ColonyNest] Assigned SoldierPrefab '{_soldierPrefab.name}' is missing an ISwarmAgent component.");
            }

            agent.Initialize(this, TrailEmitter);
            _activeSoldiers.Add(agent);
            OnSoldierSpawned?.Invoke(agent);
        }

        /// <summary>
        /// Deposits harvested food into the colony's biomass stores.
        /// </summary>
        public void DepositResource(ResourceDefinition resource, int amount)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            int earned = amount * resource.BiomassPerUnit;
            _biomass += earned;
            OnBiomassChanged?.Invoke(_biomass);
        }

        /// <summary>
        /// Attempts to spend the specified biomass cost from the colony's reserves.
        /// Returns true if successful; false if insufficient biomass.
        /// </summary>
        public bool TrySpendBiomass(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), "Biomass cost cannot be negative.");
            if (_biomass.Current < cost) return false;

            _biomass -= cost;
            OnBiomassChanged?.Invoke(_biomass);
            return true;
        }

        /// <summary>
        /// Called by a returning worker when it reaches the nest depot.
        /// Workers are persistent RTS units and are NOT destroyed upon return.
        /// </summary>
        public void NotifyWorkerReturned(ISwarmAgent agent)
        {
            // Notification hook for colony audio, UI, or telemetry.
            // Worker is maintained in _activeWorkers and transitions independently.
        }

        private void OnDrawGizmosSelected()
        {
            float radius = _config != null ? _config.NestRecruitmentRadius : 4.0f;
            Gizmos.color = _isTrailConnected ? new Color(0f, 0.9f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
