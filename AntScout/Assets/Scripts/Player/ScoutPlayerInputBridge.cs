using System;
using AntScout.Core.Enums;
using PGS.Input.Bridges;
using PGS.Input.Readers;
using PGS.Player.Input;
using PGS.Player.Motor.ThreeD;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AntScout.Player
{
    /// <summary>
    /// Event-driven, zero-polling input bridge connecting PGS.Input to AntScout and ScoutHero.
    /// Strictly adheres to com.pgs.input and com.pgs.player architecture:
    /// - Zero hardcoded action verbs (customizable via InputActionReference or auto-resolved from PgsControls).
    /// - Zero legacy polling (reactive event callbacks; no per-frame hardware polling).
    /// - Strict Hexagonal Isolation (DIP): Bridges PGS.Input, PGS.Player, and AntScout domain mechanics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoutPlayerInputBridge : PgsInputBridge, IPlayerInputProvider
    {
        [Header("Action Bindings (Generic Action Bus)")]
        [Tooltip("Action triggering dash evasion (default: Player/Jump / Space).")]
        [SerializeField] private InputActionReference _dashAction;

        [Tooltip("Action triggering alarm pheromone trails (default: Player/Interact / 'E').")]
        [SerializeField] private InputActionReference _alarmAction;

        [Tooltip("Action triggering recruitment pheromone trails (default: Player/Sprint / 'Shift').")]
        [SerializeField] private InputActionReference _recruitmentAction;

        [Header("Perspective Camera (Optional)")]
        [Tooltip("Camera for ground-plane directional projection. Defaults to Camera.main if unassigned.")]
        [SerializeField] private Camera _targetCamera;

        private ScoutHero _hero;
        private InputAction _resolvedDash;
        private InputAction _resolvedAlarm;
        private InputAction _resolvedRecruitment;
        private bool _isEnabled = true;

        #region Public Properties

        public InputActionReference DashAction => _dashAction;
        public InputActionReference AlarmAction => _alarmAction;
        public InputActionReference RecruitmentAction => _recruitmentAction;

        #endregion

        #region IPlayerInputProvider Implementation

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    ResetInput();
                }
            }
        }

        /// <summary>
        /// Produces a stack-allocated PlayerInputContext snapshot on demand for PlayerController.
        /// Zero per-tick polling overhead.
        /// </summary>
        public PlayerInputContext CurrentContext
        {
            get
            {
                if (!_isEnabled || InputReader == null)
                {
                    return PlayerInputContext.Empty;
                }

                if (_targetCamera == null)
                {
                    ResolveCamera();
                }

                Vector2 rawMove = InputReader.Primary2D;
                Vector3 worldMove = CalculateCameraRelativeMove(rawMove, _targetCamera);

                uint pressedMask = 0;
                uint heldMask = 0;

                // Channel 1 (Secondary): Recruitment Pheromone / Sprint
                if (IsRecruitmentHeld())
                {
                    heldMask |= (1u << (int)TraversalActionChannel.Secondary);
                }

                // Channel 3 (Utility): Dash Impulse
                if (IsDashTriggered())
                {
                    pressedMask |= (1u << (int)TraversalActionChannel.Utility);
                }
                if (IsDashHeld())
                {
                    heldMask |= (1u << (int)TraversalActionChannel.Utility);
                }

                return new PlayerInputContext(
                    moveVector: worldMove,
                    rawMove: rawMove,
                    facingDirection: null,
                    facingPoint: null,
                    lookDelta: InputReader.Secondary2D,
                    pressedActionMask: pressedMask,
                    heldActionMask: heldMask);
            }
        }

        public void ResetInput()
        {
            if (_hero != null)
            {
                _hero.SetLayingTrail(false);
            }
        }

        #endregion

        #region Lifecycle & Initialization

        public void InitializeBridge(ScoutHero hero)
        {
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            ResolveCamera();
        }

        protected override void Awake()
        {
            base.Awake();
            if (_hero == null)
            {
                _hero = GetComponent<ScoutHero>();
            }
            ResolveCamera();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (_hero == null)
            {
                _hero = GetComponent<ScoutHero>();
            }
            ResolveCamera();
        }
#endif

        private void ResolveCamera()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }
        }

        #endregion

        #region PgsInputBridge Lifecycle Callbacks

        protected override void OnSubscribe(PgsInputReader reader)
        {
            ResolveActionInstances(reader);

            if (_dashAction != null) reader.RegisterAction(_dashAction);
            if (_alarmAction != null) reader.RegisterAction(_alarmAction);
            if (_recruitmentAction != null) reader.RegisterAction(_recruitmentAction);

            reader.OnActionTriggered += HandleActionTriggered;
            reader.OnActionReleased += HandleActionReleased;
        }

        protected override void OnUnsubscribe(PgsInputReader reader)
        {
            reader.OnActionTriggered -= HandleActionTriggered;
            reader.OnActionReleased -= HandleActionReleased;

            if (_dashAction != null) reader.UnregisterAction(_dashAction);
            if (_alarmAction != null) reader.UnregisterAction(_alarmAction);
            if (_recruitmentAction != null) reader.UnregisterAction(_recruitmentAction);
        }

        protected override void OnResetInput()
        {
            ResetInput();
        }

        private void ResolveActionInstances(PgsInputReader reader)
        {
            InputActionAsset asset = reader.ActionsAsset;
            if (asset == null) return;

            _resolvedDash = _dashAction != null ? _dashAction.action : asset.FindAction("Player/Jump");
            _resolvedAlarm = _alarmAction != null ? _alarmAction.action : asset.FindAction("Player/Interact");
            _resolvedRecruitment = _recruitmentAction != null ? _recruitmentAction.action : asset.FindAction("Player/Sprint");
        }

        #endregion

        #region Event Handlers & Execution

        private void HandleActionTriggered(InputActionReference actionRef)
        {
            if (!_isEnabled || actionRef == null) return;

            if (MatchesAction(actionRef, _resolvedDash))
            {
                ExecuteDash();
            }
            else if (MatchesAction(actionRef, _resolvedAlarm))
            {
                _hero?.SetLayingTrail(true, PheromoneType.Alarm);
            }
            else if (MatchesAction(actionRef, _resolvedRecruitment))
            {
                _hero?.SetLayingTrail(true, PheromoneType.Recruitment);
            }
        }

        private void HandleActionReleased(InputActionReference actionRef)
        {
            if (!_isEnabled || actionRef == null) return;

            if (MatchesAction(actionRef, _resolvedAlarm) || MatchesAction(actionRef, _resolvedRecruitment))
            {
                UpdateGlandState();
            }
        }

        private void UpdateGlandState()
        {
            if (_hero == null || InputReader == null) return;

            bool isAlarm = IsAlarmHeld();
            bool isRecruitment = IsRecruitmentHeld();

            if (isAlarm)
            {
                _hero.SetLayingTrail(true, PheromoneType.Alarm);
            }
            else if (isRecruitment)
            {
                _hero.SetLayingTrail(true, PheromoneType.Recruitment);
            }
            else
            {
                _hero.SetLayingTrail(false);
            }
        }

        private void ExecuteDash()
        {
            if (_hero == null) return;

            Vector2 rawMove = InputReader != null ? InputReader.Primary2D : Vector2.zero;
            Vector3 worldMove = CalculateCameraRelativeMove(rawMove, _targetCamera);

            Vector3 dashTargetDir;
            if (worldMove.sqrMagnitude > 0.01f)
            {
                dashTargetDir = worldMove;
            }
            else if (_hero.Motor != null)
            {
                dashTargetDir = _hero.Motor.FacingForward;
            }
            else
            {
                dashTargetDir = transform.forward;
            }

            _hero.TryDash(dashTargetDir);
        }

        private bool IsDashTriggered()
        {
            if (_dashAction != null && InputReader != null) return InputReader.WasActionTriggered(_dashAction);
            return _resolvedDash != null && _resolvedDash.WasPressedThisFrame();
        }

        private bool IsDashHeld()
        {
            if (_dashAction != null && InputReader != null) return InputReader.IsActionHeld(_dashAction);
            return _resolvedDash != null && _resolvedDash.IsPressed();
        }

        private bool IsAlarmHeld()
        {
            if (_alarmAction != null && InputReader != null) return InputReader.IsActionHeld(_alarmAction);
            return _resolvedAlarm != null && _resolvedAlarm.IsPressed();
        }

        private bool IsRecruitmentHeld()
        {
            if (_recruitmentAction != null && InputReader != null) return InputReader.IsActionHeld(_recruitmentAction);
            return _resolvedRecruitment != null && _resolvedRecruitment.IsPressed();
        }

        private static bool MatchesAction(InputActionReference reference, InputAction action)
        {
            if (reference == null || action == null) return false;
            return reference.action == action || reference.action.id == action.id;
        }

        private static Vector3 CalculateCameraRelativeMove(Vector2 raw, Camera cam)
        {
            if (cam == null || raw.sqrMagnitude < 0.001f)
            {
                return new Vector3(raw.x, 0f, raw.y);
            }

            Transform camTransform = cam.transform;
            Vector3 forward = camTransform.forward;
            forward.y = 0f;

            // Top-down camera pointing straight down (pitch ~90°): project camera's up vector onto horizontal plane
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = camTransform.up;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f)
                {
                    forward = Vector3.forward;
                }
            }
            forward.Normalize();

            Vector3 right = camTransform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.001f)
            {
                right = Vector3.right;
            }
            right.Normalize();

            Vector3 worldMove = (forward * raw.y) + (right * raw.x);
            return Vector3.ClampMagnitude(worldMove, 1.0f);
        }

        #endregion
    }
}
