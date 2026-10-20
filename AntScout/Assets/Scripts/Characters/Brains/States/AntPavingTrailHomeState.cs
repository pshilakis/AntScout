using AntScout.Characters.Brains.StateContext;
using AntScout.Resources.Components;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an ant lays a channeled recruitment pheromone trail from a discovered food source.
    /// Supports Arterial Branching (Steiner Tree): If another actively-mined food source is closer than the home nest,
    /// paves a direct branch connector to that node to create an efficient multi-node highway network with minimal chemical cost.
    /// Otherwise, paves the full highway back to the home nest depot.
    /// Grounded in Single Responsibility (SRP).
    /// </summary>
    public sealed class AntPavingTrailHomeState : StateBase<AntAiContext>
    {
        private static readonly int s_obstacleLayerMask = LayerMask.GetMask("Obstacle") != 0 ? LayerMask.GetMask("Obstacle") : (1 << 6);
        public override void OnEnter(AntAiContext context)
        {
            var def = context.Definition;
            if (def != null && def.RecruitmentTrailAction != null && context.Character.Gland != null)
            {
                context.Character.Gland.StartLayingTrail(def.RecruitmentTrailAction);
            }

            // Record discovered food as reported with an active cooldown so this and other sorties don't redundant-pave it
            if (context.TargetFood != null)
            {
                context.MarkFoodReported(context.TargetFood, 45f);

                // Check for a closer actively-mined resource node to create an arterial highway branch
                context.PavingDestinationFood = context.FindCloserActiveHarvestFoodSource(context.TargetFood);
            }
            else
            {
                context.ClearPavingDestinationFood();
            }
        }

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
            // Boundary horizon and physical obstacle deflection check
            context.CheckObstacleDeflection(deltaTime);

            Vector3 targetPos;
            bool isTargetingConnectingFood = false;

            if (context.PavingDestinationFood != null)
            {
                if (context.PavingDestinationFood.CanHarvest)
                {
                    targetPos = context.PavingDestinationFood.WorldPosition;
                    isTargetingConnectingFood = true;
                }
                else
                {
                    // Connected food source depleted mid-transit; fall back to home nest depot
                    context.ClearPavingDestinationFood();
                    targetPos = context.HomeNest != null ? context.HomeNest.SpawnPosition : context.Character.transform.position;
                }
            }
            else
            {
                targetPos = context.HomeNest != null ? context.HomeNest.SpawnPosition : context.Character.transform.position;
            }

            Vector3 diff = targetPos - context.Character.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;

            var def = context.Definition;
            float arrivalDist = def != null ? def.ArrivalDistance : 1.2f;

            if (dist <= arrivalDist)
            {
                if (isTargetingConnectingFood)
                {
                    FoodSource connectedFood = context.PavingDestinationFood;
                    context.ClearPavingDestinationFood();
                    context.ClearTargetFood();

                    if (connectedFood != null)
                    {
                        context.MarkFoodReported(connectedFood, 45f);
                    }

                    // Seal current trail segment at the connected node
                    if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
                    {
                        context.Character.Gland.StopLayingTrail();
                    }

                    // Vitals & Gland Check: If scout still has sufficient energy & chemical reserves,
                    // keep scouting rather than hiking all the way back to base
                    bool glandReady = context.Character.Gland == null ||
                                      context.Character.Gland.NormalizedCapacity >= (def != null ? def.MinGlandRatioToEmbark : 0.5f);
                    bool healthReady = context.Character.NormalizedHealth >= 0.85f;

                    if (glandReady && healthReady)
                    {
                        context.RollNewRoamingLeg();
                        context.StateMachine.TransitionTo<AntScoutingFieldState>();
                        return;
                    }

                    // Gland depleted or wounded; return to home nest along the active highway to recharge
                    context.StateMachine.TransitionTo<AntRestingAtNestState>();
                    return;
                }
                else
                {
                    // Full highway back to nest reached
                    context.ClearTargetFood();
                    context.StateMachine.TransitionTo<AntRestingAtNestState>();
                    return;
                }
            }

            Vector3 moveDir = diff.normalized;
            if (Physics.SphereCast(context.Character.transform.position + Vector3.up * 0.3f, 0.35f, moveDir, out RaycastHit hit, 0.8f, s_obstacleLayerMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 avoidance = (moveDir + hit.normal * 1.2f).normalized;
                avoidance.y = 0f;
                if (avoidance.sqrMagnitude > 0.01f)
                {
                    moveDir = avoidance;
                }
            }

            context.SetMovement(moveDir, moveDir);
        }

        public override void OnExit(AntAiContext context)
        {
            context.ClearPavingDestinationFood();

            if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }
        }
    }
}
