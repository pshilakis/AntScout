using AntScout.Core.Enums;
using UnityEngine;

namespace AntScout.Pheromone
{
    /// <summary>
    /// Immutable representation of an individual chemical scent node deposited in 3D world space.
    /// Tracks 3D position, drop timestamp, chemical type, and traffic strength/concentration.
    /// </summary>
    [System.Serializable]
    public readonly struct PheromoneNode
    {
        public Vector3 Position { get; }
        public float TimeStamp { get; }
        public float Strength { get; }
        public PheromoneType Type { get; }

        public const float DefaultInitialStrength = 1.0f;
        public const float MaxStrength = 5.0f;

        public PheromoneNode(Vector3 position, float timeStamp, PheromoneType type, float strength = DefaultInitialStrength)
        {
            Position = position;
            TimeStamp = timeStamp;
            Type = type;
            Strength = Mathf.Clamp(strength, 0f, MaxStrength);
        }

        /// <summary>
        /// Returns a copy of this node with an updated timestamp and boosted traffic strength.
        /// </summary>
        public PheromoneNode WithReinforcement(float currentTime, float strengthBoost = 0.5f)
        {
            float newStrength = Mathf.Min(Strength + strengthBoost, MaxStrength);
            return new PheromoneNode(Position, currentTime, Type, newStrength);
        }

        /// <summary>
        /// Returns a copy of this node with migrated 3D position, refreshed timestamp, and boosted traffic strength.
        /// Used by worker stigmergic optimization to gently pull curves toward actual traveled paths.
        /// </summary>
        public PheromoneNode WithAdjustedPosition(Vector3 newPosition, float currentTime, float strengthBoost = 0.5f)
        {
            float newStrength = Mathf.Min(Strength + strengthBoost, MaxStrength);
            return new PheromoneNode(newPosition, currentTime, Type, newStrength);
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
