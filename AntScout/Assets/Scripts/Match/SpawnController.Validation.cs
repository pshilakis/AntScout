using System;

namespace AntScout.Match
{
    public partial class SpawnController
    {
        public void ValidateConfiguration()
        {
            if (_slots == null || _slots.Length == 0)
            {
                throw new InvalidOperationException($"[{name}] SpawnController has no participant slots configured!");
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                SpawnSlot slot = _slots[i];
                if (slot == null)
                {
                    throw new InvalidOperationException($"[{name}] SpawnSlot at index {i} is null!");
                }

                slot.ValidateOrThrow(name);
            }

            if (_corpseDespawnDelay < 0f)
            {
                throw new InvalidOperationException($"[{name}] CorpseDespawnDelay ({_corpseDespawnDelay}) cannot be negative!");
            }
        }
    }
}
