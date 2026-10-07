using System;
using AntScout.Config;
using AntScout.Core.Interfaces;
using Pathfinding.RVO;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Executes 3D top-down locomotion and rotation on the XZ ground plane using CharacterController and A* RVOController.
    /// Implements IMotor and IUpdatable to adhere to Dependency Inversion (DIP) and Interface Segregation (ISP).
    /// Broadcasts velocity to Aron Granberg's RVO solver so swarm ants predictively steer clear of the player.
    /// Receives deterministic frame updates via PGS.Core.Time on TimeChannel.Player.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(RVOController))]
    public class ScoutMotor : MonoBehaviour, IMotor, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for scout movement speed, acceleration, and rotation.")]
        [SerializeField] private ScoutConfigSO _config;

        [Header("Plane Constraints")]
        [Tooltip("Freeze Y displacement to lock movement to a flat horizontal plane. Uncheck for 3D terrain slopes.")]
        [SerializeField] private bool _lockToGroundPlane = true;

        private CharacterController _controller;
        private RVOController _rvoController;

        private Vector2 _moveInput;
        private Vector3 _currentVelocity;
        private Vector3 _velocityOverride;
        private float _overrideTimer;

        public Vector3 CurrentVelocity => _currentVelocity;
        public bool IsMoving => _currentVelocity.sqrMagnitude > (_config != null ? _config.IdleThreshold * _config.IdleThreshold : 0.001f);

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _rvoController = GetComponent<RVOController>();

            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutMotor] Missing required ScoutConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ScoutConfigSO in the Inspector.");
            }

            if (_controller == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutMotor] Missing required CharacterController on GameObject '{gameObject.name}'.");
            }

            if (_rvoController == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutMotor] Missing required RVOController on GameObject '{gameObject.name}'.");
            }

            ConfigureRVO();
        }

        private void ConfigureRVO()
        {
            // Give player high priority (1.0) so worker and soldier ants always yield corridor space to the player
            _rvoController.priority = 1.0f;
            _rvoController.lockWhenNotMoving = false;
            _rvoController.maxNeighbours = 6;
            _rvoController.agentTimeHorizon = 0.75f;
            _rvoController.obstacleTimeHorizon = 0.4f;
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
            _currentVelocity = overrideVelocity;
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Continuous, TimeChannel.Player);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
            if (_rvoController != null)
            {
                _rvoController.Move(Vector3.zero);
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_overrideTimer > 0f)
            {
                _overrideTimer -= deltaTime;
                _currentVelocity = _velocityOverride;
            }
            else
            {
                // Map 2D input (X = horizontal, Y = vertical) to 3D world (X = horizontal, Z = forward)
                Vector3 targetVelocity = new Vector3(_moveInput.x, 0f, _moveInput.y) * _config.MoveSpeed;

                _currentVelocity = Vector3.MoveTowards(
                    _currentVelocity,
                    targetVelocity,
                    _config.Acceleration * deltaTime
                );
            }

            if (_lockToGroundPlane)
            {
                _currentVelocity.y = 0f;
            }

            // 1. Move physics controller through static scene obstacles
            if (_controller.enabled)
            {
                _controller.Move(_currentVelocity * deltaTime);
            }

            // 2. Broadcast velocity to RVO simulator so all swarm ants predictively avoid the player
            if (_rvoController != null)
            {
                _rvoController.Move(_currentVelocity);
            }

            // 3. Orient ant facing direction towards movement
            if (_currentVelocity.sqrMagnitude > 0.01f)
            {
                UpdateRotation(_currentVelocity, deltaTime);
            }
        }

        private void UpdateRotation(Vector3 direction, float deltaTime)
        {
            Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z);
            if (horizontalDir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRotation = Quaternion.LookRotation(horizontalDir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _config.TurnSpeed * deltaTime);
        }
    }
}
