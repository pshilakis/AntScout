using System;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Partial class managing active and passive hero abilities (Dash/Scurry impulse, cooldowns, timers).
    /// Grounded in Single Responsibility (SRP): Keeps ability execution cleanly separated from motor physics and RVO.
    /// </summary>
    public sealed partial class ScoutHero
    {
        private float _dashCooldownTimer;

        public event Action OnDashTriggered;

        public bool CanDash => _dashCooldownTimer <= 0f;

        public float DashCooldownProgress => _config != null && _config.DashCooldown > 0f
            ? Mathf.Clamp01(1f - (_dashCooldownTimer / _config.DashCooldown))
            : 1f;

        private void BootstrapAbilities()
        {
            _dashCooldownTimer = 0f;
        }

        private void UpdateAbilities(float deltaTime)
        {
            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= deltaTime;
            }
        }

        /// <summary>
        /// Executes the Scout Ant's 3D evasive dash (Scurry) across the horizontal plane.
        /// Applies an instantaneous kinematic velocity impulse to KinematicMotor3D.
        /// </summary>
        /// <param name="direction">World-space dash heading. If zero, falls back to motor facing forward.</param>
        /// <returns>True if dash was successfully executed; false if still on cooldown.</returns>
        public bool TryDash(Vector3 direction)
        {
            if (!CanDash || _motor == null) return false;

            Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z);
            Vector3 dashHeading = horizontalDir.sqrMagnitude > 0.01f
                ? horizontalDir.normalized
                : _motor.FacingForward;

            Vector3 impulse = dashHeading * _config.DashSpeed;
            _motor.ApplyImpulse(impulse);
            _dashCooldownTimer = _config.DashCooldown;

            OnDashTriggered?.Invoke();
            return true;
        }
    }
}
