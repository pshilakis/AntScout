using Pathfinding.RVO;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class managing Aron Granberg's A* Pathfinding RVO local avoidance integration.
    /// Grounded in Single Responsibility (SRP): Feeds true kinematic motor velocities to RVO
    /// so swarm worker ants steer around the character without coupling the motor to A*.
    /// </summary>
    public sealed partial class AntCharacter
    {
        private RVOController _rvoController;

        public RVOController RvoController => _rvoController;

        private void BootstrapRvo()
        {
            _rvoController = GetComponent<RVOController>();
            if (_rvoController == null)
            {
                _rvoController = gameObject.AddComponent<RVOController>();
            }

            _rvoController.priority = 1.0f;
            _rvoController.lockWhenNotMoving = false;
            _rvoController.maxNeighbours = 6;
            _rvoController.agentTimeHorizon = 0.75f;
            _rvoController.obstacleTimeHorizon = 0.4f;
        }

        private void UpdateRvoVelocity()
        {
            if (_rvoController != null && _motor != null)
            {
                _rvoController.Move(_motor.Velocity);
            }
        }
    }
}
