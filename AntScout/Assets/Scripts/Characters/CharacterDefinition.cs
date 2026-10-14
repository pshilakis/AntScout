using System;
using PGS.Combat.Execution;
using PGS.Core;
using PGS.Input.Common;
using PGS.Input.Readers;
using PGS.Player.Motor.ThreeD;
using PGS.Player.Traversal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntScout.Characters
{
    /// <summary>
    /// Universal character definition ScriptableObject.
    /// Single Source of Truth for character identity, locomotion motor settings (com.pgs.player),
    /// traversal modules, combat action kit (com.pgs.combat), input action pairings (com.pgs.input),
    /// and chemical gland tuning.
    /// Reusable across both Player Heroes and AI Entities (Interface Segregation & OCP).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDefinition_New", menuName = "AntScout/Character/Character Definition")]
    public class CharacterDefinition : ScriptableObject
    {
        [ColoredHeader("Character Identity", HeaderColor.Cyan)]
        [SerializeField] private string _characterName = "Scout Ant";
        [SerializeField] private Sprite _icon;

        [ColoredHeader("Locomotion & Traversal", HeaderColor.Gold)]
        [Tooltip("Physics and kinematic motor settings asset (Speed, acceleration, turn speed, slope angles).")]
        [SerializeField] private MotorSettings3DDefinition _motorSettings;

        [Tooltip("Traversal definition asset containing modules (Ground, Air, etc.).")]
        [SerializeField] private TraversalDefinition _traversalDefinition;

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
        public MotorSettings3DDefinition MotorSettings => _motorSettings;
        public TraversalDefinition TraversalDefinition => _traversalDefinition;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            AutoAssignDefaultInputs();
        }

        private void AutoAssignDefaultInputs()
        {
            if (_primaryInput != null && _secondaryInput != null && _utilityInput != null &&
                _recruitmentTrailInput != null && _alarmTrailInput != null)
            {
                return;
            }

            string controlsPath = UnityEditor.AssetDatabase.GUIDToAssetPath("7f9c2d1045934ea29104b9015c7e10a1");
            if (string.IsNullOrEmpty(controlsPath))
            {
                return;
            }

            UnityEngine.Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(controlsPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is InputActionReference actionRef && actionRef.action != null)
                {
                    string actionName = actionRef.action.name;
                    if (_primaryInput == null && actionName == "Action1") _primaryInput = actionRef;
                    else if (_secondaryInput == null && actionName == "Action2") _secondaryInput = actionRef;
                    else if (_utilityInput == null && actionName == "Action3") _utilityInput = actionRef;
                    else if (_recruitmentTrailInput == null && actionName == "Action4") _recruitmentTrailInput = actionRef;
                    else if (_alarmTrailInput == null && actionName == "Action6") _alarmTrailInput = actionRef;
                }
            }
        }
#endif

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

        public void Validate()
        {
            if (_motorSettings == null)
            {
                throw new InvalidOperationException($"[{name}] Missing MotorSettings3DDefinition! Assign one in the Inspector.");
            }

            if (_traversalDefinition == null)
            {
                throw new InvalidOperationException($"[{name}] Missing TraversalDefinition! Assign one in the Inspector.");
            }

            if (_utilityAction == null)
            {
                throw new InvalidOperationException($"[{name}] Missing UtilityAction (CombatActionDefinition)! Assign one in the Inspector.");
            }
        }
    }
}
