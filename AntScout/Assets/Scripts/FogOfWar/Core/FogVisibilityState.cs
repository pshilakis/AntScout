namespace AntScout.FogOfWar.Core
{
    /// <summary>
    /// Three-tier Fog of War visibility states adhering to real-time strategy industry standards (StarCraft, Age of Empires).
    /// </summary>
    public enum FogVisibilityState : byte
    {
        /// <summary>
        /// Pitch black shroud. Never scouted by the colony.
        /// </summary>
        Unexplored = 0,

        /// <summary>
        /// Previously discovered territory. Static terrain/landmarks are visible, but dynamic enemy entities are hidden.
        /// </summary>
        Explored = 1,

        /// <summary>
        /// Actively within friendly line-of-sight. Fully lit, showing real-time enemy movements.
        /// </summary>
        Visible = 2
    }
}
