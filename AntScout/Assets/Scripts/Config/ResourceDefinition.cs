using System;
using AntScout.Resources.Components;
using PGS.Core;
using PGS.Loot.Random;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Definition asset for an individual physical resource type (e.g. Sugar Granule, Fallen Fruit, Bug Carcass).
    /// Analogous to ItemDefinition in com.pgs.loot.
    /// Grounded in Open/Closed Principle (OCP): designers create new physical resources as assets without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceDefinition_NewResource", menuName = "AntScout/Config/Resource Definition", order = 3)]
    public class ResourceDefinition : ScriptableObject
    {
        [ColoredHeader("Resource Identity", HeaderColor.Cyan)]
        [Tooltip("Human-readable name of this physical resource.")]
        [SerializeField] private string _displayName = "Sugar Granule";

        [Tooltip("Rarity tier of this resource, which determines its drop weight and pity tier.")]
        [SerializeField] private RarityDefinition _rarity;

        [Tooltip("FoodSource prefab to instantiate in the world for this resource.")]
        [SerializeField] private FoodSource _prefab;

        [ColoredHeader("Yield & Harvest Rates", HeaderColor.Green)]
        [Tooltip("Min and Max harvestable food units stored in this entity when spawned.")]
        [RangeBounds]
        [SerializeField] private IntRange _capacityRange = new IntRange(15, 25);

        [Tooltip("Time in seconds for a worker ant to harvest 1 unit.")]
        [Range(0.1f, 10.0f)]
        [SerializeField] private float _harvestDuration = 1.0f;

        [Tooltip("Biomass currency granted to the colony per unit delivered to the nest.")]
        [Range(1, 100)]
        [SerializeField] private int _biomassPerUnit = 5;

        [ColoredHeader("Visual Variation", HeaderColor.Orange)]
        [Tooltip("Min and Max scale multiplier applied when spawned for organic size variation.")]
        [SerializeField] private Vector2 _scaleRange = new Vector2(0.8f, 1.2f);

        public string DisplayName => _displayName;
        public RarityDefinition Rarity => _rarity;
        public FoodSource Prefab => _prefab;
        public IntRange CapacityRange => _capacityRange;
        public int MinCapacity => _capacityRange.Min;
        public int MaxCapacity => _capacityRange.Max;
        public float HarvestDuration => _harvestDuration;
        public int BiomassPerUnit => _biomassPerUnit;
        public Vector2 ScaleRange => _scaleRange;

        public int DropWeight => _rarity != null 
            ? _rarity.GetWeight() 
            : throw new InvalidOperationException($"[{name}] ResourceDefinition '{_displayName}' is missing an assigned RarityDefinition!");

        /// <summary>
        /// Rolls an initial capacity of food units within the configured bounded range [_capacityRange.Min, _capacityRange.Max]
        /// using the provided decoupled random provider.
        /// </summary>
        public int RollUnits(IRandomProvider random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            if (_capacityRange.Max < _capacityRange.Min)
            {
                throw new InvalidOperationException(
                    $"[{name}] ResourceDefinition '{_displayName}' has invalid CapacityRange: Max ({_capacityRange.Max}) is less than Min ({_capacityRange.Min}).");
            }

            if (_capacityRange.Min <= 0)
            {
                throw new InvalidOperationException(
                    $"[{name}] ResourceDefinition '{_displayName}' has invalid CapacityRange: Min ({_capacityRange.Min}) must be greater than 0.");
            }

            return random.NextInt(_capacityRange.Min, _capacityRange.Max + 1);
        }

        public void SetRuntimeValues(string displayName, RarityDefinition rarity, FoodSource prefab, IntRange capacityRange, float harvestDuration, int biomassPerUnit, Vector2 scaleRange)
        {
            _displayName = displayName;
            _rarity = rarity;
            _prefab = prefab;
            _capacityRange = capacityRange;
            _harvestDuration = harvestDuration;
            _biomassPerUnit = biomassPerUnit;
            _scaleRange = scaleRange;
        }
    }
}
