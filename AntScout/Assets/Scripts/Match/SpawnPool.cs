using System;
using System.Collections.Generic;
using PGS.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace AntScout.Match
{
    /// <summary>
    /// Centralized high-performance object pooling service for dynamic entities (Swarm agents, bonanzas, effects).
    /// Uses native <see cref="IObjectPool{T}"/> under the hood with sub-pools per prefab.
    /// Exposes a zero-allocation static facade (Spawn / Despawn) backed by a scene coordinator instance.
    /// Strictly adheres to Single Responsibility (SRP) and Fail-Fast standards.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed partial class SpawnPool : MonoBehaviour
    {
        private static SpawnPool s_instance;

        [ColoredHeader("Prewarming", HeaderColor.Cyan)]
        [Tooltip("Optional prewarm configurations to instantiate on match start to eliminate runtime hitches.")]
        [SerializeField] private PrewarmEntry[] _prewarmEntries = Array.Empty<PrewarmEntry>();

        [ColoredHeader("Capacity Guardrails", HeaderColor.Coral)]
        [Tooltip("Safety ceiling: maximum inactive instances retained per pool before extras are destroyed rather than pooled.")]
        [Range(10, 500)]
        [SerializeField] private int _globalMaxPoolCapacity = 100;

        private readonly Dictionary<int, IObjectPool<GameObject>> _pools = new();
        private readonly Dictionary<int, Transform> _subPoolRoots = new();

        public static SpawnPool Instance => s_instance;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                throw new InvalidOperationException(
                    $"[{name}] SpawnPool: Multiple SpawnPool components detected! Existing: '{s_instance.name}', Duplicate: '{name}'. " +
                    "There can only be one SpawnPool coordinator per scene.");
            }

            s_instance = this;
            ValidateConfiguration();
            InitializePrewarmEntries();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }

            foreach (var kvp in _pools)
            {
                kvp.Value.Clear();
            }
            _pools.Clear();
            _subPoolRoots.Clear();
        }

        private void InitializePrewarmEntries()
        {
            if (_prewarmEntries == null) return;

            for (int i = 0; i < _prewarmEntries.Length; i++)
            {
                PrewarmEntry entry = _prewarmEntries[i];
                if (entry == null || entry.Prefab == null) continue;

                int key = entry.Prefab.GetInstanceID();
                IObjectPool<GameObject> pool = GetOrCreatePool(entry.Prefab, entry.PrewarmCount);

                // Prewarm instances into the pool buffer
                var prewarmed = new List<GameObject>(entry.PrewarmCount);
                for (int count = 0; count < entry.PrewarmCount; count++)
                {
                    prewarmed.Add(pool.Get());
                }

                for (int count = 0; count < prewarmed.Count; count++)
                {
                    pool.Release(prewarmed[count]);
                }
            }
        }

        private IObjectPool<GameObject> GetOrCreatePool(GameObject prefab, int initialCapacity = 16)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab), "[SpawnPool] Cannot create or get a pool for a null prefab!");
            }

            int key = prefab.GetInstanceID();
            if (_pools.TryGetValue(key, out IObjectPool<GameObject> existingPool))
            {
                return existingPool;
            }

            GameObject subPoolGo = new GameObject($"SubPool_{prefab.name}");
            subPoolGo.transform.SetParent(transform);
            _subPoolRoots[key] = subPoolGo.transform;

            int capacity = Mathf.Max(1, initialCapacity);
            int maxCapacity = Mathf.Max(_globalMaxPoolCapacity, capacity);

            IObjectPool<GameObject> newPool = new ObjectPool<GameObject>(
                createFunc: () => CreatePooledInstance(prefab, key, subPoolGo.transform),
                actionOnGet: go =>
                {
                    if (go != null)
                    {
                        go.SetActive(true);
                    }
                },
                actionOnRelease: go =>
                {
                    if (go != null)
                    {
                        go.transform.SetParent(subPoolGo.transform);
                        go.SetActive(false);
                    }
                },
                actionOnDestroy: go =>
                {
                    if (go != null)
                    {
                        Destroy(go);
                    }
                },
                collectionCheck: false,
                defaultCapacity: capacity,
                maxSize: maxCapacity
            );

            _pools[key] = newPool;
            return newPool;
        }

        private GameObject CreatePooledInstance(GameObject prefab, int key, Transform parent)
        {
            GameObject instance = Instantiate(prefab, parent);
            instance.name = $"{prefab.name}_Pooled";

            PooledObjectIdentity identity = instance.GetComponent<PooledObjectIdentity>();
            if (identity == null)
            {
                identity = instance.AddComponent<PooledObjectIdentity>();
            }

            identity.PrefabKey = key;
            identity.OwningPool = this;
            identity.IsInPool = true;
            identity.CachePoolables();

            return instance;
        }

        #region Static Facade API

        /// <summary>
        /// Retrieves an instance of the specified prefab from its pool, repositions it, and activates it.
        /// If no pool exists for this prefab, one is created automatically.
        /// Throws <see cref="InvalidOperationException"/> if no SpawnPool coordinator exists in the active scene.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (s_instance == null)
            {
                throw new InvalidOperationException(
                    "[SpawnPool] No active SpawnPool component found in the scene! " +
                    "Ensure a SpawnPool coordinator is present on a GameObject in the match scene.");
            }

            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab), "[SpawnPool] Spawn called with a null prefab reference!");
            }

            IObjectPool<GameObject> pool = s_instance.GetOrCreatePool(prefab);

            GameObject instance = pool.Get();

            Transform t = instance.transform;
            t.SetParent(parent);
            t.SetPositionAndRotation(position, rotation);

            PooledObjectIdentity identity = instance.GetComponent<PooledObjectIdentity>();
            if (identity != null)
            {
                identity.IsInPool = false;
                identity.NotifySpawned();
            }

            return instance;
        }

        /// <summary>
        /// Typed overload retrieving a component directly from a pooled prefab instance.
        /// </summary>
        public static T Spawn<T>(T prefabComponent, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefabComponent == null)
            {
                throw new ArgumentNullException(nameof(prefabComponent), "[SpawnPool] Spawn<T> called with a null component reference!");
            }

            GameObject instanceGo = Spawn(prefabComponent.gameObject, position, rotation, parent);
            T component = instanceGo.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"[SpawnPool] Spawned instance of '{prefabComponent.name}' is missing component of type '{typeof(T).Name}'!");
            }

            return component;
        }

        /// <summary>
        /// Recycles an active pooled instance back into its sub-pool.
        /// Invokes <see cref="IPoolable.OnDespawnedToPool"/> and deactivates the GameObject.
        /// If the object was not created by SpawnPool, it is safely destroyed.
        /// </summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (s_instance == null)
            {
                Destroy(instance);
                return;
            }

            PooledObjectIdentity identity = instance.GetComponent<PooledObjectIdentity>();
            if (identity == null)
            {
                // Not managed by SpawnPool; fallback destroy
                Destroy(instance);
                return;
            }

            if (identity.IsInPool)
            {
                // Guard against duplicate release
                return;
            }

            identity.NotifyDespawned();
            identity.IsInPool = true;

            if (s_instance._pools.TryGetValue(identity.PrefabKey, out IObjectPool<GameObject> pool))
            {
                pool.Release(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        /// <summary>
        /// Typed overload recycling an active pooled component instance back into its sub-pool.
        /// </summary>
        public static void Despawn<T>(T instanceComponent) where T : Component
        {
            if (instanceComponent != null)
            {
                Despawn(instanceComponent.gameObject);
            }
        }

        #endregion
    }
}
