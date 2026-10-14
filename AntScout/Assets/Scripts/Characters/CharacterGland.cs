using System;
using AntScout.Core.Enums;
using AntScout.Pheromone;
using PGS.Core.Time;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Manages the ant's chemical pheromone gland capacity reserves and active trail emission.
    /// Grounded in Single Responsibility (SRP): Exclusively owns chemical drain and regeneration.
    /// Driven by PheromoneTrailAction (com.pgs.combat).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterGland : MonoBehaviour, IUpdatable
    {
        [Header("Pheromone Visual Prefab")]
        [Tooltip("Prefab containing LineRenderer and PheromoneTrailEmitter. Spawned dynamically in world space if absent.")]
        [SerializeField] private GameObject _pheromoneTrailPrefab;

        private CharacterDefinition _definition;
        private PheromoneTrailEmitter _emitter;
        private PheromoneTrailAction _activeAction;
        private float _currentCapacity;
        private bool _isLayingTrail;
        private bool _isDepleted;

        public event Action<float, float> OnCapacityChanged; // (current, max)
        public event Action OnGlandDepleted;

        private PheromoneTrailAction PrimaryTrailTemplate =>
            _activeAction != null ? _activeAction :
            (_definition != null && _definition.RecruitmentTrailAction != null ? _definition.RecruitmentTrailAction :
             (_definition != null ? _definition.AlarmTrailAction : null));

        public float CurrentCapacity => _currentCapacity;
        public float MaxCapacity => PrimaryTrailTemplate != null ? PrimaryTrailTemplate.MaxCapacity : 100f;
        public float NormalizedCapacity => MaxCapacity > 0f ? Mathf.Clamp01(_currentCapacity / MaxCapacity) : 0f;
        public bool IsLayingTrail => _isLayingTrail;
        public bool IsDepleted => _isDepleted;
        public PheromoneTrailEmitter Emitter => _emitter;

        public void Initialize(CharacterDefinition definition, PheromoneTrailEmitter emitter = null)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _currentCapacity = MaxCapacity;
            _isDepleted = false;

            if (emitter != null)
            {
                _emitter = emitter;
            }
            else if (_emitter == null)
            {
                ResolveEmitter();
            }
        }

        private void Awake()
        {
            ResolveEmitter();
        }

        private void OnEnable()
        {
            PgsTime.Register(this, UpdateRate.Continuous, TimeChannel.Player);
        }

        private void OnDisable()
        {
            PgsTime.Unregister(this);
            if (_isLayingTrail)
            {
                StopLayingTrail();
            }
        }

        private void ResolveEmitter()
        {
            if (_emitter == null)
            {
                _emitter = FindFirstObjectByType<PheromoneTrailEmitter>();
                if (_emitter == null && _pheromoneTrailPrefab != null)
                {
                    GameObject instance = Instantiate(_pheromoneTrailPrefab, transform.position, Quaternion.identity);
                    instance.name = "PheromoneTrail_World";
                    _emitter = instance.GetComponent<PheromoneTrailEmitter>();
                }
            }
        }

        /// <summary>
        /// Commands the gland to begin laying a trail based on the provided PheromoneTrailAction.
        /// </summary>
        public void StartLayingTrail(PheromoneTrailAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_isDepleted || _currentCapacity <= 0f) return;

            ResolveEmitter();
            if (_emitter == null) return;

            _activeAction = action;
            _isLayingTrail = true;
            _emitter.StartNewSegment(action.PheromoneType);
            _emitter.EmitNode(transform.position, action.PheromoneType);
        }

        /// <summary>
        /// Halts current trail emission.
        /// </summary>
        public void StopLayingTrail()
        {
            if (_isLayingTrail)
            {
                _isLayingTrail = false;
                _activeAction = null;
                if (_emitter != null)
                {
                    _emitter.EndCurrentSegment();
                }
            }
        }

        public void OnUpdate(float deltaTime)
        {
            if (_definition == null) return;

            if (_isLayingTrail && _activeAction != null)
            {
                if (_currentCapacity > 0f)
                {
                    float cost = _activeAction.ChemicalCostPerSecond * deltaTime;
                    _currentCapacity = Mathf.Max(0f, _currentCapacity - cost);
                    OnCapacityChanged?.Invoke(_currentCapacity, MaxCapacity);

                    if (_emitter != null)
                    {
                        _emitter.EmitNode(transform.position, _activeAction.PheromoneType);
                    }

                    if (_currentCapacity <= 0f)
                    {
                        _isDepleted = true;
                        StopLayingTrail();
                        OnGlandDepleted?.Invoke();
                    }
                }
            }
            else
            {
                // Passive regeneration driven by the trail action's recharge rate
                var template = PrimaryTrailTemplate;
                float maxCap = MaxCapacity;
                if (template != null && _currentCapacity < maxCap)
                {
                    float recharge = template.RechargeRatePerSecond * deltaTime;
                    _currentCapacity = Mathf.Min(maxCap, _currentCapacity + recharge);
                    OnCapacityChanged?.Invoke(_currentCapacity, maxCap);

                    if (_isDepleted && _currentCapacity >= maxCap * 0.15f)
                    {
                        _isDepleted = false;
                    }
                }
            }
        }
    }
}
