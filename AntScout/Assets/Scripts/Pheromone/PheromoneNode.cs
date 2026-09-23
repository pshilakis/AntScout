using AntScout.Core.Enums;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Immutable representation of an individual chemical scent node deposited in 3D world space.
    /// </summary>
    [System.Serializable]
    public readonly struct PheromoneNode
    {
        public Vector3 Position { get; }
        public float TimeStamp { get; }
        public PheromoneType Type { get; }

        public PheromoneNode(Vector3 position, float timeStamp, PheromoneType type)
        {
            Position = position;
            TimeStamp = timeStamp;
            Type = type;
        }

        /// <summary>
        /// Calculates the remaining normalized scent intensity (1.0 down to 0.0) based on elapsed time and lifetime.
        /// </summary>
        /// <param name="currentTime">Current time in seconds (Time.time).</param>
        /// <param name="lifetime">Total lifetime duration of a node in seconds.</param>
        /// <returns>Normalized intensity clamped between 0.0 and 1.0.</returns>
        public float GetCurrentIntensity(float currentTime, float lifetime)
        {
            if (lifetime <= 0f) return 0f;
            float elapsed = currentTime - TimeStamp;
            float remainingRatio = 1.0f - (elapsed / lifetime);
            return Mathf.Clamp01(remainingRatio);
        }
    }
}
