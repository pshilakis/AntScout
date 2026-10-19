using System;
using AntScout.Characters;
using AntScout.Characters.Brains;
using AntScout.Resources.Components;
using UnityEngine;

namespace AntScout.Characters.Brains.Behaviors
{
    /// <summary>
    /// Autonomous territory patrol and aggressive food-seeking behavior for AI Heroes (e.g. Soldier / Vanguard).
    /// Actively seeks out food caches across the yard to secure resources for the colony, pushes into contested
    /// midfield zones with high tactical aggression, and paves a scent trail back to base upon claiming food.
    /// Grounded in Single Responsibility (SRP) and Open/Closed Principle (OCP).
    /// </summary>
    [Serializable]
    public class PerimeterPatrolBehavior : IAiBehavior
    {
        [Tooltip("If true, paves a recruitment scent trail back to base upon discovering and reaching food to mobilize the worker swarm.")]
        [SerializeField] private bool _paveOnFoodDiscovered = true;

        [Tooltip("If true, activates the alarm scent trail right out of nest spawn to draw a defensive tripwire.")]
        [SerializeField] private bool _layAlarmTrailOnEmbark = false;

        public bool PaveOnFoodDiscovered => _paveOnFoodDiscovered;
        public bool LayAlarmTrailOnEmbark => _layAlarmTrailOnEmbark;

        public void OnBehaviorStarted(AntAiBrain brain, AntCharacter character)
        {
            brain.RollNewRoamingLeg();

            var def = character.Definition;
            if (_layAlarmTrailOnEmbark && def != null && def.AlarmTrailAction != null && character.Gland != null)
            {
                character.Gland.StartLayingTrail(def.AlarmTrailAction);
            }
            else if (character.Gland != null && character.Gland.IsLayingTrail)
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

            // Sensory check: Soldier ants actively seek out food to secure resources for the colony
            FoodSource food = FoodSource.FindClosest(character.transform.position, foodSenseRadius);
            if (food != null && food.CanHarvest)
            {
                brain.TargetFood = food;
                Vector3 foodDiff = food.WorldPosition - character.transform.position;
                foodDiff.y = 0f;
                float distToFood = foodDiff.magnitude;

                if (distToFood <= brain.ArrivalDistance)
                {
                    // Reached the food cache! Pave recruitment trail back to nest
                    if (_paveOnFoodDiscovered && def.RecruitmentTrailAction != null && character.Gland != null)
                    {
                        character.Gland.StartLayingTrail(def.RecruitmentTrailAction);
                    }
                    return BehaviorResult.ReturnHomeWithTrail;
                }

                // Move aggressively toward discovered food
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
