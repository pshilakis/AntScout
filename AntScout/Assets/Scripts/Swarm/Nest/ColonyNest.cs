using System;
using System.Collections.Generic;
using AntScout.Characters;
using AntScout.Config;
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

        [Tooltip("Spawn origin point for deployed ants. Defaults to Nest transform if unassigned.")]
        [SerializeField] private Transform _spawnPoint;

        private readonly List<ISwarmAgent> _activeWorkers = new List<ISwarmAgent>();
        private bool _isTrailConnected;
        private float _spawnTimer;

        [Header("Live Colony Telemetry")]
        [Tooltip("Live current and maximum biomass reserves for the colony nest.")]
        [SerializeField] private BoundedInt _biomass;

        public event Action<bool> OnTrailConnectionChanged;
        public event Action<ISwarmAgent> OnWorkerSpawned;
        public event Action<BoundedInt> OnBiomassChanged;

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
        public BoundedInt Biomass => _biomass;
        public int TotalBiomass => _biomass.Current;

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
            s_nestsByTeam[_colonyTeamId] = this;
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
        }

        private void OnDisable()
        {
            if (s_nestsByTeam.TryGetValue(_colonyTeamId, out ColonyNest registered) && registered == this)
            {
                s_nestsByTeam.Remove(_colonyTeamId);
            }
            PgsTime.Unregister(this);
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
                    RallyNestDefenders(attackable);
                    break;
                }
            }
        }

        /// <summary>
        /// Rallies all active defenders in this colony to converge on and attack a hostile intruder (Red Alert).
        /// </summary>
        public void RallyNestDefenders(IAttackable hostileTarget)
        {
            if (hostileTarget == null || !hostileTarget.IsAttackable) return;

            for (int i = 0; i < _activeWorkers.Count; i++)
            {
                ISwarmAgent agent = _activeWorkers[i];
                if (agent is SwarmAgent swarmAgent)
                {
                    swarmAgent.EngageTarget(hostileTarget);
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
        }

        /// <summary>
        /// Explicitly unregisters an agent from active worker tracking (e.g. upon death or pool recycling).
        /// </summary>
        public void UnregisterWorker(ISwarmAgent agent)
        {
            if (agent != null)
            {
                _activeWorkers.Remove(agent);
            }
        }

        /// <summary>
        /// Broadcasts an alarm signal to all active swarm agents within the specified radius of the origin.
        /// Zero heap allocation (iterates internal active worker list).
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
            // StarCraft-style economy: Nest incubates and hatches new workers by spending harvested biomass
            if (_activeWorkers.Count >= _config.MaxActiveWorkers || _biomass.Current < _config.WorkerBiomassCost)
            {
                _spawnTimer = 0f;
                return;
            }

            _spawnTimer += deltaTime;
            if (_spawnTimer >= _config.WorkerSpawnInterval)
            {
                _spawnTimer = 0f;
                _biomass -= _config.WorkerBiomassCost;
                OnBiomassChanged?.Invoke(_biomass);
                SpawnWorker();
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
