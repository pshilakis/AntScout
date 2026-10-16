using System.Threading;
using AntScout.UI;
using Cysharp.Threading.Tasks;
using PGS.States;
using UnityEngine;

namespace AntScout.Match.States
{
    /// <summary>
    /// Initial match setup state. Explicitly orchestrates participant spawning, UI instantiation, and camera targeting
    /// before the match countdown begins, completely eliminating [DefaultExecutionOrder] race conditions.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public sealed class MatchSetupState : AsyncStateBase<MatchDirector>
    {
        public override async UniTask EnterAsync(MatchDirector director, CancellationToken ct)
        {
            // Instantiate Match UI prefab if assigned and not already present in the active scene
            if (director.MatchUiPrefab != null && Object.FindFirstObjectByType<MatchHudView>() == null)
            {
                Object.Instantiate(director.MatchUiPrefab);
                Debug.Log("[MatchSetupState] Match UI prefab instantiated.");
            }

            // Explicit deterministic spawning of all participants
            if (director.SpawnController != null)
            {
                await director.SpawnController.InitializeAndSpawnAllAsync(ct);
                director.SpawnController.SetAllCharactersLocomotionLocked(true);
            }

            Debug.Log("[MatchSetupState] Setup complete. All participants spawned and locked.");

            // Advance to the pre-match countdown phase
            await director.StateMachine.TransitionToAsync<MatchPreCountdownState>(ct);
        }
    }
}
