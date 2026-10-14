using System;

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
        }

        private void ValidateTrailEmitter()
        {
            if (_trailEmitter == null)
            {
                throw new InvalidOperationException(
                    $"[ColonyNest] Unable to find a PheromoneTrailEmitter in the scene for GameObject '{gameObject.name}'. " +
                    $"Please ensure a Scout or PheromoneTrail exists in the scene.");
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
