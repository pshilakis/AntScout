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

            if (_visionRadius <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{contextName}] StructureDefinition '{name}' VisionRadius ({_visionRadius}) must be greater than zero!");
            }

            if (_auras != null)
            {
                for (int i = 0; i < _auras.Length; i++)
                {
                    var aura = _auras[i];
                    if (aura == null)
                    {
                        throw new InvalidOperationException(
                            $"[{contextName}] StructureDefinition '{name}' contains a null StructureAuraConfig at index {i}!");
                    }

                    if (aura.Shape == null)
                    {
                        throw new InvalidOperationException(
                            $"[{contextName}] StructureDefinition '{name}' aura '{aura.AuraName}' (index {i}) is missing an assigned ISpatialShape!");
                    }

                    if (aura.PulseInterval <= 0f)
                    {
                        throw new InvalidOperationException(
                            $"[{contextName}] StructureDefinition '{name}' aura '{aura.AuraName}' PulseInterval ({aura.PulseInterval}) must be greater than zero!");
                    }

                    if (aura.Effects != null)
                    {
                        for (int e = 0; e < aura.Effects.Count; e++)
                        {
                            var effect = aura.Effects[e];
                            if (effect == null)
                            {
                                throw new InvalidOperationException(
                                    $"[{contextName}] StructureDefinition '{name}' aura '{aura.AuraName}' has a null IHitEffect at index {e}!");
                            }

                            effect.ValidateOrThrow($"StructureDefinition '{name}' -> Aura '{aura.AuraName}'");
                        }
                    }
                }
            }
        }
    }
}
