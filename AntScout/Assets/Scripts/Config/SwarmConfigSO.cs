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

        [Header("Worker Ant Navigation (A* Pathfinding)")]
        [Tooltip("Movement speed applied to the A* IAstarAI agent.")]
        [Range(1f, 20f)]
        [SerializeField] private float _workerMoveSpeed = 5.5f;

        [Tooltip("Maximum sensory distance for detecting active pheromone nodes.")]
        [Range(1f, 10f)]
        [SerializeField] private float _trailSamplingRadius = 4.0f;

        [Tooltip("Forward distance along the detected pheromone chain to project the next navigation waypoint.")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float _waypointLookAheadDistance = 1.2f;

        [Tooltip("Distance threshold to consider an intermediate waypoint reached.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float _arrivalThreshold = 0.6f;

        [Tooltip("Distance threshold to consider the home nest reached upon returning.")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float _nestArrivalThreshold = 1.2f;

        [Tooltip("Duration in seconds workers wait at the tip of the trail before turning back if no new nodes appear.")]
        [Range(0.5f, 15.0f)]
        [SerializeField] private float _trailEndSearchDuration = 3.0f;

        // Public read-only accessors
        public float NestRecruitmentRadius => _nestRecruitmentRadius;
        public float WorkerSpawnInterval => _workerSpawnInterval;
        public int MaxActiveWorkers => _maxActiveWorkers;

        public float WorkerMoveSpeed => _workerMoveSpeed;
        public float TrailSamplingRadius => _trailSamplingRadius;
        public float WaypointLookAheadDistance => _waypointLookAheadDistance;
        public float ArrivalThreshold => _arrivalThreshold;
        public float NestArrivalThreshold => _nestArrivalThreshold;
        public float TrailEndSearchDuration => _trailEndSearchDuration;

        private void OnValidate()
        {
            if (_nestRecruitmentRadius <= 0.5f) _nestRecruitmentRadius = 0.5f;
            if (_workerSpawnInterval <= 0.1f) _workerSpawnInterval = 0.1f;
            if (_maxActiveWorkers < 1) _maxActiveWorkers = 1;
            if (_workerMoveSpeed <= 0.5f) _workerMoveSpeed = 0.5f;
            if (_trailSamplingRadius <= 0.5f) _trailSamplingRadius = 0.5f;
            if (_waypointLookAheadDistance <= 0.1f) _waypointLookAheadDistance = 0.1f;
            if (_arrivalThreshold <= 0.05f) _arrivalThreshold = 0.05f;
            if (_nestArrivalThreshold <= 0.2f) _nestArrivalThreshold = 0.2f;
            if (_trailEndSearchDuration <= 0.1f) _trailEndSearchDuration = 0.1f;
        }
    }
}
