using System.Threading;
using Cysharp.Threading.Tasks;
using PGS.States;
using UnityEngine;

namespace AntScout.Match.States
{
    /// <summary>
    /// Pre-match countdown state (e.g. 3... 2... 1...).
    /// Keeps participant controls locked while decrementing countdown telemetry.
    /// Transitions to MatchInProgressState upon countdown completion.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public sealed class MatchPreCountdownState : AsyncStateBase<MatchDirector>
    {
        public override async UniTask EnterAsync(MatchDirector director, CancellationToken ct)
        {
            if (director.AutoLockParticipantsOnConclusion && director.SpawnController != null)
            {
                director.SpawnController.SetAllCharactersLocomotionLocked(true);
            }

            float duration = director.PreMatchDuration;
            director.SetPreMatchRemaining(duration);

            Debug.Log($"[MatchPreCountdownState] Pre-match countdown started ({duration:F1}s). Participants locked.");

            if (duration <= 0f)
            {
                await director.StateMachine.TransitionToAsync<MatchInProgressState>(ct);
            }
        }

        public override void OnUpdate(MatchDirector director, float deltaTime)
        {
            if (director.PreMatchRemaining <= 0f) return;

            director.DecrementPreMatchTimer(deltaTime);

            if (director.PreMatchRemaining <= 0f)
            {
                director.StateMachine.TransitionToAsync<MatchInProgressState>(director.destroyCancellationToken).Forget();
            }
        }
    }
}
