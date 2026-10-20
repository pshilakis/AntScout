using System;
using System.Collections.Generic;
using PGS.Core;
using UnityEngine;

namespace AntScout.Maps
{
    /// <summary>
    /// Root identifier component attached to map environment prefabs.
    /// Exposes strongly-typed spatial layout data such as base spawn points, eliminating
    /// untyped GameObject references, scene searches, and GetComponent lookups.
    /// Strictly adheres to Single Responsibility (SRP) and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    public class MapEnvironment : MonoBehaviour
    {
        [ColoredHeader("Start Locations", HeaderColor.Mint)]
        [Tooltip("Authoritative base spawn locations defined on this map.")]
        [SerializeField] private MapBaseSpawnPoint[] _spawnPoints;

        public IReadOnlyList<MapBaseSpawnPoint> SpawnPoints => _spawnPoints;

        /// <summary>
        /// Validates that this map environment provides valid, complete start location anchors.
        /// Adheres strictly to the Fail-Fast standard.
        /// </summary>
        public void ValidateConfiguration()
        {
            if (_spawnPoints == null || _spawnPoints.Length < 2)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapEnvironment requires at least 2 assigned MapBaseSpawnPoints! " +
                    "Please ensure child spawn markers (e.g. Spawn1, Spawn2) have a MapBaseSpawnPoint component.");
            }

            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] == null)
                {
                    throw new InvalidOperationException(
                        $"[{name}] MapEnvironment has an unassigned or missing MapBaseSpawnPoint at index {i}!");
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Auto-wire child spawn points in the Editor if not already assigned
            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                _spawnPoints = GetComponentsInChildren<MapBaseSpawnPoint>(true);
            }
        }
#endif
    }
}
