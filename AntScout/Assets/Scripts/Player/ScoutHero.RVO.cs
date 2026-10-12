using Pathfinding.RVO;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Partial class managing Aron Granberg's A* Pathfinding RVO local avoidance integration.
    /// Grounded in Single Responsibility (SRP): Feeds true kinematic motor velocities to RVO
    /// so swarm worker ants steer around the player without coupling the collision motor to A*.
    /// </summary>
    public sealed partial class ScoutHero
    {
        private RVOController _rvoController;

        public RVOController RvoController => _rvoController;

        private void BootstrapRvo()
        {
            // Dynamically ensure RVOController exists
            _rvoController = GetComponent<RVOController>();
            if (_rvoController == null)
            {
                _rvoController = gameObject.AddComponent<RVOController>();
            }

            // High priority (1.0) so worker and soldier ants always yield space to the player
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
