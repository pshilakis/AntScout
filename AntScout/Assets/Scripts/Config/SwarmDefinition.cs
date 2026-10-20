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
        [SerializeField] private int _maxActiveWorkers = 40;

        [Tooltip("Seconds between successive soldier ant deployments from the nest.")]
        [Range(0.5f, 10.0f)]
        [SerializeField] private float _soldierSpawnInterval = 4.0f;

        [Tooltip("Maximum concurrent soldier ants deployed in the field.")]
        [Range(0, 50)]
        [SerializeField] private int _maxActiveSoldiers = 12;

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

        [Tooltip("Number of persistent soldier ants deployed at the start of the simulation.")]
        [Range(0, 20)]
        [SerializeField] private int _initialSoldierCount = 2;

        [Tooltip("Biomass cost deducted from the nest to incubate and hatch a new soldier ant.")]
        [Range(10, 300)]
        [SerializeField] private int _soldierBiomassCost = 75;

        [Tooltip("Number of active workers required to justify supporting one soldier ant in peacetime (e.g. 3 = 1 soldier for every 3 workers).")]
        [Range(1, 10)]
        [SerializeField] private int _workersPerSoldierRatio = 3;

        [Tooltip("Enables emergency wartime surge mobilization. When true, active alarm trails or nest attacks cause 100% of biomass to be funneled into hatching soldiers.")]
        [SerializeField] private bool _enableWartimeSurge = true;

        [ColoredHeader("Emergency Survival & Anti-Deadlock", HeaderColor.Coral)]
        [Tooltip("Enables the Queen's Emergency Brood. If all workers die and biomass is insufficient to hatch a replacement, the nest will slowly incubate a free rescue worker to prevent soft-locks.")]
        [SerializeField] private bool _enableEmergencyBrood = true;

        [Tooltip("Seconds required for the Queen to incubate an emergency worker when the colony is bankrupt and workerless.")]
        [Range(5f, 30f)]
        [SerializeField] private float _emergencyWorkerIncubationTime = 12.0f;

        [ColoredHeader("Opening Buildup & Pacing", HeaderColor.Blue)]
        [Tooltip("Duration in seconds of the opening economic buildup phase at match start.")]
        [Range(0f, 180f)]
        [SerializeField] private float _buildupPhaseDuration = 45.0f;

        [Tooltip("Maximum exploration and foraging radius (in meters) from home nest during the opening buildup phase.")]
        [Range(5f, 50f)]
        [SerializeField] private float _buildupForagingRadius = 18.0f;

        [Tooltip("If true, AI heroes in buildup phase will not hunt enemy units unless directly attacked first or their nest perimeter is breached.")]
        [SerializeField] private bool _suppressEarlyHeroAggression = true;

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

        [ColoredHeader("Swarm Defense & Aggression", HeaderColor.Coral)]
        [Tooltip("Sensory radius in meters within which non-player ants passively detect hostile intruders.")]
        [Range(1.0f, 10.0f)]
        [SerializeField] private float _swarmAggroRadius = 2.5f;

        [Tooltip("Maximum pursuit distance in meters from where the fight began before ants abandon the chase and leash back to work.")]
        [Range(3.0f, 20.0f)]
        [SerializeField] private float _swarmLeashRadius = 7.0f;

        [Tooltip("Broadcast radius in meters alerting nearby sister ants in the colony when an ant is attacked.")]
        [Range(1.0f, 15.0f)]
        [SerializeField] private float _swarmAlarmRadius = 4.5f;

        [Tooltip("Seconds of inactivity with no damage dealt or taken before an ant de-escalates and returns to its routine.")]
        [Range(1.0f, 10.0f)]
        [SerializeField] private float _swarmCombatTimeout = 3.0f;

        [Tooltip("Sensory defense perimeter in meters around the nest depot. If an enemy enters this zone or attacks the nest, the entire colony rallies to attack.")]
        [Range(3.0f, 25.0f)]
        [SerializeField] private float _nestDefenseRadius = 8.0f;

        [Tooltip("Multiplier applied to nest healing auras while the base is under attack or recipient hero is in combat (0 = completely halted, 0.2 = 80% reduction).")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _nestUnderAttackHealMultiplier = 0.2f;

        [Tooltip("Seconds the nest remains in the 'Under Attack' state after an intruder leaves the defense perimeter or damage stops.")]
        [Range(1.0f, 15.0f)]
        [SerializeField] private float _nestUnderAttackLingerDuration = 5.0f;

        [Tooltip("Seconds an ant remains considered 'In Combat' after taking hostile damage (throttles passive triage and fountain healing).")]
        [Range(1.0f, 10.0f)]
        [SerializeField] private float _combatCooldownDuration = 4.0f;

        [Tooltip("Portion (0% to 100%) of active soldiers assigned to patrol and defend active recruitment trails. The remainder stay anchored at the home nest.")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _soldierTrailEscortRatio = 0.5f;

        [Tooltip("Duration in seconds that an escorting soldier ant stands defensive guard at the food harvest perimeter before rotating back down the trail.")]
        [Range(1.0f, 30.0f)]
        [SerializeField] private float _soldierHarvestGuardDuration = 6.0f;

        [Tooltip("Lateral distance in meters that escort soldiers maintain from the centerline of the trail to avoid impeding worker lines.")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float _soldierFlankOffset = 1.2f;

        // Public read-only accessors
        public float NestRecruitmentRadius => _nestRecruitmentRadius;
        public float WorkerSpawnInterval => _workerSpawnInterval;
        public int MaxActiveWorkers => _maxActiveWorkers;
        public float SoldierSpawnInterval => _soldierSpawnInterval;
        public int MaxActiveSoldiers => _maxActiveSoldiers;
        public int InitialWorkerCount => _initialWorkerCount;
        public int InitialSoldierCount => _initialSoldierCount;
        public BoundedInt InitialBiomass => _initialBiomass;
        public int WorkerBiomassCost => _workerBiomassCost;
        public int SoldierBiomassCost => _soldierBiomassCost;
        public int WorkersPerSoldierRatio => _workersPerSoldierRatio;
        public bool EnableWartimeSurge => _enableWartimeSurge;
        public float NestWanderRadius => _nestWanderRadius;

        public float SoldierTrailEscortRatio => _soldierTrailEscortRatio;
        public float SoldierHarvestGuardDuration => _soldierHarvestGuardDuration;
        public float SoldierFlankOffset => _soldierFlankOffset;

        public float WorkerMoveSpeed => 5.5f;
        public float SoldierMoveSpeed => 4.0f;
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

        public float SwarmAggroRadius => _swarmAggroRadius;
        public float SwarmLeashRadius => _swarmLeashRadius;
        public float SwarmAlarmRadius => _swarmAlarmRadius;
        public float SwarmCombatTimeout => _swarmCombatTimeout;
        public float NestDefenseRadius => _nestDefenseRadius;
        public float NestUnderAttackHealMultiplier => _nestUnderAttackHealMultiplier;
        public float NestUnderAttackLingerDuration => _nestUnderAttackLingerDuration;
        public float CombatCooldownDuration => _combatCooldownDuration;

        public bool EnableEmergencyBrood => _enableEmergencyBrood;
        public float EmergencyWorkerIncubationTime => _emergencyWorkerIncubationTime;
        public float BuildupPhaseDuration => _buildupPhaseDuration;
        public float BuildupForagingRadius => _buildupForagingRadius;
        public bool SuppressEarlyHeroAggression => _suppressEarlyHeroAggression;
    }
}
