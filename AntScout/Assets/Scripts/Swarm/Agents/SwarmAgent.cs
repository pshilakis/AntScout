using System;
using AntScout.Characters;
using AntScout.Config;
using AntScout.Match;
using AntScout.Pheromone;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using AntScout.UI.Overhead;
using PGS.Core.Time;
using PGS.FogOfWar.Core;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Abstract base class for all non-player colony swarm agents (Workers, Soldiers, Guards).
    /// Centralizes:
    /// - AntTrailFollower locomotion & A* navigation integration
    /// - ColonyVitals, stats, status effects, and pooling lifecycles
    /// - CombatActionExecutor primary action execution
    /// - Reactive Enmity, social alarm broadcasting, and leashing
    /// Grounded in Liskov Substitution (LSP), Single Responsibility (SRP), and Interface Segregation (ISP).
    /// </summary>
    [RequireComponent(typeof(AntTrailFollower))]
    public abstract partial class SwarmAgent : MonoBehaviour, ISwarmAgent, IUpdatable, IPoolable
    {
        [Header("Configuration & Caste Identity")]
        [Tooltip("Source of truth for nest-wide simulation, sensing, and aggression parameters.")]
        [SerializeField] protected SwarmDefinition _config;

        [Tooltip("CharacterDefinition asset providing BodyRadius, MaxHealth, Defense, Attack, and PrimaryAction.")]
        [SerializeField] protected CharacterDefinition _characterDefinition;

        [Header("Visual Model & Body Renderer")]
        [Tooltip("MeshRenderer or SkinnedMeshRenderer for the ant's body mesh. Auto-cached if unassigned.")]
        [SerializeField] protected Renderer _bodyRenderer;

        protected AntTrailFollower _follower;
        protected ColonyNest _homeNest;
        protected PheromoneTrailEmitter _trailEmitter;
        protected SwarmAgentState _currentState = SwarmAgentState.Idle;

        public SwarmAgentState CurrentState => _currentState;
        public Transform AgentTransform => transform;
        public ColonyNest HomeNest => _homeNest;
        public CharacterDefinition CharacterDefinition => _characterDefinition;
        public AntTrailFollower Follower => _follower;
        public SwarmDefinition Config => _config;

        public Renderer BodyRenderer
        {
            get
            {
                if (_bodyRenderer == null)
                {
                    _bodyRenderer = ResolveBodyRenderer();
                }
                return _bodyRenderer;
            }
        }

        private Renderer ResolveBodyRenderer()
        {
            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                var rend = visual.GetComponentInChildren<Renderer>();
                if (rend != null) return rend;
            }
            return GetComponentInChildren<Renderer>();
        }

        public PheromoneTrailEmitter TrailEmitter
        {
            get
            {
                if (_trailEmitter == null || !_trailEmitter.gameObject.scene.IsValid())
                {
                    if (_homeNest != null)
                    {
                        _trailEmitter = _homeNest.TrailEmitter;
                    }
                    else if (PheromoneTrailEmitter.TryGetForColony(ColonyTeamId, out var emitter))
                    {
                        _trailEmitter = emitter;
                    }
                }
                return _trailEmitter;
            }
        }

        public event Action<SwarmAgentState> OnStateChanged;

        protected virtual void Awake()
        {
            _follower = GetComponent<AntTrailFollower>();
            ValidateSwarmAgentComponents();
            BootstrapCombat();
        }

        protected virtual void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
            FogOfWarManager.Register(this);
            FogOfWarManager.RegisterCullable(this);
            if (_follower != null)
            {
                _follower.OnReachedTrailTip += HandleReachedTrailTip;
                _follower.OnTrailResumed += HandleTrailResumed;
                _follower.OnTrailLost += HandleTrailLost;
                _follower.OnReturnedToNest += HandleReturnedToNest;
            }
        }

        protected virtual void OnDisable()
        {
            PgsTime.Unregister(this);
            FogOfWarManager.Unregister(this);
            FogOfWarManager.UnregisterCullable(this);
            if (_follower != null)
            {
                _follower.OnReachedTrailTip -= HandleReachedTrailTip;
                _follower.OnTrailResumed -= HandleTrailResumed;
                _follower.OnTrailLost -= HandleTrailLost;
                _follower.OnReturnedToNest -= HandleReturnedToNest;
            }
        }

        protected virtual void OnDestroy()
        {
            CleanupCombat();
        }

        public virtual void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter)
        {
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));
            _trailEmitter = trailEmitter ?? throw new ArgumentNullException(nameof(trailEmitter));

            InitializeCombat(homeNest);
            _follower.Initialize(homeNest, trailEmitter, _config, _characterDefinition);
            _combatTarget = null;
            _combatInactivityTimer = 0f;
            SetupHealthBar();
        }

        private void SetupHealthBar()
        {
            if (_characterDefinition != null && _characterDefinition.EnableHealthBar)
            {
                var bar = OverheadHealthBar.Attach(
                    gameObject,
                    _characterDefinition.HealthBarHeightOffset,
                    _characterDefinition.HealthBarWidth,
                    _characterDefinition.HealthBarHeight,
                    _characterDefinition.HealthBarBorderThickness,
                    _characterDefinition.HealthBarSegmentSize,
                    _characterDefinition.HealthBarSegmentGap,
                    _characterDefinition.HealthBarNotchWidth,
                    _characterDefinition.HealthBarVisibility);
                bar.BindHealth(this);
            }
        }

        public void SetState(SwarmAgentState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            OnStateChanged?.Invoke(_currentState);
        }

        public virtual void OrderReturnHome()
        {
            if (_currentState == SwarmAgentState.ReturningHome) return;

            SetState(SwarmAgentState.ReturningHome);
            DisengageCombat();
        }

        public void OnUpdate(float deltaTime)
        {
            if (_currentState == SwarmAgentState.Defending)
            {
                EvaluateCombatDefense(deltaTime);
                return;
            }

            EvaluateAgentState(deltaTime);
        }

        /// <summary>
        /// Caste-specific state evaluation (e.g. foraging, harvesting, patrolling, guarding).
        /// Executed every frame when the agent is not in the universal Defending state.
        /// </summary>
        protected abstract void EvaluateAgentState(float deltaTime);

        protected virtual void HandleReachedTrailTip() { }
        protected virtual void HandleTrailResumed() { }
        protected virtual void HandleTrailLost() { }
        protected virtual void HandleReturnedToNest() { }

        #region IPoolable Implementation

        public virtual void OnSpawnedFromPool()
        {
            _combatTarget = null;
            _combatInactivityTimer = 0f;
            _currentState = SwarmAgentState.Idle;
            if (_vitals != null)
            {
                _vitals.Reset();
            }
            var bar = GetComponentInChildren<OverheadHealthBar>();
            if (bar != null)
            {
                bar.BindHealth(this);
            }
        }

        public virtual void OnDespawnedToPool()
        {
            DisengageCombat();
            if (_follower != null)
            {
                _follower.StopMovement();
            }
            _currentState = SwarmAgentState.Idle;
        }

        #endregion

        #region Validation

        private void ValidateSwarmAgentComponents()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Missing required SwarmDefinition on GameObject '{gameObject.name}'.");
            }
            if (_characterDefinition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Missing required CharacterDefinition reference on GameObject '{gameObject.name}'.");
            }
            if (_follower == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] Missing required AntTrailFollower on GameObject '{gameObject.name}'.");
            }
        }

        #endregion
    }
}
