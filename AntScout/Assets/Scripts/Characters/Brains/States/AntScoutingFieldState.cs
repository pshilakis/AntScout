using System;
using AntScout.Characters;
using AntScout.Characters.Brains;
using AntScout.Characters.Brains.StateContext;
using AntScout.Config;
using AntScout.Core.Interfaces;
using AntScout.Match;
using AntScout.Resources.Components;
using PGS.Combat.Targeting;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.States
{
    /// <summary>
    /// Operating state where an ant autonomously roams the backyard on exploration sorties.
    /// Evaluates sensory inputs against designer-tuned AiPriorityProfile weights to dynamically decide
    /// between foraging, engaging threats, or continuing organic perimeter sweeps.
    /// Grounded in Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public sealed class AntScoutingFieldState : StateBase<AntAiContext>
    {
        private static readonly Collider[] s_scanBuffer = new Collider[16];
        private static readonly int s_obstacleLayerMask = LayerMask.GetMask("Obstacle") != 0 ? LayerMask.GetMask("Obstacle") : (1 << 6);

        public override void OnEnter(AntAiContext context)
        {
            // Conserve precious pheromones while searching
            if (context.Character.Gland != null && context.Character.Gland.IsLayingTrail)
            {
                context.Character.Gland.StopLayingTrail();
            }

            context.ClearTargetFood();
            context.ClearTargetEnemy();
            context.StopSamplingAir();
        }

        public override void OnUpdate(AntAiContext context, float deltaTime)
        {
            if (context.Character == null || !context.Character.IsAlive) return;

            // 1. Boundary horizon and physical collision deflection check
            context.CheckObstacleDeflection(deltaTime);

            var def = context.Definition;
            float perceptionMult = context.Brain.PerceptionMultiplier;

            // 2. Threat perception scan
            IAttackable nearbyThreat = ScanForThreats(context, perceptionMult);

            // 3. Food perception scan: strictly bounded by physical vision radius and line-of-sight
            float maxVision = def != null ? def.VisionRadius : 7.0f;
            float configuredSense = def != null ? def.FoodDetectionRadius : maxVision;
            float effectiveFoodRadius = Mathf.Min(configuredSense, maxVision) * perceptionMult;
            Vector3 antEyePos = context.Character.transform.position + Vector3.up * 0.3f;

            FoodSource activeHarvest = context.GetHighestYieldActiveFoodSource();
            float activeYield = 0f;
            float activeRemainingRatio = 0f;
            if (activeHarvest != null)
            {
                activeYield = activeHarvest.HarvestDuration > 0.01f
                    ? (float)activeHarvest.BiomassValue / activeHarvest.HarvestDuration
                    : (float)activeHarvest.BiomassValue;

                activeRemainingRatio = activeHarvest.Units.Max > 0
                    ? (float)activeHarvest.Units.Current / activeHarvest.Units.Max
                    : 0f;
            }

            FoodSource nearbyFood = FoodSource.FindClosest(
                context.Character.transform.position,
                effectiveFoodRadius,
                source =>
                {
                    if (context.IsFoodReportedOrActive(source)) return false;

                    // Economic pre-filter: If colony has an active healthy harvest (>= 20% capacity remaining),
                    // do not divert scouting attention to inferior food sources.
                    if (activeHarvest != null && activeRemainingRatio >= 0.2f)
                    {
                        float sourceYield = source.HarvestDuration > 0.01f
                            ? (float)source.BiomassValue / source.HarvestDuration
                            : (float)source.BiomassValue;

                        if (sourceYield < activeYield - 0.2f)
                        {
                            return false;
                        }
                    }

                    // Line-of-sight check: ensure food is not occluded behind physical obstacles/walls
                    Vector3 foodTargetPos = source.WorldPosition + Vector3.up * 0.3f;
                    Vector3 rayDir = foodTargetPos - antEyePos;
                    float dist = rayDir.magnitude;
                    if (dist > 0.1f)
                    {
                        if (Physics.Raycast(antEyePos, rayDir.normalized, dist, s_obstacleLayerMask, QueryTriggerInteraction.Ignore))
                        {
                            return false; // Obstructed by physical wall or obstacle
                        }
                    }

                    return true;
                });

            // 4. Utility decision evaluation (Tactical Combat Calculator & Food Priorities)
            AiPriorityProfile priorities = context.Priorities;
            float foodScore = nearbyFood != null ? priorities.ForagingPriority : 0f;

            // If a threat is in sight, evaluate tactical combat options (Solo Duel, Rally Swarm, or Evasion)
            if (nearbyThreat != null)
            {
                if (context.TacticalCalculator.TryEvaluateAndExecute(context, nearbyThreat))
                {
                    if (context.TargetEnemy != null)
                    {
                        return; // Transitioned to combat or swarm rally state
                    }
                }
            }

            if (foodScore > 0.1f)
            {
                context.TargetFood = nearbyFood;
                context.StateMachine.TransitionTo<AntMovingToFoodState>();
                return;
            }

            if (context.IsStallBreaking)
            {
                // Active stall escape maneuver has absolute priority; do not overwrite escape locomotion!
                return;
            }

            // 5. Antenna sampling micro-pause evaluation
            if (context.IsSamplingAir)
            {
                if (context.UpdateSamplingAir(deltaTime))
                {
                    return; // Micro-pause ongoing
                }
            }

            // 6. Continuous field roaming locomotion
            context.IncrementLegTimer(deltaTime);
            if (context.IsLegComplete)
            {
                float pauseChance = def != null ? def.ScoutPauseChance : 0.75f;
                if (UnityEngine.Random.value < pauseChance)
                {
                    context.StartSamplingAir();
                    return;
                }

                context.RollNewRoamingLeg();
            }

            Vector3 moveDir = context.ComputeRoamingMovement();
            context.SetMovement(moveDir, moveDir);
        }

        public override void OnExit(AntAiContext context)
        {
            context.StopSamplingAir();
        }

        private IAttackable ScanForThreats(AntAiContext context, float perceptionMult)
        {
            float radius = context.Brain.DetectionRadius * perceptionMult;
            int count = Physics.OverlapSphereNonAlloc(
                context.Character.transform.position,
                radius,
                s_scanBuffer,
                context.Brain.TargetLayerMask,
                QueryTriggerInteraction.Ignore);

            var swarmConfig = context.HomeNest != null ? context.HomeNest.Config : null;
            bool isBuildup = swarmConfig != null && MatchDirector.Instance != null &&
                             MatchDirector.Instance.ElapsedSimulationTime < swarmConfig.BuildupPhaseDuration;
            bool suppressHunts = isBuildup && swarmConfig != null && swarmConfig.SuppressEarlyHeroAggression && context.HomeNest != null;

            Vector3 toNest = context.HomeNest != null ? (context.Character.transform.position - context.HomeNest.SpawnPosition) : Vector3.zero;
            toNest.y = 0f;
            bool isDefendingHomeBase = swarmConfig != null && toNest.sqrMagnitude <= swarmConfig.NestDefenseRadius * swarmConfig.NestDefenseRadius;

            Vector3 selfPos = context.Character.transform.position;

            for (int i = 0; i < count; i++)
            {
                Collider col = s_scanBuffer[i];
                if (col == null) continue;

                IAttackable attackable = col.GetComponentInParent<IAttackable>();
                if (attackable == null || !attackable.IsAttackable) continue;

                // Respect temporary threat avoidance cooldown
                if (context.IsThreatTemporarilyAvoided(attackable)) continue;

                IColonyFactionMember faction = col.GetComponentInParent<IColonyFactionMember>();
                if (faction == null) continue;

                if (ColonyAffiliation.Evaluate(context.Character, faction) == TargetAffiliation.Enemy)
                {
                    if (suppressHunts && !isDefendingHomeBase)
                    {
                        // In early buildup outside base, suppress proactive long-range hunting of workers/depots,
                        // but always react if an enemy hero directly confronts us within personal vision (<= 6.0m)
                        bool isEnemyHero = col.GetComponentInParent<AntCharacter>() != null;
                        Vector3 toThreat = (attackable.Transform != null ? attackable.Transform.position : selfPos) - selfPos;
                        toThreat.y = 0f;
                        float distToThreat = toThreat.magnitude;

                        if (isEnemyHero && distToThreat <= 6.0f)
                        {
                            return attackable; // Close-range hero duel engagement!
                        }

                        // Suppress proactive hunting of non-heroes or distant units during buildup
                        continue;
                    }

                    return attackable;
                }
            }

            return null;
        }
    }
}
