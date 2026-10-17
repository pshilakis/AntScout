using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Resources.Interfaces;
using PGS.Core;
using UnityEngine;

namespace AntScout.Resources.Components
{
    /// <summary>
    /// Represents a harvestable food bonanza (Fruit, Sugar, Carcass) in the backyard world.
    /// Implements IHarvestable to provide loose coupling for swarm workers and UI gauges.
    /// Manages visual depletion and automatically cleans up when exhausted.
    /// </summary>
    public partial class FoodSource : MonoBehaviour, IHarvestable
    {
        private static readonly List<FoodSource> s_activeSources = new List<FoodSource>();
        public static IReadOnlyList<FoodSource> ActiveSources => s_activeSources;

        [Header("Visual Depletion")]
        [Tooltip("If enabled, the food object visibly shrinks as units are harvested.")]
        [SerializeField] private bool _shrinkOnDeplete = true;

        [Tooltip("Minimum scale factor when nearly depleted.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _minScaleRatio = 0.25f;

        private ResourceDefinition _definition;
        private BoundedInt _units;
        private Vector3 _initialScale;

        public event Action<FoodSource> OnDepleted;
        public event Action<BoundedInt> OnUnitsChanged;

        public ResourceDefinition Definition => _definition != null 
            ? _definition 
            : throw new InvalidOperationException($"[{name}] FoodSource has not been initialized with a ResourceDefinition! Initialize via Initialize(ResourceDefinition, Vector3, int).");

        public RarityDefinition Rarity => Definition.Rarity;
        public BoundedInt Units => _units;
        public int RemainingUnits => _units.Current;
        public bool CanHarvest => !_units.IsDepleted;
        public float HarvestDuration => Definition.HarvestDuration;
        public int BiomassValue => Definition.BiomassPerUnit;
        public Vector3 WorldPosition => transform.position;

        /// <summary>
        /// Explicitly initializes this food source instance from a ResourceDefinition, scale, and rolled unit capacity.
        /// </summary>
        public void Initialize(ResourceDefinition definition, Vector3 scale, int totalUnits)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (totalUnits <= 0)
            {
                throw new InvalidOperationException(
                    $"[{name}] FoodSource.Initialize: Invalid totalUnits ({totalUnits}) for ResourceDefinition '{definition.name}'. Must be greater than 0.");
            }

            _definition = definition;
            _units = BoundedInt.Full(totalUnits);
            transform.localScale = scale;
            _initialScale = scale;
            OnUnitsChanged?.Invoke(_units);
        }

        private void Awake()
        {
            _initialScale = transform.localScale;
        }

        private void Start()
        {
            ValidateConfiguration();
        }

        private void OnEnable()
        {
            s_activeSources.Add(this);
        }

        private void OnDisable()
        {
            s_activeSources.Remove(this);
        }

        public bool TryHarvest(int amountRequested, out int amountHarvested)
        {
            if (_units.IsDepleted)
            {
                amountHarvested = 0;
                return false;
            }

            amountHarvested = Mathf.Min(amountRequested, _units.Current);
            _units -= amountHarvested;

            OnUnitsChanged?.Invoke(_units);

            if (_shrinkOnDeplete && _units.Max > 0)
            {
                float scaleMultiplier = Mathf.Lerp(_minScaleRatio, 1.0f, _units.Normalized);
                transform.localScale = _initialScale * scaleMultiplier;
            }

            if (_units.IsDepleted)
            {
                OnDepleted?.Invoke(this);
                Destroy(gameObject);
            }

            return true;
        }

        /// <summary>
        /// Finds the closest active food source within the specified sensory radius on the horizontal XZ plane.
        /// </summary>
        public static FoodSource FindClosest(Vector3 position, float maxRadius)
        {
            float maxRadiusSqr = maxRadius * maxRadius;
            FoodSource closest = null;
            float closestDistSqr = float.MaxValue;

            for (int i = 0; i < s_activeSources.Count; i++)
            {
                FoodSource source = s_activeSources[i];
                if (source == null || !source.CanHarvest) continue;

                Vector3 foodPos = source.WorldPosition;
                float dx = foodPos.x - position.x;
                float dz = foodPos.z - position.z;
                float distSqr = dx * dx + dz * dz;

                if (distSqr <= maxRadiusSqr && distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closest = source;
                }
            }

            return closest;
        }
    }
}
