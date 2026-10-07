using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Resources.Enums;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Resources.Components
{
    /// <summary>
    /// Periodically drops high-value food bonanzas (Sugar, Fruit, Carcasses) across the yard map.
    /// Manages drop pacing, area boundaries, and concurrency caps.
    /// Implements IUpdatable to receive deterministic updates on TimeChannel.World.
    /// </summary>
    public class BonanzaSpawner : MonoBehaviour, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for spawn intervals and map half-extents.")]
        [SerializeField] private ResourceConfigSO _config;

        [Header("Food Prefabs")]
        [Tooltip("List of harvestable food prefabs that can be dynamically spawned.")]
        [SerializeField] private GameObject[] _foodPrefabs;

        [Tooltip("Origin center for spawning. Defaults to this transform if unassigned.")]
        [SerializeField] private Transform _centerOrigin;

        private readonly List<GameObject> _activeDrops = new List<GameObject>();
        private float _spawnTimer;

        public event Action<Vector3, ResourceType> OnBonanzaSpawned;

        public int ActiveDropCount => _activeDrops.Count;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[BonanzaSpawner] Missing required ResourceConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ResourceConfigSO in the Inspector.");
            }

            if (_foodPrefabs == null || _foodPrefabs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[BonanzaSpawner] No food prefabs assigned on GameObject '{gameObject.name}'. " +
                    $"Please assign at least one food prefab in the Inspector.");
            }

            if (_centerOrigin == null)
            {
                _centerOrigin = transform;
            }

            ResetTimer();
        }

        private void ResetTimer()
        {
            _spawnTimer = UnityEngine.Random.Range(_config.MinSpawnInterval, _config.MaxSpawnInterval);
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            CleanupDestroyedDrops();

            if (_activeDrops.Count >= _config.MaxActiveBonanzas)
            {
                return;
            }

            _spawnTimer -= deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnBonanza();
                ResetTimer();
            }
        }

        private void CleanupDestroyedDrops()
        {
            for (int i = _activeDrops.Count - 1; i >= 0; i--)
            {
                if (_activeDrops[i] == null)
                {
                    _activeDrops.RemoveAt(i);
                }
            }
        }

        public GameObject SpawnBonanza()
        {
            Vector2 extents = _config.SpawnAreaHalfExtents;
            float randomX = UnityEngine.Random.Range(-extents.x, extents.x);
            float randomZ = UnityEngine.Random.Range(-extents.y, extents.y);

            Vector3 spawnPos = _centerOrigin.position + new Vector3(randomX, 0f, randomZ);

            GameObject prefab = _foodPrefabs[UnityEngine.Random.Range(0, _foodPrefabs.Length)];
            GameObject dropInstance = Instantiate(prefab, spawnPos, Quaternion.identity);

            _activeDrops.Add(dropInstance);

            FoodSource foodComp = dropInstance.GetComponent<FoodSource>();
            ResourceType type = foodComp != null ? foodComp.Type : ResourceType.SugarGranule;

            OnBonanzaSpawned?.Invoke(spawnPos, type);
            return dropInstance;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = _centerOrigin != null ? _centerOrigin.position : transform.position;
            Vector2 extents = _config != null ? _config.SpawnAreaHalfExtents : new Vector2(20f, 20f);

            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.3f);
            Gizmos.DrawWireCube(center, new Vector3(extents.x * 2f, 1f, extents.y * 2f));
        }
    }
}
