using System;
using System.Collections.Generic;
using PGS.Core;
using PGS.Loot.Drops;
using PGS.Loot.Pity;
using PGS.Loot.Random;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Map-level resource economy configuration and drop table.
    /// Manages match-start counts, spawn bounds, replenishment cadence, nest exclusion buffers,
    /// and weighted resource selection via PGS.Loot.Drops.LootTable with bad-luck pity protection.
    /// </summary>
    [CreateAssetMenu(fileName = "MapResourceDefinition_Default", menuName = "AntScout/Config/Map Resource Definition", order = 4)]
    public class MapResourceDefinition : ScriptableObject
    {
        [ColoredHeader("Drop Pacing & Concurrency", HeaderColor.Orange)]
        [Tooltip("Number of food bonanzas immediately spawned across neutral territory at match start.")]
        [Range(1, 20)]
        [SerializeField] private int _initialSpawnCount = 4;

        [Tooltip("Minimum number of active food nodes on the map. If active count falls below this, replenishment cadence accelerates.")]
        [Range(1, 10)]
        [SerializeField] private int _minActiveFoodCount = 3;

        [Tooltip("Maximum concurrent active unharvested food sources permitted on the map.")]
        [Range(1, 20)]
        [SerializeField] private int _maxActiveFoodCount = 6;

        [Tooltip("Minimum and Maximum interval in seconds between dynamic replenishment drops during active match play.")]
        [RangeBounds]
        [SerializeField] private FloatRange _spawnInterval = new FloatRange(15.0f, 35.0f);

        [ColoredHeader("Competitive Fairness & Placement", HeaderColor.Teal)]
        [Tooltip("Horizontal X and Z half-extents of the playable yard area where food can drop.")]
        [SerializeField] private Vector2 _spawnAreaHalfExtents = new Vector2(22f, 18f);

        [Tooltip("Hard exclusion radius around any colony nest (in meters). No food will ever spawn within this radius, eliminating RNG doorstep advantages.")]
        [Range(5.0f, 30.0f)]
        [SerializeField] private float _nestExclusionRadius = 12.0f;

        [Tooltip("Minimum spacing distance between food sources (in meters). Prevents crumb clumps.")]
        [Range(2.0f, 15.0f)]
        [SerializeField] private float _minDistanceBetweenFood = 6.0f;

        [Tooltip("Weighting strength (0.0 to 1.0) favoring neutral contested midfield placement equidistant between both colony nests.")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _midfieldContestBias = 0.75f;

        [ColoredHeader("Bad-Luck Protection (Pity)", HeaderColor.Gold)]
        [Tooltip("Pity rules preventing players from experiencing long streaks of consecutive common drops.")]
        [SerializeField] private PityConfig _pityConfig = new PityConfig
        {
            IsEnabled = true,
            SoftPityThreshold = 3,
            HardPityThreshold = 5,
            BaseHighTierRate = 0.30f,
            SoftPityRateIncrease = 0.35f
        };

        [ColoredHeader("Resource Pool", HeaderColor.Mint)]
        [Tooltip("List of physical ResourceDefinitions that can drop on this map. Drop weights and pity tiers are governed by their assigned RarityDefinition.")]
        [SerializeField] private List<ResourceDefinition> _resourcePool = new List<ResourceDefinition>();

        public int InitialSpawnCount => _initialSpawnCount;
        public int MinActiveFoodCount => _minActiveFoodCount;
        public int MaxActiveFoodCount => _maxActiveFoodCount;
        public FloatRange SpawnInterval => _spawnInterval;
        public float MinSpawnInterval => _spawnInterval.Min;
        public float MaxSpawnInterval => _spawnInterval.Max;
        public Vector2 SpawnAreaHalfExtents => _spawnAreaHalfExtents;
        public float NestExclusionRadius => _nestExclusionRadius;
        public float MinDistanceBetweenFood => _minDistanceBetweenFood;
        public float MidfieldContestBias => _midfieldContestBias;
        public PityConfig PityConfig => _pityConfig;
        public IReadOnlyList<ResourceDefinition> ResourcePool => _resourcePool;

        /// <summary>
        /// Builds and returns a new LootTable<ResourceDefinition> configured with pity protection and rarity pools.
        /// </summary>
        public LootTable<ResourceDefinition> CreateLootTable(PityTracker pityTracker)
        {
            if (_resourcePool == null || _resourcePool.Count == 0)
            {
                throw new InvalidOperationException($"[{name}] MapResourceDefinition: ResourcePool is empty! Please assign at least one ResourceDefinition.");
            }

            var table = new LootTable<ResourceDefinition>(pityTracker, tableId: name, displayName: name);

            for (int i = 0; i < _resourcePool.Count; i++)
            {
                var resource = _resourcePool[i];
                if (resource == null) continue;

                if (resource.Rarity == null)
                {
                    throw new InvalidOperationException($"[{name}] Resource '{resource.name}' in pool is missing an assigned RarityDefinition!");
                }

                int weight = resource.DropWeight;
                if (weight <= 0) continue;

                if (resource.Rarity.IsHighTier)
                {
                    table.HighTierTable.Add(resource, weight);
                }
                else
                {
                    table.StandardTable.Add(resource, weight);
                }
            }

            if (table.StandardTable.Count == 0 && table.HighTierTable.Count == 0)
            {
                throw new InvalidOperationException($"[{name}] MapResourceDefinition: No valid resources with weight > 0 found in pool!");
            }

            // Ensure standard table is never empty so standard rolls never fail
            if (table.StandardTable.Count == 0 && table.HighTierTable.Count > 0)
            {
                for (int i = 0; i < table.HighTierTable.Entries.Count; i++)
                {
                    var entry = table.HighTierTable.Entries[i];
                    table.StandardTable.Add(entry.Value, entry.Weight);
                }
            }

            return table;
        }
    }
}
