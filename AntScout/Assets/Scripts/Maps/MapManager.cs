using System;
using PGS.Core;
using UnityEngine;

namespace AntScout.Maps
{
    /// <summary>
    /// Master coordinator for map environments, spatial play area extents, and physical boundary enforcement.
    /// Manages the active MapDefinition, instantiates environment prefabs, dynamically constructs airtight
    /// perimeter boundary walls, and provides global bounds query APIs for AI steering, resource spawning, and void recovery.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public partial class MapManager : MonoBehaviour
    {
        private static MapManager s_instance;
        public static MapManager Instance => s_instance;

        [ColoredHeader("Active Map Configuration", HeaderColor.Orange)]
        [Tooltip("Active map definition asset dictating environment prefab, playable extents, wall sizing, and kill-plane elevation.")]
        [SerializeField] private MapDefinition _activeMapDefinition;

        [ColoredHeader("Environment Placement Anchor", HeaderColor.Teal)]
        [Tooltip("Parent transform for map geometry and boundary walls. Defaults to this transform if unassigned.")]
        [SerializeField] private Transform _mapRoot;

        [ColoredHeader("Boundary Generation", HeaderColor.Mint)]
        [Tooltip("If true, automatically creates 4 airtight invisible BoxCollider boundary walls enclosing the playable area at match start.")]
        [SerializeField] private bool _autoGenerateBoundaries = true;

        [Tooltip("Layer assigned to the generated perimeter boundary wall colliders.")]
        [SerializeField] private LayerMask _boundaryLayer = 1; // Default layer (bit 0)

        [ColoredHeader("Scene Instance Binding (Optional)", HeaderColor.Coral)]
        [Tooltip("Direct reference to a pre-placed map environment GameObject in the scene. Auto-wired at editor time or assigned via prefab instantiation.")]
        [ReadOnly]
        [SerializeField] private GameObject _environmentInstance;

        private GameObject _boundaryContainer;
        private Bounds _playableBounds;
        private bool _isInitialized;

        #region Public Properties

        public MapDefinition ActiveMap => _activeMapDefinition;
        public GameObject EnvironmentInstance => _environmentInstance;
        public Bounds PlayableBounds => _playableBounds;
        public float KillPlaneY => _activeMapDefinition != null ? _activeMapDefinition.KillPlaneY : -4.0f;
        public bool HasActiveMap => _activeMapDefinition != null;
        public bool IsInitialized => _isInitialized;

        #endregion

        #region Lifecycle & Initialization

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                throw new InvalidOperationException(
                    $"[{name}] MapManager: Multiple instances detected! Existing: '{s_instance.name}', Duplicate: '{name}'.");
            }
            s_instance = this;

            if (_mapRoot == null)
            {
                _mapRoot = transform;
            }

            ValidateConfiguration();
            InitializeMap();
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        /// <summary>
        /// Explicit top-down initialization. Sets the active map definition, instantiates environment geometry,
        /// and establishes physical boundaries.
        /// </summary>
        public void InitializeMap(MapDefinition overrideDefinition = null)
        {
            if (overrideDefinition != null)
            {
                _activeMapDefinition = overrideDefinition;
            }

            ValidateConfiguration();

            Vector3 origin = _mapRoot != null ? _mapRoot.position : transform.position;
            Vector2 halfExtents = _activeMapDefinition.PlayableHalfExtents;
            float wallHeight = _activeMapDefinition.BoundaryWallHeight;

            _playableBounds = new Bounds(
                new Vector3(origin.x, origin.y + (wallHeight * 0.5f), origin.z),
                new Vector3(halfExtents.x * 2.0f, wallHeight, halfExtents.y * 2.0f));

            InstantiateEnvironmentPrefab();

            if (_autoGenerateBoundaries)
            {
                GeneratePerimeterBoundaries(origin, halfExtents, wallHeight, _activeMapDefinition.BoundaryWallThickness);
            }

            _isInitialized = true;
        }

        private void InstantiateEnvironmentPrefab()
        {
            // If an environment instance is already bound in the scene (serialized in Inspector), use it directly with 0 search overhead
            if (_environmentInstance != null) return;

            if (_activeMapDefinition.EnvironmentPrefab == null) return;

            _environmentInstance = Instantiate(_activeMapDefinition.EnvironmentPrefab, _mapRoot);
            _environmentInstance.name = $"{_activeMapDefinition.MapName}_Environment";
            _environmentInstance.transform.localPosition = Vector3.zero;
            _environmentInstance.transform.localRotation = Quaternion.identity;
        }

        private void GeneratePerimeterBoundaries(Vector3 origin, Vector2 halfExtents, float height, float thickness)
        {
            if (_boundaryContainer != null)
            {
                Destroy(_boundaryContainer);
            }

            _boundaryContainer = new GameObject("MapBoundaries");
            _boundaryContainer.transform.SetParent(_mapRoot, false);
            _boundaryContainer.transform.position = origin;

            float hx = halfExtents.x;
            float hz = halfExtents.y;
            float halfThick = thickness * 0.5f;
            float yCenter = height * 0.5f;
            int layerIndex = ResolveBoundaryLayer();

            // North Wall (+Z)
            CreateBoundaryWall(
                "Boundary_North",
                new Vector3(0f, yCenter, hz + halfThick),
                new Vector3((hx * 2f) + (thickness * 2f), height, thickness),
                layerIndex);

            // South Wall (-Z)
            CreateBoundaryWall(
                "Boundary_South",
                new Vector3(0f, yCenter, -hz - halfThick),
                new Vector3((hx * 2f) + (thickness * 2f), height, thickness),
                layerIndex);

            // East Wall (+X)
            CreateBoundaryWall(
                "Boundary_East",
                new Vector3(hx + halfThick, yCenter, 0f),
                new Vector3(thickness, height, hz * 2f),
                layerIndex);

            // West Wall (-X)
            CreateBoundaryWall(
                "Boundary_West",
                new Vector3(-hx - halfThick, yCenter, 0f),
                new Vector3(thickness, height, hz * 2f),
                layerIndex);
        }

        private int ResolveBoundaryLayer()
        {
            int mask = _boundaryLayer.value;
            if (mask <= 0) return 0;
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0) return i;
            }
            return 0;
        }

        private void CreateBoundaryWall(string wallName, Vector3 localCenter, Vector3 size, int layerIndex)
        {
            GameObject wallObj = new GameObject(wallName);
            wallObj.layer = layerIndex;
            wallObj.transform.SetParent(_boundaryContainer.transform, false);
            wallObj.transform.localPosition = localCenter;

            BoxCollider boxCol = wallObj.AddComponent<BoxCollider>();
            boxCol.size = size;
            boxCol.isTrigger = false;
        }

        #endregion

        #region Spatial Query API

        /// <summary>
        /// Checks whether a world position lies within the horizontal playable area of the current map.
        /// </summary>
        public bool IsInsideBounds(Vector3 position, float margin = 0f)
        {
            Vector3 origin = _mapRoot != null ? _mapRoot.position : transform.position;
            Vector2 halfExtents = _activeMapDefinition != null
                ? _activeMapDefinition.PlayableHalfExtents
                : new Vector2(_playableBounds.extents.x, _playableBounds.extents.z);

            float minX = origin.x - (halfExtents.x - margin);
            float maxX = origin.x + (halfExtents.x - margin);
            float minZ = origin.z - (halfExtents.y - margin);
            float maxZ = origin.z + (halfExtents.y - margin);

            return position.x >= minX && position.x <= maxX &&
                   position.z >= minZ && position.z <= maxZ;
        }

        /// <summary>
        /// Clamps a world position to remain inside the playable area with an inward safety margin.
        /// </summary>
        public Vector3 ClampToBounds(Vector3 position, float margin = 0.5f)
        {
            Vector3 origin = _mapRoot != null ? _mapRoot.position : transform.position;
            Vector2 halfExtents = _activeMapDefinition != null
                ? _activeMapDefinition.PlayableHalfExtents
                : new Vector2(_playableBounds.extents.x, _playableBounds.extents.z);

            float minX = origin.x - (halfExtents.x - margin);
            float maxX = origin.x + (halfExtents.x - margin);
            float minZ = origin.z - (halfExtents.y - margin);
            float maxZ = origin.z + (halfExtents.y - margin);

            return new Vector3(
                Mathf.Clamp(position.x, minX, maxX),
                position.y,
                Mathf.Clamp(position.z, minZ, maxZ));
        }

        /// <summary>
        /// Checks whether a world position is below the lethal void kill-plane threshold.
        /// </summary>
        public bool IsBelowKillPlane(Vector3 position)
        {
            return position.y < KillPlaneY;
        }

        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_activeMapDefinition == null) return;

            Vector3 origin = _mapRoot != null ? _mapRoot.position : transform.position;
            Vector2 half = _activeMapDefinition.PlayableHalfExtents;
            float height = _activeMapDefinition.BoundaryWallHeight;

            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(
                new Vector3(origin.x, origin.y + (height * 0.5f), origin.z),
                new Vector3(half.x * 2f, height, half.y * 2f));

            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.5f);
            Gizmos.DrawLine(
                new Vector3(origin.x - half.x, _activeMapDefinition.KillPlaneY, origin.z - half.y),
                new Vector3(origin.x + half.x, _activeMapDefinition.KillPlaneY, origin.z + half.y));
        }
#endif
    }
}
