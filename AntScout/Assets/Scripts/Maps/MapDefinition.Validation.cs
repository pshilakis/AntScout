using System;
using UnityEngine;

namespace AntScout.Maps
{
    public partial class MapDefinition
    {
        public void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_mapName))
            {
                throw new InvalidOperationException($"[{name}] MapDefinition: _mapName cannot be null or empty!");
            }

            if (_mapSize.x <= 0f || _mapSize.y <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapDefinition: _mapSize ({_mapSize.x}, {_mapSize.y}) must be strictly positive!");
            }

            if (_boundaryWallHeight <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapDefinition: _boundaryWallHeight ({_boundaryWallHeight}) must be strictly positive!");
            }

            if (_boundaryWallThickness <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapDefinition: _boundaryWallThickness ({_boundaryWallThickness}) must be strictly positive!");
            }

            if (_nestPrefab == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapDefinition: _nestPrefab is not assigned! Please assign a ColonyNest prefab in the Inspector.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_mapSize.x < 1f) _mapSize.x = 1f;
            if (_mapSize.y < 1f) _mapSize.y = 1f;
            if (_boundaryWallHeight < 1f) _boundaryWallHeight = 1f;
            if (_boundaryWallThickness < 0.2f) _boundaryWallThickness = 0.2f;
        }
#endif
    }
}
