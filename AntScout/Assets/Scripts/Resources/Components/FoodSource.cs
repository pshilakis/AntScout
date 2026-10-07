using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Resources.Enums;
using AntScout.Resources.Interfaces;
using UnityEngine;

namespace AntScout.Resources.Components
{
    /// <summary>
    /// Represents a harvestable food bonanza (Fruit, Sugar, Carcass) in the backyard world.
    /// Implements IHarvestable to provide loose coupling for swarm workers.
    /// Manages visual depletion and automatically cleans up when exhausted.
    /// </summary>
    public class FoodSource : MonoBehaviour, IHarvestable
    {
        private static readonly List<FoodSource> s_activeSources = new List<FoodSource>();
        public static IReadOnlyList<FoodSource> ActiveSources => s_activeSources;

        [Header("Configuration")]
        [Tooltip("Source of truth for food units and harvesting parameters.")]
        [SerializeField] private ResourceConfigSO _config;

        [Tooltip("Type of food resource represented by this entity.")]
        [SerializeField] private ResourceType _type = ResourceType.SugarGranule;

        [Header("Visual Depletion")]
        [Tooltip("If enabled, the food object visibly shrinks as units are harvested.")]
        [SerializeField] private bool _shrinkOnDeplete = true;

        [Tooltip("Minimum scale factor when nearly depleted.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _minScaleRatio = 0.25f;

        private int _remainingUnits;
        private int _initialUnits;
        private Vector3 _initialScale;

        public event Action<FoodSource> OnDepleted;
        public event Action<int, int> OnUnitsChanged; // (remaining, initial)

        public ResourceType Type => _type;
        public int RemainingUnits => _remainingUnits;
        public bool CanHarvest => _remainingUnits > 0;
        public Vector3 WorldPosition => transform.position;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[FoodSource] Missing required ResourceConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ResourceConfigSO in the Inspector.");
            }

            _initialUnits = _config.GetTotalUnits(_type);
            _remainingUnits = _initialUnits;
            _initialScale = transform.localScale;
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
            if (_remainingUnits <= 0)
            {
                amountHarvested = 0;
                return false;
            }

            amountHarvested = Mathf.Min(amountRequested, _remainingUnits);
            _remainingUnits -= amountHarvested;

            OnUnitsChanged?.Invoke(_remainingUnits, _initialUnits);

            if (_shrinkOnDeplete && _initialUnits > 0)
            {
                float ratio = (float)_remainingUnits / _initialUnits;
                float scaleMultiplier = Mathf.Lerp(_minScaleRatio, 1.0f, ratio);
                transform.localScale = _initialScale * scaleMultiplier;
            }

            if (_remainingUnits <= 0)
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
