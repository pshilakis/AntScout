using System;
using UnityEngine;

namespace AntScout.Config
{
    /// <summary>
    /// Centralized configuration asset for Scout Ant locomotion, evasion, and chemical gland properties.
    /// Eliminates hardcoded balance constants across all scout systems (Single Source of Truth).
    /// </summary>
    [CreateAssetMenu(fileName = "ScoutConfig", menuName = "AntScout/Config/ScoutConfig", order = 0)]
    public class ScoutConfigSO : ScriptableObject
    {
        [Header("Locomotion (Top-Down 3D / XZ Ground)")]
        [Tooltip("Maximum movement speed in units per second.")]
        [Range(1f, 25f)]
        [SerializeField] private float _moveSpeed = 8.0f;

        [Tooltip("Acceleration responsiveness when changing direction or starting from rest.")]
        [Range(5f, 100f)]
        [SerializeField] private float _acceleration = 40.0f;

        [Tooltip("Angular turning rate in degrees per second.")]
        [Range(180f, 2160f)]
        [SerializeField] private float _turnSpeed = 1080.0f;

        [Tooltip("Threshold velocity below which the ant is considered idle.")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float _idleThreshold = 0.05f;

        [Header("Dash (Scurry)")]
        [Tooltip("Instantaneous speed multiplier magnitude during dash burst.")]
        [Range(10f, 50f)]
        [SerializeField] private float _dashSpeed = 22.0f;

        [Tooltip("Duration of the active dash burst in seconds.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _dashDuration = 0.18f;

        [Tooltip("Cooldown period in seconds between consecutive dashes.")]
        [Range(0.2f, 5.0f)]
        [SerializeField] private float _dashCooldown = 1.25f;

        [Header("Pheromone Gland")]
        [Tooltip("Maximum capacity of chemical pheromone stored in the scout abdomen.")]
        [Range(10f, 200f)]
        [SerializeField] private float _maxGlandCapacity = 100.0f;

        [Tooltip("Chemical capacity consumed per second while continuously laying trails.")]
        [Range(1f, 50f)]
        [SerializeField] private float _glandDrainRate = 20.0f;

        [Tooltip("Chemical capacity recovered per second when gland is idle.")]
        [Range(1f, 50f)]
        [SerializeField] private float _glandRechargeRate = 15.0f;

        [Tooltip("Minimum distance in units the scout must travel before dropping the next scent node.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] private float _nodePlacementDistance = 0.35f;

        [Tooltip("Duration in seconds before an active pheromone node completely decays and expires.")]
        [Range(1f, 60f)]
        [SerializeField] private float _nodeLifetime = 12.0f;

        // Public read-only accessors
        public float MoveSpeed => _moveSpeed;
        public float Acceleration => _acceleration;
        public float TurnSpeed => _turnSpeed;
        public float IdleThreshold => _idleThreshold;

        public float DashSpeed => _dashSpeed;
        public float DashDuration => _dashDuration;
        public float DashCooldown => _dashCooldown;

        public float MaxGlandCapacity => _maxGlandCapacity;
        public float GlandDrainRate => _glandDrainRate;
        public float GlandRechargeRate => _glandRechargeRate;
        public float NodePlacementDistance => _nodePlacementDistance;
        public float NodeLifetime => _nodeLifetime;

        private void OnValidate()
        {
            if (_moveSpeed <= 0f) _moveSpeed = 1f;
            if (_acceleration <= 0f) _acceleration = 1f;
            if (_turnSpeed <= 0f) _turnSpeed = 180f;
            if (_dashSpeed <= 0f) _dashSpeed = 5f;
            if (_dashDuration <= 0f) _dashDuration = 0.05f;
            if (_dashCooldown <= 0f) _dashCooldown = 0.1f;
            if (_maxGlandCapacity <= 0f) _maxGlandCapacity = 10f;
            if (_glandDrainRate <= 0f) _glandDrainRate = 1f;
            if (_glandRechargeRate <= 0f) _glandRechargeRate = 1f;
            if (_nodePlacementDistance <= 0.05f) _nodePlacementDistance = 0.05f;
            if (_nodeLifetime <= 0.5f) _nodeLifetime = 0.5f;
        }
    }
}
