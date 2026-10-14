using System;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Partial class containing fail-fast configuration validation
    /// for <see cref="PheromoneTrailEmitter"/> (Single Responsibility Principle).
    /// </summary>
    public partial class PheromoneTrailEmitter
    {
        private void ValidateEmitterConfiguration()
        {
            if (_nodePlacementDistance <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] PheromoneTrailEmitter requires _nodePlacementDistance > 0.");
            }

            if (_nodeLifetime <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] PheromoneTrailEmitter requires _nodeLifetime > 0.");
            }
        }
    }
}
