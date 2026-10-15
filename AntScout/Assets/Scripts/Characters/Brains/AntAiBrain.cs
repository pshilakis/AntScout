using System;
using AntScout.Characters;
using AntScout.Core.Interfaces;
using AntScout.Pheromone;
using AntScout.Resources.Components;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using PGS.Core.Time;
using PGS.Effects;
using UnityEngine;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Operating state of the autonomous AI Hero commander.
    /// </summary>
    public enum AiHeroState
    {
        [Tooltip("Resting at the home nest depot awaiting chemical gland recharge or food availability.")]
        IdleAtNest = 0,

        [Tooltip("Actively paving a recruitment scent trail from the nest toward a harvestable food bonanza.")]
        PavingTrailToFood = 1,

        [Tooltip("Returning to the home nest depot to complete the trail circuit and link to the recruitment zone.")]
        ReturningToNest = 2,

        [Tooltip("Engaging hostile faction members in combat with offensive and mobility abilities.")]
        EngagingEnemy = 3
    }

    /// <summary>
    /// Autonomous AI Hero commander brain driving an AntCharacter chassis.
    /// Strictly decoupled from player input (zero IPlayerInputProvider inheritance):
    /// evaluates threats, paves pheromone highways, manages chemical reserves, and produces
    /// pure spatial movement and aiming vectors for AntBrainController.
    /// Grounded in Single Responsibility (SRP), Open/Closed (OCP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AntCharacter))]
    public partial class AntAiBrain : MonoBehaviour, IUpdatable
    {
        [Header("Colony Affiliation")]
        [Tooltip("Optional direct reference to the home ColonyNest. If unassigned, automatically resolves the ColonyNest in the scene matching AntCharacter's ColonyTeamId.")]
        [SerializeField] private ColonyNest _homeNest;

        [Header("Perception & Leashing")]
        [Tooltip("Physics LayerMask scanned for hostile targets.")]
        [SerializeField] private LayerMask _targetLayerMask = ~0;

        [Tooltip("Sensory detection radius to spot enemy heroes, workers, or structures.")]
        [Range(2.0f, 30.0f)]
        [SerializeField] private float _detectionRadius = 8.0f;

        [Tooltip("Maximum distance from home nest or current objective before breaking pursuit.")]
        [Range(4.0f, 50.0f)]
        [SerializeField] private float _leashRadius = 14.0f;

        [Header("Macro & Economy")]
        [Tooltip("Minimum gland capacity ratio before embarking from nest to pave a trail to food.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _minGlandRatioToPave = 0.5f;

        [Tooltip("Arrival distance threshold on XZ plane to consider a food source or nest reached.")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float _arrivalDistance = 1.2f;

        [Tooltip("Time in seconds between threat and target evaluation scans.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float _scanInterval = 0.15f;

        private AntCharacter _character;
        private AiHeroState _currentState = AiHeroState.IdleAtNest;
        private Vector3 _desiredMovement;
        private Vector3? _desiredFacing;
        private IAttackable _targetEnemy;
        private FoodSource _targetFood;
        private float _scanTimer;
        private readonly Collider[] _overlapBuffer = new Collider[16];

        public Vector3 DesiredMovement => _desiredMovement;
        public Vector3? DesiredFacing => _desiredFacing;
        public AiHeroState CurrentState => _currentState;
        public ColonyNest HomeNest => _homeNest;
        public IAttackable TargetEnemy => _targetEnemy;

        #region Lifecycle & Initialization

        private void Awake()
        {
            _character = GetComponent<AntCharacter>();
            ValidateConfiguration();
        }

        private void Start()
        {
            if (!enabled) return;
            ResolveHomeNest();
            ValidateHomeNest();
        }

        private void OnEnable()
        {
            _character ??= GetComponent<AntCharacter>();
            PgsTime.Register(this, UpdateRate.Continuous, TimeChannel.World);
            ResolveHomeNest();
            ValidateHomeNest();

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
            if (context.Instigator == null) return;

            IAttackable attacker = context.Instigator.GetComponentInParent<IAttackable>();
            if (attacker != null && attacker.IsAttackable)
            {
                _targetEnemy = attacker;
                if (_character != null && _character.Gland != null && _character.Gland.IsLayingTrail)
                {
                    _character.Gland.StopLayingTrail();
                }
                _currentState = AiHeroState.EngagingEnemy;
            }
        }

        public void ResetBrain()
        {
            _desiredMovement = Vector3.zero;
            _desiredFacing = null;
            _targetEnemy = null;
            _targetFood = null;

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

            _homeNest = null;
            ColonyNest[] nests = FindObjectsByType<ColonyNest>(FindObjectsSortMode.None);
            for (int i = 0; i < nests.Length; i++)
            {
                if (nests[i].ColonyTeamId == _character.ColonyTeamId)
                {
                    _homeNest = nests[i];
                    break;
                }
            }
        }

        #endregion

        #region Decision & Behavior Loop

        public void OnUpdate(float deltaTime)
        {
            if (!enabled || _character == null || !_character.IsAlive)
            {
                ResetBrain();
                return;
            }

            _scanTimer += deltaTime;
            if (_scanTimer >= _scanInterval)
            {
                _scanTimer = 0f;
                ScanForThreats();
            }

            switch (_currentState)
            {
                case AiHeroState.EngagingEnemy:
                    ExecuteCombatState();
                    break;

                case AiHeroState.IdleAtNest:
                    ExecuteIdleAtNestState();
                    break;

                case AiHeroState.PavingTrailToFood:
                    ExecutePavingTrailState();
                    break;

                case AiHeroState.ReturningToNest:
                    ExecuteReturningToNestState();
                    break;
            }
        }

        private void ScanForThreats()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                _detectionRadius,
                _overlapBuffer,
                _targetLayerMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _overlapBuffer[i];
                if (col == null) continue;

                IAttackable attackable = col.GetComponentInParent<IAttackable>();
                if (attackable == null || !attackable.IsAttackable) continue;

                IColonyFactionMember faction = col.GetComponentInParent<IColonyFactionMember>();
                if (faction == null) continue;

                if (ColonyAffiliation.Evaluate(_character, faction) == TargetAffiliation.Enemy)
                {
                    _targetEnemy = attackable;
                    if (_character.Gland != null && _character.Gland.IsLayingTrail)
                    {
                        _character.Gland.StopLayingTrail();
                    }
                    _currentState = AiHeroState.EngagingEnemy;
                    break;
                }
            }
        }

        private void ExecuteCombatState()
        {
            if (_targetEnemy == null || !_targetEnemy.IsAttackable || _targetEnemy.Transform == null)
            {
                _targetEnemy = null;
                _currentState = AiHeroState.ReturningToNest;
                return;
            }

            Vector3 selfPos = transform.position;
            Vector3 enemyPos = _targetEnemy.Transform.position;
            Vector3 diff = enemyPos - selfPos;
            diff.y = 0f;
            float planarDist = diff.magnitude;

            if (planarDist > _leashRadius)
            {
                // Disengage if enemy moves too far
                _targetEnemy = null;
                _currentState = AiHeroState.ReturningToNest;
                return;
            }

            Vector3 aimDir = planarDist > 0.001f ? diff / planarDist : transform.forward;
            _desiredFacing = aimDir;

            var definition = _character.Definition;

            // Mobility closure: Dash if utility action is equipped and enemy is far
            if (definition != null && definition.UtilityAction != null && planarDist > 4.5f)
            {
                _character.TryExecuteAction(definition.UtilityAction, aimDir);
            }

            // Offensive cast: Secondary ability
            if (definition != null && definition.SecondaryAction != null && planarDist <= definition.SecondaryAction.Range)
            {
                _character.TryExecuteAction(definition.SecondaryAction, aimDir);
            }

            // Offensive cast: Primary ability
            if (definition != null && definition.PrimaryAction != null && planarDist <= definition.PrimaryAction.Range)
            {
                _character.TryExecuteAction(definition.PrimaryAction, aimDir);
            }

            float minStopDistance = (definition != null && definition.PrimaryAction != null)
                ? definition.PrimaryAction.Range * 0.75f
                : _arrivalDistance;

            if (planarDist > minStopDistance)
            {
                _desiredMovement = aimDir;
            }
            else
            {
                _desiredMovement = Vector3.zero;
            }
        }

        private void ExecuteIdleAtNestState()
        {
            if (_homeNest == null) return;

            Vector3 depotPos = _homeNest.SpawnPosition;
            Vector3 diff = depotPos - transform.position;
            diff.y = 0f;
            float distToNest = diff.magnitude;

            if (distToNest > _arrivalDistance)
            {
                _desiredMovement = diff.normalized;
                _desiredFacing = _desiredMovement;
                return;
            }

            _desiredMovement = Vector3.zero;
            _desiredFacing = null;

            bool glandReady = _character.Gland == null ||
                              _character.Gland.NormalizedCapacity >= _minGlandRatioToPave;

            if (glandReady)
            {
                _targetFood = FoodSource.FindClosest(transform.position, float.MaxValue);
                if (_targetFood != null && _targetFood.CanHarvest)
                {
                    if (_character.Definition != null &&
                        _character.Definition.RecruitmentTrailAction != null &&
                        _character.Gland != null)
                    {
                        _character.Gland.StartLayingTrail(_character.Definition.RecruitmentTrailAction);
                    }

                    _currentState = AiHeroState.PavingTrailToFood;
                }
            }
        }

        private void ExecutePavingTrailState()
        {
            if (_targetFood == null || !_targetFood.CanHarvest)
            {
                _targetFood = null;
                _currentState = AiHeroState.ReturningToNest;
                return;
            }

            Vector3 foodPos = _targetFood.WorldPosition;
            Vector3 diff = foodPos - transform.position;
            diff.y = 0f;
            float planarDist = diff.magnitude;

            if (planarDist <= _arrivalDistance)
            {
                // Food reached: trail successfully laid!
                _currentState = AiHeroState.ReturningToNest;
                return;
            }

            if (_character.Gland != null && _character.Gland.IsDepleted)
            {
                // Gland ran dry, turn back to depot
                _currentState = AiHeroState.ReturningToNest;
                return;
            }

            _desiredMovement = diff.normalized;
            _desiredFacing = _desiredMovement;
        }

        private void ExecuteReturningToNestState()
        {
            if (_homeNest == null)
            {
                _desiredMovement = Vector3.zero;
                return;
            }

            Vector3 depotPos = _homeNest.SpawnPosition;
            Vector3 diff = depotPos - transform.position;
            diff.y = 0f;
            float planarDist = diff.magnitude;

            if (planarDist <= _arrivalDistance)
            {
                if (_character.Gland != null && _character.Gland.IsLayingTrail)
                {
                    _character.Gland.StopLayingTrail();
                }

                _desiredMovement = Vector3.zero;
                _desiredFacing = null;
                _currentState = AiHeroState.IdleAtNest;
                return;
            }

            _desiredMovement = diff.normalized;
            _desiredFacing = _desiredMovement;
        }

        #endregion
    }
}
