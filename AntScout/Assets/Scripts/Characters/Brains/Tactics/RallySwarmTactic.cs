using System;
using AntScout.Characters.Brains.StateContext;
using AntScout.Characters.Brains.States;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using UnityEngine;

namespace AntScout.Characters.Brains.Tactics
{
    /// <summary>
    /// Tactical option for disengaging and paving an Alarm Pheromone Trail back to the home nest depot.
    /// Grounded in Single Responsibility (SRP): Evaluates rally suitability and transitions to AntRallyingSwarmState.
    /// Favored when facing formidable threats (such as enemy base fortresses or overwhelming forces)
    /// where summoning colony reinforcements is far superior to suicidal solo engagement.
    /// </summary>
    public sealed class RallySwarmTactic : ICombatTacticalOption
    {
        public string TacticName => "RallySwarm";

        public float EvaluateScore(AntAiContext context, IAttackable threat)
        {
            if (context == null || threat == null || !threat.IsAttackable) return 0f;

            var def = context.Definition;
            if (def == null || def.AlarmTrailAction == null || context.Character.Gland == null)
            {
                return 0f; // Unit lacks chemical gland or alarm action
            }

            float glandCapacity = context.Character.Gland.NormalizedCapacity;
            if (glandCapacity < 0.25f)
            {
                return 0f; // Insufficient chemical reserves to lay a meaningful highway
            }

            float alarmPriority = context.Priorities.AlarmTrailOnCombat;
            if (alarmPriority <= 0.05f) return 0f;

            // Scenario 1: Threat is an enemy ColonyNest fortress
            // Solo attack against a base is suicide; paving an alarm trail back to summon the swarm is optimal
            if (threat is ColonyNest)
            {
                return Mathf.Clamp01(Mathf.Max(0.85f, alarmPriority) * glandCapacity);
            }

            // Scenario 2: Swarm availability check (Cannot rally a fallen army)
            // If the colony has zero active defenders, laying an alarm trail is useless—the commander must fight or evade directly
            if (context.HomeNest != null && context.HomeNest.ActiveSoldiers.Count == 0 && context.HomeNest.ActiveWorkers.Count == 0)
            {
                return 0f;
            }

            // Scenario 3: Mobile enemy threat
            float hpRatio = context.Character.NormalizedHealth;
            float retreatRatio = def.RetreatHealthRatio;

            // If injured or outmatched, urgency to call reinforcements rises
            float urgency = hpRatio <= retreatRatio + 0.2f ? 0.3f : 0.0f;
            float score = (alarmPriority * glandCapacity) + urgency;

            return Mathf.Clamp01(score);
        }

        public void Execute(AntAiContext context, IAttackable threat)
        {
            if (context == null || threat == null) return;

            context.TargetEnemy = threat;
            context.StateMachine.TransitionTo<AntRallyingSwarmState>();
        }
    }
}
