using System;
using AntScout.Config;
using AntScout.Swarm.Nest;
using PGS.Core;
using UnityEngine;

namespace AntScout.Maps
{
    /// <summary>
    /// Configuration asset defining a complete playable map environment.
    /// Encapsulates strongly-typed environment geometry prefab, colony nest template, playable extents,
    /// perimeter boundary wall sizing, void kill-plane threshold, and linked resource economy drop tables.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    [CreateAssetMenu(fileName = "MapDefinition_Default", menuName = "AntScout/Maps/Map Definition", order = 1)]
    public partial class MapDefinition : ScriptableObject
    {
        [ColoredHeader("Identity & Environment Prefab", HeaderColor.Teal)]
        [Tooltip("Human-readable name of this arena environment.")]
        [SerializeField] private string _mapName = "Backyard Lawn";

        [Tooltip("Strongly-typed environment prefab root containing terrain geometry and base spawn anchors.")]
        [SerializeField] private MapEnvironment _environmentPrefab;

        [ColoredHeader("Colony Nest Template", HeaderColor.Teal)]
        [Tooltip("Strongly-typed ColonyNest prefab template instantiated at each base spawn location.")]
        [SerializeField] private ColonyNest _nestPrefab;

        [ColoredHeader("Playable Space & Boundaries", HeaderColor.Orange)]
        [Tooltip("Full horizontal X (width) and Z (length) size of the playable arena in meters (e.g. (50, 50) for a 50x50m map).")]
        [SerializeField] private Vector2 _mapSize = new Vector2(50f, 50f);

        [Tooltip("Height of the invisible perimeter boundary walls in meters.")]
        [Range(2.0f, 15.0f)]
        [SerializeField] private float _boundaryWallHeight = 6.0f;

        [Tooltip("Thickness of the invisible perimeter boundary walls in meters.")]
        [Range(0.5f, 5.0f)]
        [SerializeField] private float _boundaryWallThickness = 1.0f;

        [Tooltip("World Y elevation below which any entity is considered out-of-bounds / fallen through the map.")]
        [SerializeField] private float _killPlaneY = -4.0f;

        [ColoredHeader("Resource Economy", HeaderColor.Mint)]
        [Tooltip("Optional linked resource economy drop table and pacing configuration for this environment.")]
        [SerializeField] private MapResourceDefinition _resourceDefinition;

        #region Public Properties

        public string MapName => _mapName;
        public MapEnvironment EnvironmentPrefab => _environmentPrefab;
        public ColonyNest NestPrefab => _nestPrefab;
        public Vector2 MapSize => _mapSize;
        public Vector2 PlayableHalfExtents => _mapSize * 0.5f;
        public float BoundaryWallHeight => _boundaryWallHeight;
        public float BoundaryWallThickness => _boundaryWallThickness;
        public float KillPlaneY => _killPlaneY;
        public MapResourceDefinition ResourceDefinition => _resourceDefinition;

        #endregion
    }
}
