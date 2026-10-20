using AntScout.Characters.Brains.StateContext;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an ant maneuvers toward a discovered food source.
    /// Manages arrival distance thresholds and dynamically re-evaluates if threats contest the cache.
    /// Grounded in Single Responsibility (SRP).
    /// </summary>
    public sealed class AntMovingToFoodState : StateBase<AntAiContext>
    {
        private static readonly int s_obstacleLayerMask = LayerMask.GetMask("Obstacle") != 0 ? LayerMask.GetMask("Obstacle") : (1 << 6);

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
            // Boundary horizon and physical obstacle deflection check
            context.CheckObstacleDeflection(deltaTime);

            var food = context.TargetFood;
            if (food == null || !food.CanHarvest)
            {
                context.ClearTargetFood();
                context.StateMachine.TransitionTo<AntScoutingFieldState>();
                return;
            }

            Vector3 selfPos = context.Character.transform.position;
            Vector3 diff = food.WorldPosition - selfPos;
            diff.y = 0f;
            float distToFood = diff.magnitude;

            var def = context.Definition;
            float arrivalDist = def != null ? def.ArrivalDistance : 1.2f;

            if (distToFood <= arrivalDist)
            {
                // Food reached! Evaluate economic foraging options (Pave Highway vs Bookmark & Continue Scouting)
                if (context.ForagingCalculator.TryEvaluateAndExecute(context, food))
                {
                    return;
                }

                // Fallback: claim food and resume scouting
                context.MarkFoodReported(food);
                context.ClearTargetFood();
                context.StateMachine.TransitionTo<AntScoutingFieldState>();
                return;
            }

            if (context.IsStallBreaking)
            {
                // Active stall escape maneuver has absolute priority; do not overwrite escape locomotion!
                return;
            }

            Vector3 moveDir = context.ComputeSteeredMovement(diff.normalized);
            context.SetMovement(moveDir, moveDir);
        }
    }
}
