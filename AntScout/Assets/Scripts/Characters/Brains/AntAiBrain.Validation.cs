using System;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Partial class containing fail-fast configuration validation
    /// for <see cref="AntAiBrain"/> (Single Responsibility Principle).
    /// </summary>
    public partial class AntAiBrain
    {
        private void ValidateConfiguration()
        {
            if (_character == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] AntAiBrain requires an AntCharacter component on the same GameObject!");
            }

            if (_detectionRadius <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AntAiBrain requires _detectionRadius > 0!");
            }

            if (_leashRadius < _detectionRadius)
            {
                throw new InvalidOperationException(
                    $"[{name}] AntAiBrain requires _leashRadius ({_leashRadius}) >= _detectionRadius ({_detectionRadius}) to prevent thrashing!");
            }
        }

        private void ValidateHomeNest()
        {
            if (_homeNest == null)
            {
                throw new InvalidOperationException(
                    $"[AntAiBrain] Unable to find a ColonyNest matching ColonyTeamId {_character.ColonyTeamId} " +
                    $"in the scene for GameObject '{name}'. Please place an allied ColonyNest in the scene or assign one in the Inspector.");
            }

            if (_homeNest.ColonyTeamId != _character.ColonyTeamId)
            {
                throw new InvalidOperationException(
                    $"[AntAiBrain] Assigned ColonyNest '{_homeNest.name}' (ColonyTeamId {_homeNest.ColonyTeamId}) " +
                    $"does not match AntCharacter '{name}' (ColonyTeamId {_character.ColonyTeamId}).");
            }
        }
    }
}
