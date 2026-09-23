using System;
using AntScout.Config;
using AntScout.Core.Interfaces;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Executes the Scout Ant's 3D evasive dash (Scurry) mechanic across the XZ plane.
    /// Implements IDashable to isolate dash triggers and cooldown state queries from UI or state machines.
    /// </summary>
    [RequireComponent(typeof(ScoutMotor))]
    public class ScoutDash : MonoBehaviour, IDashable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for dash speed, duration, and cooldown.")]
        [SerializeField] private ScoutConfigSO _config;

        private ScoutMotor _motor;
        private float _cooldownTimer;

        public event Action OnDashTriggered;

        public bool CanDash => _cooldownTimer <= 0f;
        public float CooldownProgress => _config != null && _config.DashCooldown > 0f
            ? Mathf.Clamp01(1.0f - (_cooldownTimer / _config.DashCooldown))
            : 1.0f;

        private void Awake()
        {
            _motor = GetComponent<ScoutMotor>();

            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutDash] Missing required ScoutConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ScoutConfigSO in the Inspector.");
            }

            if (_motor == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutDash] Missing required ScoutMotor component on GameObject '{gameObject.name}'.");
            }
        }

        private void Update()
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }
        }

        public bool TryDash(Vector3 direction)
        {
            if (!CanDash) return false;

            Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z);
            Vector3 dashDirection = horizontalDir.sqrMagnitude > 0.01f
                ? horizontalDir.normalized
                : transform.forward;

            Vector3 impulse = dashDirection * _config.DashSpeed;
            _motor.ApplyVelocityOverride(impulse, _config.DashDuration);
            _cooldownTimer = _config.DashCooldown;

            OnDashTriggered?.Invoke();
            return true;
        }
    }
}
