using System;
using AntScout.Characters;
using AntScout.Characters.Brains.StateContext;
using AntScout.Characters.Brains.States;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using UnityEngine;

namespace AntScout.Characters.Brains.Tactics
{
    /// <summary>
    /// Tactical option for engaging an enemy threat in direct solo combat.
    /// Grounded in Single Responsibility (SRP): Evaluates duel viability and transitions to AntCombatEngagementState.
    /// Favored when self combat aggression and health are high and the enemy is isolated or vulnerable.
    /// </summary>
    public sealed class SoloDuelTactic : ICombatTacticalOption
    {
        public string TacticName => "SoloDuel";

        public float EvaluateScore(AntAiContext context, IAttackable threat)
        {
            if (context == null || threat == null || !threat.IsAttackable) return 0f;

            // Never attempt a solo duel against an entire enemy base fortress
            if (threat is ColonyNest) return 0f;

            var def = context.Definition;
            float retreatRatio = def != null ? def.RetreatHealthRatio : 0.3f;
            float hpRatio = context.Character.NormalizedHealth;

            // Injured units close to retreat threshold will not initiate new duels
            if (hpRatio <= retreatRatio + 0.1f) return 0f;

            float aggression = context.Priorities.CombatAggression;
            if (aggression <= 0.05f) return 0f;

            // Health weighting: higher HP creates higher willingness to duel
            float healthScore = Mathf.Clamp01((hpRatio - retreatRatio) / (1f - retreatRatio));

            // Aggression baseline
            float baseScore = aggression * healthScore;

            // Scenario A: Threat is an enemy Hero commander (AntCharacter)
            // When facing the enemy hero, combat urgency surges—heroes actively fight and pursue rivals!
            if (threat is AntCharacter || (threat.Transform != null && threat.Transform.GetComponentInParent<AntCharacter>() != null))
            {
                baseScore = Mathf.Max(baseScore, 0.85f * healthScore);
            }

            // Scenario B: Swarm is depleted (Last Stand Defense)
            // If self has no living swarm defenders, the hero stands and fights for self-defense
            if (context.HomeNest != null && context.HomeNest.ActiveSoldiers.Count == 0 && context.HomeNest.ActiveWorkers.Count == 0)
            {
                baseScore = Mathf.Max(baseScore, 0.90f * healthScore);
            }

            return Mathf.Clamp01(baseScore);
        }

        public void Execute(AntAiContext context, IAttackable threat)
        {
            if (context == null || threat == null) return;

            context.TargetEnemy = threat;
            context.StateMachine.TransitionTo<AntCombatEngagementState>();
        }
    }
}
