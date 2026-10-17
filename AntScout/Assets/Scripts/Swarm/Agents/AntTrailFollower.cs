using System;
using AntScout.Characters;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Match;
using AntScout.Pheromone;
using AntScout.Swarm.Nest;
using Pathfinding;
using Pathfinding.RVO;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Operating modes for the AntTrailFollower steering layer.
    /// </summary>
    public enum FollowerMode
    {
        Inactive = 0,
        OutboundTrail = 1,
        InboundNest = 2,
        Wander = 3,
        MeanderHome = 4
    }

    /// <summary>
    /// Bridges the pheromone trail simulation with A* Pathfinding Project Pro (IAstarAI).
    /// Samples scent gradients from PheromoneTrailEmitter and projects navigation destinations to A*.
    /// Implements IUpdatable to receive deterministic simulation updates on TimeChannel.World.
    /// 
    /// Split across partial classes for strict Single Responsibility (SRP):
    /// - AntTrailFollower.cs: Core lifecycle, fields, properties, A* bridging, and simulation tick dispatcher.
    /// - AntTrailFollower.Outbound.cs: Outbound trail following, lookahead projection, lateral lane dispersion, defection, and tip arrival.
    /// - AntTrailFollower.Inbound.cs: Inbound nest retracing, highway lookahead shortcuts, and food cargo stigmergy trail reinforcement/migration.
    /// - AntTrailFollower.Wander.cs: Freeform roaming, target approach steering, and biased random-walk home meandering.
    /// </summary>
    public partial class AntTrailFollower : MonoBehaviour, IUpdatable, IPoolable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for worker movement speed, sensory radii, and waypoint lookahead.")]
        [SerializeField] private SwarmDefinition _config;

        private IAstarAI _ai;
        private RVOController _rvoController;
        private PheromoneTrailEmitter _emitter;
        private ColonyNest _homeNest;

        private PheromoneTrailSegment _trackedSegment;
        private int _trackedNodeIndex = -1;
        private float _lateralOffsetSeed;
        private float _lostTrailTimer;
        private float _freshnessCheckTimer;

        private float _meanderTimer;
        private Vector3 _currentMeanderWaypoint;

        private FollowerMode _mode = FollowerMode.Inactive;
        private bool _isInitialized;
        private bool _hasCargo;
        private CharacterDefinition _characterDefinition;

        public event Action OnReachedTrailTip;
        public event Action OnTrailLost;
        public event Action OnReturnedToNest;
        public event Action OnTrailResumed;

        public FollowerMode CurrentMode => _mode;
        public bool IsOutbound => _mode == FollowerMode.OutboundTrail;
        public bool HasCargo => _hasCargo;
        public PheromoneTrailSegment TrackedSegment => _trackedSegment;

        /// <summary>
        /// Effective base movement speed. Resolves from the assigned CharacterDefinition (Single Source of Truth),
        /// with graceful fallback to SwarmDefinition or default.
        /// </summary>
        public float MoveSpeed => (_characterDefinition != null && _characterDefinition.BaseMoveSpeed > 0.001f)
            ? _characterDefinition.BaseMoveSpeed
            : (_config != null ? _config.WorkerMoveSpeed : 5.5f);

        public PheromoneTrailEmitter Emitter
        {
            get
            {
                if (_emitter == null || !_emitter.gameObject.scene.IsValid())
                {
                    if (_homeNest != null)
                    {
                        _emitter = _homeNest.TrailEmitter;
                    }
                }
                return _emitter;
            }
        }

        public float Radius => _ai != null
            ? _ai.radius
            : throw new InvalidOperationException($"[{name}] IAstarAI is missing on AntTrailFollower!");

        private void Awake()
        {
            _ai = GetComponent<IAstarAI>();
            _rvoController = GetComponent<RVOController>();

            if (_ai == null)
            {
                throw new InvalidOperationException(
                    $"[AntTrailFollower] Missing required IAstarAI movement component (e.g., AIPath, RichAI) " +
                    $"on GameObject '{gameObject.name}'.");
            }
        }

        public void Initialize(ColonyNest homeNest, PheromoneTrailEmitter emitter, SwarmDefinition config, CharacterDefinition characterDefinition = null)
        {
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));
            _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _characterDefinition = characterDefinition;

            _ai.maxSpeed = MoveSpeed;
            _ai.canSearch = true;
            _ai.isStopped = false;

            if (_ai is AIPath aiPath)
            {
                aiPath.slowWhenNotFacingTarget = false;
                aiPath.slowdownDistance = 0.2f;
                aiPath.rotationSpeed = 720f;
                aiPath.rvoDensityBehavior.enabled = false;
            }

            if (_rvoController != null)
            {
                // Flow following set to 0 to restore natural lateral swarm separation across the scent corridor
                _rvoController.flowFollowingStrength = 0.0f;
                _rvoController.maxNeighbours = 6;
                _rvoController.agentTimeHorizon = 1.0f;
                _rvoController.obstacleTimeHorizon = 0.4f;
            }

            // Assign each ant a unique lateral lane across the pheromone plume
            _lateralOffsetSeed = UnityEngine.Random.Range(-1.0f, 1.0f);
            _trackedSegment = null;
            _trackedNodeIndex = -1;
            _lostTrailTimer = 0f;
            _freshnessCheckTimer = 0f;
            _mode = FollowerMode.Inactive;
            _isInitialized = true;
        }

        public void StopMovement()
        {
            _mode = FollowerMode.Inactive;
            if (_ai != null)
            {
                _ai.isStopped = true;
                _ai.destination = transform.position;
                if (_ai is AIPath aiPath)
                {
                    aiPath.updateRotation = false;
                }
            }
            if (_rvoController != null)
            {
                _rvoController.locked = true;
            }
        }

        private void ResumeMovement(float speed = -1f)
        {
            if (_ai != null)
            {
                _ai.isStopped = false;
                if (speed > 0f)
                {
                    _ai.maxSpeed = speed;
                }
                if (_ai is AIPath aiPath)
                {
                    aiPath.updateRotation = true;
                }
            }
            if (_rvoController != null)
            {
                _rvoController.locked = false;
            }
        }

        public void SetOutbound(bool outbound)
        {
            if (outbound)
            {
                FollowTrailOutbound();
            }
            else
            {
                ReturnToNest();
            }
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_isInitialized) return;

            switch (_mode)
            {
                case FollowerMode.OutboundTrail:
                    NavigateOutbound(deltaTime);
                    break;

                case FollowerMode.InboundNest:
                    NavigateInbound(deltaTime);
                    break;

                case FollowerMode.MeanderHome:
                    NavigateMeanderHome(deltaTime);
                    break;

                case FollowerMode.Wander:
                case FollowerMode.Inactive:
                    break;
            }
        }

        #region IPoolable Implementation

        public void OnSpawnedFromPool()
        {
            if (_ai != null)
            {
                _ai.Teleport(transform.position);
                _ai.isStopped = false;
            }

            if (_rvoController != null)
            {
                _rvoController.Move(Vector3.zero);
            }

            _trackedSegment = null;
            _trackedNodeIndex = -1;
            _lostTrailTimer = 0f;
            _freshnessCheckTimer = 0f;
            _mode = FollowerMode.Inactive;
        }

        public void OnDespawnedToPool()
        {
            StopMovement();
            _trackedSegment = null;
            _trackedNodeIndex = -1;
            _lostTrailTimer = 0f;
            _freshnessCheckTimer = 0f;
            _mode = FollowerMode.Inactive;
        }

        #endregion
    }
}
