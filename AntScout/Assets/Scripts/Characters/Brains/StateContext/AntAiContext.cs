using System;
using System.Collections.Generic;
using AntScout.Characters;
using AntScout.Characters.Brains.Foraging;
using AntScout.Characters.Brains.Tactics;
using AntScout.Core.Interfaces;
using AntScout.Maps;
using AntScout.Resources.Components;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using PGS.Core.Time;
using PGS.States;
using UnityEngine;

namespace AntScout.Characters.Brains.StateContext
{
    /// <summary>
    /// Shared blackboard and sensory memory context for an autonomous AI Hero.
    /// Encapsulates known world knowledge (reported food sources, targets, expedition progress),
    /// physical host actuators, and spatial motion commands.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    public sealed class AntAiContext
    {
        private readonly AntAiBrain _brain;
        private readonly AntCharacter _character;
        private readonly ColonyNest _homeNest;
        private StateMachine<AntAiContext> _stateMachine;

        // Structured tactical memory: tracks when food was reported and active trail connections
        private readonly Dictionary<FoodSource, float> _reportedFoodCooldowns = new Dictionary<FoodSource, float>();

        // Temporary threat avoidance memory (avoids thrashing when evading threats)
        private readonly Dictionary<IAttackable, float> _temporarilyAvoidedThreats = new Dictionary<IAttackable, float>();
        private readonly TacticalCombatCalculator _tacticalCalculator;
        private readonly ForagingOpportunityCalculator _foragingCalculator;

        // Sensory targets
        private IAttackable _targetEnemy;
        private FoodSource _targetFood;
        private FoodSource _pavingDestinationFood;

        // Kinematic outputs consumed by motor/controller
        private Vector3 _desiredMovement;
        private Vector3? _desiredFacing;

        // Expedition roaming state
        private float _currentHeadingAngle;
        private Vector3 _currentLegStartPosition;
        private float _currentLegTargetDistance;
        private float _currentLegTargetDuration;
        private float _legTimer;
        private float _obstacleStuckTimer;
        private Vector3 _lastWorldPosition;
        private readonly float _jitterSeed;

        // Organic antenna sampling (micro-pauses)
        private bool _isSamplingAir;
        private float _pauseTimer;
        private float _pauseDuration;
        private float _baseFacingAngle;

        public AntAiBrain Brain => _brain;
        public AntCharacter Character => _character;
        public ColonyNest HomeNest => _homeNest;
        public StateMachine<AntAiContext> StateMachine => _stateMachine ?? throw new InvalidOperationException("StateMachine has not been bound to AntAiContext yet! Ensure BindStateMachine() was invoked.");
        public CharacterDefinition Definition => _character != null ? _character.Definition : null;
        public AiPriorityProfile Priorities => Definition != null ? Definition.Priorities : AiPriorityProfile.DefaultScout;
        public TacticalCombatCalculator TacticalCalculator => _tacticalCalculator;
        public ForagingOpportunityCalculator ForagingCalculator => _foragingCalculator;

        public IAttackable TargetEnemy
        {
            get => _targetEnemy;
            set => _targetEnemy = value;
        }

        public FoodSource TargetFood
        {
            get => _targetFood;
            set => _targetFood = value;
        }

        public FoodSource PavingDestinationFood
        {
            get => _pavingDestinationFood;
            set => _pavingDestinationFood = value;
        }

        public Vector3 DesiredMovement => _desiredMovement;
        public Vector3? DesiredFacing => _desiredFacing;
        public float CurrentHeadingAngle => _currentHeadingAngle;
        public float LegTimer => _legTimer;
        public float ObstacleStuckTimer => _obstacleStuckTimer;
        public float JitterSeed => _jitterSeed;

        public AntAiContext(AntAiBrain brain, AntCharacter character, ColonyNest homeNest)
        {
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
            _character = character ?? throw new ArgumentNullException(nameof(character));
            _homeNest = homeNest ?? throw new ArgumentNullException(nameof(homeNest));

            _tacticalCalculator = new TacticalCombatCalculator(
                new SoloDuelTactic(),
                new RallySwarmTactic(),
                new TacticalEvasionTactic());

            _foragingCalculator = new ForagingOpportunityCalculator(
                new PaveRecruitmentHighwayOption(),
                new BookmarkAndContinueScoutingOption());

            _jitterSeed = UnityEngine.Random.Range(0f, 1000f);
            _currentHeadingAngle = UnityEngine.Random.Range(0f, 360f);
            _lastWorldPosition = _character.transform.position;
            _currentLegStartPosition = _character.transform.position;
        }

        /// <summary>
        /// Binds the synchronous state machine to this blackboard context.
        /// </summary>
        public void BindStateMachine(StateMachine<AntAiContext> stateMachine)
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        }

        #region Motion & Actuator Commands

        public void SetMovement(Vector3 movement, Vector3? facing = null)
        {
            _desiredMovement = movement;
            _desiredFacing = facing;
        }

        public void StopMovement()
        {
            _desiredMovement = Vector3.zero;
            _desiredFacing = null;
        }

        public void IncrementLegTimer(float deltaTime) => _legTimer += deltaTime;
        public void ResetLegTimer() => _legTimer = 0f;

        #endregion

        #region Tactical Food Memory (Fixing V-Loop Thrashing)

        /// <summary>
        /// Registers a food source as discovered and reported, starting a cooldown to prevent redundant trail paving.
        /// </summary>
        public void MarkFoodReported(FoodSource food, float cooldownSeconds = 45f)
        {
            if (food == null) return;
            _reportedFoodCooldowns[food] = PgsTime.ElapsedTime + cooldownSeconds;
        }

        /// <summary>
        /// Checks whether a food source has already been reported recently or is actively connected by recruitment pheromone nodes.
        /// </summary>
        public bool IsFoodReportedOrActive(FoodSource food)
        {
            if (food == null || !food.CanHarvest) return true;

            // 1. Hero Memory Cooldown Check
            if (_reportedFoodCooldowns.TryGetValue(food, out float expireTime))
            {
                if (PgsTime.ElapsedTime < expireTime)
                {
                    return true;
                }
                _reportedFoodCooldowns.Remove(food);
            }

            // 2. Physical Scent Check: Does allied colony TrailEmitter already have active recruitment nodes near this food?
            if (_homeNest != null && _homeNest.TrailEmitter != null)
            {
                // 3m radius squared = 9f
                if (_homeNest.TrailEmitter.HasRecruitmentNear(food.WorldPosition, 9.0f))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Scans active FoodSources in the world to find the highest economic yield food source
        /// currently connected by an active allied recruitment trail. Returns null if no active food trail exists.
        /// </summary>
        public FoodSource GetHighestYieldActiveFoodSource()
        {
            if (_homeNest == null || _homeNest.TrailEmitter == null) return null;

            FoodSource bestActiveFood = null;
            float bestYieldRate = 0f;

            IReadOnlyList<FoodSource> sources = FoodSource.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                FoodSource source = sources[i];
                if (source == null || !source.CanHarvest) continue;

                // Check if this food source has an active allied recruitment trail node within 3.5m (sqr = 12.25f)
                if (_homeNest.TrailEmitter.HasRecruitmentNear(source.WorldPosition, 12.25f))
                {
                    float yieldRate = source.HarvestDuration > 0.01f
                        ? (float)source.BiomassValue / source.HarvestDuration
                        : (float)source.BiomassValue;

                    if (yieldRate > bestYieldRate)
                    {
                        bestYieldRate = yieldRate;
                        bestActiveFood = source;
                    }
                }
            }

            return bestActiveFood;
        }

        /// <summary>
        /// Scans active FoodSources to find an actively mined food source (connected to allied recruitment trails)
        /// that is strictly closer to the candidate food source than the home nest depot.
        /// Returns null if no closer active food source exists.
        /// </summary>
        public FoodSource FindCloserActiveHarvestFoodSource(FoodSource candidateFood)
        {
            if (candidateFood == null || _homeNest == null || _homeNest.TrailEmitter == null) return null;

            Vector3 candidatePos = candidateFood.WorldPosition;
            Vector3 nestPos = _homeNest.SpawnPosition;
            float dxNest = candidatePos.x - nestPos.x;
            float dzNest = candidatePos.z - nestPos.z;
            float distToNestSqr = dxNest * dxNest + dzNest * dzNest;

            FoodSource closestActiveSource = null;
            float closestDistSqr = distToNestSqr; // Must be strictly closer than the nest

            IReadOnlyList<FoodSource> sources = FoodSource.ActiveSources;
            for (int i = 0; i < sources.Count; i++)
            {
                FoodSource source = sources[i];
                if (source == null || source == candidateFood || !source.CanHarvest) continue;

                // Check if this food source is actively mined by our colony (allied recruitment trail node within 3.5m)
                if (_homeNest.TrailEmitter.HasRecruitmentNear(source.WorldPosition, 12.25f))
                {
                    Vector3 sourcePos = source.WorldPosition;
                    float dx = candidatePos.x - sourcePos.x;
                    float dz = candidatePos.z - sourcePos.z;
                    float distSqr = dx * dx + dz * dz;

                    if (distSqr < closestDistSqr)
                    {
                        closestDistSqr = distSqr;
                        closestActiveSource = source;
                    }
                }
            }

            return closestActiveSource;
        }

        public void ClearTargetFood()
        {
            _targetFood = null;
        }

        public void ClearPavingDestinationFood()
        {
            _pavingDestinationFood = null;
        }

        public void ClearTargetEnemy()
        {
            _targetEnemy = null;
        }

        public void MarkThreatTemporarilyAvoided(IAttackable threat, float duration)
        {
            if (threat == null) return;
            _temporarilyAvoidedThreats[threat] = PgsTime.ElapsedTime + duration;
        }

        public bool IsThreatTemporarilyAvoided(IAttackable threat)
        {
            if (threat == null) return true;
            if (_temporarilyAvoidedThreats.TryGetValue(threat, out float expireTime))
            {
                if (PgsTime.ElapsedTime < expireTime)
                {
                    return true;
                }
                _temporarilyAvoidedThreats.Remove(threat);
            }
            return false;
        }

        #endregion

        #region Organic Roaming & Deflection

        /// <summary>
        /// Rolls a randomized exploration leg with distance, duration, and directional bias.
        /// </summary>
        public void RollNewRoamingLeg()
        {
            _legTimer = 0f;
            _obstacleStuckTimer = 0f;
            _currentLegStartPosition = _character.transform.position;

            var def = Definition;
            Vector2 distRange = def != null ? def.ScoutDistanceRange : new Vector2(8f, 22f);
            Vector2 durRange = def != null ? def.ScoutDurationRange : new Vector2(4f, 10f);

            _currentLegTargetDistance = UnityEngine.Random.Range(distRange.x, distRange.y);
            _currentLegTargetDuration = UnityEngine.Random.Range(durRange.x, durRange.y);

            Vector3 selfPos = _character.transform.position;

            // Boundary horizon check: If near boundary, angle inward toward map center (0, 0)
            if (MapManager.Instance != null && !MapManager.Instance.IsInsideBounds(selfPos, margin: 4.0f))
            {
                Vector3 toCenter = -selfPos;
                toCenter.y = 0f;
                float centerAngle = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                _currentHeadingAngle = (centerAngle + UnityEngine.Random.Range(-45f, 45f) + 360f) % 360f;
                return;
            }

            // Hostile nest proximity check: If already near an enemy base, angle directly away from it
            float nestAvoidDist = (def != null ? def.HostileNestAvoidanceRadius : 8.0f) + 3.0f;
            foreach (var nest in ColonyNest.ActiveNests)
            {
                if (nest != null && nest.ColonyTeamId != _character.ColonyTeamId)
                {
                    Vector3 toNest = nest.SpawnPosition - selfPos;
                    toNest.y = 0f;
                    if (toNest.sqrMagnitude <= nestAvoidDist * nestAvoidDist)
                    {
                        Vector3 awayFromEnemy = -toNest;
                        float awayAngle = Mathf.Atan2(awayFromEnemy.x, awayFromEnemy.z) * Mathf.Rad2Deg;
                        _currentHeadingAngle = (awayAngle + UnityEngine.Random.Range(-40f, 40f) + 360f) % 360f;
                        return;
                    }
                }
            }

            // Tactical Midfield Bias (Weighted from AiPriorityProfile)
            if (UnityEngine.Random.value < Priorities.MidfieldBias)
            {
                // Angle toward contested center
                Vector3 toCenter = -selfPos;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 4.0f)
                {
                    float centerAngle = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                    _currentHeadingAngle = (centerAngle + UnityEngine.Random.Range(-35f, 35f) + 360f) % 360f;
                    return;
                }
            }

            // Pure random exploration angle
            _currentHeadingAngle = UnityEngine.Random.Range(0f, 360f);
        }

        public bool IsSamplingAir => _isSamplingAir;

        public bool IsLegComplete
        {
            get
            {
                Vector3 delta = _character.transform.position - _currentLegStartPosition;
                delta.y = 0f;
                return _legTimer >= _currentLegTargetDuration || delta.magnitude >= _currentLegTargetDistance;
            }
        }

        public bool CheckLegCompletion()
        {
            if (IsLegComplete)
            {
                RollNewRoamingLeg();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Halts locomotion and initiates a natural antenna sampling micro-pause.
        /// </summary>
        public void StartSamplingAir()
        {
            _isSamplingAir = true;
            _pauseTimer = 0f;
            var def = Definition;
            Vector2 pauseRange = def != null ? def.ScoutPauseDurationRange : new Vector2(0.4f, 1.0f);
            _pauseDuration = UnityEngine.Random.Range(pauseRange.x, pauseRange.y);
            _baseFacingAngle = _currentHeadingAngle;
            StopMovement();
        }

        /// <summary>
        /// Ticks the antenna sampling micro-pause, subtly sweeping head facing angle (+/- 15 degrees).
        /// Returns true while the pause is ongoing, false once completed.
        /// </summary>
        public bool UpdateSamplingAir(float deltaTime)
        {
            if (!_isSamplingAir) return false;

            _pauseTimer += deltaTime;
            if (_pauseTimer >= _pauseDuration)
            {
                _isSamplingAir = false;
                RollNewRoamingLeg();
                return false;
            }

            // Natural, low-frequency sinusoidal/noise head & antenna twitch
            float twitchOffset = Mathf.Sin(_pauseTimer * 12.0f) * 14.0f + (Mathf.PerlinNoise(Time.time * 4f, _jitterSeed) * 2f - 1f) * 6.0f;
            float rad = (_baseFacingAngle + twitchOffset) * Mathf.Deg2Rad;
            Vector3 twitchFacing = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            SetMovement(Vector3.zero, twitchFacing);
            return true;
        }

        public void StopSamplingAir()
        {
            _isSamplingAir = false;
            _pauseTimer = 0f;
        }

        /// <summary>
        /// Applies atmospheric antenna twitches and subtle head sweeps while stationary (e.g. resting at the nest depot).
        /// Keeps the unit looking alert and living rather than freezing into a static mesh.
        /// </summary>
        public void UpdateStationaryIdle(float deltaTime)
        {
            float twitchOffset = Mathf.Sin(Time.time * 8.0f) * 12.0f + (Mathf.PerlinNoise(Time.time * 3f, _jitterSeed) * 2f - 1f) * 5.0f;
            float currentAngle = _character != null ? _character.transform.eulerAngles.y : _currentHeadingAngle;
            float rad = (currentAngle + twitchOffset) * Mathf.Deg2Rad;
            Vector3 twitchFacing = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            SetMovement(Vector3.zero, twitchFacing);
        }

        public Vector3 ComputeRoamingMovement()
        {
            var def = Definition;
            float jitterDegrees = def != null ? def.ScoutAngleJitter : 25f;
            float jitter = (Mathf.PerlinNoise(Time.time * 0.75f, _jitterSeed) * 2f - 1f) * jitterDegrees;
            float rad = (_currentHeadingAngle + jitter) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
        }

        public void DeflectFromObstacle(Vector3? surfaceNormal = null)
        {
            if (surfaceNormal.HasValue && surfaceNormal.Value.sqrMagnitude > 0.01f)
            {
                Vector3 curDir = new Vector3(Mathf.Sin(_currentHeadingAngle * Mathf.Deg2Rad), 0f, Mathf.Cos(_currentHeadingAngle * Mathf.Deg2Rad));
                Vector3 reflected = Vector3.Reflect(curDir, surfaceNormal.Value);
                reflected.y = 0f;
                if (reflected.sqrMagnitude > 0.01f)
                {
                    float baseAngle = Mathf.Atan2(reflected.x, reflected.z) * Mathf.Rad2Deg;
                    _currentHeadingAngle = (baseAngle + UnityEngine.Random.Range(-30f, 30f) + 360f) % 360f;
                }
                else
                {
                    _currentHeadingAngle = (_currentHeadingAngle + 180f + UnityEngine.Random.Range(-30f, 30f) + 360f) % 360f;
                }
            }
            else
            {
                Vector3 toCenter = -_character.transform.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 1.0f)
                {
                    float centerAngle = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
                    _currentHeadingAngle = (centerAngle + UnityEngine.Random.Range(-40f, 40f) + 360f) % 360f;
                }
                else
                {
                    _currentHeadingAngle = (_currentHeadingAngle + 135f + UnityEngine.Random.Range(-30f, 30f) + 360f) % 360f;
                }
            }

            _obstacleStuckTimer = 0f;
            _currentLegStartPosition = _character.transform.position;
            _legTimer = 0f;
        }

        /// <summary>
        /// Explicitly redirects the ant's roaming heading to a specified angle and resets leg progress.
        /// </summary>
        public void DeflectHeading(float newHeadingAngle)
        {
            _currentHeadingAngle = (newHeadingAngle + 360f) % 360f;
            _currentLegStartPosition = _character.transform.position;
            _legTimer = 0f;
            _obstacleStuckTimer = 0f;
        }

        public void CheckObstacleDeflection(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            Vector3 selfPos = _character.transform.position;

            // 1. Boundary horizon check
            if (MapManager.Instance != null && !MapManager.Instance.IsInsideBounds(selfPos + _desiredMovement * 1.0f, margin: 0.8f))
            {
                DeflectFromObstacle();
                return;
            }

            // 2. Hostile ColonyNest avoidance perimeter (Deflects strictly outward away from enemy base)
            var def = Definition;
            float avoidanceRadius = def != null ? def.HostileNestAvoidanceRadius : 8.0f;
            float sqrAvoidance = avoidanceRadius * avoidanceRadius;

            foreach (var nest in ColonyNest.ActiveNests)
            {
                if (nest != null && nest.ColonyTeamId != _character.ColonyTeamId)
                {
                    Vector3 away = selfPos - nest.SpawnPosition;
                    away.y = 0f;
                    if (away.sqrMagnitude <= sqrAvoidance)
                    {
                        // Deflect strictly outward away from the hostile depot back toward midfield
                        float awayAngle = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                        DeflectHeading((awayAngle + UnityEngine.Random.Range(-25f, 25f) + 360f) % 360f);
                        return;
                    }
                }
            }

            // 3. Physical displacement stall check
            if (_desiredMovement.sqrMagnitude > 0.05f)
            {
                Vector3 displacement = selfPos - _lastWorldPosition;
                displacement.y = 0f;
                float actualSpeed = displacement.magnitude / deltaTime;

                if (actualSpeed < 0.15f)
                {
                    _obstacleStuckTimer += deltaTime;
                    if (_obstacleStuckTimer >= 0.25f)
                    {
                        RaycastHit hit;
                        Vector3 rayOrigin = selfPos + Vector3.up * 0.2f;
                        if (Physics.SphereCast(rayOrigin, 0.3f, _desiredMovement.normalized, out hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            DeflectFromObstacle(hit.normal);
                        }
                        else
                        {
                            DeflectFromObstacle();
                        }
                    }
                }
                else
                {
                    _obstacleStuckTimer = 0f;
                }
            }
            else
            {
                _obstacleStuckTimer = 0f;
            }

            _lastWorldPosition = selfPos;
        }

        private static readonly Collider[] s_socialBuffer = new Collider[16];

        /// <summary>
        /// Checks whether allied units (sister heroes or workers) or an active allied recruitment trail are within sensory range.
        /// Grounded in Single Responsibility (SRP): Prevents broadcasting alarm pheromones into a vacuum when isolated deep in hostile territory.
        /// </summary>
        public bool HasAlliesOrTrailNearby(float radius)
        {
            Vector3 selfPos = _character.transform.position;

            // 1. Check if near an active allied recruitment trail network
            if (_homeNest != null && _homeNest.TrailEmitter != null)
            {
                if (_homeNest.TrailEmitter.HasRecruitmentNear(selfPos, radius * radius))
                {
                    return true;
                }
            }

            // 2. Sensory scan for allied faction members
            int count = Physics.OverlapSphereNonAlloc(selfPos, radius, s_socialBuffer, _brain.TargetLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = s_socialBuffer[i];
                if (col == null) continue;

                IColonyFactionMember faction = col.GetComponentInParent<IColonyFactionMember>();
                if (faction != null && ColonyAffiliation.Evaluate(_character, faction) == TargetAffiliation.Ally)
                {
                    // Ignore self
                    if (col.transform.root != _character.transform.root)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion
    }
}
