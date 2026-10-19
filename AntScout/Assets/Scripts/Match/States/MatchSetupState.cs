using System;
using System.Threading;
using AntScout.Maps;
using AntScout.Resources.Components;
using AntScout.Swarm.Nest;
using AntScout.UI;
using Cysharp.Threading.Tasks;
using PGS.States;
using UnityEngine;

namespace AntScout.Match.States
{
    /// <summary>
    /// Initial match setup state. Explicitly orchestrates participant spawning, UI instantiation, and camera targeting
    /// before the match countdown begins, completely eliminating [DefaultExecutionOrder] race conditions.
    /// Strictly adheres to Single Responsibility (SRP), Open/Closed (OCP), and Dependency Inversion (DIP).
    /// </summary>
    public sealed class MatchSetupState : AsyncStateBase<MatchDirector>
    {
        public override async UniTask EnterAsync(MatchDirector director, CancellationToken ct)
        {
            // 0. Resolve and validate MapManager
            if (MapManager.Instance == null)
            {
                MapManager sceneManager = UnityEngine.Object.FindFirstObjectByType<MapManager>();
                if (sceneManager == null)
                {
                    throw new InvalidOperationException(
                        "[MatchSetupState] MapManager is missing from the scene! " +
                        "Please use 'AntScout > Setup > Setup MapManager in Current Scene' from the Unity menu bar to add it to [ Managers ].");
                }
            }

            // 1. Resolve and validate colony nests
            int playerTeamId = director.PlayerTeamId;
            if (!ColonyNest.TryGetForTeam(playerTeamId, out ColonyNest playerNest) || playerNest == null)
            {
                throw new InvalidOperationException($"[MatchSetupState] Player ColonyNest (Team {playerTeamId}) is missing or not registered!");
            }

            ColonyNest enemyNest = null;
            foreach (var nest in ColonyNest.ActiveNests)
            {
                if (nest != null && nest.ColonyTeamId != playerTeamId)
                {
                    enemyNest = nest;
                    break;
                }
            }

            if (enemyNest == null)
            {
                throw new InvalidOperationException("[MatchSetupState] Enemy ColonyNest is missing or not registered!");
            }

            // 2. Explicitly bind Colony Nests to ResourceSpawnManager
            if (ResourceSpawnManager.Instance != null)
            {
                ResourceSpawnManager.Instance.BindColonyNests(ColonyNest.ActiveNests);
            }

            // 3. Instantiate and bind Match UI
            MatchHudView hudView = UnityEngine.Object.FindFirstObjectByType<MatchHudView>();
            if (hudView == null && director.MatchUiPrefab != null)
            {
                GameObject hudObj = UnityEngine.Object.Instantiate(director.MatchUiPrefab);
                hudView = hudObj.GetComponentInChildren<MatchHudView>();
                Debug.Log("[MatchSetupState] Match UI prefab instantiated.");
            }

            if (hudView != null)
            {
                hudView.BindNests(playerNest, enemyNest);
            }

            // Bind nest visuals (styles initial workers/soldiers and hooks ongoing spawns)
            director.RefreshAllNestVisualBindings();

            // 4. Explicit deterministic spawning of all participants
            if (director.SpawnController != null)
            {
                await director.SpawnController.InitializeAndSpawnAllAsync(ct);
                director.SpawnController.SetAllCharactersLocomotionLocked(true);
            }

            Debug.Log("[MatchSetupState] Setup complete. All nests registered, UI bound, and participants spawned.");

            // Advance to the pre-match countdown phase
            await director.StateMachine.TransitionToAsync<MatchPreCountdownState>(ct);
        }
    }
}
