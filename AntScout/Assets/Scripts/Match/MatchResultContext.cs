using System;

namespace AntScout.Match
{
    /// <summary>
    /// Immutable context summarizing the outcome of a completed match.
    /// Tracks both real-world calendar timestamps (UTC) and simulation clocks (PgsTime)
    /// to support backend analytics and pause-immune StarCraft-style active match timing.
    /// </summary>
    public readonly struct MatchResultContext
    {
        public readonly MatchResult Result;
        public readonly int WinningTeamId;
        public readonly int TotalBiomassHarvested;

        /// <summary>
        /// Real-world calendar timestamp (UTC) when active gameplay began.
        /// </summary>
        public readonly DateTimeOffset StartTimeUtc;

        /// <summary>
        /// Real-world calendar timestamp (UTC) when the match concluded.
        /// </summary>
        public readonly DateTimeOffset EndTimeUtc;

        /// <summary>
        /// Simulation timestamp in seconds (via <see cref="PGS.Core.Time.PgsTime.ElapsedTime"/>) when active gameplay began.
        /// </summary>
        public readonly float StartSimulationTime;

        /// <summary>
        /// Simulation timestamp in seconds (via <see cref="PGS.Core.Time.PgsTime.ElapsedTime"/>) when the match concluded.
        /// </summary>
        public readonly float EndSimulationTime;

        /// <summary>
        /// Active gameplay duration in seconds, excluding pauses (StarCraft RTS standard).
        /// </summary>
        public float ActivePlayDuration => EndSimulationTime - StartSimulationTime;

        /// <summary>
        /// Total real-world wall-clock elapsed time, including any pause menus.
        /// </summary>
        public TimeSpan WallClockDuration => EndTimeUtc - StartTimeUtc;

        public DateTime StartTime => StartTimeUtc.UtcDateTime;
        public DateTime EndTime => EndTimeUtc.UtcDateTime;

        public MatchResultContext(
            MatchResult result,
            int winningTeamId,
            int totalBiomassHarvested,
            DateTimeOffset startTimeUtc,
            DateTimeOffset endTimeUtc,
            float startSimulationTime,
            float endSimulationTime)
        {
            Result = result;
            WinningTeamId = winningTeamId;
            TotalBiomassHarvested = totalBiomassHarvested;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            StartSimulationTime = startSimulationTime;
            EndSimulationTime = endSimulationTime;
        }
    }
}
