namespace AntScout.UI.Overhead
{
    /// <summary>
    /// Explicit visibility policies for overhead floating vitals bars.
    /// Decouples event-based combat linger from state-based wounded detection.
    /// </summary>
    public enum HealthBarVisibilityPolicy
    {
        /// <summary>
        /// Permanently visible while the entity is alive.
        /// </summary>
        AlwaysVisible = 0,

        /// <summary>
        /// State-based: Visible whenever CurrentHealth &lt; MaxHealth. Fades out when healed to 100%.
        /// </summary>
        InjuredOnly = 1,

        /// <summary>
        /// Event-based: Pops into view upon taking damage and stays visible for a combat linger duration.
        /// </summary>
        CombatLinger = 2,

        /// <summary>
        /// Hybrid: Visible during active combat OR whenever below 100% health.
        /// </summary>
        CombatOrInjured = 3
    }
}
