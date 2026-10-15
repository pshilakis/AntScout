using System;
using UnityEngine;

namespace AntScout.Match
{
    public sealed partial class SpawnPool
    {
        private void ValidateConfiguration()
        {
            if (_globalMaxPoolCapacity <= 0)
            {
                throw new InvalidOperationException(
                    $"[{name}] SpawnPool has invalid GlobalMaxPoolCapacity: {_globalMaxPoolCapacity}. Must be > 0.");
            }

            if (_prewarmEntries != null)
            {
                for (int i = 0; i < _prewarmEntries.Length; i++)
                {
                    if (_prewarmEntries[i] == null)
                    {
                        throw new InvalidOperationException($"[{name}] SpawnPool has a null PrewarmEntry at index [{i}]!");
                    }

                    _prewarmEntries[i].Validate(i);
                }
            }
        }

        private void OnValidate()
        {
            if (_globalMaxPoolCapacity < 1)
            {
                _globalMaxPoolCapacity = 1;
            }
        }
    }
}
