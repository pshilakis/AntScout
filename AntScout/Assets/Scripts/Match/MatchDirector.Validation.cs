using System;
using AntScout.Config;
using UnityEngine;

namespace AntScout.Match
{
    public sealed partial class MatchDirector
    {
        private void ValidateConfiguration()
        {
            if (_preMatchDuration < 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] MatchDirector has invalid PreMatchDuration: {_preMatchDuration}. Must be >= 0.");
            }

            if (_spawnController == null)
            {
                _spawnController = FindFirstObjectByType<SpawnController>();
                if (_spawnController == null)
                {
                    throw new InvalidOperationException(
                        $"[{name}] MatchDirector requires a SpawnController in the scene! " +
                        "Please assign the SpawnController field or ensure a SpawnController GameObject exists.");
                }
            }
        }

        private void OnValidate()
        {
            if (_preMatchDuration < 0f)
            {
                _preMatchDuration = 0f;
            }
        }
    }
}
