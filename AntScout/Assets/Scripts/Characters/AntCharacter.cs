using System;
using AntScout.Characters;
using PGS.Camera.Interfaces;
using PGS.Combat.Execution;
using PGS.Core.Time;
using PGS.Player.Controller;
using PGS.Player.Motor.ThreeD;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Universal aggregate coordinator for ant characters (Player Heroes and AI entities).
    /// Acts as the chassis coordinator configuring kinematic motor physics, traversal modules,
    /// combat action execution (com.pgs.combat), camera target framing (com.pgs.camera), and local crowd avoidance.
    /// 
    /// Split across partial classes for strict Single Responsibility (SRP):
    /// - AntCharacter.cs: Core lifecycle, definition validation, component references, ICameraTarget.
    /// - AntCharacter.Combat.cs: IMovementReceiver, IAttackable, IFactionMember for com.pgs.combat.
    /// - AntCharacter.Locomotion.cs: KinematicMotor3D & PlayerController traversal module setup.
    /// - AntCharacter.RVO.cs: Aron Granberg RVOController avoidance synchronization.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CharacterCollider3D))]
    [RequireComponent(typeof(KinematicMotor3D))]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(CombatActionExecutor))]
    public sealed partial class AntCharacter : MonoBehaviour, IFixedUpdatable, IUpdatable, ICameraTarget
    {
        [Header("Character Definition (Single Source of Truth)")]
        [Tooltip("Universal character definition holding locomotion settings, traversal modules, and combat action kit.")]
        [SerializeField] private CharacterDefinition _definition;

        [Header("Visual Model Reference")]
        [Tooltip("Transform of the child visual model to rotate and interpolate at 144Hz+.")]
        [SerializeField] private Transform _modelTransform;

        private KinematicMotor3D _motor;
        private CharacterCollider3D _characterCollider;
        private PlayerController _playerController;
        private CombatActionExecutor _combatExecutor;
        private CharacterGland _gland;
        private bool _isAlive = true;

        #region Public Properties

        public CharacterDefinition Definition => _definition;
        public KinematicMotor3D Motor => _motor;
        public CharacterCollider3D CharacterCollider => _characterCollider;
        public PlayerController PlayerController => _playerController;
        public CombatActionExecutor CombatExecutor => _combatExecutor;
        public CharacterGland Gland => _gland;
        public bool IsAlive => _isAlive;

        #endregion

        #region ICameraTarget Implementation (com.pgs.camera)

        public Transform TargetTransform => _modelTransform != null ? _modelTransform : transform;

        public Vector3 Position
        {
            get
            {
                Vector3 basePos = _modelTransform != null ? _modelTransform.position : transform.position;
                float eyeOffset = _characterCollider != null
                    ? _characterCollider.CurrentHeight * _characterCollider.EyeHeightRatio
                    : 0.88f;
                return basePos + (Vector3.up * eyeOffset);
            }
        }

        public Quaternion Rotation => _motor != null ? _motor.FacingRotation : transform.rotation;

        public Vector3 Velocity => _motor != null ? _motor.Velocity : Vector3.zero;

        public bool IsValid => this != null && isActiveAndEnabled && _isAlive;

        #endregion

        #region Lifecycle & Initialization

        private void Awake()
        {
            ValidateConfiguration();
            BootstrapChassis();
            BootstrapLocomotion();
            BootstrapCombat();
            BootstrapGland();
            BootstrapRvo();
        }

        private void Start()
        {
            ValidateLocomotionModules();
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

        public void SetDefinition(CharacterDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            ValidateConfiguration();
            BootstrapLocomotion();
            BootstrapGland();
        }

        private void ValidateConfiguration()
        {
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Missing required CharacterDefinition on AntCharacter! " +
                    $"Please assign a valid CharacterDefinition asset in the Inspector.");
            }

            _definition.Validate();
        }

        private void BootstrapChassis()
        {
            _motor = GetComponent<KinematicMotor3D>();
            if (_motor == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a KinematicMotor3D component on itself!");
            }

            _characterCollider = GetComponent<CharacterCollider3D>();
            if (_characterCollider == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a CharacterCollider3D component on itself!");
            }

            _playerController = GetComponent<PlayerController>();
            if (_playerController == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a PlayerController component on itself!");
            }

            _combatExecutor = GetComponent<CombatActionExecutor>();
            if (_combatExecutor == null)
            {
                throw new InvalidOperationException($"[{name}] AntCharacter requires a CombatActionExecutor component on itself!");
            }

            if (_modelTransform == null)
            {
                Transform visualChild = transform.Find("Visual");
                if (visualChild != null)
                {
                    _modelTransform = visualChild;
                }
            }
        }

        private void BootstrapGland()
        {
            if (_definition != null && _definition.HasTrailActions)
            {
                _gland = GetComponent<CharacterGland>();
                if (_gland == null)
                {
                    _gland = gameObject.AddComponent<CharacterGland>();
                }
                _gland.Initialize(_definition);
            }
        }

        #endregion

        #region Simulation Ticks

        public void OnFixedUpdate(float fixedDeltaTime, ulong currentTick)
        {
            UpdateRvoVelocity();
        }

        public void OnUpdate(float deltaTime)
        {
            // Per-frame character updates
        }

        #endregion
    }
}
