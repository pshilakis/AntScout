using System;

namespace AntScout.Match
{
    /// <summary>
    /// Partial class containing fail-fast runtime validation for <see cref="SpawnSlot"/>.
    /// Strictly adheres to the fail-fast standard with zero silent fallbacks.
    /// </summary>
    public partial class SpawnSlot
    {
        public void ValidateOrThrow(string contextName)
        {
            if (string.IsNullOrWhiteSpace(_slotName))
            {
                throw new InvalidOperationException($"[{contextName}] A SpawnSlot has an empty or null SlotName!");
            }

            if (_characterPrefab == null)
            {
                throw new InvalidOperationException($"[{contextName}] SpawnSlot '{_slotName}' is missing a CharacterPrefab!");
            }

            if (_teamId < 0)
            {
                throw new InvalidOperationException($"[{contextName}] SpawnSlot '{_slotName}' TeamId ({_teamId}) cannot be negative!");
            }

            if (_respawnDelay <= 0f)
            {
                throw new InvalidOperationException($"[{contextName}] SpawnSlot '{_slotName}' RespawnDelay ({_respawnDelay}) must be greater than zero!");
            }
        }
    }
}
