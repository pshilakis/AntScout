using System;
using AntScout.Characters;
using AntScout.Swarm.Interfaces;
using PGS.Combat.Execution;
using PGS.Combat.Targeting;
using PGS.Effects;
using UnityEngine;

namespace AntScout.Swarm.Agents
{
    /// <summary>
    /// Partial class implementing reactive enmity, social alarm broadcasting, pursuit,
    /// and leashing/disengagement for non-player colony swarm agents.
    /// Grounded in Single Responsibility (SRP) and Open/Closed Principle (OCP).
    /// </summary>
    public abstract partial class SwarmAgent
    {
        protected IAttackable _combatTarget;
        protected Vector3 _combatAnchorPosition;
        protected float _combatInactivityTimer;

        private static readonly Collider[] s_proximityBuffer = new Collider[8];

        public IAttackable CurrentCombatTarget => _combatTarget;

        /// <summary>
        /// Senses nearby hostile entities within the specified radius.
        /// Zero heap allocation (uses shared non-alloc PhysX buffer).
        /// </summary>
        public bool TryDetectHostileIntruder(float radius, out IAttackable hostile)
        {
            hostile = null;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, s_proximityBuffer, ~0, QueryTriggerInteraction.Collide);
            if (count == 0) return false;

            for (int i = 0; i < count; i++)
            {
                Collider col = s_proximityBuffer[i];
                if (col == null) continue;

                IAttackable attackable = col.GetComponent<IAttackable>() ?? col.GetComponentInParent<IAttackable>();
                if (attackable == null || !attackable.IsAttackable || attackable == (this as IAttackable)) continue;

                IFactionMember faction = (attackable as IFactionMember) ?? col.GetComponentInParent<IFactionMember>();
                if (faction != null && GetAffiliationTowards(faction) == TargetAffiliation.Enemy)
                {
                    hostile = attackable;
                    return true;
                }
            }

            return false;
        }

        #region Enmity Retaliation & Social Alarm

        /// <summary>
        /// Retaliates immediately against whoever dealt damage to this agent (Direct Provocation).
        /// </summary>
        protected virtual void HandleDamagedEnmityRetaliation(DamageResult result, HitContext context)
        {
            if (context.Instigator == null || context.Instigator == gameObject) return;

            IAttackable attacker = context.Instigator.GetComponent<IAttackable>() ??
                                   context.Instigator.GetComponentInParent<IAttackable>();

            if (attacker == null || !attacker.IsAttackable) return;

            // Faction verification: Only retaliate against hostiles
            IFactionMember attackerFaction = (attacker as IFactionMember) ??
                                             context.Instigator.GetComponentInParent<IFactionMember>();

            if (attackerFaction != null && GetAffiliationTowards(attackerFaction) != TargetAffiliation.Enemy)
            {
                return;
            }

            EngageTarget(attacker);
            BroadcastAlarmToSisters(attacker);
        }

        /// <summary>
        /// Engages the specified hostile target in self-defense.
        /// </summary>
        public virtual void EngageTarget(IAttackable target)
        {
            if (target == null || !target.IsAttackable) return;

            _combatTarget = target;
            _combatAnchorPosition = transform.position;
            _combatInactivityTimer = 0f;
            SetState(SwarmAgentState.Defending);
        }

        /// <summary>
        /// Alerts nearby sister ants belonging to the same colony nest (Social Alarm).
        /// </summary>
        protected virtual void BroadcastAlarmToSisters(IAttackable hostileTarget)
        {
            if (_homeNest != null)
            {
                _homeNest.BroadcastSwarmAlarm(this, hostileTarget, _config.SwarmAlarmRadius);
            }
        }

        /// <summary>
        /// Invoked when a sister ant in the colony broadcasts an alarm signal nearby.
        /// Can be overridden by subclasses (e.g. workers carrying food can choose to ignore and flee home).
        /// </summary>
        public virtual void OnAlarmReceived(IAttackable hostileTarget)
        {
            if (_currentState == SwarmAgentState.Defending || !IsAlive || hostileTarget == null || !hostileTarget.IsAttackable)
            {
                return;
            }

            EngageTarget(hostileTarget);
        }

        #endregion

        #region Combat Defense Loop & Leashing

        /// <summary>
        /// Evaluates pursuit, primary action execution, leash bounds, and combat timeout.
        /// </summary>
        protected virtual void EvaluateCombatDefense(float deltaTime)
        {
            // 1. Target validity check (Target dead or destroyed)
            if (_combatTarget == null || !_combatTarget.IsAttackable || _combatTarget.Transform == null)
            {
                DisengageCombat();
                return;
            }

            Vector3 myPos = transform.position;
            Vector3 targetPos = _combatTarget.Transform.position;

            // 2. Leash check: Return to routine if enemy moves beyond maximum chase distance from fight anchor
            float dxAnchor = myPos.x - _combatAnchorPosition.x;
            float dzAnchor = myPos.z - _combatAnchorPosition.z;
            float leashDistSqr = dxAnchor * dxAnchor + dzAnchor * dzAnchor;
            if (leashDistSqr > (_config.SwarmLeashRadius * _config.SwarmLeashRadius))
            {
                DisengageCombat();
                return;
            }

            // 3. Combat timeout check: De-escalate if inactive for too long
            _combatInactivityTimer += deltaTime;
            if (_combatInactivityTimer >= _config.SwarmCombatTimeout)
            {
                DisengageCombat();
                return;
            }

            // 4. Primary combat action validation
            CombatActionDefinition action = _characterDefinition != null ? _characterDefinition.PrimaryAction : null;
            if (action == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] CharacterDefinition '{_characterDefinition.name}' is missing an assigned PrimaryAction for SwarmAgent combat!");
            }

            // 5. Pursuit & Attack Execution
            float dxTarget = targetPos.x - myPos.x;
            float dzTarget = targetPos.z - myPos.z;
            float distToTargetSqr = dxTarget * dxTarget + dzTarget * dzTarget;
            float effectiveRange = action.Range;

            if (distToTargetSqr > (effectiveRange * effectiveRange))
            {
                // Out of attack range: Close in on target
                _follower.ApproachTarget(targetPos);
            }
            else
            {
                // In attack range: Stop locomotion, face enemy, and execute PrimaryAction
                _follower.StopMovement();

                Vector3 aimDir = new Vector3(dxTarget, 0f, dzTarget);
                if (aimDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(aimDir.normalized),
                        360f * deltaTime);
                }

                if (_combatExecutor == null)
                {
                    _combatExecutor = GetComponent<CombatActionExecutor>();
                    if (_combatExecutor == null)
                    {
                        throw new InvalidOperationException(
                            $"[{name}] Missing CombatActionExecutor component on '{name}' for SwarmAgent combat!");
                    }
                    _combatExecutor.TargetsHit += HandleCombatTargetsHit;
                }

                if (_combatExecutor.TryExecuteAction(action, aimDir.normalized))
                {
                    _combatInactivityTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Disengages from active combat, clearing target and notifying subclass to resume duties.
        /// </summary>
        public virtual void DisengageCombat()
        {
            if (_combatTarget == null && _currentState != SwarmAgentState.Defending) return;

            _combatTarget = null;
            _combatInactivityTimer = 0f;

            if (_combatExecutor != null && _combatExecutor.IsExecuting)
            {
                _combatExecutor.CancelAction(ActionCancelReason.Manual);
            }

            if (_currentState == SwarmAgentState.Defending)
            {
                SetState(SwarmAgentState.Idle);
            }

            OnCombatDisengaged();
        }

        /// <summary>
        /// Subclass hook called upon disengaging to return to routine duties (e.g. foraging, patrolling).
        /// </summary>
        protected virtual void OnCombatDisengaged()
        {
            SetState(SwarmAgentState.Idle);
        }

        #endregion
    }
}
