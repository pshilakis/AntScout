using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AntScout.Rendering
{
    /// <summary>
    /// Master runtime display optimizer for WebGL and high-DPI displays.
    /// Dynamically decouples 3D scene render resolution from UI Canvas resolution by clamping
    /// UniversalRenderPipelineAsset.renderScale to a designer-tuned maximum vertical height (e.g. 1080p).
    /// Prevents catastrophic 400%-800% GPU fillrate spikes on 1440p and 4K displays in fullscreen
    /// while keeping HUD, text, and health bars rendered at 100% native monitor sharpness.
    /// Strictly adheres to Single Responsibility (SRP) and Open/Closed (OCP).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class WebGlDisplayOptimizer : MonoBehaviour
    {
        private static WebGlDisplayOptimizer s_instance;
        public static WebGlDisplayOptimizer Instance => s_instance;

        [Header("Resolution Clamping")]
        [Tooltip("Maximum vertical resolution in pixels for 3D scene rendering. Fullscreen displays above this height (e.g. 1440p, 4K) are scaled down to match.")]
        [Range(540, 1440)]
        [SerializeField] private int _targetMaxRenderHeight = 1080;

        [Tooltip("Minimum allowed render scale multiplier under extreme resolutions.")]
        [Range(0.25f, 0.75f)]
        [SerializeField] private float _minRenderScale = 0.5f;

        [Tooltip("Maximum allowed render scale multiplier.")]
        [Range(0.75f, 1.0f)]
        [SerializeField] private float _maxRenderScale = 1.0f;

        [Header("Editor & Testing")]
        [Tooltip("If true, simulates resolution clamping in the Unity Editor Game View. Defaults to false in Editor to avoid asset dirtying.")]
        [SerializeField] private bool _enableInEditor = false;

        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _lastFullscreen;
        private float _originalRenderScale = 1.0f;
        private UniversalRenderPipelineAsset _urpAsset;

        public int TargetMaxRenderHeight => _targetMaxRenderHeight;
        public float CurrentAppliedRenderScale => _urpAsset != null ? _urpAsset.renderScale : 1.0f;

        #region Runtime Bootstrap

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (s_instance != null) return;

            GameObject bootstrapObj = new GameObject("[ WebGL Display Optimizer ]");
            DontDestroyOnLoad(bootstrapObj);
            s_instance = bootstrapObj.AddComponent<WebGlDisplayOptimizer>();
        }

        #endregion

        #region Lifecycle & Frame Pacing

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            DontDestroyOnLoad(gameObject);

            ResolvePipelineAsset();
            ConfigureFramePacing();
            ApplyDisplayOptimization(force: true);
        }

        private void OnEnable()
        {
            ApplyDisplayOptimization(force: true);
        }

        private void OnDisable()
        {
            RestoreOriginalRenderScale();
        }

        private void OnDestroy()
        {
            RestoreOriginalRenderScale();
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Update()
        {
            // Detect window resize or fullscreen transition
            if (Screen.width != _lastScreenWidth || 
                Screen.height != _lastScreenHeight || 
                Screen.fullScreen != _lastFullscreen)
            {
                ApplyDisplayOptimization(force: false);
            }
        }

        #endregion

        #region Optimization Logic

        private void ResolvePipelineAsset()
        {
            RenderPipelineAsset currentPipeline = GraphicsSettings.currentRenderPipeline ?? QualitySettings.renderPipeline;
            if (currentPipeline is UniversalRenderPipelineAsset urp)
            {
                _urpAsset = urp;
                _originalRenderScale = urp.renderScale;
            }
            else
            {
                Debug.LogWarning("[WebGlDisplayOptimizer] Active render pipeline is not a UniversalRenderPipelineAsset. Dynamic render scale disabled.");
            }
        }

        private void ConfigureFramePacing()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // In WebGL, browser requestAnimationFrame syncs to display refresh.
            // Clamping targetFrameRate and enabling VSync prevents animation loop thrashing.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
#endif
        }

        /// <summary>
        /// Evaluates current screen dimensions and applies optimal render scale to URP.
        /// </summary>
        public void ApplyDisplayOptimization(bool force = false)
        {
            int currentWidth = Screen.width;
            int currentHeight = Screen.height;
            bool currentFullscreen = Screen.fullScreen;

            if (!force && currentWidth == _lastScreenWidth && currentHeight == _lastScreenHeight && currentFullscreen == _lastFullscreen)
            {
                return;
            }

            _lastScreenWidth = currentWidth;
            _lastScreenHeight = currentHeight;
            _lastFullscreen = currentFullscreen;

            if (_urpAsset == null)
            {
                ResolvePipelineAsset();
                if (_urpAsset == null) return;
            }

#if UNITY_EDITOR
            if (!_enableInEditor)
            {
                _urpAsset.renderScale = 1.0f;
                return;
            }
#endif

            if (currentHeight <= 0) return;

            // Clamping formula: If screen height exceeds target max (e.g. 1440p or 4K), scale down 3D render buffer
            float idealScale = (float)_targetMaxRenderHeight / currentHeight;
            float clampedScale = Mathf.Clamp(idealScale, _minRenderScale, _maxRenderScale);

            // Round to 3 decimal places to avoid micro float jitter
            clampedScale = Mathf.Round(clampedScale * 1000f) / 1000f;

            if (Mathf.Abs(_urpAsset.renderScale - clampedScale) > 0.005f)
            {
                _urpAsset.renderScale = clampedScale;
                Debug.Log($"[WebGlDisplayOptimizer] Display: {currentWidth}x{currentHeight} (Fullscreen: {currentFullscreen}) -> URP RenderScale set to {clampedScale:F2} (Effective 3D Buffer: ~{Mathf.RoundToInt(currentWidth * clampedScale)}x{Mathf.RoundToInt(currentHeight * clampedScale)})");
            }
        }

        private void RestoreOriginalRenderScale()
        {
            if (_urpAsset != null)
            {
                _urpAsset.renderScale = _originalRenderScale;
            }
        }

        #endregion
    }
}
