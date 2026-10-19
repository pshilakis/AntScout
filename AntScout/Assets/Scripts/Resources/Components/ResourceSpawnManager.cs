using System;
using System.Collections.Generic;
using AntScout.Config;
using AntScout.Maps;
using AntScout.Swarm.Nest;
using Pathfinding;
using PGS.Core;
using PGS.Core.Time;
using PGS.Loot.Drops;
using PGS.Loot.Pity;
using PGS.Loot.Random;
using UnityEngine;

namespace AntScout.Resources.Components
{
    /// <summary>
    /// Master coordinator for dynamic map food drops and competitive resource distribution.
    /// Eliminates doorstep RNG by enforcing strict nest buffer exclusions and contested neutral midfield placement.
    /// Handles initial match-start distribution and continuous replenishment via MapResourceDefinition,
    /// com.pgs.loot LootTable, and stateful bad-luck PityTracker protection.
    /// Implements IUpdatable for deterministic simulation updates on TimeChannel.World.
    /// </summary>
    public class ResourceSpawnManager : MonoBehaviour, IUpdatable
    {
        private static ResourceSpawnManager s_instance;
        public static ResourceSpawnManager Instance => s_instance;

        [ColoredHeader("Placement Anchors", HeaderColor.Teal)]
        [Tooltip("Center origin for spawning bounds. Defaults to this transform if unassigned.")]
        [SerializeField] private Transform _centerOrigin;

        private readonly List<FoodSource> _activeFoodSources = new List<FoodSource>();
        private readonly List<ColonyNest> _trackedNests = new List<ColonyNest>();
        private float _spawnTimer;
        private SystemRandomProvider _randomProvider;
        private PityTracker _pityTracker;
        private LootTable<ResourceDefinition> _lootTable;
        private bool _isInitialized;

        public event Action<Vector3, ResourceDefinition> OnResourceSpawned;

        public int ActiveFoodCount => _activeFoodSources.Count;
        public PityTracker PityTracker => _pityTracker;
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Authoritative resource economy configuration resolved dynamically from the active MapDefinition on MapManager.
        /// </summary>
        public MapResourceDefinition ResourceConfig
        {
            get
            {
                if (MapManager.Instance == null || MapManager.Instance.ActiveMap == null)
                {
                    throw new InvalidOperationException(
                        $"[{name}] ResourceSpawnManager: MapManager or ActiveMap is missing in the scene! " +
                        "Resource spawning requires an active MapManager with an assigned MapDefinition.");
                }

                var config = MapManager.Instance.ActiveMap.ResourceDefinition;
                if (config == null)
                {
                    throw new InvalidOperationException(
                        $"[{name}] ResourceSpawnManager: Active MapDefinition '{MapManager.Instance.ActiveMap.MapName}' " +
                        "is missing an assigned MapResourceDefinition! Please assign one in the MapDefinition asset.");
                }

                return config;
            }
        }

        public MapResourceDefinition MapDefinition => ResourceConfig;

        /// <summary>
        /// Computes effective horizontal spawn extents clamped inside the active map bounds with an inward safety margin.
        /// </summary>
        public Vector2 GetEffectiveSpawnExtents()
        {
            Vector2 baseExtents = ResourceConfig.SpawnAreaHalfExtents;
            if (MapManager.Instance != null && MapManager.Instance.ActiveMap != null)
            {
                Vector2 mapHalf = MapManager.Instance.ActiveMap.PlayableHalfExtents;
                float maxX = Mathf.Max(2f, mapHalf.x - 2f);
                float maxZ = Mathf.Max(2f, mapHalf.y - 2f);
                return new Vector2(Mathf.Min(baseExtents.x, maxX), Mathf.Min(baseExtents.y, maxZ));
            }
            return baseExtents;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                throw new InvalidOperationException($"[{name}] ResourceSpawnManager: Multiple instances detected! Existing: '{s_instance.name}', Duplicate: '{name}'.");
            }
            s_instance = this;

            ValidateConfiguration();

            if (_centerOrigin == null)
            {
                _centerOrigin = transform;
            }

            _randomProvider = new SystemRandomProvider();
            _pityTracker = new PityTracker(ResourceConfig.PityConfig);
            _lootTable = ResourceConfig.CreateLootTable(_pityTracker);
            ResetTimer();
        }

        private void Start()
        {
            CacheNests();
            SpawnInitialResources();
            _isInitialized = true;
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
            UnsubscribeAllFoodSources();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (!_isInitialized) return;

            CleanupDestroyedSources();

            var config = ResourceConfig;

            // Accelerated replenishment if map is starving below minimum threshold
            bool isStarving = _activeFoodSources.Count < config.MinActiveFoodCount;
            float effectiveDelta = isStarving ? (deltaTime * 2.0f) : deltaTime;

            if (_activeFoodSources.Count >= config.MaxActiveFoodCount)
            {
                return;
            }

            _spawnTimer -= effectiveDelta;
            if (_spawnTimer <= 0f)
            {
                TrySpawnFairResource();
                ResetTimer();
            }
        }

        private void ResetTimer()
        {
            var config = ResourceConfig;
            if (config != null)
            {
                _spawnTimer = _randomProvider != null
                    ? config.SpawnInterval.Lerp(_randomProvider.NextFloat())
                    : UnityEngine.Random.Range(config.SpawnInterval.Min, config.SpawnInterval.Max);
            }
        }

        private void ValidateConfiguration()
        {
            if (MapManager.Instance == null || MapManager.Instance.ActiveMap == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] ResourceSpawnManager: MapManager or ActiveMap is missing in the scene! " +
                    "Resource spawning requires an active MapManager with an assigned MapDefinition.");
            }

            var config = ResourceConfig;
            if (config.SpawnInterval.Max <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] ResourceSpawnManager: MapResourceDefinition '{config.name}' has invalid SpawnInterval Max ({config.SpawnInterval.Max}). Max must be greater than 0.");
            }
        }

        /// <summary>
        /// Explicit top-down Dependency Injection (DIP) entrypoint to bind allied Colony Nests.
        /// Eliminates ambient scene searching.
        /// </summary>
        public void BindColonyNests(IEnumerable<ColonyNest> nests)
        {
            _trackedNests.Clear();
            if (nests != null)
            {
                foreach (var nest in nests)
                {
                    if (nest != null && !_trackedNests.Contains(nest))
                    {
                        _trackedNests.Add(nest);
                    }
                }
            }
        }

        private void CacheNests()
        {
            if (_trackedNests.Count > 0) return;

            // Query static ColonyNest registry (Single Source of Truth)
            foreach (var nest in ColonyNest.ActiveNests)
            {
                if (nest != null && !_trackedNests.Contains(nest))
                {
                    _trackedNests.Add(nest);
                }
            }

            if (_trackedNests.Count == 0)
            {
                throw new InvalidOperationException(
                    $"[{name}] ResourceSpawnManager: No ColonyNest instances registered! " +
                    "Nests must be active before resource spawning occurs.");
            }
        }

        /// <summary>
        /// Spawns the configured initial resource nodes across neutral territory at match start.
        /// </summary>
        public void SpawnInitialResources()
        {
            CleanupDestroyedSources();
            var config = ResourceConfig;
            int countToSpawn = Mathf.Max(0, config.InitialSpawnCount - _activeFoodSources.Count);
            for (int i = 0; i < countToSpawn; i++)
            {
                TrySpawnFairResource();
            }
        }

        /// <summary>
        /// Selects a fair, walkable position and rolls a ResourceDefinition from the LootTable,
        /// applying PityTracker bad-luck protection to prevent streaks of common spawns.
        /// </summary>
        public GameObject TrySpawnFairResource()
        {
            var config = ResourceConfig;
            if (config == null || config.ResourcePool.Count == 0) return null;
            if (_activeFoodSources.Count >= config.MaxActiveFoodCount) return null;

            if (!TryFindFairSpawnPosition(out Vector3 spawnPosition))
            {
                return null;
            }

            if (_randomProvider == null)
            {
                _randomProvider = new SystemRandomProvider();
            }

            if (_lootTable == null)
            {
                _pityTracker = new PityTracker(config.PityConfig);
                _lootTable = config.CreateLootTable(_pityTracker);
            }

            DropResult<ResourceDefinition> dropResult = _lootTable.Roll(_randomProvider);
            ResourceDefinition resDef = dropResult.Item;
            if (resDef == null || resDef.Prefab == null) return null;

            float randomScale = UnityEngine.Random.Range(resDef.ScaleRange.x, resDef.ScaleRange.y);
            Vector3 finalScale = resDef.Prefab.transform.localScale * randomScale;

            FoodSource foodComp = Instantiate(resDef.Prefab, spawnPosition, Quaternion.identity);
            foodComp.name = $"Food_{resDef.DisplayName}";

            int rolledUnits = resDef.RollUnits(_randomProvider);
            foodComp.Initialize(resDef, finalScale, rolledUnits);
            foodComp.OnDepleted += HandleFoodDepleted;
            _activeFoodSources.Add(foodComp);

            OnResourceSpawned?.Invoke(spawnPosition, resDef);
            return foodComp.gameObject;
        }

        /// <summary>
        /// Competitive candidate-sampling placement algorithm:
        /// 1. Rejects positions within _nestExclusionRadius of any nest (Anti-doorstep RNG).
        /// 2. Snaps candidate to walkable A* NavMesh.
        /// 3. Rejects positions too close to existing food sources.
        /// 4. Scores candidate by symmetric distance between nests to favor neutral contested midfield.
        /// </summary>
        private bool TryFindFairSpawnPosition(out Vector3 bestPosition)
        {
            bestPosition = Vector3.zero;
            Vector3 center = _centerOrigin != null ? _centerOrigin.position : transform.position;
            var config = ResourceConfig;
            Vector2 extents = GetEffectiveSpawnExtents();

            if (_trackedNests.Count == 0)
            {
                CacheNests();
            }

            float bestFairnessScore = float.MinValue;
            bool foundValidCandidate = false;
            const int maxSampleAttempts = 15;

            for (int attempt = 0; attempt < maxSampleAttempts; attempt++)
            {
                float rx = UnityEngine.Random.Range(-extents.x, extents.x);
                float rz = UnityEngine.Random.Range(-extents.y, extents.y);
                Vector3 rawPos = center + new Vector3(rx, 0f, rz);

                // 1. A* Walkable NavMesh Snapping
                Vector3 walkablePos = rawPos;
                if (AstarPath.active != null)
                {
                    var nearest = AstarPath.active.GetNearest(rawPos, NearestNodeConstraint.Walkable);
                    if (nearest.node == null) continue;
                    walkablePos = (Vector3)nearest.node.position;

                    // Ensure snapped node is reasonably close to raw sample
                    float snapDistSqr = (walkablePos.x - rawPos.x) * (walkablePos.x - rawPos.x) + (walkablePos.z - rawPos.z) * (walkablePos.z - rawPos.z);
                    if (snapDistSqr > 4.0f * 4.0f) continue;
                }

                // 2. Strict Nest Exclusion Radius (Doorstep protection)
                bool insideNestExclusion = false;
                float distNest0 = float.MaxValue;
                float distNest1 = float.MaxValue;

                for (int n = 0; n < _trackedNests.Count; n++)
                {
                    if (_trackedNests[n] == null) continue;
                    Vector3 nPos = _trackedNests[n].SpawnPosition;
                    float dxN = walkablePos.x - nPos.x;
                    float dzN = walkablePos.z - nPos.z;
                    float dSqr = dxN * dxN + dzN * dzN;

                    if (dSqr < (config.NestExclusionRadius * config.NestExclusionRadius))
                    {
                        insideNestExclusion = true;
                        break;
                    }

                    float dist = Mathf.Sqrt(dSqr);
                    if (n == 0) distNest0 = dist;
                    else if (n == 1) distNest1 = dist;
                }

                if (insideNestExclusion) continue;

                // 3. Spacing from existing active food sources
                bool tooCloseToFood = false;
                float minFoodDistSqr = config.MinDistanceBetweenFood * config.MinDistanceBetweenFood;
                for (int f = 0; f < _activeFoodSources.Count; f++)
                {
                    if (_activeFoodSources[f] == null) continue;
                    Vector3 fPos = _activeFoodSources[f].WorldPosition;
                    float dxF = walkablePos.x - fPos.x;
                    float dzF = walkablePos.z - fPos.z;
                    if ((dxF * dxF + dzF * dzF) < minFoodDistSqr)
                    {
                        tooCloseToFood = true;
                        break;
                    }
                }

                if (tooCloseToFood) continue;

                // 4. Competitive Midfield Fairness Scoring
                // Score is maximized when equidistant from both nests (|distA - distB| -> 0)
                float fairnessScore = 1.0f;
                if (distNest0 < 1000f && distNest1 < 1000f)
                {
                    float totalDist = distNest0 + distNest1;
                    float diff = Mathf.Abs(distNest0 - distNest1);
                    float symmetryRatio = totalDist > 0.1f ? (1.0f - (diff / totalDist)) : 1.0f;
                    fairnessScore = Mathf.Lerp(1.0f, symmetryRatio, config.MidfieldContestBias);
                }

                // Add slight jitter so we don't always pick the exact dead center
                fairnessScore += UnityEngine.Random.Range(0f, 0.2f);

                if (fairnessScore > bestFairnessScore)
                {
                    bestFairnessScore = fairnessScore;
                    bestPosition = walkablePos;
                    foundValidCandidate = true;
                }
            }

            return foundValidCandidate;
        }

        private void HandleFoodDepleted(FoodSource source)
        {
            if (source != null)
            {
                source.OnDepleted -= HandleFoodDepleted;
                _activeFoodSources.Remove(source);
            }

            // Immediately accelerate replenishment if active food dropped below minimum
            if (_activeFoodSources.Count < ResourceConfig.MinActiveFoodCount)
            {
                _spawnTimer = Mathf.Min(_spawnTimer, 3.0f);
            }
        }

        private void CleanupDestroyedSources()
        {
            for (int i = _activeFoodSources.Count - 1; i >= 0; i--)
            {
                if (_activeFoodSources[i] == null)
                {
                    _activeFoodSources.RemoveAt(i);
                }
            }
        }

        private void UnsubscribeAllFoodSources()
        {
            for (int i = 0; i < _activeFoodSources.Count; i++)
            {
                if (_activeFoodSources[i] != null)
                {
                    _activeFoodSources[i].OnDepleted -= HandleFoodDepleted;
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = _centerOrigin != null ? _centerOrigin.position : transform.position;
            Vector2 extents = MapManager.Instance != null && MapManager.Instance.ActiveMap != null
                ? MapManager.Instance.ActiveMap.PlayableHalfExtents
                : new Vector2(25f, 25f);

            // Playable bounds
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.25f);
            Gizmos.DrawWireCube(center, new Vector3(extents.x * 2f, 1f, extents.y * 2f));

            // Nest exclusion zones
            if (MapManager.Instance != null && MapManager.Instance.ActiveMap != null && MapManager.Instance.ActiveMap.ResourceDefinition != null)
            {
                var config = MapManager.Instance.ActiveMap.ResourceDefinition;
                Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.35f);
                for (int i = 0; i < _trackedNests.Count; i++)
                {
                    if (_trackedNests[i] != null)
                    {
                        Gizmos.DrawWireSphere(_trackedNests[i].SpawnPosition, config.NestExclusionRadius);
                    }
                }
            }
        }
#endif
    }
}
