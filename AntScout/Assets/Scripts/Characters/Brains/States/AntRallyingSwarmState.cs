using AntScout.Characters.Brains.StateContext;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an ant rallies colony reinforcements by laying an Alarm Pheromone Trail
    /// from the discovered danger site back to the home nest depot.
    /// Grounded in Single Responsibility (SRP): Manages alarm highway paving and homeward sprint.
    /// </summary>
    public sealed class AntRallyingSwarmState : StateBase<AntAiContext>
    {
        public override void OnEnter(AntAiContext context)
        {
            context.ClearTargetFood();

            var def = context.Definition;
            if (def != null && def.AlarmTrailAction != null && context.Character.Gland != null)
            {
                context.Character.Gland.StartLayingTrail(def.AlarmTrailAction);
            }

            // Optional mobility burst to disengage rapidly toward home depot
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

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
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

            // Reached home depot: alarm highway successfully connects danger site to the colony
            if (distToNest <= arrivalDist)
            {
                context.StateMachine.TransitionTo<AntRestingAtNestState>();
                return;
            }

            Vector3 moveDir = diff.normalized;
            context.SetMovement(moveDir, moveDir);
        }

        public override void OnExit(AntAiContext context)
        {
            if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }
        }
    }
}
