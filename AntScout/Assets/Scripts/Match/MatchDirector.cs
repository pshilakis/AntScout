using System;
using System.Threading;
using AntScout.Match.States;
using AntScout.Swarm.Nest;
using Cysharp.Threading.Tasks;
using PGS.Core;
using PGS.Core.Time;
using PGS.States;
using UnityEngine;

namespace AntScout.Match
{
    /// <summary>
    /// Master referee and game loop coordinator for RTS skirmish matches.
    /// Drives match timeline via an asynchronous, zero-allocation state machine (AsyncStateMachine<MatchDirector>).
    /// Orchestrates deterministic participant setup, pre-match countdown, active combat, and match conclusion.
    /// Strictly adheres to Single Responsibility (SRP), Open/Closed (OCP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class MatchDirector : MonoBehaviour, IUpdatable
    {
        private static MatchDirector s_instance;

        [ColoredHeader("Match Configuration", HeaderColor.Cyan)]
        [Tooltip("Countdown duration in seconds before active match control begins.")]
        [Range(0f, 10f)]
        [SerializeField] private float _preMatchDuration = 2.0f;

        [Tooltip("Team ID corresponding to the local player.")]
        [SerializeField] private int _playerTeamId = 0;

        [Tooltip("Whether to automatically freeze locomotion and cancel pending respawns on match conclusion.")]
        [SerializeField] private bool _autoLockParticipantsOnConclusion = true;

        [ColoredHeader("Coordinator References", HeaderColor.Mint)]
        [Tooltip("Reference to the SpawnController coordinating participant lifecycles.")]
        [SerializeField] private SpawnController _spawnController;

        private AsyncStateMachine<MatchDirector> _stateMachine;
        private float _preMatchTimer;
        private DateTimeOffset _startTimeUtc;
        private DateTimeOffset _endTimeUtc;
        private float _startSimulationTime;
        private float _endSimulationTime;
        private MatchResultContext? _finalResult;

        private MatchResult _pendingResult = MatchResult.None;
        private int _pendingWinningTeamId = -1;

        #region Public Properties

        public static MatchDirector Instance => s_instance;
        public IAsyncStateMachine<MatchDirector> StateMachine => _stateMachine;
        public SpawnController SpawnController => _spawnController;
        public IAsyncState<MatchDirector> CurrentState => _stateMachine?.CurrentState;

        public MatchState State => _stateMachine?.CurrentState switch
        {
            MatchSetupState => MatchState.PreMatch,
            MatchPreCountdownState => MatchState.PreMatch,
            MatchInProgressState => MatchState.InProgress,
            MatchConcludedState => MatchState.Concluded,
            _ => MatchState.PreMatch
        };

        public MatchResultContext? FinalResult => _finalResult;
        public float PreMatchDuration => _preMatchDuration;
        public float PreMatchRemaining => Mathf.Max(0f, _preMatchTimer);
        public int PlayerTeamId => _playerTeamId;
        public bool AutoLockParticipantsOnConclusion => _autoLockParticipantsOnConclusion;

        public float ElapsedSimulationTime => _stateMachine?.CurrentState is MatchInProgressState
            ? PgsTime.ElapsedTime - _startSimulationTime
            : (_finalResult.HasValue ? _finalResult.Value.ActivePlayDuration : 0f);

        #endregion

        #region Events (Telemetry & UI)

        public event Action<MatchState> OnMatchStateChanged;
        public event Action<MatchResultContext> OnMatchConcluded;

        #endregion

        #region Lifecycle & Initialization

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                throw new InvalidOperationException(
                    $"[{name}] MatchDirector: Multiple instances detected! Existing: '{s_instance.name}', Duplicate: '{name}'.");
            }
            s_instance = this;
            ValidateConfiguration();
        }

        private async void Start()
        {
            _stateMachine = new AsyncStateMachine<MatchDirector>(this);
            _stateMachine.RegisterWithTime(TimeChannel.World, UpdateRate.Continuous);
            _stateMachine.OnStateChanged += (prev, next) => OnMatchStateChanged?.Invoke(State);

            // Register pre-allocated match states (Flyweight Pattern, 0-GC transitions)
            _stateMachine.RegisterState(new MatchSetupState());
            _stateMachine.RegisterState(new MatchPreCountdownState());
            _stateMachine.RegisterState(new MatchInProgressState());
            _stateMachine.RegisterState(new MatchConcludedState());

            // Begin deterministic async flow (cancels safely if GameObject is destroyed)
            await _stateMachine.TransitionToAsync<MatchSetupState>(destroyCancellationToken);
        }

        private void OnDestroy()
        {
            _stateMachine?.UnregisterFromTime();

            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public void OnUpdate(float deltaTime)
        {
            _stateMachine?.OnUpdate(deltaTime);
        }

        #endregion

        #region State Machine Coordination Hooks

        public void SetPreMatchRemaining(float duration)
        {
            _preMatchTimer = duration;
        }

        public void DecrementPreMatchTimer(float delta)
        {
            _preMatchTimer -= delta;
        }

        public void RecordMatchStartTime()
        {
            _startTimeUtc = DateTimeOffset.UtcNow;
            _startSimulationTime = PgsTime.ElapsedTime;
        }

        public void PrepareConclusion(MatchResult result, int winningTeamId)
        {
            _pendingResult = result;
            _pendingWinningTeamId = winningTeamId;
        }

        public void FinalizeConclusion()
        {
            _endTimeUtc = DateTimeOffset.UtcNow;
            _endSimulationTime = PgsTime.ElapsedTime;

            int totalBiomass = 0;
            if (ColonyNest.TryGetForTeam(_playerTeamId, out ColonyNest playerNest) && playerNest != null)
            {
                totalBiomass = playerNest.TotalBiomass;
            }

            var context = new MatchResultContext(
                result: _pendingResult,
                winningTeamId: _pendingWinningTeamId,
                totalBiomassHarvested: totalBiomass,
                startTimeUtc: _startTimeUtc,
                endTimeUtc: _endTimeUtc,
                startSimulationTime: _startSimulationTime,
                endSimulationTime: _endSimulationTime
            );

            _finalResult = context;

            Debug.Log($"[MatchDirector] Match Concluded: {_pendingResult}! Winning Team: {_pendingWinningTeamId}. " +
                      $"Active Play Time: {context.ActivePlayDuration:F1}s, Biomass Harvested: {context.TotalBiomassHarvested}");

            OnMatchConcluded?.Invoke(context);
        }

        /// <summary>
        /// Explicitly triggers match conclusion from external cheat/debug consoles or forfeit triggers.
        /// </summary>
        public void ConcludeMatch(MatchResult result, int winningTeamId)
        {
            if (_stateMachine?.CurrentState is MatchConcludedState) return;
            PrepareConclusion(result, winningTeamId);
            _stateMachine.TransitionToAsync<MatchConcludedState>(destroyCancellationToken).Forget();
        }

        #endregion
    }
}
