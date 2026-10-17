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
}
