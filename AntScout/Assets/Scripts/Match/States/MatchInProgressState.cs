using System.Threading;
using AntScout.Swarm.Nest;
using Cysharp.Threading.Tasks;
using PGS.States;
using UnityEngine;

namespace AntScout.Match.States
{
    /// <summary>
    /// Active RTS skirmish match state.
    /// Unlocks participant controls, tracks active play simulation time,
    /// and listens to ColonyNest destruction events to trigger Victory or Defeat.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public sealed class MatchInProgressState : AsyncStateBase<MatchDirector>
    {
        private MatchDirector _director;

        public override UniTask EnterAsync(MatchDirector director, CancellationToken ct)
        {
            _director = director;
            director.RecordMatchStartTime();

            if (director.AutoLockParticipantsOnConclusion && director.SpawnController != null)
            {
                director.SpawnController.SetAllCharactersLocomotionLocked(false);
            }

            ColonyNest.OnAnyNestDestroyed += HandleNestDestroyed;
            Debug.Log("[MatchInProgressState] Match Started! Controls unlocked. Battle commenced.");

            return UniTask.CompletedTask;
        }

        public override UniTask ExitAsync(MatchDirector director, CancellationToken ct)
        {
            ColonyNest.OnAnyNestDestroyed -= HandleNestDestroyed;
            _director = null;
            return UniTask.CompletedTask;
        }

        private void HandleNestDestroyed(ColonyNest destroyedNest)
        {
            if (_director == null) return;
            if (destroyedNest == null) return;

            int teamId = destroyedNest.ColonyTeamId;
            int playerTeamId = _director.PlayerTeamId;

            Debug.Log($"[MatchInProgressState] Nest destroyed for Team {teamId} ('{destroyedNest.name}'). Evaluating match outcome...");

            MatchResult result;
            int winningTeam;

            if (teamId == playerTeamId)
            {
                // Player's nest fell -> Defeat
                result = MatchResult.Defeat;
                winningTeam = (playerTeamId == 0) ? 1 : 0;
            }
            else
            {
                // Enemy's nest fell -> Victory
                result = MatchResult.Victory;
                winningTeam = playerTeamId;
            }

            _director.PrepareConclusion(result, winningTeam);
            _director.StateMachine.TransitionToAsync<MatchConcludedState>(_director.destroyCancellationToken).Forget();
        }
    }
}
