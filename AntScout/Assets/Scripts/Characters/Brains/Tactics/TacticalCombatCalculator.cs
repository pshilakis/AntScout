using System;
using System.Collections.Generic;
using AntScout.Characters.Brains.StateContext;
using PGS.Combat.Targeting;

namespace AntScout.Characters.Brains.Tactics
{
    /// <summary>
    /// Composite utility calculator executing polymorphic combat decision evaluations.
    /// Grounded in Open/Closed Principle (OCP) and Dependency Inversion Principle (DIP):
    /// Operates entirely against ICombatTacticalOption contracts without hardcoded enums.
    /// </summary>
    public sealed class TacticalCombatCalculator
    {
        private readonly IReadOnlyList<ICombatTacticalOption> _tactics;

        public IReadOnlyList<ICombatTacticalOption> Tactics => _tactics;

        public TacticalCombatCalculator(IEnumerable<ICombatTacticalOption> tactics)
        {
            if (tactics == null) throw new ArgumentNullException(nameof(tactics));
            _tactics = new List<ICombatTacticalOption>(tactics).AsReadOnly();

            if (_tactics.Count == 0)
            {
                throw new InvalidOperationException("[TacticalCombatCalculator] Must be initialized with at least one ICombatTacticalOption!");
            }
        }

        public TacticalCombatCalculator(params ICombatTacticalOption[] tactics)
            : this((IEnumerable<ICombatTacticalOption>)tactics)
        {
        }

        /// <summary>
        /// Evaluates all registered tactics and executes the highest-scoring candidate.
        /// Returns true if a tactic was executed, false if all scores fell below threshold.
        /// </summary>
        public bool TryEvaluateAndExecute(AntAiContext context, IAttackable threat, float minThreshold = 0.1f)
        {
            if (context == null || threat == null || !threat.IsAttackable) return false;

            ICombatTacticalOption bestTactic = null;
            float bestScore = minThreshold;

            for (int i = 0; i < _tactics.Count; i++)
            {
                ICombatTacticalOption candidate = _tactics[i];
                float score = candidate.EvaluateScore(context, threat);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTactic = candidate;
                }
            }

            if (bestTactic != null)
            {
                bestTactic.Execute(context, threat);
                return true;
            }

            return false;
        }
    }
}
