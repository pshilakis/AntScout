using System;
using System.Collections.Generic;
using AntScout.Characters.Brains.StateContext;
using AntScout.Resources.Components;

namespace AntScout.Characters.Brains.Foraging
{
    /// <summary>
    /// Composite utility calculator evaluating polymorphic foraging opportunity options.
    /// Grounded in Open/Closed Principle (OCP) and Dependency Inversion Principle (DIP):
    /// Decouples food exploitation decisions from rigid enums and hardcoded switches.
    /// </summary>
    public sealed class ForagingOpportunityCalculator
    {
        private readonly IReadOnlyList<IForagingOpportunityOption> _options;

        public IReadOnlyList<IForagingOpportunityOption> Options => _options;

        public ForagingOpportunityCalculator(IEnumerable<IForagingOpportunityOption> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = new List<IForagingOpportunityOption>(options).AsReadOnly();

            if (_options.Count == 0)
            {
                throw new InvalidOperationException("[ForagingOpportunityCalculator] Must be initialized with at least one IForagingOpportunityOption!");
            }
        }

        public ForagingOpportunityCalculator(params IForagingOpportunityOption[] options)
            : this((IEnumerable<IForagingOpportunityOption>)options)
        {
        }

        /// <summary>
        /// Evaluates all registered foraging options against candidate food and active colony harvest,
        /// executing the highest-scoring candidate above the minimum viability threshold.
        /// </summary>
        public bool TryEvaluateAndExecute(AntAiContext context, FoodSource candidateFood, float minThreshold = 0.1f)
        {
            if (context == null || candidateFood == null || !candidateFood.CanHarvest) return false;

            FoodSource activeHarvestFood = context.GetHighestYieldActiveFoodSource();

            IForagingOpportunityOption bestOption = null;
            float bestScore = minThreshold;

            for (int i = 0; i < _options.Count; i++)
            {
                IForagingOpportunityOption candidate = _options[i];
                float score = candidate.EvaluateScore(context, candidateFood, activeHarvestFood);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestOption = candidate;
                }
            }

            if (bestOption != null)
            {
                bestOption.Execute(context, candidateFood);
                return true;
            }

            return false;
        }
    }
}
