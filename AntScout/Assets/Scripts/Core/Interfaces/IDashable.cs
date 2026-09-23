using UnityEngine;

namespace AntScout.Core.Interfaces
{
    /// <summary>
    /// Contract for high-acceleration dash evasion behaviors.
    /// Encapsulates cooldown queries and activation to support UI indicators and decoupled state machines.
    /// </summary>
    public interface IDashable
    {
        /// <summary>
        /// Whether the dash is off cooldown and ready for activation.
        /// </summary>
        bool CanDash { get; }

        /// <summary>
        /// Normalized cooldown progress from 0.0 (cooling down) to 1.0 (fully ready).
        /// </summary>
        float CooldownProgress { get; }

        /// <summary>
        /// Attempts to execute an evasive dash impulse in the specified 3D world direction.
        /// </summary>
        /// <param name="direction">The 3D direction vector of the dash impulse.</param>
        /// <returns>True if the dash was initiated successfully; false if on cooldown or restricted.</returns>
        bool TryDash(Vector3 direction);
    }
}
