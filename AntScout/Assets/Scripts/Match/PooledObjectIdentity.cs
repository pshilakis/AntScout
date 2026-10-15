using UnityEngine;

namespace AntScout.Match
{
    /// <summary>
    /// Component automatically attached to all instances managed by <see cref="SpawnPool"/>.
    /// Holds the prefab instance key, reference to owning pool, and cached IPoolable references for zero-allocation recycling.
    /// Strictly adheres to Single Responsibility (SRP).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObjectIdentity : MonoBehaviour
    {
        private IPoolable[] _poolables;

        public int PrefabKey { get; internal set; }
        public SpawnPool OwningPool { get; internal set; }
        public bool IsInPool { get; internal set; }

        private void Awake()
        {
            CachePoolables();
        }

        internal void CachePoolables()
        {
            _poolables = GetComponentsInChildren<IPoolable>(true);
        }

        internal void NotifySpawned()
        {
            if (_poolables == null)
            {
                CachePoolables();
            }

            for (int i = 0; i < _poolables.Length; i++)
            {
                _poolables[i]?.OnSpawnedFromPool();
            }
        }

        internal void NotifyDespawned()
        {
            if (_poolables == null)
            {
                CachePoolables();
            }

            for (int i = 0; i < _poolables.Length; i++)
            {
                _poolables[i]?.OnDespawnedToPool();
            }
        }
    }
}
