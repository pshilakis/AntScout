using System;
using AntScout.Config;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Player
{
    /// <summary>
    /// Manages the Scout Ant's chemical gland reserves, handling pheromone depletion and passive regeneration.
    /// Interacts with the trail emitter when the gland is triggered.
    /// Implements IUpdatable to receive deterministic frame updates on TimeChannel.Player.
    /// </summary>
    public class ScoutGland : MonoBehaviour, IUpdatable
    {
        [Header("Configuration")]
        [Tooltip("Source of truth for gland capacity, drain, and recharge rates.")]
        [SerializeField] private ScoutConfigSO _config;

        [Header("Emitter Dependency")]
        [Tooltip("Pheromone emitter responsible for tracking and storing deposited scent nodes.")]
        [SerializeField] private PheromoneTrailEmitter _emitter;

        private float _currentCapacity;
        private bool _isLayingTrail;
        private bool _isDepleted;
        private PheromoneType _activeScentType = PheromoneType.Recruitment;

        public event Action<float, float> OnCapacityChanged; // (current, max)

        public float CurrentCapacity => _currentCapacity;
        public float MaxCapacity => _config != null ? _config.MaxGlandCapacity : 100f;
        public float NormalizedCapacity => MaxCapacity > 0f ? Mathf.Clamp01(_currentCapacity / MaxCapacity) : 0f;
        public bool IsLayingTrail => _isLayingTrail;
        public bool IsDepleted => _isDepleted;

        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutGland] Missing required ScoutConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ScoutConfigSO in the Inspector.");
            }

            if (_emitter == null)
            {
                _emitter = GetComponentInChildren<PheromoneTrailEmitter>();
            }

            if (_emitter == null)
            {
                throw new InvalidOperationException(
                    $"[ScoutGland] Missing required PheromoneTrailEmitter on or under GameObject '{gameObject.name}'. " +
                    $"Please assign a valid emitter in the Inspector.");
            }

            _currentCapacity = _config.MaxGlandCapacity;
            _isDepleted = false;
        }

        public void SetLayingTrail(bool active, PheromoneType type = PheromoneType.Recruitment)
        {
            if (active && !_isDepleted && _currentCapacity > 0f)
            {
                if (!_isLayingTrail || _activeScentType != type)
                {
                    _isLayingTrail = true;
                    _activeScentType = type;
                    _emitter.StartNewSegment(_activeScentType);
                }

                _emitter.EmitNode(transform.position, _activeScentType);
            }
            else
            {
                if (_isLayingTrail)
                {
                    _isLayingTrail = false;
                    _emitter.EndCurrentSegment();
                }
            }
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Continuous, TimeChannel.Player);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
        }

        public void OnUpdate(float deltaTime)
        {
            if (_isLayingTrail)
            {
                if (_currentCapacity > 0f)
                {
                    _currentCapacity = Mathf.Max(0f, _currentCapacity - _config.GlandDrainRate * deltaTime);
                    _emitter.EmitNode(transform.position, _activeScentType);
                    OnCapacityChanged?.Invoke(_currentCapacity, MaxCapacity);

                    if (_currentCapacity <= 0f)
                    {
                        _isDepleted = true;
                        _isLayingTrail = false;
                        _emitter.EndCurrentSegment();
                    }
                }
            }
            else if (_currentCapacity < _config.MaxGlandCapacity)
            {
                _currentCapacity = Mathf.Min(_config.MaxGlandCapacity, _currentCapacity + _config.GlandRechargeRate * deltaTime);
                OnCapacityChanged?.Invoke(_currentCapacity, MaxCapacity);

                // Recover from depleted state once at least 15% chemical capacity has refilled
                if (_isDepleted && _currentCapacity >= (_config.MaxGlandCapacity * 0.15f))
                {
                    _isDepleted = false;
                }
            }
        }
    }
}
