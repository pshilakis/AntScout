using System;
using AntScout.Characters.Brains.StateContext;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using UnityEngine;

namespace AntScout.Characters.Brains.Tactics
{
    /// <summary>
    /// Tactical option for avoiding or bypassing a threat without engaging in combat.
    /// Grounded in Single Responsibility (SRP): Evaluates evasion suitability and steers heading away from threat.
    /// Favored when carrying food cargo, outmatched, critically wounded, or encountering an enemy base fortress.
    /// </summary>
    public sealed class TacticalEvasionTactic : ICombatTacticalOption
    {
        public string TacticName => "TacticalEvasion";

        public float EvaluateScore(AntAiContext context, IAttackable threat)
        {
            if (context == null || threat == null || !threat.IsAttackable) return 0f;

            // Scenario 1: Critically wounded ant avoids all combat to survive
            var def = context.Definition;
            float retreatRatio = def != null ? def.RetreatHealthRatio : 0.3f;
            if (context.Character.NormalizedHealth <= retreatRatio + 0.05f)
            {
                return 0.95f;
            }

            // Scenario 2: Carrying food or dedicated to foraging over combat
            if (context.TargetFood != null)
            {
                return 0.9f;
            }

            // Scenario 3: Threat is an enemy ColonyNest base depot
            if (threat is ColonyNest)
            {
                // High default evasion unless rally priority supersedes it
                return 0.75f;
            }

            // Scenario 4: Forager vs Aggression priority balance
            float combatAggression = context.Priorities.CombatAggression;
            float foragingPriority = context.Priorities.ForagingPriority;

            if (foragingPriority > combatAggression + 0.2f)
            {
                return 0.7f;
            }

            // Inverse aggression baseline
            return Mathf.Clamp01(1.0f - combatAggression);
        }

        public void Execute(AntAiContext context, IAttackable threat)
        {
            if (context == null || threat == null) return;

            // Register threat on temporary avoidance cooldown (prevents frame-by-frame thrashing)
            context.MarkThreatTemporarilyAvoided(threat, 6.0f);

            // Steer heading cleanly away from threat
            Vector3 selfPos = context.Character.transform.position;
            Vector3 threatPos = threat.Transform != null ? threat.Transform.position : selfPos;
            Vector3 away = selfPos - threatPos;
            away.y = 0f;

            if (away.sqrMagnitude > 0.01f)
            {
                float awayAngle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                context.DeflectHeading((awayAngle + UnityEngine.Random.Range(-30f, 30f) + 360f) % 360f);
            }
            else
            {
                context.DeflectFromObstacle();
            }
        }
    }
}
