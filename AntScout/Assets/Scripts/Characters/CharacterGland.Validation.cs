using System;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class containing fail-fast configuration and emitter validation
    /// for <see cref="CharacterGland"/> (Single Responsibility Principle).
    /// </summary>
    public partial class CharacterGland
    {
        private void ValidateGlandInitialized()
        {
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] CharacterGland accessed before Initialize() was called with a valid CharacterDefinition.");
            }

            if (PrimaryTrailTemplate == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] CharacterDefinition '{_definition.name}' has no RecruitmentTrailAction or AlarmTrailAction assigned for CharacterGland.");
            }
        }

        private void ValidateSpawnedEmitter()
        {
            if (_emitter == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Instantiated PheromoneTrailPrefab '{_pheromoneTrailPrefab.name}' is missing a PheromoneTrailEmitter component.");
            }
        }

        private void ValidateEmitterResolved()
        {
            if (_emitter == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] CharacterGland could not find a PheromoneTrailEmitter for ColonyTeamId {_colonyTeamId} in the scene, " +
                    $"and no _pheromoneTrailPrefab is assigned to spawn one.");
            }
        }
    }
}
