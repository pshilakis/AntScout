using System;
using PGS.Player.Traversal.Modules;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class containing fail-fast configuration and component validation
    /// for <see cref="AntCharacter"/> (Single Responsibility Principle).
    /// </summary>
    public sealed partial class AntCharacter
    {
        private void ValidateConfiguration()
        {
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Missing required CharacterDefinition on AntCharacter! " +
                    $"Please assign a valid CharacterDefinition asset in the Inspector.");
            }

            _definition.Validate();
        }

        private void ValidateChassisComponents()
        {
            if (_motor == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a KinematicMotor3D component on itself!");
            }

            if (_characterCollider == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a CharacterCollider3D component on itself!");
            }

            if (_playerController == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a PlayerController component on itself!");
            }

            if (_combatExecutor == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a CombatActionExecutor component on itself!");
            }

            if (_statsController == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a StatsController component on itself!");
            }

            if (_statusReceiver == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a StatusReceiver component on itself!");
            }
        }

        private void ValidateLocomotionModules()
        {
            if (_playerController == null) return;

            if (_playerController.GetModule<GroundTraversalModule>() == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] PlayerController is missing a GroundTraversalModule! " +
                    $"Please ensure TraversalDefinition assigned to CharacterDefinition contains a GroundTraversalModule.");
            }

            if (_playerController.GetModule<AirTraversalModule>() == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] PlayerController is missing an AirTraversalModule! " +
                    $"Please ensure TraversalDefinition assigned to CharacterDefinition contains an AirTraversalModule for gravity/falling.");
            }
        }
    }
}
