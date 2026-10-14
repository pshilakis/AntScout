using System;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Partial class containing fail-fast configuration validation
    /// for <see cref="AntBrainController"/> (Single Responsibility Principle).
    /// </summary>
    public partial class AntBrainController
    {
        private void ValidateConfiguration()
        {
            if (_playerBrain == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] AntBrainController requires an AntPlayerBrain component on GameObject '{name}'!");
            }

            if (_aiBrain == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] AntBrainController requires an AntAiBrain component on GameObject '{name}'!");
            }
        }
    }
}
