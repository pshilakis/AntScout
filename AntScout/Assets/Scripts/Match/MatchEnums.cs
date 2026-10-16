namespace AntScout.Match
{
    /// <summary>
    /// Lifecycle states for an RTS match session.
    /// </summary>
    public enum MatchState
    {
        PreMatch = 0,    // Initial countdown or camera framing before control is given
        InProgress = 1,  // Active match gameplay
        Concluded = 2    // Match has reached victory, defeat, or draw
    }

    /// <summary>
    /// Final victory/defeat outcomes for a completed match.
    /// </summary>
    public enum MatchResult
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
        Draw = 3
    }
}
