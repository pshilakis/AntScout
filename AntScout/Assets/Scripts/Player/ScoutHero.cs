using System;
using AntScout.Config;
using PGS.Core.Time;
using PGS.Player.Controller;
using PGS.Player.Motor.ThreeD;
using PGS.Player.Traversal;
using PGS.Player.Traversal.Modules;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Aggregate hero coordinator for the Scout Ant caste.
    /// Acts as the dynamic bootstrapper that configures the clean physical chassis (CapsuleCollider,
    /// CharacterCollider3D, KinematicMotor3D) and assembles all caste subsystems at runtime.
    /// 
    /// Split across partial classes for strict Single Responsibility (SRP):
    /// - ScoutHero.cs: Core chassis lifecycle, config validation, locomotion & input bridge bootstrap.
    /// - ScoutHero.Abilities.cs: Dash (Scurry) impulse execution, cooldown timers, ability queries.
    /// - ScoutHero.Pheromones.cs: Chemical gland reserve management & dynamic scent emitter spawning.
    /// - ScoutHero.RVO.cs: Aron Granberg RVOController avoidance bootstrap & kinematic velocity sync.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CharacterCollider3D))]
    [RequireComponent(typeof(KinematicMotor3D))]
    public sealed partial class ScoutHero : MonoBehaviour, IFixedUpdatable, IUpdatable
    {
        [Header("Caste Configuration (Single Source of Truth)")]
        [Tooltip("All stats, abilities, gland capacity, and balance parameters for this ant.")]
        [SerializeField] private ScoutConfigSO _config;

        [Header("Visual Model Reference")]
        [Tooltip("Transform of the child Visual mesh model to rotate and interpolate at 144Hz+.")]
        [SerializeField] private Transform _modelTransform;

        private KinematicMotor3D _motor;
        private CharacterCollider3D _characterCollider;
        private PlayerController _playerController;
        private ScoutPlayerInputBridge _inputBridge;

        #region Public Properties

        public ScoutConfigSO Config => _config;
        public KinematicMotor3D Motor => _motor;
        public CharacterCollider3D CharacterCollider => _characterCollider;
        public PlayerController PlayerController => _playerController;
        public ScoutPlayerInputBridge InputBridge => _inputBridge;

        #endregion

        #region Lifecycle & Initialization

        private void Awake()
        {
            ValidateConfiguration();
            BootstrapChassis();
            BootstrapLocomotion();
            BootstrapInputBridge();
            BootstrapAbilities();
            BootstrapPheromones();
            BootstrapRvo();
        }

        private void OnEnable()
        {
            PgsTime.Register((IFixedUpdatable)this, TimeChannel.Player);
            PgsTime.Register((IUpdatable)this, UpdateRate.Continuous, TimeChannel.Player);
        }

        private void OnDisable()
        {
            PgsTime.Unregister((IFixedUpdatable)this);
            PgsTime.Unregister((IUpdatable)this);
        }

        private void ValidateConfiguration()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Missing required ScoutConfigSO on ScoutHero! " +
                    $"Please assign a valid ScoutConfigSO asset in the Inspector.");
            }
        }

        private void BootstrapChassis()
        {
            _motor = GetComponent<KinematicMotor3D>();
            if (_motor == null)
            {
                throw new InvalidOperationException($"[{name}] ScoutHero requires a KinematicMotor3D component on itself!");
            }

            _characterCollider = GetComponent<CharacterCollider3D>();
            if (_characterCollider == null)
            {
                throw new InvalidOperationException($"[{name}] ScoutHero requires a CharacterCollider3D component on itself!");
            }

            if (_modelTransform == null)
            {
                Transform visualChild = transform.Find("Visual");
                _modelTransform = visualChild != null ? visualChild : transform;
            }
        }

        private void Start()
        {
            ValidateLocomotionModules();
        }

        private void BootstrapLocomotion()
        {
            _playerController = GetComponent<PlayerController>();
            if (_playerController == null)
            {
                throw new InvalidOperationException($"[{name}] ScoutHero requires a PlayerController component on itself!");
            }

            if (_playerController.TraversalDefinition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] PlayerController requires an assigned TraversalDefinition asset!");
            }
        }

        private void ValidateLocomotionModules()
        {
            if (_playerController == null)
            {
                throw new InvalidOperationException($"[{name}] ScoutHero requires a PlayerController component on itself!");
            }

            if (!_playerController.TryGetModule<GroundTraversalModule>(out _))
            {
                throw new InvalidOperationException(
                    $"[{name}] PlayerController is missing a GroundTraversalModule! " +
                    $"Please ensure a TraversalDefinition asset containing a GroundTraversalModule is assigned to PlayerController.");
            }
        }

        private void BootstrapInputBridge()
        {
            _inputBridge = GetComponent<ScoutPlayerInputBridge>();
            if (_inputBridge == null)
            {
                throw new InvalidOperationException($"[{name}] ScoutHero requires a ScoutPlayerInputBridge component on itself!");
            }

            _inputBridge.InitializeBridge(this);
        }

        #endregion

        #region Simulation Ticks

        public void OnFixedUpdate(float fixedDeltaTime, ulong currentTick)
        {
            UpdateRvoVelocity();
        }

        public void OnUpdate(float deltaTime)
        {
            UpdateAbilities(deltaTime);
        }

        #endregion
    }
}
