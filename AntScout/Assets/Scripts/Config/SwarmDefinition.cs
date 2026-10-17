using PGS.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace AntScout.Config
{
    /// <summary>
    /// Centralized configuration asset for Colony Nest spawning and Swarm Agent navigation/sensing.
    /// Eliminates hardcoded values across swarm simulation systems (Single Source of Truth).
    /// </summary>
    [CreateAssetMenu(fileName = "SwarmDefinition", menuName = "AntScout/Definitions/SwarmDefinition", order = 1)]
    public partial class SwarmDefinition : ScriptableObject
    {
        [ColoredHeader("Colony Nest & Spawning", HeaderColor.Cyan)]
        [Tooltip("Sensory perimeter around the nest; recruitment scent nodes inside this radius trigger worker dispatch.")]
        [Range(1f, 15f)]
        [SerializeField] private float _nestRecruitmentRadius = 4.0f;

        [Tooltip("Seconds between successive worker ant deployments from the nest.")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float _workerSpawnInterval = 1.0f;

        [Tooltip("Maximum concurrent worker ants deployed in the field.")]
        [Range(1, 100)]
        [SerializeField] private int _maxActiveWorkers = 20;

        [Tooltip("Radius around the nest depot within which idle workers roam and wander.")]
        [Range(1f, 15f)]
        [SerializeField] private float _nestWanderRadius = 5.0f;

        [ColoredHeader("Biomass Economy", HeaderColor.Green)]
        [Tooltip("Starting and maximum biomass capacity for the colony nest (Current = Starting Biomass, Max = Max Storage Capacity).")]
        [SerializeField] private BoundedInt _initialBiomass = new BoundedInt(100, 500);

        [Tooltip("Number of persistent worker ants deployed at the start of the simulation.")]
        [Range(1, 30)]
        [SerializeField] private int _initialWorkerCount = 6;

        [Tooltip("Biomass cost deducted from the nest to incubate and hatch a new worker ant.")]
        [Range(10, 200)]
        [SerializeField] private int _workerBiomassCost = 50;

        [ColoredHeader("Navigation & Arrival Thresholds", HeaderColor.Coral)]
        [Tooltip("Distance threshold to consider an intermediate waypoint reached.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float _arrivalThreshold = 0.6f;

        [Tooltip("Distance threshold to consider the home nest reached upon returning.")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float _nestArrivalThreshold = 1.2f;

        [ColoredHeader("Pheromone & Food Detection", HeaderColor.Orange)]
        [Tooltip("Maximum sensory distance for detecting active pheromone nodes.")]
        [Range(1f, 25f)]
        [SerializeField] private float _trailSamplingRadius = 12.0f;

        [Tooltip("Sensory radius when arriving at trail end to detect and latch onto nearby food sources.")]
        [Range(1f, 10f)]
        [SerializeField] private float _foodDetectionRadius = 3.5f;

        [Tooltip("Forward distance along the detected pheromone chain to project the next navigation waypoint.")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float _waypointLookAheadDistance = 1.2f;

        [Tooltip("Number of neighboring nodes checked in local window search before falling back to global scan.")]
        [Range(1, 10)]
        [SerializeField] private int _localSearchNodeWindow = 4;

        [ColoredHeader("Highway Optimization (Stigmergy)", HeaderColor.Teal)]
        [Tooltip("Optimization speed (0% to 100%) at which returning food carriers physically relax and straighten curved trails into direct highways (0% = completely static path, 50% = gentle natural straightening, 100% = brisk highway snapping).")]
        [Range(0f, 100f)]
        [FormerlySerializedAs("_trailOptimizationRate")]
        [SerializeField] private float _trailOptimizationPercentage = 50.0f;

        [Tooltip("Forward scent horizon distance along the ribbon. Larger values cause ants to naturally cut corners across curves without any global nest knowledge.")]
        [Min(0.2f)]
        [SerializeField] private float _trailLookaheadDistance = 2.8f;

        [ColoredHeader("Swarm Corridor & Dispersion", HeaderColor.Mint)]
        [Tooltip("Maximum lateral distance (in meters) workers can randomly stray off the center of the trail.")]
        [Min(0.0f)]
        [SerializeField] private float _trailWanderMaxDeviation = 1.8f;

        [Tooltip("Lateral dispersion radius perpendicular to the scent trail; allows ants to swarm organically around the ribbon rather than walking in a single file.")]
        [Min(0.0f)]
        [SerializeField] private float _trailSwarmDispersion = 0.6f;

        [ColoredHeader("Trail Tip Search & Organic Return", HeaderColor.Purple)]
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

        [ColoredHeader("Trail Recruitment & Freshness", HeaderColor.Orange)]
        [Tooltip("Duration in seconds that a newly drawn player trail receives priority recruitment over older established trails.")]
        [Range(5.0f, 120.0f)]
        [SerializeField] private float _newTrailPriorityDuration = 30.0f;

        [Tooltip("Recruitment score multiplier applied to newly drawn trails. Allows fresh player trails to recruit nearby workers over older entrenched paths.")]
        [Range(1.0f, 20.0f)]
        [SerializeField] private float _newTrailPriorityMultiplier = 8.0f;

        [Tooltip("Directness efficiency advantage (0% to 50%) required for an outbound worker to switch mid-stride from its current trail to a straighter parallel trail.")]
        [Range(0.0f, 0.5f)]
        [SerializeField] private float _trailSwitchEfficiencyAdvantage = 0.15f;

        // Public read-only accessors
        public float NestRecruitmentRadius => _nestRecruitmentRadius;
        public float WorkerSpawnInterval => _workerSpawnInterval;
        public int MaxActiveWorkers => _maxActiveWorkers;
        public int InitialWorkerCount => _initialWorkerCount;
        public BoundedInt InitialBiomass => _initialBiomass;
        public int WorkerBiomassCost => _workerBiomassCost;
        public float NestWanderRadius => _nestWanderRadius;

        public float WorkerMoveSpeed => 5.5f;
        public float TrailSamplingRadius => _trailSamplingRadius;
        public float FoodDetectionRadius => _foodDetectionRadius;
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
        public float TrailWanderMaxDeviation => _trailWanderMaxDeviation;
        public float TrailLookaheadDistance => _trailLookaheadDistance;
        /// <summary>
        /// Physical scaling multiplier mapping 50% on the designer slider to the baseline gentle relaxation rate (0.01).
        /// </summary>
        private const float TRAIL_OPTIMIZATION_PHYSICAL_SCALE = 0.02f;

        public float TrailOptimizationPercentage => _trailOptimizationPercentage;
        public float TrailOptimizationNormalized => Mathf.Clamp01((_trailOptimizationPercentage / 100.0f) * TRAIL_OPTIMIZATION_PHYSICAL_SCALE);
        public float TrailOptimizationRate => TrailOptimizationNormalized;
        public float NewTrailPriorityDuration => _newTrailPriorityDuration;
        public float NewTrailPriorityMultiplier => _newTrailPriorityMultiplier;
        public float TrailSwitchEfficiencyAdvantage => _trailSwitchEfficiencyAdvantage;
    }
}
