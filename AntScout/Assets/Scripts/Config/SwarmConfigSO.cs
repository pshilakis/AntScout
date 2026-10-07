using System;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Centralized configuration asset for Colony Nest spawning and Swarm Agent movement/sensing.
    /// Eliminates hardcoded values across swarm simulation systems (Single Source of Truth).
    /// </summary>
    [CreateAssetMenu(fileName = "SwarmConfig", menuName = "AntScout/Config/SwarmConfig", order = 1)]
    public class SwarmConfigSO : ScriptableObject
    {
        [Header("Colony Nest Parameters")]
        [Tooltip("Sensory perimeter around the nest; recruitment scent nodes inside this radius trigger worker dispatch.")]
        [Range(1f, 15f)]
        [SerializeField] private float _nestRecruitmentRadius = 4.0f;

        [Tooltip("Seconds between successive worker ant deployments from the nest.")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float _workerSpawnInterval = 1.0f;

        [Tooltip("Maximum concurrent worker ants deployed in the field.")]
        [Range(1, 100)]
        [SerializeField] private int _maxActiveWorkers = 20;

        [Header("Colony RTS Economy")]
        [Tooltip("Number of persistent worker ants deployed at the start of the simulation.")]
        [Range(1, 30)]
        [SerializeField] private int _initialWorkerCount = 6;

        [Tooltip("Biomass cost deducted from the nest to incubate and hatch a new worker ant.")]
        [Range(10, 200)]
        [SerializeField] private int _workerBiomassCost = 50;

        [Tooltip("Radius around the nest depot within which idle workers roam and wander.")]
        [Range(1f, 15f)]
        [SerializeField] private float _nestWanderRadius = 5.0f;

        [Header("Worker Ant Navigation (A* Pathfinding)")]
        [Tooltip("Movement speed applied to the A* IAstarAI agent.")]
        [Range(1f, 20f)]
        [SerializeField] private float _workerMoveSpeed = 5.5f;

        [Tooltip("Maximum sensory distance for detecting active pheromone nodes.")]
        [Range(1f, 25f)]
        [SerializeField] private float _trailSamplingRadius = 12.0f;

        [Tooltip("Forward distance along the detected pheromone chain to project the next navigation waypoint.")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float _waypointLookAheadDistance = 1.2f;

        [Tooltip("Distance threshold to consider an intermediate waypoint reached.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float _arrivalThreshold = 0.6f;

        [Tooltip("Distance threshold to consider the home nest reached upon returning.")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float _nestArrivalThreshold = 1.2f;

        [Tooltip("Number of neighboring nodes checked in local window search before falling back to global scan.")]
        [Range(1, 10)]
        [SerializeField] private int _localSearchNodeWindow = 4;

        [Tooltip("Lateral dispersion radius perpendicular to the scent trail; allows ants to swarm organically around the ribbon rather than walking in a single file.")]
        [Range(0.0f, 2.0f)]
        [SerializeField] private float _trailSwarmDispersion = 0.6f;

        [Header("Trail End Area Search & Organic Return")]
        [Tooltip("Radius around the tip of a trail within which workers mill and search for food or extended scent trails.")]
        [Range(1.0f, 20.0f)]
        [SerializeField] private float _trailTipWanderRadius = 6.0f;

        [Tooltip("Duration in seconds workers actively roam the trail end before meandering home.")]
        [Range(1.0f, 20.0f)]
        [SerializeField] private float _trailTipWanderDuration = 6.0f;

        [Tooltip("Exploratory movement speed applied when meandering home empty-handed (prevents rapid beelines).")]
        [Range(0.5f, 10.0f)]
        [SerializeField] private float _workerReturnWanderSpeed = 2.8f;

        [Tooltip("Maximum random angular variation in degrees for biased random walk steps toward the nest.")]
        [Range(5.0f, 60.0f)]
        [SerializeField] private float _meanderJitterAngle = 35.0f;

        // Public read-only accessors
        public float NestRecruitmentRadius => _nestRecruitmentRadius;
        public float WorkerSpawnInterval => _workerSpawnInterval;
        public int MaxActiveWorkers => _maxActiveWorkers;
        public int InitialWorkerCount => _initialWorkerCount;
        public int WorkerBiomassCost => _workerBiomassCost;
        public float NestWanderRadius => _nestWanderRadius;

        public float WorkerMoveSpeed => _workerMoveSpeed;
        public float TrailSamplingRadius => _trailSamplingRadius;
        public float WaypointLookAheadDistance => _waypointLookAheadDistance;
        public float ArrivalThreshold => _arrivalThreshold;
        public float NestArrivalThreshold => _nestArrivalThreshold;
        public float TrailEndSearchDuration => _trailTipWanderDuration;
        public float TrailTipWanderRadius => _trailTipWanderRadius;
        public float TrailTipWanderDuration => _trailTipWanderDuration;
        public float WorkerReturnWanderSpeed => _workerReturnWanderSpeed;
        public float MeanderJitterAngle => _meanderJitterAngle;
        public int LocalSearchNodeWindow => _localSearchNodeWindow;
        public float TrailSwarmDispersion => _trailSwarmDispersion;

        private void OnValidate()
        {
            if (_nestRecruitmentRadius <= 0.5f) _nestRecruitmentRadius = 0.5f;
            if (_workerSpawnInterval <= 0.1f) _workerSpawnInterval = 0.1f;
            if (_maxActiveWorkers < 1) _maxActiveWorkers = 1;
            if (_initialWorkerCount < 1) _initialWorkerCount = 1;
            if (_workerBiomassCost < 1) _workerBiomassCost = 1;
            if (_nestWanderRadius < 0.5f) _nestWanderRadius = 0.5f;
            if (_workerMoveSpeed <= 0.5f) _workerMoveSpeed = 0.5f;
            if (_trailSamplingRadius <= 0.5f) _trailSamplingRadius = 0.5f;
            if (_waypointLookAheadDistance <= 0.1f) _waypointLookAheadDistance = 0.1f;
            if (_arrivalThreshold <= 0.05f) _arrivalThreshold = 0.05f;
            if (_nestArrivalThreshold <= 0.2f) _nestArrivalThreshold = 0.2f;
            if (_trailTipWanderRadius <= 0.5f) _trailTipWanderRadius = 0.5f;
            if (_trailTipWanderDuration <= 0.5f) _trailTipWanderDuration = 0.5f;
            if (_workerReturnWanderSpeed <= 0.5f) _workerReturnWanderSpeed = 0.5f;
            if (_meanderJitterAngle < 5.0f) _meanderJitterAngle = 5.0f;
            if (_localSearchNodeWindow < 1) _localSearchNodeWindow = 1;
            if (_trailSwarmDispersion < 0f) _trailSwarmDispersion = 0f;
        }
    }
}
