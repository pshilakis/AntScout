using System.Threading;
using Cysharp.Threading.Tasks;
using PGS.States;
using UnityEngine;

namespace AntScout.Match.States
{
    /// <summary>
    /// Concluded match state (Victory, Defeat, or Draw).
    /// Locks participant controls, cancels respawn timers, and broadcasts final match summary telemetry.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public sealed class MatchConcludedState : AsyncStateBase<MatchDirector>
    {
        public override UniTask EnterAsync(MatchDirector director, CancellationToken ct)
        {
            if (director.AutoLockParticipantsOnConclusion && director.SpawnController != null)
            {
                director.SpawnController.CancelAllRespawnTimers();
                director.SpawnController.SetAllCharactersLocomotionLocked(true);
            }

            director.FinalizeConclusion();
            return UniTask.CompletedTask;
        }
    }
}
