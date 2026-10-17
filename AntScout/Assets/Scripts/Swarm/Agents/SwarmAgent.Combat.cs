using System;
using System.Collections.Generic;
using AntScout.Characters;
using AntScout.Core.Interfaces;
using AntScout.Core.Stats;
using AntScout.Match;
using AntScout.Swarm.Interfaces;
using AntScout.Swarm.Nest;
using PGS.Combat.Execution;
using PGS.Combat.Targeting;
using PGS.Effects;
using PGS.Stats;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class implementing combat targetability (IAttackable), colony faction membership (IColonyFactionMember),
    /// vitals (IDamageReceiver, IHealReceiver), and combat action execution for SwarmAgent.
    /// Grounded in Interface Segregation (ISP) and Fail-Fast standards.
    /// </summary>
    [RequireComponent(typeof(StatsController))]
    [RequireComponent(typeof(StatusReceiver))]
    public abstract partial class SwarmAgent : IAttackable, IColonyFactionMember, IDamageReceiver, IHealReceiver
    {
        protected StatsController _statsController;
        protected StatusReceiver _statusReceiver;
        protected CombatActionExecutor _combatExecutor;
        protected readonly ColonyVitals _vitals = new ColonyVitals();
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

        public event Action<SwarmAgent, HitContext> OnAgentDied;

        public StatsController Stats => _statsController;
        public StatusReceiver StatusReceiver => _statusReceiver;
        public CombatActionExecutor CombatExecutor => _combatExecutor;
        public float CurrentHealth => _vitals.CurrentHealth;
        public float MaxHealth => _vitals.MaxHealth;
        public float NormalizedHealth => _vitals.NormalizedHealth;
        public bool IsAlive => _vitals.IsAlive;

        #region IAttackable Implementation

        public Transform Transform => transform;
        public float BodyRadius => _follower != null
            ? _follower.Radius
            : throw new InvalidOperationException($"[{name}] AntTrailFollower is missing on SwarmAgent!");
        public bool IsAttackable => _vitals.IsAlive;

        #endregion

        #region IColonyFactionMember (IFactionMember) Implementation

        public int ColonyTeamId => _homeNest != null
            ? _homeNest.ColonyTeamId
            : throw new InvalidOperationException($"[{name}] SwarmAgent ColonyTeamId accessed before Initialize(homeNest)!");

        public TargetAffiliation GetAffiliationTowards(IFactionMember other)
        {
            return ColonyAffiliation.Evaluate(this, other);
        }

        #endregion

        #region IDamageReceiver & IHealReceiver Implementation

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

        protected virtual void BootstrapCombat()
        {
            _statsController = GetComponent<StatsController>();
            _statusReceiver = GetComponent<StatusReceiver>();
            _combatExecutor = GetComponent<CombatActionExecutor>();

            if (_statsController == null)
            {
                throw new InvalidOperationException($"[{name}] Missing required StatsController component on '{name}'.");
            }
            if (_statusReceiver == null)
            {
                throw new InvalidOperationException($"[{name}] Missing required StatusReceiver component on '{name}'.");
            }
            // Note: CombatActionExecutor is checked at runtime when entering combat or executing attacks.

            _characterDefinition.ValidateVitals(name);

            _vitals.Initialize(
                _statsController,
                _characterDefinition.MaxHealth,
                _characterDefinition.Defense,
                _characterDefinition.Attack);

            if (!_combatSubscribed)
            {
                _vitals.OnDied += HandleAgentVitalsDied;
                _vitals.OnDamaged += HandleDamagedEnmityRetaliation;
                if (_combatExecutor != null)
                {
                    _combatExecutor.TargetsHit += HandleCombatTargetsHit;
                }
                _combatSubscribed = true;
            }
        }

        protected virtual void InitializeCombat(ColonyNest homeNest)
        {
            if (homeNest == null) throw new ArgumentNullException(nameof(homeNest));
            _vitals.Reset();
        }

        protected virtual void CleanupCombat()
        {
            if (_combatSubscribed)
            {
                _vitals.OnDied -= HandleAgentVitalsDied;
                _vitals.OnDamaged -= HandleDamagedEnmityRetaliation;
                if (_combatExecutor != null)
                {
                    _combatExecutor.TargetsHit -= HandleCombatTargetsHit;
                }
                _combatSubscribed = false;
            }
            _vitals.Cleanup();
        }

        protected virtual void HandleCombatTargetsHit(
            CombatActionDefinition action,
            IReadOnlyList<IAttackable> targets,
            int hitCount,
            float effectivePotency,
            bool isComboSuccess)
        {
            if (effectivePotency <= 0f || hitCount <= 0 || targets == null) return;

            // Attack scaling: AttackStat * (EffectivePotency / 100f)
            float rawDamage = _vitals.Attack * (effectivePotency / 100f);
            Vector3 origin = transform.position;

            for (int i = 0; i < hitCount; i++)
            {
                IAttackable target = targets[i];
                if (target == null || target.Transform == null) continue;

                GameObject targetObj = target.Transform.gameObject;
                IDamageReceiver receiver = (target as IDamageReceiver) ?? targetObj.GetComponentInParent<IDamageReceiver>();
                if (receiver == null) continue;

                Vector3 hitPoint = target.Transform.position;
                Vector3 hitNormal = (origin - hitPoint);
                hitNormal.y = 0f;
                hitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;

                Collider hitCol = targetObj.GetComponent<Collider>();
                var context = new HitContext(hitPoint, hitNormal, hitCol, targetObj, gameObject);
                receiver.ReceiveDamage(rawDamage, in context);
            }
        }

        protected virtual void HandleAgentVitalsDied(HitContext context)
        {
            DisengageCombat();
            if (_follower != null)
            {
                _follower.StopMovement();
            }

            if (_homeNest != null)
            {
                _homeNest.UnregisterWorker(this);
            }

            OnAgentDied?.Invoke(this, context);
            SpawnPool.Despawn(gameObject);
        }

        #endregion
    }
}
