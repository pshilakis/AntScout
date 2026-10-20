using System;
using UnityEngine;

namespace AntScout.Maps
{
    public partial class MapManager
    {
        public void ValidateConfiguration()
        {
            if (_activeMapDefinition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapManager: _activeMapDefinition is not assigned! " +
                    "Please assign a MapDefinition asset in the Inspector.");
            }

            _activeMapDefinition.ValidateConfiguration();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_mapRoot == null)
            {
                _mapRoot = transform;
            }

            if (_activeMapDefinition != null && _activeMapDefinition.EnvironmentPrefab != null)
            {
                if (_environmentInstance == null ||
                    (!_environmentInstance.name.StartsWith(_activeMapDefinition.EnvironmentPrefab.name, StringComparison.OrdinalIgnoreCase) &&
                     !_environmentInstance.name.StartsWith(_activeMapDefinition.MapName, StringComparison.OrdinalIgnoreCase)))
                {
                    GameObject existing = GameObject.Find(_activeMapDefinition.EnvironmentPrefab.name);
                    _environmentInstance = existing != null ? existing.GetComponent<MapEnvironment>() : null;
                }
            }
        }
#endif
    }
}
