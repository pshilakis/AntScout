using UnityEngine;

namespace AntScout.Core.Interfaces
{
    /// <summary>
    /// Contract for 3D entity locomotion and horizontal facing orientation.
    /// Grounded in Interface Segregation Principle (ISP) to decouple input/AI steering from physics execution.
    /// </summary>
    public interface IMotor
    {
        /// <summary>
        /// Supplies the 2D input direction (e.g. WASD or stick input) to be translated into 3D world motion.
        /// </summary>
        /// <param name="inputDirection">2D directional vector (X = Horizontal, Y = Vertical).</param>
        void SetMoveInput(Vector2 inputDirection);

        /// <summary>
        /// The instantaneous 3D world velocity of the entity.
        /// </summary>
        Vector3 CurrentVelocity { get; }

        /// <summary>
        /// True if the entity is actively traveling above an idle velocity threshold.
        /// </summary>
        bool IsMoving { get; }
    }
}
