using System;
using UnityEngine;

namespace AntScout.FogOfWar.Config
{
    /// <summary>
    /// ScriptableObject defining world-space boundaries, grid fidelity, vision radii, and visual presentation parameters.
    /// Single Source of Truth for the Fog of War system.
    /// </summary>
    [CreateAssetMenu(fileName = "FogOfWarDefinition", menuName = "AntScout/Fog of War/Definition", order = 10)]
    public class FogOfWarDefinition : ScriptableObject
    {
        [Header("World Bounding Bounds (X, Z)")]
        [Tooltip("Minimum world coordinate (X, Z) covered by the Fog grid. E.g. (-30, -30) for a 50m map.")]
        [SerializeField] private Vector2 _mapMin = new Vector2(-30f, -30f);

        [Tooltip("Maximum world coordinate (X, Z) covered by the Fog grid. E.g. (30, 30) for a 50m map.")]
        [SerializeField] private Vector2 _mapMax = new Vector2(30f, 30f);

        [Header("Grid & Texture Fidelity")]
        [Tooltip("Resolution of the 2D vision grid and baked texture (typically 128 or 256).")]
        [Range(64, 512)]
        [SerializeField] private int _resolution = 256;

        [Header("Default Caste Vision Radii (Meters)")]
        [Tooltip("Forward scouting vision radius for Hero Ants (Scout, Vanguard).")]
        [SerializeField] private float _heroVisionRadius = 12.0f;

        [Tooltip("Stationary territory vision radius for the Colony Nest structure.")]
        [SerializeField] private float _nestVisionRadius = 15.0f;

        [Tooltip("Line-of-sight vision radius for Worker Ants.")]
        [SerializeField] private float _workerVisionRadius = 5.0f;

        [Header("Visual Presentation")]
        [Tooltip("Color applied to completely unexplored territory (pitch black).")]
        [SerializeField] private Color _unexploredColor = Color.black;

        [Tooltip("Color tint applied to explored territory in fog (dark grey / desaturated tint).")]
        [SerializeField] private Color _exploredColor = new Color(0.08f, 0.08f, 0.12f, 0.65f);

        [Tooltip("Feathering softness applied at the transition boundary of vision circles.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _edgeFeather = 0.2f;

        public Vector2 MapMin => _mapMin;
        public Vector2 MapMax => _mapMax;
        public int Resolution => _resolution;
        public float HeroVisionRadius => _heroVisionRadius;
        public float NestVisionRadius => _nestVisionRadius;
        public float WorkerVisionRadius => _workerVisionRadius;
        public Color UnexploredColor => _unexploredColor;
        public Color ExploredColor => _exploredColor;
        public float EdgeFeather => _edgeFeather;

        public float WorldWidth => _mapMax.x - _mapMin.x;
        public float WorldHeight => _mapMax.y - _mapMin.y;

        public void Validate(string contextName)
        {
            if (_resolution < 64)
            {
                throw new InvalidOperationException($"[{contextName}] FogOfWarDefinition resolution must be at least 64 (configured: {_resolution}).");
            }
            if (_mapMax.x <= _mapMin.x || _mapMax.y <= _mapMin.y)
            {
                throw new InvalidOperationException($"[{contextName}] FogOfWarDefinition invalid map bounds: Min ({_mapMin.x}, {_mapMin.y}) >= Max ({_mapMax.x}, {_mapMax.y}).");
            }
            if (_heroVisionRadius <= 0f || _nestVisionRadius <= 0f || _workerVisionRadius <= 0f)
            {
                throw new InvalidOperationException($"[{contextName}] FogOfWarDefinition vision radii must be strictly positive.");
            }
        }
    }
}
