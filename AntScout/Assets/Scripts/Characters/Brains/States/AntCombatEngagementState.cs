using AntScout.Characters.Brains.StateContext;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an ant actively engages a hostile target in tactical combat.
    /// Manages offensive action kit execution, mobility closures, and survivability retreats.
    /// Grounded in Single Responsibility (SRP).
    /// </summary>
    public sealed class AntCombatEngagementState : StateBase<AntAiContext>
    {
        public override void OnEnter(AntAiContext context)
        {
            if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }
        }

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
            var target = context.TargetEnemy;
            var def = context.Definition;
            float retreatRatio = def != null ? def.RetreatHealthRatio : 0.3f;

            // Target lost / destroyed check
            if (target == null || !target.IsAttackable || target.Transform == null)
            {
                context.ClearTargetEnemy();
                if (context.Character.NormalizedHealth <= retreatRatio)
                {
                    context.StateMachine.TransitionTo<AntRetreatState>();
                }
                else
                {
                    context.RollNewRoamingLeg();
                    context.StateMachine.TransitionTo<AntScoutingFieldState>();
                }
                return;
            }

            // Low health retreat check
            if (context.Character.NormalizedHealth <= retreatRatio)
            {
                context.StateMachine.TransitionTo<AntRetreatState>();
                return;
            }

            Vector3 selfPos = context.Character.transform.position;
            Vector3 enemyPos = target.Transform.position;
            Vector3 diff = enemyPos - selfPos;
            diff.y = 0f;
            float planarDist = diff.magnitude;

            float leashRadius = def != null ? def.LeashRadius : 14.0f;
            if (planarDist > leashRadius)
            {
                context.ClearTargetEnemy();
                if (context.Character.NormalizedHealth <= retreatRatio)
                {
                    context.StateMachine.TransitionTo<AntRetreatState>();
                }
                else
                {
                    context.RollNewRoamingLeg();
                    context.StateMachine.TransitionTo<AntScoutingFieldState>();
                }
                return;
            }

            Vector3 aimDir = planarDist > 0.001f ? diff / planarDist : context.Character.transform.forward;

            // Mobility closure: Dash if equipped and allowed
            if (def != null && def.UseMobilityToEngage && def.UtilityAction != null && planarDist > 4.5f)
            {
                context.Character.TryExecuteAction(def.UtilityAction, aimDir);
            }

            // Offensive cast: Secondary ability
            if (def != null && def.SecondaryAction != null && planarDist <= def.SecondaryAction.Range)
            {
                context.Character.TryExecuteAction(def.SecondaryAction, aimDir);
            }

            // Offensive cast: Primary ability
            if (def != null && def.PrimaryAction != null && planarDist <= def.PrimaryAction.Range)
            {
                context.Character.TryExecuteAction(def.PrimaryAction, aimDir);
            }

            float minStopDistance = (def != null && def.PrimaryAction != null)
                ? def.PrimaryAction.Range * 0.75f
                : (def != null ? def.ArrivalDistance : 1.2f);

            if (planarDist > minStopDistance)
            {
                context.SetMovement(aimDir, aimDir);
            }
            else
            {
                context.SetMovement(Vector3.zero, aimDir);
            }
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
