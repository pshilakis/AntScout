using PGS.Combat.Targeting;

namespace AntScout.Core.Interfaces
{
    /// <summary>
    /// Extends <see cref="IFactionMember"/> (com.pgs.combat) with an explicit Colony Team ID
    /// so opposing ant colonies (Player vs AI or Player vs Player) evaluate affiliation with 0 GC allocations.
    /// </summary>
    public interface IColonyFactionMember : IFactionMember
    {
        /// <summary>
        /// Numeric colony team identifier (e.g., 0 = Colony 1 / Black, 1 = Colony 2 / Red, -1 = Neutral).
        /// </summary>
        int ColonyTeamId { get; }
    }

    /// <summary>
    /// Stateless, zero-allocation evaluator for <see cref="IColonyFactionMember"/> relationships.
    /// </summary>
    public static class ColonyAffiliation
    {
        public static TargetAffiliation Evaluate(IColonyFactionMember self, IFactionMember other)
        {
            if (self == null || other == null)
            {
                return TargetAffiliation.Neutral;
            }

            if (ReferenceEquals(self, other))
            {
                return TargetAffiliation.Self;
            }

            if (other is IColonyFactionMember otherColony)
            {
                if (self.ColonyTeamId < 0 || otherColony.ColonyTeamId < 0)
                {
                    return TargetAffiliation.Neutral;
                }

                return self.ColonyTeamId == otherColony.ColonyTeamId
                    ? TargetAffiliation.Ally
                    : TargetAffiliation.Enemy;
            }

            return TargetAffiliation.Enemy;
        }
    }
}
