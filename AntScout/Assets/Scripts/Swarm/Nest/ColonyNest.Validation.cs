using System;
using AntScout.Pheromone;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class containing fail-fast configuration and component validation
    /// for <see cref="ColonyNest"/> (Single Responsibility Principle).
    /// </summary>
    public partial class ColonyNest
    {
        private void ValidateNestConfiguration()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required SwarmConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid SwarmConfigSO in the Inspector.");
            }

            if (_workerPrefab == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required WorkerPrefab on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid prefab in the Inspector.");
            }

            if (_spawnPoint == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required _spawnPoint Transform on GameObject '{gameObject.name}'. " +
                    $"Please create a child GameObject named 'SpawnPoint' positioned on walkable ground outside the nest collider and assign it to the Spawn Point field.");
            }
        }

        private void ValidateTrailEmitter()
        {
            PheromoneTrailEmitter emitter = TrailEmitter;
            if (emitter == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Unable to find a PheromoneTrailEmitter for ColonyTeamId {_colonyTeamId} in the scene for GameObject '{gameObject.name}'. " +
                    $"Please ensure a PheromoneTrailEmitter or Hero with CharacterGland for Colony {_colonyTeamId} exists in the scene, or assign a valid _pheromoneTrailPrefab.");
            }

            if (emitter.ColonyTeamId != _colonyTeamId)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Assigned PheromoneTrailEmitter '{emitter.name}' belongs to ColonyTeamId {emitter.ColonyTeamId}, " +
                    $"which does not match ColonyNest '{gameObject.name}' (ColonyTeamId {_colonyTeamId}).");
            }
        }

        private void ValidateStructureConfigurationAndComponents()
        {
            if (_structureDefinition == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required StructureDefinition reference on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid StructureDefinition asset in the Inspector.");
            }

            _structureDefinition.Validate(name);

            if (_structureCollider == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required Collider component on GameObject '{gameObject.name}'.");
            }

            if (_statsController == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Missing required StatsController component on GameObject '{gameObject.name}'.");
            }
        }
    }
}
