using System;
using AntScout.Characters.Brains.StateContext;
using AntScout.Characters.Brains.States;
using AntScout.Resources.Components;
using UnityEngine;

namespace AntScout.Characters.Brains.Foraging
{
    /// <summary>
    /// Foraging option that paves a full recruitment pheromone highway from the candidate food back to the home nest depot.
    /// Grounded in Single Responsibility (SRP): Evaluates economic viability and initiates AntPavingTrailHomeState.
    /// Favored when the candidate food is of equal/higher quality than the colony's current harvest,
    /// when the colony has no active food trails, or when the active high-yield cache is nearly exhausted.
    /// </summary>
    public sealed class PaveRecruitmentHighwayOption : IForagingOpportunityOption
    {
        public string OptionName => "PaveRecruitmentHighway";

        public float EvaluateScore(AntAiContext context, FoodSource candidateFood, FoodSource activeHarvestFood)
        {
            if (context == null || candidateFood == null || !candidateFood.CanHarvest) return 0f;

            var def = context.Definition;
            if (def == null || def.RecruitmentTrailAction == null || context.Character.Gland == null)
            {
                return 0f; // Unit lacks recruitment trail capabilities or chemical gland
            }

            float glandCapacity = context.Character.Gland.NormalizedCapacity;
            if (glandCapacity < 0.2f)
            {
                return 0f; // Gland lacks chemical reserves to lay a trail back to nest
            }

            float pavePriority = context.Priorities.PaveTrailOnFood;
            if (pavePriority <= 0.05f) return 0f;

            float candidateYield = candidateFood.HarvestDuration > 0.01f
                ? (float)candidateFood.BiomassValue / candidateFood.HarvestDuration
                : (float)candidateFood.BiomassValue;

            // Scenario 1: Colony has NO active recruitment trails (Any food is a great discovery)
            if (activeHarvestFood == null)
            {
                return Mathf.Clamp01(pavePriority * glandCapacity);
            }

            float activeYield = activeHarvestFood.HarvestDuration > 0.01f
                ? (float)activeHarvestFood.BiomassValue / activeHarvestFood.HarvestDuration
                : (float)activeHarvestFood.BiomassValue;

            float activeRemainingRatio = activeHarvestFood.Units.Max > 0
                ? (float)activeHarvestFood.Units.Current / activeHarvestFood.Units.Max
                : 0f;

            // Scenario 2: Active high-value cache is nearly exhausted (< 20% remaining capacity)
            // Opening a secondary/replacement highway is urgently required so the swarm does not starve when the main cache dies.
            if (activeRemainingRatio < 0.2f)
            {
                return Mathf.Clamp01(pavePriority * glandCapacity * 0.95f);
            }

            // Scenario 3: Candidate food is superior or equal in quality to current active harvest
            if (candidateYield >= activeYield - 0.2f)
            {
                return Mathf.Clamp01(pavePriority * glandCapacity);
            }

            // Scenario 4: Candidate food is strictly inferior in quality, and active high-value cache has abundant reserves
            // Prevent economic cannibalization: do not draw a new trail that would pull workers away from the gold mine!
            return 0.05f;
        }

        public void Execute(AntAiContext context, FoodSource candidateFood)
        {
            if (context == null || candidateFood == null) return;

            context.TargetFood = candidateFood;
            context.StateMachine.TransitionTo<AntPavingTrailHomeState>();
        }
    }
}
