using System;
using AntScout.Core.Interfaces;
using AntScout.Core.Stats;
using PGS.Combat.Targeting;
using PGS.Core;
using PGS.Effects;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class managing the Colony Nest's physical structure identity, faction affiliation,
    /// spatial attackability (<see cref="IAttackable"/>), and structural health (<see cref="IDamageReceiver"/>, <see cref="IHealReceiver"/>).
    /// Adheres strictly to the Fail-Fast standard with zero silent fallbacks.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(StatsController))]
    public partial class ColonyNest : IAttackable, IColonyFactionMember, IDamageReceiver, IHealReceiver
    {
        [Header("Structure Definition & Faction")]
        [Tooltip("StructureDefinition asset providing MaxHealth and Defense stats for this Colony Nest.")]
        [SerializeField] private StructureDefinition _structureDefinition;

        [Tooltip("Numeric team identifier (0 = Colony 1 / Player, 1 = Colony 2 / Enemy AI).")]
        [SerializeField] private int _colonyTeamId = 0;

        [Header("Vitals (Live Inspector Telemetry)")]
        [Tooltip("Live current structural health for editor inspection.")]
        [ReadOnly] [SerializeField] private float _currentHealth;

        [Tooltip("Maximum structural health derived from StructureDefinition and stats.")]
        [ReadOnly] [SerializeField] private float _maxHealth;

        private Collider _structureCollider;
        private StatsController _statsController;
        private readonly ColonyVitals _vitals = new ColonyVitals();
        private bool _structureSubscribed;

        public event Action<float, float> OnHealthChanged
        {
            add => _vitals.OnHealthChanged += value;
            remove => _vitals.OnHealthChanged -= value;
        }

        public event Action<DamageResult, HitContext> OnDamaged
        {
            add => _vitals.OnDamaged += value;
            remove => _vitals.OnDamaged -= value;
        }

        public event Action<ColonyNest> OnNestDestroyed;
        public static event Action<ColonyNest> OnAnyNestDestroyed;

        public StructureDefinition StructureDefinition => _structureDefinition;
        public StatsController Stats => _statsController;
        public float CurrentHealth => _vitals.CurrentHealth;
        public float MaxHealth => _vitals.MaxHealth;
        public float NormalizedHealth => _vitals.NormalizedHealth;
        public bool IsStructureAlive => _vitals.IsAlive;

        #region IAttackable Implementation

        public Transform Transform => transform;
        public float BodyRadius => _structureCollider != null
            ? Mathf.Max(_structureCollider.bounds.extents.x, _structureCollider.bounds.extents.z)
            : throw new InvalidOperationException($"[{name}] Collider is missing on ColonyNest!");
        public bool IsAttackable => _vitals.IsAlive;

        #endregion

        #region IColonyFactionMember (IFactionMember) Implementation

        public int ColonyTeamId => _colonyTeamId;

        public void SetColonyTeam(int teamId)
        {
            _colonyTeamId = teamId;
        }

        public TargetAffiliation GetAffiliationTowards(IFactionMember other)
        {
            return ColonyAffiliation.Evaluate(this, other);
        }

        #endregion

        #region IDamageReceiver & IHealReceiver Implementation (com.pgs.effects)

        public DamageResult ReceiveDamage(float amount, in HitContext context)
        {
            return _vitals.ReceiveDamage(amount, in context);
        }

        public void ReceiveHealing(float amount, in HitContext context)
        {
            _vitals.ReceiveHealing(amount, in context);
        }

        #endregion

        #region Structure Lifecycle

        private void BootstrapStructure()
        {
            _structureCollider = GetComponent<Collider>();
            _statsController = GetComponent<StatsController>();
            ValidateStructureConfigurationAndComponents();

            _vitals.Initialize(
                _statsController,
                _structureDefinition.MaxHealth,
                _structureDefinition.Defense);

            _currentHealth = _vitals.CurrentHealth;
            _maxHealth = _vitals.MaxHealth;

            if (!_structureSubscribed)
            {
                _vitals.OnHealthChanged += HandleHealthChanged;
                _vitals.OnDied += HandleStructureDestroyed;
                _structureSubscribed = true;
            }
        }

        private void CleanupStructure()
        {
            if (_structureSubscribed)
            {
                _vitals.OnHealthChanged -= HandleHealthChanged;
                _vitals.OnDied -= HandleStructureDestroyed;
                _structureSubscribed = false;
            }

            _vitals.Cleanup();
        }

        private void HandleHealthChanged(float current, float max)
        {
            _currentHealth = current;
            _maxHealth = max;
        }

        private void HandleStructureDestroyed(HitContext context)
        {
            OnNestDestroyed?.Invoke(this);
            OnAnyNestDestroyed?.Invoke(this);
        }

        #endregion
    }
}
