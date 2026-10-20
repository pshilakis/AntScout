using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace AntScout.Backend
{
    /// <summary>
    /// Pure data transfer object (DTO) describing telemetry recorded when an RTS skirmish concludes.
    /// Grounded in Interface Segregation (ISP) and Dependency Inversion (DIP).
    /// </summary>
    [Serializable]
    public struct MatchTelemetryContext
    {
        public string MatchId;
        public string MatchOutcome; // "Victory", "Defeat", "Forfeit"
        public float DurationSeconds;
        public string FormattedDuration;
        public string MapName;
        public int PlayerRemainingNestHealth;
        public int EnemyRemainingNestHealth;
        public int PlayerRemainingWorkers;
        public int PlayerRemainingSoldiers;
        public int EnemyRemainingWorkers;
        public int EnemyRemainingSoldiers;
        public int WinningTeamId;
        public int PlayerTeamId;
        public string GameVersion;
        public string TimestampUtc;
    }

    /// <summary>
    /// Contract for recording match lifecycle and telemetry data.
    /// High-level gameplay systems depend only on this abstraction, decoupling game logic from PlayFab SDK.
    /// </summary>
    public interface IMatchTelemetryService
    {
        UniTask RecordMatchConcludedAsync(MatchTelemetryContext context, CancellationToken ct = default);
    }
}
