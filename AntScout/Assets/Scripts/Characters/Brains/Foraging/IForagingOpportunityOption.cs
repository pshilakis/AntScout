using AntScout.Characters.Brains.StateContext;
using AntScout.Resources.Components;

namespace AntScout.Characters.Brains.Foraging
{
    /// <summary>
    /// Contract for a self-contained, polymorphic foraging opportunity evaluator.
    /// Grounded in Open/Closed Principle (OCP) and Single Responsibility Principle (SRP):
    /// Allows novel food exploitation tactics (paving highways, bookmarking, local harvesting)
    /// to be evaluated dynamically without hardcoded switches or enums.
    /// </summary>
    public interface IForagingOpportunityOption
    {
        /// <summary>
        /// Diagnostic name of this foraging opportunity option.
        /// </summary>
        string OptionName { get; }

        /// <summary>
        /// Computes a normalized utility score (0.0 to 1.0) evaluating how suitable
        /// this foraging action is given the candidate food and current colony mining context.
        /// </summary>
        float EvaluateScore(AntAiContext context, FoodSource candidateFood, FoodSource activeHarvestFood);

        /// <summary>
        /// Executes the foraging action, configuring the blackboard and transitioning states.
        /// </summary>
        void Execute(AntAiContext context, FoodSource candidateFood);
    }
}
