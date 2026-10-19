using System;
using AntScout.Characters.Brains.Behaviors;
using PGS.Combat.Execution;
using PGS.Core;
using PGS.Input.Common;
using PGS.Input.Readers;
using PGS.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntScout.Characters
{
    /// <summary>
    /// Universal character definition ScriptableObject.
    /// Single Source of Truth for character identity, physical target radius, base stats/vitals (com.pgs.stats),
    /// combat action kit (com.pgs.combat), input action pairings (com.pgs.input), and chemical gland actions.
    /// Reusable across both Player/AI Heroes and Swarm castes (Interface Segregation & OCP).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDefinition_New", menuName = "AntScout/Character/Character Definition")]
    public partial class CharacterDefinition : ScriptableObject
    {
        [ColoredHeader("Character Identity", HeaderColor.Cyan)]
        [SerializeField] private string _characterName = "Scout Ant";
        [SerializeField] private Sprite _icon;

        [ColoredHeader("Base Stats & Vitals (com.pgs.stats)", HeaderColor.Green)]
        [Tooltip("Maximum Health StatTypeSO and starting base value seeded into StatsController.")]
        [SerializeField] private StatsController.InitialStatConfig _maxHealth;

        [Tooltip("Flat Armor/Defense StatTypeSO and starting base value seeded into StatsController.")]
        [SerializeField] private StatsController.InitialStatConfig _defense;

        [Tooltip("Base Attack Damage StatTypeSO and starting base value seeded into StatsController (scales CombatAction potency).")]
        [SerializeField] private StatsController.InitialStatConfig _attack;

        [ColoredHeader("Locomotion", HeaderColor.LightBlue)]
        [Tooltip("Base movement speed in meters per second. Drives player ground locomotion and AI navigation.")]
        [Range(1.0f, 25.0f)]
        [SerializeField] private float _baseMoveSpeed = 7.0f;

        [ColoredHeader("Vision & Sensing", HeaderColor.Yellow)]
        [Tooltip("Sensory sight radius in meters projected into the Fog of War.")]
        [Min(0.5f)]
        [SerializeField] private float _visionRadius = 12.0f;

        [ColoredHeader("AI Decision Priorities (Utility Weights)", HeaderColor.Orange)]
        [Tooltip("Priority weights driving autonomous decision making between foraging, combat, and exploration.")]
        [SerializeField] private AiPriorityProfile _priorities = AiPriorityProfile.DefaultScout;

        [ColoredHeader("Hero AI & Autonomous Behavior", HeaderColor.Yellow)]
        [Tooltip("Polymorphic macro behavior executed when this character is piloted by an AI commander (AntAiBrain).")]
        [SerializeReference, SubclassSelector]
        private IAiBehavior _aiBehavior = new ResourceScoutBehavior();

        [Tooltip("Health percentage threshold (0.0 to 0.8) where the AI disengages combat and retreats home to heal.")]
        [Range(0.0f, 0.8f)]
        [SerializeField] private float _retreatHealthRatio = 0.3f;

        [Tooltip("Maximum pursuit distance in meters from nest or patrol anchor before breaking combat pursuit.")]
        [Range(4.0f, 50.0f)]
        [SerializeField] private float _leashRadius = 14.0f;

        [Tooltip("Reaction time delay in seconds between threat and target evaluation scans (Difficulty tuning).")]
        [Range(0.02f, 0.5f)]
        [SerializeField] private float _scanInterval = 0.15f;

        [Tooltip("Horizontal arrival distance threshold in meters to consider an objective, food source, or nest depot reached.")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float _arrivalDistance = 1.2f;

        [Tooltip("Minimum gland capacity ratio before embarking from nest to execute behavior.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] private float _minGlandRatioToEmbark = 0.5f;

        [Tooltip("If true, AI uses mobility abilities (Dash/Leap) to close distance on fleeing targets.")]
        [SerializeField] private bool _useMobilityToEngage = true;

        [Tooltip("If true, AI uses mobility abilities to escape when retreating at low health.")]
        [SerializeField] private bool _useMobilityToRetreat = true;

        [Tooltip("Sensory sight/smell detection radius in meters for discovering harvestable food sources when piloted by an AI commander (AntAiBrain).")]
        [Range(2.0f, 25.0f)]
        [SerializeField] private float _foodDetectionRadius = 8.0f;

        [Tooltip("Minimum distance in meters to keep away from hostile enemy nests during autonomous exploration (Prevents suicide wandering into enemy bases).")]
        [Range(4.0f, 20.0f)]
        [SerializeField] private float _hostileNestAvoidanceRadius = 8.0f;

        [Tooltip("Min and Max distance in meters for an autonomous exploration roaming leg.")]
        [SerializeField] private Vector2 _scoutDistanceRange = new Vector2(8.0f, 22.0f);

        [Tooltip("Min and Max duration in seconds for an autonomous exploration roaming leg.")]
        [SerializeField] private Vector2 _scoutDurationRange = new Vector2(4.0f, 10.0f);

        [Tooltip("Degrees of random angular wander variation applied during exploration walk steps.")]
        [Range(0.0f, 45.0f)]
        [SerializeField] private float _scoutAngleJitter = 25.0f;

        [Tooltip("Chance (0.0 to 1.0) that the ant halts to sample the air with its antennae upon completing an exploration leg.")]
        [Range(0.0f, 1.0f)]
        [SerializeField] private float _scoutPauseChance = 0.75f;

        [Tooltip("Min and Max duration in seconds for an antenna sampling pause.")]
        [SerializeField] private Vector2 _scoutPauseDurationRange = new Vector2(0.4f, 1.0f);

        [ColoredHeader("Primary Slot", HeaderColor.Coral)]
        [Tooltip("Primary offensive action (e.g. Mandible Bite).")]
        [SerializeField] private CombatActionDefinition _primaryAction;

        [Tooltip("Input action triggering the primary action (default: PgsControls Player/Action1 - LMB).")]
        [PgsInputAction]
        [SerializeField] private InputActionReference _primaryInput;

        [ColoredHeader("Secondary Slot", HeaderColor.Orange)]
        [Tooltip("Secondary ability action (e.g. Acid Spit or Cleave).")]
        [SerializeField] private CombatActionDefinition _secondaryAction;

        [Tooltip("Input action triggering the secondary action (default: PgsControls Player/Action2 - RMB).")]
        [PgsInputAction]
        [SerializeField] private InputActionReference _secondaryInput;

        [ColoredHeader("Utility / Mobility Slot", HeaderColor.Mint)]
        [Tooltip("Utility / Mobility action (e.g. Scurry Dash or Bull Rush).")]
        [SerializeField] private CombatActionDefinition _utilityAction;

        [Tooltip("Input action triggering the utility action (default: PgsControls Player/Action3 - Space).")]
        [PgsInputAction]
        [SerializeField] private InputActionReference _utilityInput;

        [ColoredHeader("Recruitment Trail Slot", HeaderColor.Teal)]
        [Tooltip("Channeled recruitment scent trail action.")]
        [SerializeField] private PheromoneTrailAction _recruitmentTrailAction;

        [Tooltip("Input action held to lay recruitment trail (default: PgsControls Player/Action4 - Left Shift).")]
        [PgsInputAction]
        [SerializeField] private InputActionReference _recruitmentTrailInput;

        [ColoredHeader("Alarm Trail Slot", HeaderColor.Red)]
        [Tooltip("Channeled alarm scent trail action.")]
        [SerializeField] private PheromoneTrailAction _alarmTrailAction;

        [Tooltip("Input action held to lay alarm trail (default: PgsControls Player/Action6 - E).")]
        [PgsInputAction]
        [SerializeField] private InputActionReference _alarmTrailInput;

        public string CharacterName => _characterName;
        public Sprite Icon => _icon;
        public StatsController.InitialStatConfig MaxHealth => _maxHealth;
        public StatsController.InitialStatConfig Defense => _defense;
        public StatsController.InitialStatConfig Attack => _attack;
        public float BaseMoveSpeed => _baseMoveSpeed;
        public float VisionRadius => _visionRadius;
        public IAiBehavior AiBehavior => _aiBehavior;
        public AiPriorityProfile Priorities => _priorities;
        public float RetreatHealthRatio => _retreatHealthRatio;
        public float LeashRadius => _leashRadius;
        public float ScanInterval => _scanInterval;
        public float ArrivalDistance => _arrivalDistance;
        public float MinGlandRatioToEmbark => _minGlandRatioToEmbark;
        public bool UseMobilityToEngage => _useMobilityToEngage;
        public bool UseMobilityToRetreat => _useMobilityToRetreat;
        public float FoodDetectionRadius => _foodDetectionRadius;
        public float HostileNestAvoidanceRadius => _hostileNestAvoidanceRadius;
        public Vector2 ScoutDistanceRange => _scoutDistanceRange;
        public Vector2 ScoutDurationRange => _scoutDurationRange;
        public float MaxScoutDistance => _scoutDistanceRange.y;
        public float MaxScoutDuration => _scoutDurationRange.y;
        public float ScoutAngleJitter => _scoutAngleJitter;
        public float ScoutPauseChance => _scoutPauseChance;
        public Vector2 ScoutPauseDurationRange => _scoutPauseDurationRange;
        public float PauseChance => _scoutPauseChance;
        public Vector2 PauseDurationRange => _scoutPauseDurationRange;
        public CombatActionDefinition PrimaryAction => _primaryAction;
        public InputActionReference PrimaryInput => _primaryInput;
        public CombatActionDefinition SecondaryAction => _secondaryAction;
        public InputActionReference SecondaryInput => _secondaryInput;
        public CombatActionDefinition UtilityAction => _utilityAction;
        public InputActionReference UtilityInput => _utilityInput;
        public PheromoneTrailAction RecruitmentTrailAction => _recruitmentTrailAction;
        public InputActionReference RecruitmentTrailInput => _recruitmentTrailInput;
        public PheromoneTrailAction AlarmTrailAction => _alarmTrailAction;
        public InputActionReference AlarmTrailInput => _alarmTrailInput;

        /// <summary>
        /// True if at least one pheromone trail action is equipped in this character's Action Kit.
        /// </summary>
        public bool HasTrailActions => _recruitmentTrailAction != null || _alarmTrailAction != null;

        /// <summary>
        /// Registers all configured InputActionReferences in this character's Action Kit with the given PgsInputReader.
        /// </summary>
        public void RegisterInputActions(PgsInputReader reader)
        {
            if (reader == null) return;

#if UNITY_EDITOR
            AutoAssignDefaultInputs();
#endif

            if (_primaryAction != null && _primaryInput != null) reader.RegisterAction(_primaryInput);
            if (_secondaryAction != null && _secondaryInput != null) reader.RegisterAction(_secondaryInput);
            if (_utilityAction != null && _utilityInput != null) reader.RegisterAction(_utilityInput);
            if (_recruitmentTrailAction != null && _recruitmentTrailInput != null) reader.RegisterAction(_recruitmentTrailInput);
            if (_alarmTrailAction != null && _alarmTrailInput != null) reader.RegisterAction(_alarmTrailInput);
        }

        /// <summary>
        /// Resolves a combat action bound to the specified InputActionReference.
        /// Encapsulates slot storage so we can freely transition to a dynamic list later without changing callers (OCP).
        /// </summary>
        public bool TryGetCombatAction(InputActionReference triggeredInput, out CombatActionDefinition action)
        {
            if (triggeredInput != null)
            {
                if (_primaryAction != null && MatchesAction(_primaryInput, triggeredInput))
                {
                    action = _primaryAction;
                    return true;
                }

                if (_secondaryAction != null && MatchesAction(_secondaryInput, triggeredInput))
                {
                    action = _secondaryAction;
                    return true;
                }

                if (_utilityAction != null && MatchesAction(_utilityInput, triggeredInput))
                {
                    action = _utilityAction;
                    return true;
                }
            }

            action = null;
            return false;
        }

        /// <summary>
        /// Resolves a pheromone trail action bound to the specified InputActionReference.
        /// </summary>
        public bool TryGetTrailAction(InputActionReference triggeredInput, out PheromoneTrailAction trailAction)
        {
            if (triggeredInput != null)
            {
                if (_recruitmentTrailAction != null && MatchesAction(_recruitmentTrailInput, triggeredInput))
                {
                    trailAction = _recruitmentTrailAction;
                    return true;
                }

                if (_alarmTrailAction != null && MatchesAction(_alarmTrailInput, triggeredInput))
                {
                    trailAction = _alarmTrailAction;
                    return true;
                }
            }

            trailAction = null;
            return false;
        }

        private static bool MatchesAction(InputActionReference slotInput, InputActionReference triggeredInput)
        {
            if (slotInput == null || triggeredInput == null) return false;
            if (ReferenceEquals(slotInput, triggeredInput)) return true;
            return slotInput.action != null && triggeredInput.action != null && slotInput.action.id == triggeredInput.action.id;
        }
    }

    /// <summary>
    /// Utility weights and decision priorities for autonomous AI commanders.
    /// Determines the relative drive to forage, brawl, defend midfield, and lay scent trails.
    /// </summary>
    [Serializable]
    public struct AiPriorityProfile
    {
        [Tooltip("Drive to search for and secure food resources across the yard (0.0 = ignores food, 1.0 = highly dedicated forager).")]
        [Range(0f, 1f)]
        public float ForagingPriority;

        [Tooltip("Drive to aggressively attack, pursue, and defend territory against enemy ants (0.0 = pure pacifist/flee, 1.0 = relentless brawler).")]
        [Range(0f, 1f)]
        public float CombatAggression;

        [Tooltip("Tendency to push toward the contested midfield center where food caches spawn vs lateral flanks (0.0 = flanker, 1.0 = center rusher).")]
        [Range(0f, 1f)]
        public float MidfieldBias;

        [Tooltip("Drive to pave a recruitment scent trail all the way back to base upon claiming food (0.0 = claims silently, 1.0 = always paves trail).")]
        [Range(0f, 1f)]
        public float PaveTrailOnFood;

        [Tooltip("Tendency to lay an alarm scent trail when entering combat to rally defenders (0.0 = fights solo, 1.0 = always calls for help).")]
        [Range(0f, 1f)]
        public float AlarmTrailOnCombat;

        public static AiPriorityProfile DefaultScout => new AiPriorityProfile
        {
            ForagingPriority = 0.95f,
            CombatAggression = 0.25f,
            MidfieldBias = 0.5f,
            PaveTrailOnFood = 1.0f,
            AlarmTrailOnCombat = 0.2f
        };

        public static AiPriorityProfile DefaultSoldier => new AiPriorityProfile
        {
            ForagingPriority = 0.65f,
            CombatAggression = 0.9f,
            MidfieldBias = 0.8f,
            PaveTrailOnFood = 0.8f,
            AlarmTrailOnCombat = 0.7f
        };
    }
}
