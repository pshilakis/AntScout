using System;
using System.Collections.Generic;
using AntScout.Core.Interfaces;
using AntScout.Core.Stats;
using PGS.Combat.Execution;
using PGS.Combat.Targeting;
using PGS.Core;
using PGS.Effects;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class implementing com.pgs.combat and com.pgs.effects contracts:
    /// - IMovementReceiver: Grants CombatActionExecutor direct movement authority during charges/impulses.
    /// - IAttackable: Exposes physical target radius and attackable state.
    /// - IColonyFactionMember (IFactionMember): Evaluates zero-allocation colony team affiliation.
    /// - IDamageReceiver & IHealReceiver: Resolves incoming damage/healing against StatsController vitals.
    /// - IKnockbackReceiver, ISlowReceiver, IStunReceiver: Supports all PGS.Effects status payloads.
    /// </summary>
    public sealed partial class AntCharacter :
        IMovementReceiver,
        IAttackable,
        IHealthEntity,
        IColonyFactionMember,
        IDamageReceiver,
        IHealReceiver,
        IKnockbackReceiver,
        ISlowReceiver,
        IStunReceiver
    {
        [Header("Colony Faction (com.pgs.combat)")]
        [Tooltip("Numeric team identifier (0 = Colony 1 / Player, 1 = Colony 2 / Enemy AI).")]
        [SerializeField] private int _colonyTeamId = 0;

        [Header("Vitals (Live Inspector Telemetry)")]
        [ReadOnly]
        [SerializeField] private float _currentHealth;

        [ReadOnly]
        [SerializeField] private float _maxHealth;

        private readonly ColonyVitals _vitals = new ColonyVitals();
        private bool _isStunned;
        private float _speedMultiplier = 1.0f;
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

        public event Action<HitContext> OnDied
        {
            add => _vitals.OnDied += value;
            remove => _vitals.OnDied -= value;
        }

        public float CurrentHealth => _vitals.CurrentHealth;
        public float MaxHealth => _vitals.MaxHealth;
        public float NormalizedHealth => _vitals.NormalizedHealth;
        public float SpeedMultiplier => _speedMultiplier;
        public float LastDamageTakenTimestamp => _vitals.LastDamageTakenTimestamp;
        public float TimeSinceLastDamaged => _vitals.TimeSinceLastDamaged;
        public bool IsInCombat(float combatDuration) => _vitals.IsInCombat(combatDuration);

        #region IMovementReceiver Implementation

        public bool IsLocomotionLocked
        {
            get => _motor != null && _motor.IsLocomotionLocked;
            set
            {
                if (_motor == null) return;
                if (value)
                {
                    _motor.LockLocomotion(this);
                }
                else
                {
                    _motor.UnlockLocomotion(this);
                }
            }
        }

        public void Move(Vector3 deltaDisplacement)
        {
            if (_motor != null)
            {
                Vector3 planarDelta = new Vector3(deltaDisplacement.x, 0f, deltaDisplacement.z);
                Quaternion dashRotation = planarDelta.sqrMagnitude > 0.000001f
                    ? Quaternion.LookRotation(planarDelta.normalized, Vector3.up)
                    : _motor.FacingRotation;

                _motor.Teleport(transform.position + deltaDisplacement, dashRotation);
            }
            else
            {
                transform.position += deltaDisplacement;
            }
        }

        public void ApplyImpulse(Vector3 impulse)
        {
            _motor?.ApplyImpulse(impulse);
        }

        #endregion

        #region IAttackable Implementation

        public Transform Transform => transform;
        public float BodyRadius => _characterCollider != null
            ? _characterCollider.Radius
            : throw new InvalidOperationException($"[{name}] CharacterCollider3D is missing on AntCharacter!");
        public bool IsAttackable => _isAlive;

        #endregion

        #region IColonyFactionMember (IFactionMember) Implementation

        public int ColonyTeamId => _colonyTeamId;

        public void SetColonyTeam(int teamId)
        {
            _colonyTeamId = teamId;
            if (_gland != null)
            {
                _gland.SetColonyTeam(teamId);
            }
        }

        public TargetAffiliation GetAffiliationTowards(IFactionMember other)
        {
            return ColonyAffiliation.Evaluate(this, other);
        }

        #endregion

        #region IDamageReceiver & IHealReceiver Implementation (com.pgs.effects)

        public DamageResult ReceiveDamage(float amount, in HitContext context)
        {
            var result = _vitals.ReceiveDamage(amount, in context);
            _currentHealth = _vitals.CurrentHealth;
            _maxHealth = _vitals.MaxHealth;
            Debug.Log($"<color=orange>[{name} - Team {_colonyTeamId}] Hit by {context.Instigator?.name ?? "Unknown"}! Took {result.DamageApplied:F1} damage ({amount:F1} raw - {_vitals.Defense:F1} def). HP: {_currentHealth:F1}/{_maxHealth:F1}</color>");
            return result;
        }

        public void ReceiveHealing(float amount, in HitContext context)
        {
            _vitals.ReceiveHealing(amount, in context);
            _currentHealth = _vitals.CurrentHealth;
            _maxHealth = _vitals.MaxHealth;
        }

        #endregion

        #region IKnockbackReceiver, ISlowReceiver, IStunReceiver Implementation (com.pgs.effects)

        public void ReceiveKnockback(Vector3 impulse)
        {
            ApplyImpulse(impulse);
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0f, multiplier);
        }

        public bool IsStunned => _isStunned;

        public void OnStunApplied(float duration, bool interruptAction)
        {
            _isStunned = true;
            if (_motor != null)
            {
                _motor.LockLocomotion(this);
            }

            if (interruptAction)
            {
                CancelActiveAction(ActionCancelReason.Interrupted);
                if (_gland != null && _gland.IsLayingTrail)
                {
                    _gland.StopLayingTrail();
                }
            }
        }

        public void OnStunRemoved()
        {
            _isStunned = false;
            if (_motor != null && (_combatExecutor == null || !_combatExecutor.IsCharging))
            {
                _motor.UnlockLocomotion(this);
            }
        }

        #endregion

        #region Combat Lifecycle & Payload Delivery

        private void BootstrapCombat()
        {
            if (_definition != null)
            {
                _vitals.Initialize(
                    _statsController,
                    _definition.MaxHealth,
                    _definition.Defense,
                    _definition.Attack);
                _isAlive = _vitals.IsAlive;
                _currentHealth = _vitals.CurrentHealth;
                _maxHealth = _vitals.MaxHealth;
            }

            if (!_combatSubscribed)
            {
                _vitals.OnHealthChanged += (curr, max) =>
                {
                    _currentHealth = curr;
                    _maxHealth = max;
                };
                _vitals.OnDied += HandleCharacterDied;
                if (_combatExecutor != null)
                {
                    _combatExecutor.TargetsHit += HandleCombatTargetsHit;
                }
                _combatSubscribed = true;
            }
        }

        private void CleanupCombat()
        {
            if (_combatSubscribed)
            {
                _vitals.OnDied -= HandleCharacterDied;
                if (_combatExecutor != null)
                {
                    _combatExecutor.TargetsHit -= HandleCombatTargetsHit;
                }
                _combatSubscribed = false;
            }

            _vitals.Cleanup();
        }

        private void HandleCombatTargetsHit(
            CombatActionDefinition action,
            IReadOnlyList<IAttackable> targets,
            int hitCount,
            float effectivePotency,
            bool isComboSuccess)
        {
            if (effectivePotency <= 0f || hitCount <= 0 || targets == null)
            {
                return;
            }

            // StarCraft-style attack scaling: AttackStat * (EffectivePotency / 100f)
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

        private void HandleCharacterDied(HitContext context)
        {
            _isAlive = false;
            CancelActiveAction(ActionCancelReason.Interrupted);
            if (_gland != null && _gland.IsLayingTrail)
            {
                _gland.StopLayingTrail();
            }
            if (_motor != null)
            {
                _motor.LockLocomotion(this);
            }
        }

        /// <summary>
        /// Revives and re-materializes the ant character at the specified spawn position with full health.
        /// Unlocks locomotion, resets vitals/status, and re-activates the GameObject with zero GC allocations.
        /// </summary>
        public void Revive(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            _vitals.Reset();
            _currentHealth = _vitals.CurrentHealth;
            _maxHealth = _vitals.MaxHealth;
            _isAlive = _vitals.IsAlive;
            _isStunned = false;
            _speedMultiplier = 1.0f;

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            gameObject.SetActive(true);

            if (_motor != null)
            {
                _motor.UnlockLocomotion(this);
                _motor.Teleport(spawnPosition, spawnRotation);
            }
        }

        /// <summary>
        /// Attempts to execute a combat action (e.g. Dash, Bite, Spit) via the attached CombatActionExecutor.
        /// </summary>
        public bool TryExecuteAction(CombatActionDefinition action, Vector3 aimDirection)
        {
            if (!_isAlive || _isStunned || _combatExecutor == null || action == null) return false;
            return _combatExecutor.TryExecuteAction(action, aimDirection);
        }

        /// <summary>
        /// Cancels any currently active combat action (e.g. canceling a channeled trail).
        /// </summary>
        public void CancelActiveAction(ActionCancelReason reason = ActionCancelReason.Manual)
        {
            if (_combatExecutor != null && _combatExecutor.IsExecuting)
            {
                _combatExecutor.CancelAction(reason);
            }
        }

        #endregion
    }
}
