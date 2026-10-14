using System;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class containing fail-fast runtime validation and Editor OnValidate guardrails
    /// for <see cref="StructureDefinition"/> (Single Responsibility Principle).
    /// </summary>
    public partial class StructureDefinition
    {
        public void Validate(string contextName)
        {
            if (_maxHealth.StatType == null)
            {
                throw new InvalidOperationException(
                    $"[{contextName}] StructureDefinition '{name}' is missing a MaxHealth StatTypeSO!");
            }

            if (_maxHealth.BaseValue <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{contextName}] StructureDefinition '{name}' MaxHealth BaseValue ({_maxHealth.BaseValue}) must be greater than zero!");
            }

            if (_defense.StatType == null)
            {
                throw new InvalidOperationException(
                    $"[{contextName}] StructureDefinition '{name}' is missing a Defense StatTypeSO!");
            }

            if (_defense.BaseValue < 0f)
            {
                throw new InvalidOperationException(
                    $"[{contextName}] StructureDefinition '{name}' Defense BaseValue ({_defense.BaseValue}) cannot be negative!");
            }
        }
    }
}
