using System;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Partial class containing fail-fast configuration validation
    /// for <see cref="AntPlayerBrain"/> (Single Responsibility Principle).
    /// </summary>
    public partial class AntPlayerBrain
    {
        private void ValidateConfiguration()
        {
            _character = GetComponent<AntCharacter>();
            if (_character == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] AntPlayerBrain requires an AntCharacter component on the same GameObject!");
            }
        }
    }
}
