using System;
using PGS.Player.Traversal.Modules;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class managing kinematic motor settings and traversal module bootstrap.
    /// Grounded in Single Responsibility (SRP): Separates locomotion configuration from combat execution.
    /// </summary>
    public sealed partial class AntCharacter
    {
        private void BootstrapLocomotion()
        {
            if (_definition == null) return;

            // Ingest motor settings definition from character definition
            if (_definition.MotorSettings != null && _motor != null)
            {
                _motor.SetSettings(_definition.MotorSettings);
            }

            // Ingest traversal definition from character definition
            if (_definition.TraversalDefinition != null && _playerController != null)
            {
                _playerController.LoadTraversalDefinition(_definition.TraversalDefinition);
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
