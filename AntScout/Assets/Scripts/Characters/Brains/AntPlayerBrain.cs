using System;
using AntScout.Characters;
using PGS.Camera.Core;
using PGS.Camera.VirtualCameras;
using PGS.Input.Bridges;
using PGS.Input.Readers;
using PGS.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Event-driven human player brain connecting PgsInputReader actions to AntCharacter.
    /// Reads ability-to-InputActionReference pairings directly from CharacterDefinition (Single Source of Truth)
    /// so no duplicate input references need to be wired on the prefab.
    /// Grounded in Single Responsibility (SRP), Open/Closed (OCP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AntCharacter))]
    public partial class AntPlayerBrain : PgsInputBridge
    {
        private AntCharacter _character;
        private bool _isEnabled = true;
        private InputActionReference _activeTrailInput;

        #region IPlayerInputProvider Implementation

        public bool IsEnabled
        {
            get => enabled && _isEnabled;
            set
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    ResetInput();
                }
            }
        }

        public PlayerInputContext CurrentContext
        {
            get
            {
                if (!enabled || !_isEnabled || InputReader == null)
                {
                    return PlayerInputContext.Empty;
                }

                Vector2 rawMove = InputReader.Primary2D;
                Vector3 moveVector = new Vector3(rawMove.x, 0f, rawMove.y);

                return new PlayerInputContext(
                    moveVector: moveVector,
                    rawMove: rawMove,
                    facingDirection: null,
                    facingPoint: null,
                    lookDelta: InputReader.Secondary2D);
            }
        }

        public void ResetInput()
        {
            _activeTrailInput = null;
            if (_character != null && _character.Gland != null)
            {
                _character.Gland.StopLayingTrail();
            }
        }

        #endregion

        #region Lifecycle & Initialization

        protected override void Awake()
        {
            ValidateConfiguration();
            base.Awake();
        }

        private void Start()
        {
            if (enabled && InputReader != null && _character != null && _character.Definition != null)
            {
                _character.Definition.RegisterInputActions(InputReader);
            }

            AttachCameraIfActive();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            AttachCameraIfActive();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetInput();
            DetachCameraIfTargeted();
        }

        private void AttachCameraIfActive()
        {
            if (!enabled || !isActiveAndEnabled || _character == null) return;

            var cameraController = CameraController.GetController(PlayerIndex) ?? CameraController.ActiveInstance;
            if (cameraController != null && cameraController.ActiveVirtualCamera != null && cameraController.ActiveVirtualCamera.Target == null)
            {
                cameraController.ActiveVirtualCamera.Target = _character;
            }
        }

        private void DetachCameraIfTargeted()
        {
            var cameraController = CameraController.GetController(PlayerIndex) ?? CameraController.ActiveInstance;
            if (cameraController != null && cameraController.ActiveVirtualCamera != null &&
                ReferenceEquals(cameraController.ActiveVirtualCamera.Target, _character))
            {
                cameraController.ActiveVirtualCamera.Target = null;
            }
        }

        #endregion

        #region PgsInputBridge Callbacks

        protected override void OnSubscribe(PgsInputReader reader)
        {
            _character ??= GetComponent<AntCharacter>();
            if (_character != null && _character.Definition != null)
            {
                _character.Definition.RegisterInputActions(reader);
            }

            reader.OnActionTriggered += HandleActionTriggered;
            reader.OnActionReleased += HandleActionReleased;
            reader.OnScalarDelta += HandleCameraZoom;
        }

        protected override void OnUnsubscribe(PgsInputReader reader)
        {
            reader.OnActionTriggered -= HandleActionTriggered;
            reader.OnActionReleased -= HandleActionReleased;
            reader.OnScalarDelta -= HandleCameraZoom;
        }

        protected override void OnResetInput()
        {
            ResetInput();
        }

        #endregion

        #region Action Handlers

        private void HandleActionTriggered(InputActionReference triggeredInput)
        {
            if (!enabled || !_isEnabled || triggeredInput == null || _character == null || _character.Definition == null)
            {
                return;
            }

            if (_character.Definition.TryGetCombatAction(triggeredInput, out var combatAction))
            {
                Vector3 aimDir = CalculateAimDirection();
                _character.TryExecuteAction(combatAction, aimDir);
            }

            if (_character.Gland != null && _character.Definition.TryGetTrailAction(triggeredInput, out var trailAction))
            {
                _activeTrailInput = triggeredInput;
                _character.Gland.StartLayingTrail(trailAction);
            }
        }

        private void HandleActionReleased(InputActionReference releasedInput)
        {
            if (_activeTrailInput != null && releasedInput != null &&
                _activeTrailInput.action != null && releasedInput.action != null &&
                _activeTrailInput.action.id == releasedInput.action.id)
            {
                _activeTrailInput = null;
                if (_character != null && _character.Gland != null)
                {
                    _character.Gland.StopLayingTrail();
                }
            }
        }

        private void HandleCameraZoom(float delta)
        {
            if (!enabled || !_isEnabled || Mathf.Abs(delta) <= 0.0001f)
            {
                return;
            }

            var cameraController = CameraController.GetController(PlayerIndex) ?? CameraController.ActiveInstance;
            if (cameraController != null && cameraController.ActiveVirtualCamera is IsometricVirtualCamera isoCam)
            {
                isoCam.ApplyZoomDelta(delta);
            }
        }

        #endregion

        #region Helpers

        private Vector3 CalculateAimDirection()
        {
            Vector2 rawMove = InputReader != null ? InputReader.Primary2D : Vector2.zero;
            Vector3 worldMove = new Vector3(rawMove.x, 0f, rawMove.y);

            if (worldMove.sqrMagnitude > 0.01f)
            {
                return worldMove.normalized;
            }

            if (_character != null && _character.Motor != null)
            {
                return _character.Motor.FacingForward;
            }

            return transform.forward;
        }

        #endregion
    }
}
