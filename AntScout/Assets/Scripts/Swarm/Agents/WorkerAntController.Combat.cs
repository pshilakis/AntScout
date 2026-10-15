using System;
using AntScout.Characters;
using AntScout.Core.Interfaces;
using AntScout.Core.Stats;
using AntScout.Match;
using AntScout.Swarm.Nest;
using PGS.Combat.Targeting;
using PGS.Effects;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class implementing combat targetability (<see cref="IAttackable"/>),
    /// colony faction membership (<see cref="IColonyFactionMember"/>), and vitals (<see cref="IDamageReceiver"/>, <see cref="IHealReceiver"/>)
    /// for Worker Ants via <see cref="CharacterDefinition"/>.
    /// Adheres strictly to the Fail-Fast standard with zero silent fallbacks.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(StatsController))]
    [RequireComponent(typeof(StatusReceiver))]
    public partial class WorkerAntController : IAttackable, IColonyFactionMember, IDamageReceiver, IHealReceiver
    {
        [Header("Character Definition (Stats & Vitals)")]
        [Tooltip("CharacterDefinition asset providing BodyRadius, MaxHealth, Defense, and Attack stats for this Worker caste.")]
        [SerializeField] private CharacterDefinition _characterDefinition;

        private StatsController _statsController;
        private StatusReceiver _statusReceiver;
        private readonly ColonyVitals _vitals = new ColonyVitals();
        private bool _combatSubscribed;

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

        public event Action<WorkerAntController, HitContext> OnDied;

        public CharacterDefinition CharacterDefinition => _characterDefinition;
        public StatsController Stats => _statsController;
        public StatusReceiver StatusReceiver => _statusReceiver;
        public float CurrentHealth => _vitals.CurrentHealth;
        public float MaxHealth => _vitals.MaxHealth;
        public float NormalizedHealth => _vitals.NormalizedHealth;
        public bool IsAlive => _vitals.IsAlive;

        #region IAttackable Implementation

        public Transform Transform => transform;
        public float BodyRadius => _follower != null
            ? _follower.Radius
            : throw new InvalidOperationException($"[{name}] AntTrailFollower is missing on WorkerAntController!");
        public bool IsAttackable => _vitals.IsAlive;

        #endregion

        #region IColonyFactionMember (IFactionMember) Implementation

        public int ColonyTeamId => _homeNest != null
            ? _homeNest.ColonyTeamId
            : throw new InvalidOperationException($"[{name}] WorkerAntController ColonyTeamId accessed before Initialize(homeNest)!");

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

        #region Combat & Vitals Lifecycle

        private void BootstrapCombat()
        {
            _statsController = GetComponent<StatsController>();
            _statusReceiver = GetComponent<StatusReceiver>();

            ValidateCombatConfigurationAndComponents();

            _vitals.Initialize(
                _statsController,
                _characterDefinition.MaxHealth,
                _characterDefinition.Defense,
                _characterDefinition.Attack);

            if (!_combatSubscribed)
            {
                _vitals.OnDied += HandleWorkerDied;
                _combatSubscribed = true;
            }
        }

        private void InitializeCombat(ColonyNest homeNest)
        {
            if (homeNest == null)
            {
                throw new ArgumentNullException(nameof(homeNest));
            }
            _vitals.Reset();
        }

        private void CleanupCombat()
        {
            if (_combatSubscribed)
            {
                _vitals.OnDied -= HandleWorkerDied;
                _combatSubscribed = false;
            }

            _vitals.Cleanup();
        }

        private void HandleWorkerDied(HitContext context)
        {
            if (_follower != null)
            {
                _follower.StopMovement();
            }

            if (_homeNest != null)
            {
                _homeNest.UnregisterWorker(this);
            }

            OnDied?.Invoke(this, context);
            SpawnPool.Despawn(gameObject);
        }

        #endregion
    }
}
