using System;
using PGS.Core;
using PGS.Player.Input;
using UnityEngine;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Master brain controller attached to an AntCharacter chassis.
    /// Acts as the single composite IPlayerInputProvider for PlayerController (com.pgs.player).
    /// Dynamically controls which brain drives the chassis: human controls (AntPlayerBrain)
    /// or autonomous AI logic (AntAiBrain) based on the configured BrainMode.
    /// Follows the Composite Pattern (GoF), Single Responsibility (SRP), and Dependency Inversion (DIP).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AntCharacter))]
    [RequireComponent(typeof(AntPlayerBrain))]
    [RequireComponent(typeof(AntAiBrain))]
    public partial class AntBrainController : MonoBehaviour, IPlayerInputProvider
    {
        [Header("Brain Selection")]
        [Tooltip("Specifies which brain is currently controlling the ant (Player vs AI).")]
        [SerializeField] private BrainMode _brainMode = BrainMode.Player;

        [Header("Brain Components")]
        [Tooltip("Human player input brain.")]
        [ReadOnly]
        [SerializeField] private AntPlayerBrain _playerBrain;

        [Tooltip("Autonomous AI decision brain.")]
        [ReadOnly]
        [SerializeField] private AntAiBrain _aiBrain;

        private bool _isEnabled = true;

        public BrainMode Mode => _brainMode;
        public AntPlayerBrain PlayerBrain => _playerBrain;
        public AntAiBrain AiBrain => _aiBrain;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_playerBrain == null) _playerBrain = GetComponent<AntPlayerBrain>();
            if (_aiBrain == null) _aiBrain = GetComponent<AntAiBrain>();
        }
#endif

        #region IPlayerInputProvider Implementation

        public bool IsEnabled
        {
            get => enabled && _isEnabled;
            set
            {
                _isEnabled = value;
                if (_playerBrain != null)
                {
                    _playerBrain.IsEnabled = value;
                }
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
                if (!enabled || !_isEnabled)
                {
                    return PlayerInputContext.Empty;
                }

                if (_brainMode == BrainMode.Player)
                {
                    return (_playerBrain != null && _playerBrain.enabled && _playerBrain.IsEnabled)
                        ? _playerBrain.CurrentContext
                        : PlayerInputContext.Empty;
                }
                else
                {
                    if (_aiBrain == null || !_aiBrain.enabled)
                    {
                        return PlayerInputContext.Empty;
                    }

                    Vector3 move = _aiBrain.DesiredMovement;
                    Vector2 raw = new Vector2(move.x, move.z);

                    return new PlayerInputContext(
                        moveVector: move,
                        rawMove: raw,
                        facingDirection: _aiBrain.DesiredFacing,
                        facingPoint: null,
                        lookDelta: Vector2.zero);
                }
            }
        }

        public void ResetInput()
        {
            if (_playerBrain != null)
            {
                _playerBrain.ResetInput();
            }

            if (_aiBrain != null)
            {
                _aiBrain.ResetBrain();
            }
        }

        #endregion

        #region Lifecycle & Coordination

        private void Awake()
        {
            _playerBrain ??= GetComponent<AntPlayerBrain>();
            _aiBrain ??= GetComponent<AntAiBrain>();

            ValidateConfiguration();
            SetBrainMode(_brainMode);
        }

        private void OnEnable()
        {
            SetBrainMode(_brainMode);
        }

        private void OnDisable()
        {
            ResetInput();
            if (_playerBrain != null) _playerBrain.enabled = false;
            if (_aiBrain != null) _aiBrain.enabled = false;
        }

        /// <summary>
        /// Switches the active brain at runtime and updates component lifecycles.
        /// </summary>
        public void SetBrainMode(BrainMode mode)
        {
            _brainMode = mode;

            if (_playerBrain != null)
            {
                _playerBrain.enabled = (mode == BrainMode.Player);
                if (mode != BrainMode.Player)
                {
                    _playerBrain.ResetInput();
                }
            }

            if (_aiBrain != null)
            {
                _aiBrain.enabled = (mode == BrainMode.AI);
                if (mode != BrainMode.AI)
                {
                    _aiBrain.ResetBrain();
                }
            }
        }

        #endregion
    }
}
