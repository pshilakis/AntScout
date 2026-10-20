using System;
using System.Collections.Generic;
using System.Threading;
using AntScout.Match;
using AntScout.Swarm.Nest;
using Cysharp.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AntScout.Backend
{
    /// <summary>
    /// Component responsible for recording RTS match results and transmitting telemetry to Microsoft PlayFab.
    /// Implements IMatchTelemetryService and observes MatchDirector.OnMatchConcluded.
    /// Follows SOLID principles: DIP (implements interface), SRP (telemetry dispatch only).
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayFabMatchTelemetryService : MonoBehaviour, IMatchTelemetryService
    {
        #region Serialized Fields

        [Header("Configuration")]
        [Tooltip("PlayFab configuration asset defining Title ID and telemetry options.")]
        [SerializeField] private PlayFabConfiguration _configuration;

        [Header("Auto-Binding Policy")]
        [Tooltip("If true, automatically hooks into MatchDirector.OnMatchConcluded on Start.")]
        [SerializeField] private bool _autoHookMatchDirector = true;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            var existing = FindFirstObjectByType<PlayFabMatchTelemetryService>();
            if (existing != null && existing != this)
            {
                Destroy(this);
                return;
            }

            if (_configuration == null)
            {
                _configuration = UnityEngine.Resources.Load<PlayFabConfiguration>("PlayFabConfiguration");
            }

            ValidateConfiguration();
            Debug.Log($"[PlayFabMatchTelemetryService] Initialized. Telemetry Enabled: {_configuration.EnableTelemetry}, TitleId: {_configuration.TitleId}");
        }

        private void Start()
        {
            if (_autoHookMatchDirector && MatchDirector.Instance != null)
            {
                MatchDirector.Instance.OnMatchConcluded += HandleMatchConcluded;
                Debug.Log("[PlayFabMatchTelemetryService] Successfully hooked into MatchDirector.OnMatchConcluded.");
            }
            else if (_autoHookMatchDirector)
            {
                Debug.LogWarning("[PlayFabMatchTelemetryService] MatchDirector.Instance is null during Start. Unable to hook conclusion event!");
            }

            // Silently begin guest authentication in background so session ticket is warm before match concludes
            if (_configuration != null && _configuration.EnableTelemetry)
            {
                PlayFabAuthService.EnsureLoggedInAsync(_configuration, this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private void OnDestroy()
        {
            if (_autoHookMatchDirector && MatchDirector.Instance != null)
            {
                MatchDirector.Instance.OnMatchConcluded -= HandleMatchConcluded;
            }
        }

        #endregion

        #region Event Handling & Transmission

        private void HandleMatchConcluded(MatchResultContext resultContext)
        {
            Debug.Log($"[PlayFabMatchTelemetryService] Match conclusion received. Result: {resultContext.Result}, Active Play: {resultContext.ActivePlayDuration:F1}s.");

            if (_configuration == null || !_configuration.EnableTelemetry)
            {
                Debug.LogWarning("[PlayFabMatchTelemetryService] Telemetry is disabled or configuration is null. Skipping dispatch.");
                return;
            }

            int playerTeamId = MatchDirector.Instance != null ? MatchDirector.Instance.PlayerTeamId : 0;
            bool isVictory = resultContext.WinningTeamId == playerTeamId;
            string outcome = isVictory ? "Victory" : (resultContext.WinningTeamId >= 0 ? "Defeat" : "Draw");

            int minutes = Mathf.FloorToInt(resultContext.ActivePlayDuration / 60f);
            int seconds = Mathf.FloorToInt(resultContext.ActivePlayDuration % 60f);

            int playerNestHp = 0;
            int enemyNestHp = 0;
            int playerWorkers = 0;
            int playerSoldiers = 0;
            if (ColonyNest.TryGetForTeam(playerTeamId, out ColonyNest playerNest) && playerNest != null)
            {
                playerNestHp = Mathf.RoundToInt(playerNest.CurrentHealth);
                playerWorkers = playerNest.ActiveWorkers.Count;
                playerSoldiers = playerNest.ActiveSoldiers.Count;
            }
            int enemyTeamId = playerTeamId == 0 ? 1 : 0;
            int enemyWorkers = 0;
            int enemySoldiers = 0;
            if (ColonyNest.TryGetForTeam(enemyTeamId, out ColonyNest enemyNest) && enemyNest != null)
            {
                enemyNestHp = Mathf.RoundToInt(enemyNest.CurrentHealth);
                enemyWorkers = enemyNest.ActiveWorkers.Count;
                enemySoldiers = enemyNest.ActiveSoldiers.Count;
            }

            string mapName = SceneManager.GetActiveScene().name;
            if (AntScout.Maps.MapManager.Instance != null && AntScout.Maps.MapManager.Instance.ActiveMap != null)
            {
                mapName = AntScout.Maps.MapManager.Instance.ActiveMap.MapName;
            }

            var telemetryContext = new MatchTelemetryContext
            {
                MatchId = Guid.NewGuid().ToString("N"),
                MatchOutcome = outcome,
                DurationSeconds = resultContext.ActivePlayDuration,
                FormattedDuration = $"{minutes:00}:{seconds:00}",
                MapName = mapName,
                PlayerRemainingNestHealth = playerNestHp,
                EnemyRemainingNestHealth = enemyNestHp,
                PlayerRemainingWorkers = playerWorkers,
                PlayerRemainingSoldiers = playerSoldiers,
                EnemyRemainingWorkers = enemyWorkers,
                EnemyRemainingSoldiers = enemySoldiers,
                WinningTeamId = resultContext.WinningTeamId,
                PlayerTeamId = playerTeamId,
                GameVersion = Application.version,
                TimestampUtc = DateTimeOffset.UtcNow.ToString("o")
            };

            RecordMatchConcludedAsync(telemetryContext, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public async UniTask RecordMatchConcludedAsync(MatchTelemetryContext context, CancellationToken ct = default)
        {
            if (_configuration == null || !_configuration.EnableTelemetry)
            {
                return;
            }

            try
            {
                // 1. Ensure authenticated
                await PlayFabAuthService.EnsureLoggedInAsync(_configuration, ct);

                // 2. Dispatch telemetry event
                var request = new WriteClientPlayerEventRequest
                {
                    EventName = "match_concluded",
                    Body = new Dictionary<string, object>
                    {
                        { "match_id", context.MatchId },
                        { "outcome", context.MatchOutcome },
                        { "duration_seconds", context.DurationSeconds },
                        { "duration_formatted", context.FormattedDuration },
                        { "map_name", context.MapName },
                        { "player_nest_hp", context.PlayerRemainingNestHealth },
                        { "enemy_nest_hp", context.EnemyRemainingNestHealth },
                        { "player_workers", context.PlayerRemainingWorkers },
                        { "player_soldiers", context.PlayerRemainingSoldiers },
                        { "enemy_workers", context.EnemyRemainingWorkers },
                        { "enemy_soldiers", context.EnemyRemainingSoldiers },
                        { "winning_team", context.WinningTeamId },
                        { "player_team", context.PlayerTeamId },
                        { "game_version", context.GameVersion },
                        { "timestamp_utc", context.TimestampUtc }
                    }
                };

                var tcs = new UniTaskCompletionSource<bool>();

                PlayFabClientAPI.WritePlayerEvent(
                    request,
                    result =>
                    {
                        Debug.Log($"[PlayFabMatchTelemetryService] Match telemetry ('match_concluded') successfully sent to PlayFab! (EventId: {result.EventId})");
                        tcs.TrySetResult(true);
                    },
                    error =>
                    {
                        Debug.LogWarning($"[PlayFabMatchTelemetryService] PlayFab event submission failed: {error.ErrorMessage} ({error.Error})");
                        tcs.TrySetResult(false);
                    }
                );

                await tcs.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException)
            {
                // Expected when quitting scene
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayFabMatchTelemetryService] Failed to record match telemetry: {ex.Message}");
            }
        }

        #endregion

        #region Validation

        private void ValidateConfiguration()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException($"[{name}] PlayFabConfiguration is unassigned on PlayFabMatchTelemetryService! Please assign the configuration asset.");
            }

            _configuration.ValidateConfiguration();
        }

        #endregion
    }
}
