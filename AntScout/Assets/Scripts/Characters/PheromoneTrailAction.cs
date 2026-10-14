using System;
using AntScout.Core.Enums;
using PGS.Combat.Execution;
using PGS.Combat.Shapes;
using PGS.Core;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Specialized combat action definition for chemical pheromone trail deposition.
    /// Inherits from CombatActionDefinition (Liskov Substitution Principle),
    /// combining circular scent node placement, self-contained duration/drain tuning,
    /// and optional combat/hazard tick payloads.
    /// </summary>
    [CreateAssetMenu(fileName = "Action_Trail_New", menuName = "AntScout/Combat/Pheromone Trail Action")]
    public class PheromoneTrailAction : CombatActionDefinition
    {
        [ColoredHeader("Pheromone Chemistry & Circular Nodes", HeaderColor.Teal)]
        [Tooltip("The chemical scent type deposited along the movement path.")]
        [SerializeField] private PheromoneType _pheromoneType = PheromoneType.Recruitment;

        [Tooltip("Circular radius in meters of each deposited pheromone node / hazard area.")]
        [Min(0.1f)]
        [SerializeField] private float _nodeRadius = 1.25f;

        [Tooltip("Minimum distance in meters between consecutive scent nodes.")]
        [Min(0.05f)]
        [SerializeField] private float _nodePlacementDistance = 0.35f;

        [Tooltip("Lifetime in seconds before a deposited scent node decays.")]
        [Min(0.5f)]
        [SerializeField] private float _nodeLifetime = 12.0f;

        [ColoredHeader("Sustained Duration & Recharge", HeaderColor.Lime)]
        [Tooltip("Maximum chemical reservoir capacity available for this trail action.")]
        [Min(1f)]
        [SerializeField] private float _maxCapacity = 100.0f;

        [Tooltip("Chemical capacity consumed per second while sustaining this trail.")]
        [Min(0.1f)]
        [SerializeField] private float _chemicalCostPerSecond = 20.0f;

        [Tooltip("Chemical capacity recovered per second when not laying a trail.")]
        [Min(0.1f)]
        [SerializeField] private float _rechargeRatePerSecond = 15.0f;

        public PheromoneType PheromoneType => _pheromoneType;
        public float NodeRadius => _nodeRadius;
        public float NodePlacementDistance => _nodePlacementDistance;
        public float NodeLifetime => _nodeLifetime;
        public float MaxCapacity => _maxCapacity;
        public float ChemicalCostPerSecond => _chemicalCostPerSecond;
        public float RechargeRatePerSecond => _rechargeRatePerSecond;

        /// <summary>
        /// Maximum continuous seconds this trail can be sustained from a full reservoir.
        /// </summary>
        public float MaxSustainedDuration => _chemicalCostPerSecond > 0f ? _maxCapacity / _chemicalCostPerSecond : 0f;
    }
}
