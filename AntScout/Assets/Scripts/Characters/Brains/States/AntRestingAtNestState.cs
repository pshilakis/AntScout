using AntScout.Characters.Brains.StateContext;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an ant rests in the home nest depot.
    /// Recovers vitals in the healing aura and recharges chemical gland reserves.
    /// Grounded in Single Responsibility (SRP).
    /// </summary>
    public sealed class AntRestingAtNestState : StateBase<AntAiContext>
    {
        public override void OnEnter(AntAiContext context)
        {
            context.StopMovement();
            context.ClearTargetEnemy();
            context.ClearTargetFood();

            if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }
        }

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
            if (context.HomeNest == null) return;

            Vector3 depotPos = context.HomeNest.SpawnPosition;
            Vector3 diff = depotPos - context.Character.transform.position;
            diff.y = 0f;
            float distToNest = diff.magnitude;

            var def = context.Definition;
            float arrivalDist = def != null ? def.ArrivalDistance : 1.2f;

            // Ensure ant is snugly inside the depot aura
            if (distToNest > arrivalDist)
            {
                context.SetMovement(diff.normalized, diff.normalized);
                return;
            }

            // Maintain alert, living stationary posture with antenna sampling sweeps
            context.UpdateStationaryIdle(deltaTime);

            // Vitals recovery checks
            bool glandReady = context.Character.Gland == null ||
                              context.Character.Gland.NormalizedCapacity >= (def != null ? def.MinGlandRatioToEmbark : 0.5f);

            bool healthReady = context.Character.NormalizedHealth >= 0.85f;

            if (glandReady && healthReady)
            {
                context.RollNewRoamingLeg();
                context.StateMachine.TransitionTo<AntScoutingFieldState>();
            }
        }
    }
}
