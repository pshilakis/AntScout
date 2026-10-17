using System;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class containing fail-fast configuration and component validation
    /// for <see cref="WorkerAntController"/> (Single Responsibility Principle).
    /// </summary>
    public partial class WorkerAntController
    {
        private void ValidateSwarmConfigurationAndComponents()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required SwarmDefinition on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid SwarmDefinition in the Inspector.");
            }

            if (_follower == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required AntTrailFollower on GameObject '{gameObject.name}'.");
            }
        }

        private void ValidateCombatConfigurationAndComponents()
        {
            if (_characterDefinition == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required CharacterDefinition reference on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid CharacterDefinition asset in the Inspector.");
            }

            _characterDefinition.ValidateVitals(name);

            if (_statsController == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required StatsController component on GameObject '{gameObject.name}'.");
            }

            if (_statusReceiver == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required StatusReceiver component on GameObject '{gameObject.name}'.");
            }
        }
    }
}
