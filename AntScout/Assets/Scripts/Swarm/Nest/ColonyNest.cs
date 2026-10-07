using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using AntScout.Resources.Enums;
using AntScout.Swarm.Interfaces;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Represents the home anthill depot. Senses nearby pheromone trails and deploys worker ants.
    /// Strictly adheres to Single Responsibility Principle (SRP) by isolating nest macro state from agent steering.
    /// Implements IUpdatable to receive deterministic updates on TimeChannel.World.
    /// </summary>
    public class ColonyNest : MonoBehaviour, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for nest recruitment radius, spawn timers, and population limits.")]
        [SerializeField] private SwarmConfigSO _config;

        [Header("References")]
        [Tooltip("The pheromone trail emitter tracked by the colony.")]
        [SerializeField] private PheromoneTrailEmitter _trailEmitter;

        [Tooltip("Prefab instantiated when deploying worker ants (must contain an ISwarmAgent component).")]
        [SerializeField] private GameObject _workerPrefab;

        [Tooltip("Spawn origin point for deployed ants. Defaults to Nest transform if unassigned.")]
        [SerializeField] private Transform _spawnPoint;

        private readonly List<ISwarmAgent> _activeWorkers = new List<ISwarmAgent>();
        private bool _isTrailConnected;
        private float _spawnTimer;
        private int _totalBiomass;

        public event Action<bool> OnTrailConnectionChanged;
        public event Action<ISwarmAgent> OnWorkerSpawned;
        public event Action<int, int> OnBiomassChanged; // (gainedAmount, newTotal)

        public Vector3 DepotPosition => _spawnPoint != null ? _spawnPoint.position : transform.position;
        public bool IsTrailConnected => _isTrailConnected;
        public int ActiveWorkerCount => _activeWorkers.Count;
        public int MaxWorkers => _config != null ? _config.MaxActiveWorkers : 0;
        public int TotalBiomass => _totalBiomass;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required SwarmConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid SwarmConfigSO in the Inspector.");
            }

            if (_workerPrefab == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required WorkerPrefab on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid prefab in the Inspector.");
            }

            if (_trailEmitter == null)
            {
                _trailEmitter = FindFirstObjectByType<PheromoneTrailEmitter>();
            }

            if (_trailEmitter == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Unable to find a PheromoneTrailEmitter in the scene for GameObject '{gameObject.name}'. " +
                    $"Please assign one in the Inspector.");
            }

            if (_spawnPoint == null)
            {
                _spawnPoint = transform;
            }
        }

        private void Start()
        {
            // Spawn initial StarCraft-style worker squad
            int initialCount = Mathf.Min(_config.InitialWorkerCount, _config.MaxActiveWorkers);
            for (int i = 0; i < initialCount; i++)
            {
                SpawnWorker();
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
            CleanupInactiveWorkers();
            EvaluateTrailConnection();
            ProcessDeployment(deltaTime);
        }

        private void CleanupInactiveWorkers()
        {
            for (int i = _activeWorkers.Count - 1; i >= 0; i--)
            {
                if (_activeWorkers[i] == null || _activeWorkers[i].AgentTransform == null)
                {
                    _activeWorkers.RemoveAt(i);
                }
            }
        }

        private void EvaluateTrailConnection()
        {
            bool wasConnected = _isTrailConnected;
            _isTrailConnected = false;

            if (_trailEmitter != null && _trailEmitter.ActiveNodeCount > 0)
            {
                float radiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                _isTrailConnected = _trailEmitter.HasRecruitmentNear(transform.position, radiusSqr);
            }

            if (wasConnected != _isTrailConnected)
            {
                OnTrailConnectionChanged?.Invoke(_isTrailConnected);
            }
        }

        private void ProcessDeployment(float deltaTime)
        {
            // StarCraft-style economy: Nest incubates and hatches new workers by spending harvested biomass
            if (_activeWorkers.Count >= _config.MaxActiveWorkers || _totalBiomass < _config.WorkerBiomassCost)
            {
                _spawnTimer = 0f;
                return;
            }

            _spawnTimer += deltaTime;
            if (_spawnTimer >= _config.WorkerSpawnInterval)
            {
                _spawnTimer = 0f;
                _totalBiomass -= _config.WorkerBiomassCost;
                OnBiomassChanged?.Invoke(-_config.WorkerBiomassCost, _totalBiomass);
                SpawnWorker();
            }
        }

        private void SpawnWorker()
        {
            GameObject workerObj = Instantiate(_workerPrefab, _spawnPoint.position, _spawnPoint.rotation);
            ISwarmAgent agent = workerObj.GetComponent<ISwarmAgent>();

            if (agent == null)
            {
                Destroy(workerObj);
                throw new InvalidOperationException(
                    $"[ColonyNest] Assigned WorkerPrefab '{_workerPrefab.name}' is missing an ISwarmAgent component.");
            }

            agent.Initialize(this, _trailEmitter);
            _activeWorkers.Add(agent);
            OnWorkerSpawned?.Invoke(agent);
        }

        /// <summary>
        /// Deposits harvested food into the colony's biomass stores.
        /// </summary>
        public void DepositResource(ResourceType type, int amount, int biomassValue)
        {
            int earned = amount * biomassValue;
            _totalBiomass += earned;
            OnBiomassChanged?.Invoke(earned, _totalBiomass);
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
