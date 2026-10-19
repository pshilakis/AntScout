using AntScout.Characters;
using AntScout.Characters.Brains;
using UnityEngine;

namespace AntScout.Characters.Brains.Behaviors
{
    /// <summary>
    /// Outcome of a single behavior evaluation tick during an active outward expedition.
    /// </summary>
    public enum BehaviorResult
    {
        [Tooltip("Behavior remains active and ongoing.")]
        Ongoing = 0,

        [Tooltip("Objective completed (e.g. food reached); returning home while actively paving a scent trail.")]
        ReturnHomeWithTrail = 1,

        [Tooltip("Sortie boundary reached (e.g. timeout or max distance); returning home empty-handed with gland off.")]
        ReturnHomeEmpty = 2
    }

    /// <summary>
    /// Polymorphic macro behavior evaluated during an AI Hero's outward expedition from the nest.
    /// Follows the Strategy Pattern (GoF) and Open/Closed Principle (OCP).
    /// Stateless policy evaluator to support safe sharing across multiple brain instances.
    /// </summary>
    public interface IAiBehavior
    {
        /// <summary>
        /// Called when the AI hero embarks from the nest to begin executing this behavior.
        /// </summary>
        void OnBehaviorStarted(AntAiBrain brain, AntCharacter character);

        /// <summary>
        /// Evaluates one frame/tick of this behavior, returning the updated sortie state.
        /// </summary>
        BehaviorResult EvaluateBehavior(AntAiBrain brain, AntCharacter character, float deltaTime);

        /// <summary>
        /// Called when the AI hero completes or cancels this behavior and returns to the nest depot.
        /// </summary>
        void OnBehaviorEnded(AntAiBrain brain, AntCharacter character);
    }
}
