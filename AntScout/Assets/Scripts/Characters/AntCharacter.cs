using System;
using AntScout.Characters;
using PGS.Camera.Interfaces;
using PGS.Combat.Execution;
using PGS.Core.Time;
using PGS.Effects;
using PGS.Player.Controller;
using PGS.Player.Motor.ThreeD;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Universal aggregate coordinator for ant characters (Player Heroes and AI entities).
    /// Acts as the chassis coordinator configuring kinematic motor physics, traversal modules,
    /// combat action execution (com.pgs.combat), stats (com.pgs.stats), status effects (com.pgs.effects),
    /// camera target framing (com.pgs.camera), and local crowd avoidance.
    /// 
    /// Split across partial classes for strict Single Responsibility (SRP):
    /// - AntCharacter.cs: Core lifecycle, definition validation, component references, ICameraTarget.
    /// - AntCharacter.Combat.cs: IMovementReceiver, IAttackable, IColonyFactionMember, IDamageReceiver, IHealReceiver, IKnockbackReceiver, ISlowReceiver, IStunReceiver.
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
    [RequireComponent(typeof(StatsController))]
    [RequireComponent(typeof(StatusReceiver))]
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
        private StatsController _statsController;
        private StatusReceiver _statusReceiver;
        private CharacterGland _gland;
        private bool _isAlive = true;

        #region Public Properties

        public CharacterDefinition Definition => _definition;
        public KinematicMotor3D Motor => _motor;
        public CharacterCollider3D CharacterCollider => _characterCollider;
        public PlayerController PlayerController => _playerController;
        public CombatActionExecutor CombatExecutor => _combatExecutor;
        public StatsController Stats => _statsController;
        public StatusReceiver StatusReceiver => _statusReceiver;
        public CharacterGland Gland => _gland;
        public bool IsAlive => _isAlive;

        #endregion

        #region ICameraTarget Implementation (com.pgs.camera)

        public Transform TargetTransform => _modelTransform != null ? _modelTransform : transform;

        public Vector3 Position
        {
            get
            {
                if (_characterCollider == null)
                {
                    throw new InvalidOperationException($"[{name}] CharacterCollider3D is missing on AntCharacter!");
                }

                Vector3 basePos = _modelTransform != null ? _modelTransform.position : transform.position;
                float eyeOffset = _characterCollider.CurrentHeight * _characterCollider.EyeHeightRatio;
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

        private void OnDestroy()
        {
            CleanupCombat();
        }

        public void SetDefinition(CharacterDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            ValidateConfiguration();
            BootstrapCombat();
            BootstrapGland();
        }

        private void BootstrapChassis()
        {
            _motor = GetComponent<KinematicMotor3D>();
            _characterCollider = GetComponent<CharacterCollider3D>();
            _playerController = GetComponent<PlayerController>();
            _combatExecutor = GetComponent<CombatActionExecutor>();
            _statsController = GetComponent<StatsController>();
            _statusReceiver = GetComponent<StatusReceiver>();

            ValidateChassisComponents();

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
                _gland.Initialize(_definition, _colonyTeamId);
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
