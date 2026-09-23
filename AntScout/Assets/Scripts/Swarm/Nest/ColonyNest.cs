using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using AntScout.Swarm.Interfaces;
using Pathfinding.RVO;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Represents the home anthill depot. Senses nearby pheromone trails and deploys worker ants.
    /// Strictly adheres to Single Responsibility Principle (SRP) by isolating nest macro state from agent steering.
    /// </summary>
    public class ColonyNest : MonoBehaviour
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

        public event Action<bool> OnTrailConnectionChanged;
        public event Action<ISwarmAgent> OnWorkerSpawned;

        public Vector3 DepotPosition => _spawnPoint != null ? _spawnPoint.position : transform.position;
        public bool IsTrailConnected => _isTrailConnected;
        public int ActiveWorkerCount => _activeWorkers.Count;
        public int MaxWorkers => _config != null ? _config.MaxActiveWorkers : 0;

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

            EnsureRVOSimulatorExists();
        }

        private void EnsureRVOSimulatorExists()
        {
            if (RVOSimulator.active == null && FindFirstObjectByType<RVOSimulator>() == null)
            {
                GameObject rvoObj = new GameObject("RVOSimulator");
                rvoObj.AddComponent<RVOSimulator>();
                Debug.Log("[ColonyNest] Automatically instantiated an RVOSimulator in the scene for ant swarm local avoidance.");
            }
        }

        private void Update()
        {
            CleanupInactiveWorkers();
            EvaluateTrailConnection();
            ProcessDeployment();
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
                Vector3 nestPos = transform.position;
                float radiusSqr = _config.NestRecruitmentRadius * _config.NestRecruitmentRadius;
                IReadOnlyList<PheromoneNode> nodes = _trailEmitter.ActiveNodes;

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Type == PheromoneType.Recruitment)
                    {
                        Vector3 nodePos = nodes[i].Position;
                        // Measure distance on the horizontal XZ plane
                        float dx = nodePos.x - nestPos.x;
                        float dz = nodePos.z - nestPos.z;
                        if ((dx * dx + dz * dz) <= radiusSqr)
                        {
                            _isTrailConnected = true;
                            break;
                        }
                    }
                }
            }

            if (wasConnected != _isTrailConnected)
            {
                OnTrailConnectionChanged?.Invoke(_isTrailConnected);
            }
        }

        private void ProcessDeployment()
        {
            if (!_isTrailConnected || _activeWorkers.Count >= _config.MaxActiveWorkers)
            {
                _spawnTimer = 0f;
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _config.WorkerSpawnInterval)
            {
                _spawnTimer = 0f;
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
        /// Called by a returning worker when it reaches the nest depot.
        /// </summary>
        public void NotifyWorkerReturned(ISwarmAgent agent)
        {
            if (_activeWorkers.Contains(agent))
            {
                _activeWorkers.Remove(agent);
            }

            if (agent.AgentTransform != null)
            {
                Destroy(agent.AgentTransform.gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            float radius = _config != null ? _config.NestRecruitmentRadius : 4.0f;
            Gizmos.color = _isTrailConnected ? new Color(0f, 0.9f, 1f, 0.4f) : new Color(0.6f, 0.6f, 0.6f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
