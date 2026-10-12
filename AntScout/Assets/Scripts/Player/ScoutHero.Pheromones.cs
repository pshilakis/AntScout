using System;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Partial class managing the Scout Ant's chemical gland reserves and dynamic pheromone trail emission.
    /// Grounded in Single Responsibility (SRP): Separates chemical emission and scent management from chassis physics.
    /// </summary>
    public sealed partial class ScoutHero
    {
        [Header("Pheromone Visual Prefab")]
        [Tooltip("Prefab containing LineRenderer and PheromoneTrailEmitter. Spawned dynamically in world space.")]
        [SerializeField] private GameObject _pheromoneTrailPrefab;

        private ScoutGland _gland;
        private PheromoneTrailEmitter _trailEmitter;

        public ScoutGland Gland => _gland;
        public PheromoneTrailEmitter TrailEmitter => _trailEmitter;
        public float CurrentGlandCapacity => _gland != null ? _gland.CurrentCapacity : 0f;
        public float NormalizedGlandCapacity => _gland != null ? _gland.NormalizedCapacity : 0f;

        private void BootstrapPheromones()
        {
            // Dynamically ensure ScoutGland component
            _gland = GetComponent<ScoutGland>();
            if (_gland == null)
            {
                _gland = gameObject.AddComponent<ScoutGland>();
            }

            // Spawn or resolve the pheromone trail emitter in world space
            if (_trailEmitter == null)
            {
                _trailEmitter = GetComponentInChildren<PheromoneTrailEmitter>();
                if (_trailEmitter == null && _pheromoneTrailPrefab != null)
                {
                    GameObject trailInstance = Instantiate(_pheromoneTrailPrefab, transform.position, Quaternion.identity);
                    trailInstance.name = "PheromoneTrail_World";
                    _trailEmitter = trailInstance.GetComponent<PheromoneTrailEmitter>();
                }
            }

            if (_trailEmitter != null)
            {
                _gland.Initialize(_config, _trailEmitter);
            }
        }

        /// <summary>
        /// Commands the chemical gland to begin or halt depositing scent nodes along the movement path.
        /// </summary>
        /// <param name="active">True to emit; false to stop.</param>
        /// <param name="type">The chemical scent type (Recruitment, Alarm, Repellent).</param>
        public void SetLayingTrail(bool active, PheromoneType type = PheromoneType.Recruitment)
        {
            if (_gland != null)
            {
                _gland.SetLayingTrail(active, type);
            }
        }
    }
}
