using System;
using AntScout.Characters;
using AntScout.Characters.Brains;
using AntScout.Resources.Components;
using UnityEngine;

namespace AntScout.Characters.Brains.Behaviors
{
    /// <summary>
    /// Autonomous economic foraging behavior for AI Heroes (e.g. Scout Ant).
    /// Explores the backyard under sensory perception limits, discovers harvestable food bonanzas,
    /// and paves a recruitment scent trail on the return trip to mobilize the worker swarm.
    /// Grounded in Single Responsibility (SRP) and Open/Closed Principle (OCP).
    /// </summary>
    [Serializable]
    public class ResourceScoutBehavior : IAiBehavior
    {
        [Tooltip("If true, explores empty-handed with gland OFF and only paves the recruitment scent trail on the return trip from discovered food.")]
        [SerializeField] private bool _paveOnReturn = true;

        public bool PaveOnReturn => _paveOnReturn;

        public void OnBehaviorStarted(AntAiBrain brain, AntCharacter character)
        {
            brain.ResetBehaviorTimer();

            // Gland remains OFF during search to conserve precious pheromone reserves
            if (character.Gland != null && character.Gland.IsLayingTrail)
            {
                character.Gland.StopLayingTrail();
            }
        }

        public BehaviorResult EvaluateBehavior(AntAiBrain brain, AntCharacter character, float deltaTime)
        {
            var def = character.Definition;
            if (def == null || brain.HomeNest == null)
            {
                return BehaviorResult.ReturnHomeEmpty;
            }

            float perceptionMult = brain.PerceptionMultiplier;
            float foodSenseRadius = def.FoodDetectionRadius * perceptionMult;

            // Sensory check: Discover unharvested food within sensory radius
            FoodSource food = FoodSource.FindClosest(character.transform.position, foodSenseRadius);
            if (food != null && food.CanHarvest)
            {
                brain.TargetFood = food;
                Vector3 foodDiff = food.WorldPosition - character.transform.position;
                foodDiff.y = 0f;
                float distToFood = foodDiff.magnitude;

                if (distToFood <= brain.ArrivalDistance)
                {
                    // Reached the food! Activate recruitment trail to pave back to the nest
                    if (_paveOnReturn && def.RecruitmentTrailAction != null && character.Gland != null)
                    {
                        character.Gland.StartLayingTrail(def.RecruitmentTrailAction);
                    }
                    return BehaviorResult.ReturnHomeWithTrail;
                }

                // Move toward food bonanza
                Vector3 moveDir = foodDiff.normalized;
                brain.SetDesiredMovement(moveDir, moveDir);
                return BehaviorResult.Ongoing;
            }

            // No food detected: advance leg timer, check completion for next leg, and continue organic wandering
            brain.IncrementBehaviorTimer(deltaTime);
            brain.CheckLegCompletion();

            Vector3 wanderDir = brain.ComputeRoamingMovement();
            brain.SetDesiredMovement(wanderDir, wanderDir);
            return BehaviorResult.Ongoing;
        }

        public void OnBehaviorEnded(AntAiBrain brain, AntCharacter character)
        {
            if (character.Gland != null && character.Gland.IsLayingTrail)
            {
                character.Gland.StopLayingTrail();
            }
        }
    }
}
