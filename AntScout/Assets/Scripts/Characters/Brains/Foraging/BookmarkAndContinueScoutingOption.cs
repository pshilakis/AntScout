using System;
using AntScout.Characters.Brains.StateContext;
using AntScout.Characters.Brains.States;
using AntScout.Resources.Components;
using UnityEngine;

namespace AntScout.Characters.Brains.Foraging
{
    /// <summary>
    /// Foraging option for bookmarking a low-yield food cache and continuing field exploration.
    /// Grounded in Single Responsibility (SRP): Protects existing high-yield mining operations from distraction.
    /// Favored when the candidate food is inferior to the colony's primary active harvest and the primary cache
    /// still contains abundant reserves.
    /// </summary>
    public sealed class BookmarkAndContinueScoutingOption : IForagingOpportunityOption
    {
        public string OptionName => "BookmarkAndContinueScouting";

        public float EvaluateScore(AntAiContext context, FoodSource candidateFood, FoodSource activeHarvestFood)
        {
            if (context == null || candidateFood == null || !candidateFood.CanHarvest) return 0f;

            // If the colony has no active harvest, any food should be paved, not ignored
            if (activeHarvestFood == null) return 0.05f;

            float candidateYield = candidateFood.HarvestDuration > 0.01f
                ? (float)candidateFood.BiomassValue / candidateFood.HarvestDuration
                : (float)candidateFood.BiomassValue;

            float activeYield = activeHarvestFood.HarvestDuration > 0.01f
                ? (float)activeHarvestFood.BiomassValue / activeHarvestFood.HarvestDuration
                : (float)activeHarvestFood.BiomassValue;

            float activeRemainingRatio = activeHarvestFood.Units.Max > 0
                ? (float)activeHarvestFood.Units.Current / activeHarvestFood.Units.Max
                : 0f;

            // If candidate is inferior to active harvest and the active cache is healthy (> 20%),
            // bookmark this candidate to protect the colony from wasteful trail switching
            if (candidateYield < activeYield - 0.2f && activeRemainingRatio >= 0.2f)
            {
                return 0.85f;
            }

            return 0.1f;
        }

        public void Execute(AntAiContext context, FoodSource candidateFood)
        {
            if (context == null || candidateFood == null) return;

            // Bookmark this low-priority food source on memory cooldown so the scout does not fixate on it
            context.MarkFoodReported(candidateFood, 60.0f);
            context.ClearTargetFood();

            // Resume autonomous field exploration
            context.RollNewRoamingLeg();
            context.StateMachine.TransitionTo<AntScoutingFieldState>();
        }
    }
}
