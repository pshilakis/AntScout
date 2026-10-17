using System;
using System.Collections.Generic;
using AntScout.FogOfWar.Config;
using AntScout.FogOfWar.Interfaces;
using PGS.Core.Time;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntScout.FogOfWar.Core
{
    /// <summary>
    /// Central manager coordinating vision revealers, updating the 2D Fog grid on TimeChannel.World,
    /// and publishing the baked dual-channel texture to global shader properties.
    /// Implements IFogOfWarService and IUpdatable.
    /// </summary>
    public class FogOfWarManager : MonoBehaviour, IFogOfWarService, IUpdatable
    {
        private static readonly int s_fogTextureProp = Shader.PropertyToID("_FogOfWarTexture");
        private static readonly int s_fogBoundsProp = Shader.PropertyToID("_FogOfWarBounds");
        private static readonly int s_unexploredColorProp = Shader.PropertyToID("_FogOfWarUnexploredColor");
        private static readonly int s_exploredColorProp = Shader.PropertyToID("_FogOfWarExploredColor");
        private static readonly int s_fogEnabledProp = Shader.PropertyToID("_FogOfWarEnabled");

        [Header("Configuration")]
        [Tooltip("Source of truth for map bounds, grid resolution, and visual parameters.")]
        [SerializeField] private FogOfWarDefinition _config;

        [Tooltip("Optional reference to the M_FogOfWar_Fullscreen material. Ensures direct material binding in addition to global shader properties.")]
        [SerializeField] private Material _fogMaterial;

        [Header("Editor & Presentation")]
        [Tooltip("If enabled, Fog of War will also render in the Scene View during Play Mode. Kept false by default for unobstructed level editing.")]
        [SerializeField] private bool _renderInSceneViewDuringPlay = false;

        [Header("Colony Affiliation")]
        [Tooltip("The local player's colony team identifier (default: 0). Fog is rendered from this perspective.")]
        [SerializeField] private int _localColonyTeamId = 0;

        private static readonly List<IVisionRevealer> s_pendingRevealers = new List<IVisionRevealer>();
        private readonly List<IVisionRevealer> _revealers = new List<IVisionRevealer>(64);

        private static readonly List<IFogCullable> s_pendingCullables = new List<IFogCullable>();
        private readonly List<IFogCullable> _cullables = new List<IFogCullable>(128);

        private FogOfWarGrid _grid;
        private Texture2D _fogTexture;

        public static FogOfWarManager Instance { get; private set; }
        public static IFogOfWarService Service => Instance;

        public Texture2D FogTexture => _fogTexture;
        public FogOfWarDefinition Config => _config;
        public int LocalColonyTeamId => _localColonyTeamId;

        public static void Register(IVisionRevealer revealer)
        {
            if (revealer == null) return;
            if (Instance != null)
            {
                Instance.RegisterRevealer(revealer);
            }
            else if (!s_pendingRevealers.Contains(revealer))
            {
                s_pendingRevealers.Add(revealer);
            }
        }

        public static void Unregister(IVisionRevealer revealer)
        {
            if (revealer == null) return;
            if (Instance != null)
            {
                Instance.UnregisterRevealer(revealer);
            }
            s_pendingRevealers.Remove(revealer);
        }

        public static void RegisterCullable(IFogCullable cullable)
        {
            if (cullable == null) return;
            if (Instance != null)
            {
                Instance.RegisterCullableInstance(cullable);
            }
            else if (!s_pendingCullables.Contains(cullable))
            {
                s_pendingCullables.Add(cullable);
            }
        }

        public static void UnregisterCullable(IFogCullable cullable)
        {
            if (cullable == null) return;
            if (Instance != null)
            {
                Instance.UnregisterCullableInstance(cullable);
            }
            s_pendingCullables.Remove(cullable);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_config == null)
            {
                throw new InvalidOperationException($"[{name}] Missing required FogOfWarDefinition on FogOfWarManager!");
            }
            _config.Validate(name);

            _grid = new FogOfWarGrid(_config);
            CreateFogTexture();
            UpdateShaderGlobals();

            for (int i = 0; i < s_pendingRevealers.Count; i++)
            {
                RegisterRevealer(s_pendingRevealers[i]);
            }
            s_pendingRevealers.Clear();

            for (int i = 0; i < s_pendingCullables.Count; i++)
            {
                RegisterCullableInstance(s_pendingCullables[i]);
            }
            s_pendingCullables.Clear();
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Medium, TimeChannel.World);
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            SetFogActiveGlobally(false);
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null) return;

            bool enableFog = false;

            if (Application.isPlaying)
            {
                if (camera.cameraType == CameraType.Game)
                {
                    enableFog = true;
                }
                else if (camera.cameraType == CameraType.SceneView && _renderInSceneViewDuringPlay)
                {
                    enableFog = true;
                }
            }

            SetFogActiveGlobally(enableFog);
        }

        private void SetFogActiveGlobally(bool isActive)
        {
            float val = isActive ? 1f : 0f;
            Shader.SetGlobalFloat(s_fogEnabledProp, val);
            if (_fogMaterial != null && _fogMaterial.HasProperty(s_fogEnabledProp))
            {
                _fogMaterial.SetFloat(s_fogEnabledProp, val);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_fogTexture != null)
            {
                Destroy(_fogTexture);
                _fogTexture = null;
            }
        }

        public void SetLocalColonyTeam(int teamId)
        {
            _localColonyTeamId = teamId;
        }

        public void RegisterRevealer(IVisionRevealer revealer)
        {
            if (revealer == null || _revealers.Contains(revealer)) return;
            _revealers.Add(revealer);
        }

        public void UnregisterRevealer(IVisionRevealer revealer)
        {
            if (revealer == null) return;
            _revealers.Remove(revealer);
        }

        public void RegisterCullableInstance(IFogCullable cullable)
        {
            if (cullable == null || _cullables.Contains(cullable)) return;
            _cullables.Add(cullable);
        }

        public void UnregisterCullableInstance(IFogCullable cullable)
        {
            if (cullable == null) return;
            _cullables.Remove(cullable);
        }

        public void OnUpdate(float deltaTime)
        {
            if (_grid == null || _fogTexture == null) return;

            _grid.ResetFrame();

            // Rasterize active revealers belonging to the local colony
            for (int i = _revealers.Count - 1; i >= 0; i--)
            {
                IVisionRevealer revealer = _revealers[i];
                if (revealer == null)
                {
                    _revealers.RemoveAt(i);
                    continue;
                }

                if (!revealer.IsRevealerActive || revealer.ColonyTeamId != _localColonyTeamId)
                {
                    continue;
                }

                _grid.RasterizeCircle(revealer.RevealerPosition, revealer.VisionRadius);
            }

            _grid.BakeToTexture(_fogTexture);

            UpdateEntityCulling();
        }

        private void UpdateEntityCulling()
        {
            for (int i = _cullables.Count - 1; i >= 0; i--)
            {
                IFogCullable cullable = _cullables[i];
                if (cullable == null)
                {
                    _cullables.RemoveAt(i);
                    continue;
                }

                if (!cullable.IsCullableActive)
                {
                    continue;
                }

                if (cullable.ColonyTeamId == _localColonyTeamId)
                {
                    cullable.OnFogVisibilityEvaluated(FogVisibilityState.Visible);
                    continue;
                }

                FogVisibilityState state = _grid.GetVisibilityState(cullable.CullPosition);
                cullable.OnFogVisibilityEvaluated(state);
            }
        }

        public FogVisibilityState GetVisibilityState(Vector3 worldPosition, int colonyTeamId)
        {
            if (_grid == null) return FogVisibilityState.Unexplored;
            return _grid.GetVisibilityState(worldPosition);
        }

        public bool IsVisible(Vector3 worldPosition, int colonyTeamId)
        {
            return _grid != null && _grid.IsVisible(worldPosition);
        }

        public bool IsExplored(Vector3 worldPosition, int colonyTeamId)
        {
            return _grid != null && _grid.IsExplored(worldPosition);
        }

        private void CreateFogTexture()
        {
            int res = _config.Resolution;
            _fogTexture = new Texture2D(res, res, TextureFormat.RGBA32, false, true)
            {
                name = "FogOfWar_DynamicTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            // Initial blank bake
            _grid.BakeToTexture(_fogTexture);
        }

        private void UpdateShaderGlobals()
        {
            if (_fogTexture == null || _config == null) return;

            Vector4 bounds = new Vector4(
                _config.MapMin.x,
                _config.MapMin.y,
                _config.WorldWidth,
                _config.WorldHeight);

            if (_fogMaterial != null && _fogMaterial.HasProperty(s_fogTextureProp))
            {
                _fogMaterial.SetTexture(s_fogTextureProp, _fogTexture);
                _fogMaterial.SetVector(s_fogBoundsProp, bounds);
                _fogMaterial.SetColor(s_unexploredColorProp, _config.UnexploredColor);
                _fogMaterial.SetColor(s_exploredColorProp, _config.ExploredColor);
            }

            Shader.SetGlobalTexture(s_fogTextureProp, _fogTexture);
            Shader.SetGlobalVector(s_fogBoundsProp, bounds);
            Shader.SetGlobalColor(s_unexploredColorProp, _config.UnexploredColor);
            Shader.SetGlobalColor(s_exploredColorProp, _config.ExploredColor);
        }

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
            Vector2 min = _config.MapMin;
            Vector2 max = _config.MapMax;
            Vector3 center = new Vector3((min.x + max.x) * 0.5f, 0f, (min.y + max.y) * 0.5f);
            Vector3 size = new Vector3(_config.WorldWidth, 1f, _config.WorldHeight);
            Gizmos.DrawWireCube(center, size);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void ResetFogOnEditorLoad()
        {
            Shader.SetGlobalFloat("_FogOfWarEnabled", 0f);
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode || state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    Shader.SetGlobalFloat("_FogOfWarEnabled", 0f);
                }
            };
        }
#endif
    }
}
