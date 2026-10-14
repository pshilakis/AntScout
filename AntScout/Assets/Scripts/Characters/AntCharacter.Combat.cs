using System;
using PGS.Combat.Execution;
using PGS.Combat.Targeting;
using UnityEngine;

namespace AntScout.Characters
{
    /// <summary>
    /// Partial class implementing com.pgs.combat contracts:
    /// - IMovementReceiver: Grants CombatActionExecutor direct movement authority during charges/impulses.
    /// - IAttackable: Exposes physical target radius and attackable state.
    /// - IFactionMember: Evaluates zero-allocation combat affiliation.
    /// </summary>
    public sealed partial class AntCharacter : IMovementReceiver, IAttackable, IFactionMember
    {
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
        public float BodyRadius => _characterCollider != null ? _characterCollider.Radius : 0.4f;
        public bool IsAttackable => _isAlive;

        #endregion

        #region IFactionMember Implementation

        public TargetAffiliation GetAffiliationTowards(IFactionMember other)
        {
            if (other == null) return TargetAffiliation.Neutral;
            if (ReferenceEquals(other, this)) return TargetAffiliation.Self;

            // Allied if both are in player colony faction
            if (other is AntCharacter)
            {
                return TargetAffiliation.Ally;
            }

            return TargetAffiliation.Enemy;
        }

        #endregion

        #region Action Execution Dispatcher

        private void BootstrapCombat()
        {
            // Any specific combat listener hookups
        }

        /// <summary>
        /// Attempts to execute a combat action (e.g. Dash, Bite, Spit) via the attached CombatActionExecutor.
        /// </summary>
        /// <param name="action">Action definition to execute.</param>
        /// <param name="aimDirection">World-space aim direction.</param>
        /// <returns>True if action was initiated; false if busy or on cooldown.</returns>
        public bool TryExecuteAction(CombatActionDefinition action, Vector3 aimDirection)
        {
            if (_combatExecutor == null || action == null) return false;
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
