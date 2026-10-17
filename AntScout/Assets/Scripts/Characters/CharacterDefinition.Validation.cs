using System;
using UnityEngine.InputSystem;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class containing fail-fast runtime validation and Editor OnValidate guardrails
    /// for <see cref="CharacterDefinition"/> (Single Responsibility Principle).
    /// </summary>
    public partial class CharacterDefinition
    {
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

        public void ValidateVitals(string contextName)
        {
            if (_maxHealth.StatType == null)
            {
                throw new InvalidOperationException($"[{contextName}] CharacterDefinition '{name}' is missing a MaxHealth StatTypeSO!");
            }

            if (_maxHealth.BaseValue <= 0f)
            {
                throw new InvalidOperationException($"[{contextName}] CharacterDefinition '{name}' MaxHealth BaseValue ({_maxHealth.BaseValue}) must be greater than zero!");
            }

            if (_defense.StatType == null)
            {
                throw new InvalidOperationException($"[{contextName}] CharacterDefinition '{name}' is missing a Defense StatTypeSO!");
            }

            if (_defense.BaseValue < 0f)
            {
                throw new InvalidOperationException($"[{contextName}] CharacterDefinition '{name}' Defense BaseValue ({_defense.BaseValue}) cannot be negative!");
            }
        }

        public void Validate()
        {
            ValidateVitals(name);

            if (_visionRadius <= 0f)
            {
                throw new InvalidOperationException($"[{name}] CharacterDefinition VisionRadius ({_visionRadius}) must be greater than zero!");
            }

            if (_primaryAction != null || _secondaryAction != null || _utilityAction != null)
            {
                if (_attack.StatType == null)
                {
                    throw new InvalidOperationException($"[{name}] CharacterDefinition is missing an Attack StatTypeSO required for combat actions!");
                }

                if (_attack.BaseValue <= 0f)
                {
                    throw new InvalidOperationException($"[{name}] CharacterDefinition Attack BaseValue ({_attack.BaseValue}) must be greater than zero!");
                }
            }

            if (_primaryAction != null) _primaryAction.Validate();
            if (_secondaryAction != null) _secondaryAction.Validate();
            if (_utilityAction != null) _utilityAction.Validate();
        }
    }
}
