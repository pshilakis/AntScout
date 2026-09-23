using System;
using AntScout.Config;
using AntScout.Pheromone;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// High-level behavioral controller and state machine for Worker Ants.
    /// Implements ISwarmAgent to allow polymorphic management by the Colony Nest and swarm coordinators.
    /// </summary>
    [RequireComponent(typeof(AntTrailFollower))]
    public class WorkerAntController : MonoBehaviour, ISwarmAgent
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for worker balance parameters.")]
        [SerializeField] private SwarmConfigSO _config;

        private AntTrailFollower _follower;
        private ColonyNest _homeNest;
        private SwarmAgentState _currentState = SwarmAgentState.Idle;
        private float _searchTimer;

        public SwarmAgentState CurrentState => _currentState;
        public Transform AgentTransform => transform;
        public ColonyNest HomeNest => _homeNest;

        public event Action<SwarmAgentState> OnStateChanged;

        private void Awake()
        {
            _follower = GetComponent<AntTrailFollower>();

            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required SwarmConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid SwarmConfigSO in the Inspector.");
            }

            if (_follower == null)
            {
                throw new InvalidOperationException(
                    $"[WorkerAntController] Missing required AntTrailFollower on GameObject '{gameObject.name}'.");
            }
        }

        private void OnEnable()
        {
            _follower.OnReachedTrailTip += HandleReachedTrailTip;
            _follower.OnTrailResumed += HandleTrailResumed;
            _follower.OnTrailLost += HandleTrailLost;
            _follower.OnReturnedToNest += HandleReturnedToNest;
        }

        private void OnDisable()
        {
            _follower.OnReachedTrailTip -= HandleReachedTrailTip;
            _follower.OnTrailResumed -= HandleTrailResumed;
            _follower.OnTrailLost -= HandleTrailLost;
            _follower.OnReturnedToNest -= HandleReturnedToNest;
        }

        public void Initialize(ColonyNest homeNest, PheromoneTrailEmitter trailEmitter)
        {
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));
            _follower.Initialize(homeNest, trailEmitter, _config);

            SetState(SwarmAgentState.FollowingTrailOutbound);
        }

        public void OrderReturnHome()
        {
            _follower.SetOutbound(false);
            SetState(SwarmAgentState.ReturningHome);
        }

        private void Update()
        {
            if (_currentState == SwarmAgentState.AtTrailEnd)
            {
                _searchTimer += Time.deltaTime;
                if (_searchTimer >= _config.TrailEndSearchDuration)
                {
                    _searchTimer = 0f;
                    OrderReturnHome();
                }
            }
        }

        private void HandleReachedTrailTip()
        {
            if (_currentState == SwarmAgentState.FollowingTrailOutbound)
            {
                _searchTimer = 0f;
                SetState(SwarmAgentState.AtTrailEnd);
            }
        }

        private void HandleTrailResumed()
        {
            if (_currentState == SwarmAgentState.AtTrailEnd)
            {
                _searchTimer = 0f;
                SetState(SwarmAgentState.FollowingTrailOutbound);
            }
        }

        private void HandleTrailLost()
        {
            if (_currentState == SwarmAgentState.FollowingTrailOutbound || _currentState == SwarmAgentState.AtTrailEnd)
            {
                OrderReturnHome();
            }
        }

        private void HandleReturnedToNest()
        {
            SetState(SwarmAgentState.Idle);
            if (_homeNest != null)
            {
                _homeNest.NotifyWorkerReturned(this);
            }
        }

        private void SetState(SwarmAgentState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            OnStateChanged?.Invoke(_currentState);
        }
    }
}
