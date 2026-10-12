using System;
using PGS.Camera.Core;
using PGS.Camera.Interfaces;
using PGS.Camera.VirtualCameras;
using PGS.Input.Bridges;
using PGS.Input.Readers;
using UnityEngine;

namespace AntScout.CameraSystem
{
    /// <summary>
    /// Bridges event-driven hardware input from PGS.Input to IsometricVirtualCamera.
    /// Preserves strict Dependency Inversion (DIP) so com.pgs.camera has zero dependency on com.pgs.input.
    /// Inherits from <see cref="PgsInputBridge"/> to guarantee leak-proof event subscription lifecycles.
    /// Handles camera boom distance zooming via scroll wheel or analog input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsometricCameraInputBridge : PgsInputBridge
    {
        [Header("Target References")]
        [Tooltip("The isometric virtual camera to drive with zoom deltas.")]
        [SerializeField] private IsometricVirtualCamera _virtualCamera;

        protected override void Awake()
        {
            base.Awake();

            if (_virtualCamera == null)
            {
                _virtualCamera = GetComponent<IsometricVirtualCamera>();
            }

            if (_virtualCamera == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] IsometricCameraInputBridge requires an IsometricVirtualCamera component on itself or assigned in the inspector.");
            }
        }

        private void Start()
        {
            if (_virtualCamera != null && _virtualCamera.Target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    var cameraTarget = player.GetComponent<ICameraTarget>() ?? player.AddComponent<CameraTargetTransform>();
                    _virtualCamera.SetTarget(cameraTarget);
                }
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (_virtualCamera == null)
            {
                _virtualCamera = GetComponent<IsometricVirtualCamera>();
            }
        }
#endif

        #region PgsInputBridge Lifecycle Hooks

        protected override void OnSubscribe(PgsInputReader reader)
        {
            reader.OnScalarDelta += HandleZoom;
        }

        protected override void OnUnsubscribe(PgsInputReader reader)
        {
            reader.OnScalarDelta -= HandleZoom;
        }

        protected override void OnResetInput()
        {
            // Zoom decay is handled natively by the virtual camera's dampening solver
        }

        #endregion

        #region Event Callbacks

        private void HandleZoom(float delta)
        {
            if (_virtualCamera != null && Mathf.Abs(delta) > 0.0001f)
            {
                _virtualCamera.ApplyZoomDelta(delta);
            }
        }

        #endregion
    }
}
