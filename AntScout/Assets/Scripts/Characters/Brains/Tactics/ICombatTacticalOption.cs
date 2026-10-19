using AntScout.Characters.Brains.StateContext;
using PGS.Combat.Targeting;

namespace AntScout.Characters.Brains.Tactics
{
    /// <summary>
    /// Contract for a self-contained, polymorphic combat tactical evaluator and executor.
    /// Grounded in Open/Closed Principle (OCP) and Single Responsibility Principle (SRP):
    /// Allows novel tactical behaviors (solo assault, swarm rally, evasion, kiting, ambushing)
    /// to be introduced without modifying enum switches or core state machines.
    /// </summary>
    public interface ICombatTacticalOption
    {
        /// <summary>
        /// Human-readable diagnostic name for the tactic.
        /// </summary>
        string TacticName { get; }

        /// <summary>
        /// Computes a normalized utility score (0.0 to 1.0) evaluating how suitable
        /// this tactical option is given the current context and target threat.
        /// </summary>
        float EvaluateScore(AntAiContext context, IAttackable threat);

        /// <summary>
        /// Executes this tactic, configuring the blackboard and transitioning the state machine.
        /// </summary>
        void Execute(AntAiContext context, IAttackable threat);
    }
}
