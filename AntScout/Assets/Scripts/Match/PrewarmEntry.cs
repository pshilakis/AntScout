using System;
using UnityEngine;

namespace AntScout.Match
{
    /// <summary>
    /// Configuration specifying a prefab and initial prewarm quantity
    /// for pre-allocating objects in the <see cref="SpawnPool"/> on match startup.
    /// Eliminates runtime instantiation hitches and GC allocations.
    /// </summary>
    [Serializable]
    public sealed class PrewarmEntry
    {
        [Tooltip("The source prefab to pool and prewarm.")]
        [SerializeField] private GameObject _prefab;

        [Tooltip("Initial number of inactive instances to pre-instantiate on Awake.")]
        [Range(1, 200)]
        [SerializeField] private int _prewarmCount = 16;

        public GameObject Prefab => _prefab;
        public int PrewarmCount => _prewarmCount;

        public PrewarmEntry() { }

        public PrewarmEntry(GameObject prefab, int prewarmCount)
        {
            _prefab = prefab;
            _prewarmCount = prewarmCount;
        }

        public void Validate(int index)
        {
            if (_prefab == null)
            {
                throw new InvalidOperationException($"[SpawnPool] PrewarmEntry at index [{index}] has an unassigned Prefab!");
            }

            if (_prewarmCount <= 0)
            {
                throw new InvalidOperationException(
                    $"[SpawnPool] PrewarmEntry at index [{index}] for prefab '{_prefab.name}' has invalid PrewarmCount: {_prewarmCount}. Must be > 0.");
            }
        }
    }
}
