using System;
using AntScout.Resources.Enums;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Centralized configuration asset for Dynamic Food Drops (Bonanzas) and Harvesting parameters.
    /// Eliminates hardcoded balance numbers across the resource economy (Single Source of Truth).
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceConfig", menuName = "AntScout/Config/ResourceConfig", order = 2)]
    public class ResourceConfigSO : ScriptableObject
    {
        [Header("Bonanza Periodic Spawner")]
        [Tooltip("Minimum seconds between dynamic bonanza food drops.")]
        [Range(5f, 120f)]
        [SerializeField] private float _minSpawnInterval = 15.0f;

        [Tooltip("Maximum seconds between dynamic bonanza food drops.")]
        [Range(10f, 300f)]
        [SerializeField] private float _maxSpawnInterval = 35.0f;

        [Tooltip("Horizontal X and Z half-extents of the map area where food can spawn.")]
        [SerializeField] private Vector2 _spawnAreaHalfExtents = new Vector2(20f, 20f);

        [Tooltip("Maximum concurrent active unharvested food drops on the map.")]
        [Range(1, 20)]
        [SerializeField] private int _maxActiveBonanzas = 5;

        [Header("Resource Total Units")]
        [Tooltip("Total harvestable units in a Sugar Granule drop.")]
        [Range(5, 100)]
        [SerializeField] private int _sugarUnits = 20;

        [Tooltip("Total harvestable units in a Fallen Fruit drop.")]
        [Range(20, 500)]
        [SerializeField] private int _fruitUnits = 80;

        [Tooltip("Total harvestable units in a Bug Carcass drop.")]
        [Range(50, 1000)]
        [SerializeField] private int _carcassUnits = 150;

        [Header("Harvesting Timings (Seconds per Chunk)")]
        [Range(0.2f, 10f)]
        [SerializeField] private float _harvestDurationSugar = 1.0f;

        [Range(0.2f, 10f)]
        [SerializeField] private float _harvestDurationFruit = 1.8f;

        [Range(0.2f, 10f)]
        [SerializeField] private float _harvestDurationCarcass = 3.0f;

        [Header("Biomass Value per Chunk")]
        [Range(1, 100)]
        [SerializeField] private int _biomassSugar = 5;

        [Range(1, 100)]
        [SerializeField] private int _biomassFruit = 15;

        [Range(1, 100)]
        [SerializeField] private int _biomassCarcass = 30;

        [Header("Worker Harvesting")]
        [Tooltip("Search radius when arriving at trail end to latch onto nearby food sources.")]
        [Range(1f, 10f)]
        [SerializeField] private float _foodDetectionRadius = 3.5f;

        // Public read-only accessors
        public float MinSpawnInterval => _minSpawnInterval;
        public float MaxSpawnInterval => _maxSpawnInterval;
        public Vector2 SpawnAreaHalfExtents => _spawnAreaHalfExtents;
        public int MaxActiveBonanzas => _maxActiveBonanzas;

        public int SugarUnits => _sugarUnits;
        public int FruitUnits => _fruitUnits;
        public int CarcassUnits => _carcassUnits;

        public float FoodDetectionRadius => _foodDetectionRadius;

        public float GetHarvestDuration(ResourceType type) => type switch
        {
            ResourceType.SugarGranule => _harvestDurationSugar,
            ResourceType.FallenFruit => _harvestDurationFruit,
            ResourceType.BugCarcass => _harvestDurationCarcass,
            _ => 1.5f
        };

        public int GetBiomassValue(ResourceType type) => type switch
        {
            ResourceType.SugarGranule => _biomassSugar,
            ResourceType.FallenFruit => _biomassFruit,
            ResourceType.BugCarcass => _biomassCarcass,
            _ => 10
        };

        public int GetTotalUnits(ResourceType type) => type switch
        {
            ResourceType.SugarGranule => _sugarUnits,
            ResourceType.FallenFruit => _fruitUnits,
            ResourceType.BugCarcass => _carcassUnits,
            _ => 20
        };

        private void OnValidate()
        {
            if (_minSpawnInterval <= 1f) _minSpawnInterval = 1f;
            if (_maxSpawnInterval < _minSpawnInterval) _maxSpawnInterval = _minSpawnInterval + 5f;
            if (_spawnAreaHalfExtents.x <= 1f) _spawnAreaHalfExtents.x = 10f;
            if (_spawnAreaHalfExtents.y <= 1f) _spawnAreaHalfExtents.y = 10f;
            if (_maxActiveBonanzas < 1) _maxActiveBonanzas = 1;
            if (_foodDetectionRadius <= 0.5f) _foodDetectionRadius = 0.5f;
        }
    }
}
