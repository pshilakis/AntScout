namespace AntScout.Match
{
    /// <summary>
    /// Interface implemented by MonoBehaviours that require explicit lifecycle
    /// notifications when borrowed from or returned to the SpawnPool.
    /// Follows Interface Segregation Principle (ISP).
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Invoked immediately after the object is retrieved from the pool and re-activated.
        /// </summary>
        void OnSpawnedFromPool();

        /// <summary>
        /// Invoked immediately before the object is deactivated and returned to the pool.
        /// </summary>
        void OnDespawnedToPool();
    }
}
