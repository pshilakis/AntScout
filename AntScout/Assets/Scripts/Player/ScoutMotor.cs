using System;
using AntScout.Config;
using AntScout.Core.Interfaces;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Executes 3D top-down locomotion and rotation on the XZ ground plane using Unity 6 Rigidbody physics.
    /// Implements IMotor to adhere to Dependency Inversion (DIP) and Interface Segregation (ISP).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ScoutMotor : MonoBehaviour, IMotor
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for scout movement speed, acceleration, and rotation.")]
        [SerializeField] private ScoutConfigSO _config;

        [Header("Physics Constraints")]
        [Tooltip("Freeze Y position to lock movement to a flat horizontal plane. Uncheck for 3D terrain/slopes.")]
        [SerializeField] private bool _lockToGroundPlane = true;

        private Rigidbody _rigidbody;
        private Vector2 _moveInput;
        private Vector3 _velocityOverride;
        private float _overrideTimer;

        public Vector3 CurrentVelocity => _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
        public bool IsMoving => CurrentVelocity.sqrMagnitude > (_config != null ? _config.IdleThreshold * _config.IdleThreshold : 0.001f);

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutMotor] Missing required ScoutConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ScoutConfigSO in the Inspector.");
            }

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

            RigidbodyConstraints constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            if (_lockToGroundPlane)
            {
                constraints |= RigidbodyConstraints.FreezePositionY;
                _rigidbody.useGravity = false;
            }

            _rigidbody.constraints = constraints;
        }

        public void SetMoveInput(Vector2 inputDirection)
        {
            _moveInput = Vector2.ClampMagnitude(inputDirection, 1.0f);
        }

        /// <summary>
        /// Applies an external velocity override for a specified duration (used by Dash/Scurry or knockbacks).
        /// </summary>
        public void ApplyVelocityOverride(Vector3 overrideVelocity, float duration)
        {
            if (duration <= 0f) return;
            _velocityOverride = overrideVelocity;
            _overrideTimer = duration;
            _rigidbody.linearVelocity = overrideVelocity;
        }

        private void FixedUpdate()
        {
            if (_overrideTimer > 0f)
            {
                _overrideTimer -= Time.fixedDeltaTime;
                _rigidbody.linearVelocity = _velocityOverride;
                UpdateRotation(_velocityOverride);
                return;
            }

            // Map 2D input (X = horizontal, Y = vertical) to 3D world (X = horizontal, Z = forward)
            Vector3 targetVelocity = new Vector3(_moveInput.x, 0f, _moveInput.y) * _config.MoveSpeed;

            Vector3 currentVel = _rigidbody.linearVelocity;
            if (!_lockToGroundPlane)
            {
                // Preserve vertical gravity velocity if navigating 3D slopes
                targetVelocity.y = currentVel.y;
            }

            _rigidbody.linearVelocity = Vector3.MoveTowards(
                currentVel,
                targetVelocity,
                _config.Acceleration * Time.fixedDeltaTime
            );

            if (_moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 worldDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
                UpdateRotation(worldDir);
            }
        }

        private void UpdateRotation(Vector3 direction)
        {
            Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z);
            if (horizontalDir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(horizontalDir.normalized, Vector3.up);
            Quaternion newRotation = Quaternion.RotateTowards(_rigidbody.rotation, targetRotation, _config.TurnSpeed * Time.fixedDeltaTime);
            _rigidbody.MoveRotation(newRotation);
        }
    }
}
