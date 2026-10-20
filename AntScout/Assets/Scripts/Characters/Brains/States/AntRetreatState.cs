using AntScout.Characters.Brains.StateContext;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an injured ant disengages and flees back to the home nest depot.
    /// Uses mobility dash escapes when available and transitions to resting upon arrival.
    /// Grounded in Single Responsibility (SRP).
    /// </summary>
    public sealed class AntRetreatState : StateBase<AntAiContext>
    {
        public override void OnEnter(AntAiContext context)
        {
            var def = context.Definition;

            // If forced to retreat from battle, lay an alarm trail back to the home nest to alert the colony
            if (context.Priorities.AlarmTrailOnCombat > 0.5f && def != null && def.AlarmTrailAction != null &&
                context.Character.Gland != null && context.Character.Gland.NormalizedCapacity >= 0.2f)
            {
                context.Character.Gland.StartLayingTrail(def.AlarmTrailAction);
            }
            else if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }

            context.ClearTargetEnemy();
            context.ClearTargetFood();

            if (context.HomeNest != null && def != null && def.UseMobilityToRetreat && def.UtilityAction != null)
            {
                Vector3 fleeDir = context.HomeNest.SpawnPosition - context.Character.transform.position;
                fleeDir.y = 0f;
                if (fleeDir.sqrMagnitude > 0.001f)
                {
                    context.Character.TryExecuteAction(def.UtilityAction, fleeDir.normalized);
                }
            }
        }

        public override void OnExit(AntAiContext context)
        {
            if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }
        }

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
            // Boundary horizon and physical obstacle deflection check
            context.CheckObstacleDeflection(deltaTime);

            if (context.HomeNest == null)
            {
                context.StopMovement();
                return;
            }

            Vector3 depotPos = context.HomeNest.SpawnPosition;
            Vector3 diff = depotPos - context.Character.transform.position;
            diff.y = 0f;
            float distToNest = diff.magnitude;

            var def = context.Definition;
            float arrivalDist = def != null ? def.ArrivalDistance : 1.2f;

            if (distToNest <= arrivalDist)
            {
                context.StateMachine.TransitionTo<AntRestingAtNestState>();
                return;
            }

            Vector3 moveDir = context.ComputeSteeredMovement(diff.normalized);
            context.SetMovement(moveDir, moveDir);
        }
    }
}
