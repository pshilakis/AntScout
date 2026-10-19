using System;
using AntScout.Characters;
using AntScout.Characters.Brains.Behaviors;
using AntScout.Characters.Brains.StateContext;
using AntScout.Characters.Brains.States;
using AntScout.Core.Interfaces;
using AntScout.Resources.Components;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using PGS.Core.Time;
using PGS.Effects;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Autonomous AI Hero commander brain driving an AntCharacter chassis.
    /// Strictly decoupled from player input (zero IPlayerInputProvider inheritance).
    /// Bridges Unity component lifecycle, perception layers, and AntBrainController to a high-performance,
    /// zero-allocation synchronous PGS.States.StateMachine<AntAiContext>.
    /// Grounded in Single Responsibility (SRP), Open/Closed (OCP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AntCharacter))]
    public partial class AntAiBrain : MonoBehaviour, IUpdatable
    {
        [Header("AI Strategy Override (Optional)")]
        [Tooltip("Optional strategy profile override. If unassigned, defaults to the AI behavior and thresholds defined on AntCharacter's CharacterDefinition.")]
        [SerializeField] private AiStrategyProfile _strategyOverride;

        [Header("Colony Affiliation")]
        [Tooltip("Optional direct reference to the home ColonyNest. If unassigned, automatically resolves the ColonyNest in the scene matching AntCharacter's ColonyTeamId.")]
        [SerializeField] private ColonyNest _homeNest;

        [Header("Perception Layer")]
        [Tooltip("Physics LayerMask scanned for hostile targets.")]
        [SerializeField] private LayerMask _targetLayerMask = ~0;

        [Tooltip("Base sensory detection radius to spot enemy heroes, workers, or structures.")]
        [Range(2.0f, 30.0f)]
        [SerializeField] private float _detectionRadius = 8.0f;

        private AntCharacter _character;
        private AntAiContext _context;
        private StateMachine<AntAiContext> _stateMachine;

        public Vector3 DesiredMovement => _context != null ? _context.DesiredMovement : Vector3.zero;
        public Vector3? DesiredFacing => _context != null ? _context.DesiredFacing : null;
        public ColonyNest HomeNest => _homeNest;
        public LayerMask TargetLayerMask => _targetLayerMask;
        public float DetectionRadius => _detectionRadius;
        public AntAiContext Context => _context;
        public StateMachine<AntAiContext> StateMachine => _stateMachine;

        public AiStrategyProfile StrategyOverride => _strategyOverride;
        public float PerceptionMultiplier => _strategyOverride != null ? _strategyOverride.PerceptionMultiplier : 1.0f;

        public IAttackable TargetEnemy
        {
            get => _context != null ? _context.TargetEnemy : null;
            set
            {
                if (_context != null) _context.TargetEnemy = value;
            }
        }

        public FoodSource TargetFood
        {
            get => _context != null ? _context.TargetFood : null;
            set
            {
                if (_context != null) _context.TargetFood = value;
            }
        }

        public float CurrentHeadingAngle => _context != null ? _context.CurrentHeadingAngle : 0f;
        public float JitterSeed => _context != null ? _context.JitterSeed : 0f;

        // Backward compatibility properties for any inspector or legacy checks
        public IAiBehavior ActiveBehavior => _strategyOverride != null ? _strategyOverride.Behavior : (_character != null && _character.Definition != null ? _character.Definition.AiBehavior : null);
        public float RetreatHealthRatio => _strategyOverride != null ? _strategyOverride.RetreatHealthRatio : (_character != null && _character.Definition != null ? _character.Definition.RetreatHealthRatio : 0.3f);
        public float LeashRadius => _strategyOverride != null ? _strategyOverride.LeashRadius : (_character != null && _character.Definition != null ? _character.Definition.LeashRadius : 14.0f);
        public float ScanInterval => _strategyOverride != null ? _strategyOverride.ScanInterval : (_character != null && _character.Definition != null ? _character.Definition.ScanInterval : 0.15f);
        public float ArrivalDistance => _strategyOverride != null ? _strategyOverride.ArrivalDistance : (_character != null && _character.Definition != null ? _character.Definition.ArrivalDistance : 1.2f);
        public float MinGlandRatioToEmbark => _strategyOverride != null ? _strategyOverride.MinGlandRatioToEmbark : (_character != null && _character.Definition != null ? _character.Definition.MinGlandRatioToEmbark : 0.5f);
        public bool UseMobilityToEngage => _strategyOverride != null ? _strategyOverride.UseMobilityToEngage : (_character != null && _character.Definition != null ? _character.Definition.UseMobilityToEngage : true);
        public bool UseMobilityToRetreat => _strategyOverride != null ? _strategyOverride.UseMobilityToRetreat : (_character != null && _character.Definition != null ? _character.Definition.UseMobilityToRetreat : true);

        #region Backward Compatibility & External Adapters

        public void ResetBehaviorTimer() => _context?.ResetLegTimer();
        public void IncrementBehaviorTimer(float deltaTime) => _context?.IncrementLegTimer(deltaTime);
        public void SetDesiredMovement(Vector3 movement, Vector3? facing = null) => _context?.SetMovement(movement, facing);
        public void RollNewRoamingLeg() => _context?.RollNewRoamingLeg();
        public bool CheckLegCompletion() => _context != null && _context.CheckLegCompletion();
        public Vector3 ComputeRoamingMovement() => _context != null ? _context.ComputeRoamingMovement() : Vector3.zero;

        #endregion

        #region Lifecycle & Initialization

        private void Awake()
        {
            _character = GetComponent<AntCharacter>();
            ValidateConfiguration();
        }

        private void Start()
        {
            // Initialization deferred to explicit Initialize() or first OnUpdate() tick
        }

        private void OnEnable()
        {
            _character ??= GetComponent<AntCharacter>();
            PgsTime.Register(this, UpdateRate.Continuous, TimeChannel.World);

            if (_character != null)
            {
                _character.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
            if (_character != null)
            {
                _character.OnDamaged -= HandleDamaged;
            }
            ResetBrain();
        }

        private void HandleDamaged(DamageResult result, HitContext context)
        {
            if (context.Instigator == null || _context == null || _stateMachine == null) return;

            IAttackable attacker = context.Instigator.GetComponentInParent<IAttackable>();
            if (attacker != null && attacker.IsAttackable)
            {
                _context.TargetEnemy = attacker;
                if (_character != null && _character.Gland != null && _character.Gland.IsLayingTrail)
                {
                    _character.Gland.StopLayingTrail();
                }
                _stateMachine.TransitionTo<AntCombatEngagementState>();
            }
        }

        /// <summary>
        /// Explicit top-down initialization called deterministically by AntCharacter or SpawnController.
        /// Grounded in Dependency Inversion Principle (DIP): Injects allied home nest depot and optional strategy override.
        /// </summary>
        public void Initialize(ColonyNest homeNest, AiStrategyProfile strategyOverride = null)
        {
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));
            if (strategyOverride != null)
            {
                _strategyOverride = strategyOverride;
            }
            ValidateConfiguration();
            ValidateHomeNest();

            BuildStateMachine();
        }

        private void TryInitializeStateMachine()
        {
            if (_stateMachine != null) return;
            if (_character == null) _character = GetComponent<AntCharacter>();

            // Resolve home nest matching this character's current colony team
            if (_homeNest == null || _homeNest.ColonyTeamId != _character.ColonyTeamId)
            {
                if (ColonyNest.TryGetForTeam(_character.ColonyTeamId, out ColonyNest registeredNest) && registeredNest != null)
                {
                    _homeNest = registeredNest;
                }
            }

            if (_homeNest != null && _homeNest.ColonyTeamId == _character.ColonyTeamId)
            {
                BuildStateMachine();
            }
        }

        private void BuildStateMachine()
        {
            if (_character == null) _character = GetComponent<AntCharacter>();
            ValidateConfiguration();
            ValidateHomeNest();

            _context = new AntAiContext(this, _character, _homeNest);
            _stateMachine = new StateMachine<AntAiContext>(_context);
            _context.BindStateMachine(_stateMachine);

            _stateMachine.RegisterState(new AntRestingAtNestState());
            _stateMachine.RegisterState(new AntScoutingFieldState());
            _stateMachine.RegisterState(new AntMovingToFoodState());
            _stateMachine.RegisterState(new AntPavingTrailHomeState());
            _stateMachine.RegisterState(new AntCombatEngagementState());
            _stateMachine.RegisterState(new AntRallyingSwarmState());
            _stateMachine.RegisterState(new AntRetreatState());

            _stateMachine.TransitionTo<AntRestingAtNestState>();
        }

        public void ResetBrain()
        {
            if (_context != null)
            {
                _context.StopMovement();
                _context.ClearTargetEnemy();
                _context.ClearTargetFood();
            }

            if (_character != null && _character.Gland != null && _character.Gland.IsLayingTrail)
            {
                _character.Gland.StopLayingTrail();
            }
        }

        private void ResolveHomeNest()
        {
            if (_character == null)
            {
                _character = GetComponent<AntCharacter>();
            }

            if (_homeNest != null && _homeNest.ColonyTeamId == _character.ColonyTeamId) return;

            if (ColonyNest.TryGetForTeam(_character.ColonyTeamId, out ColonyNest registeredNest) && registeredNest != null)
            {
                _homeNest = registeredNest;
                return;
            }

            throw new InvalidOperationException(
                $"[{name}] AntAiBrain: No ColonyNest registered for Team {_character.ColonyTeamId}! " +
                "Ensure the allied ColonyNest is initialized before spawning AI units.");
        }

        #endregion

        #region Simulation Loop

        public void OnUpdate(float deltaTime)
        {
            if (!enabled || _character == null || !_character.IsAlive)
            {
                ResetBrain();
                return;
            }

            if (_stateMachine == null)
            {
                TryInitializeStateMachine();
                if (_stateMachine == null) return;
            }

            _stateMachine.OnUpdate(deltaTime);
        }

        #endregion
    }
}
