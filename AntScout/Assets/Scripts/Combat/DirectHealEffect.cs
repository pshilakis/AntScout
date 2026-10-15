using System;
using PGS.Core;
using PGS.Effects;
using UnityEngine;

namespace AntScout.Combat
{
    /// <summary>
    /// Instant impact or pulse effect that restores hit points to any entity implementing <see cref="IHealReceiver"/>.
    /// Follows the Open/Closed Principle (OCP) and Strategy Pattern via <see cref="IHitEffect"/>.
    /// Adheres to the strict Fail-Fast standard with zero silent fallbacks.
    /// </summary>
    [Serializable]
    public sealed class DirectHealEffect : IHitEffect
    {
        [Tooltip("Descriptive name for this healing archetype.")]
        [SerializeField] private string _effectName = nameof(DirectHealEffect);

        [Tooltip("Base health restored upon pulse/impact.")]
        [Min(0.1f)]
        [SerializeField] private float _baseHealing = 5.0f;

        public string EffectName => _effectName;
        public float BaseHealing => _baseHealing;

        public DirectHealEffect() 
        { 
            _effectName = nameof(DirectHealEffect);
        }

        public DirectHealEffect(float baseHealing, string effectName)
        {
            if (string.IsNullOrWhiteSpace(effectName))
            {
                throw new ArgumentException("Effect name cannot be null or whitespace.", nameof(effectName));
            }

            if (baseHealing <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealing), "Base healing must be greater than zero.");
            }

            _baseHealing = baseHealing;
            _effectName = effectName;
        }

        public void Apply(in HitContext context)
        {
            if (context.Target == null) return;

            var receiver = context.Target.GetComponentInParent<IHealReceiver>();
            if (receiver != null)
            {
                float finalHeal = _baseHealing * context.BaseMultiplier;
                receiver.ReceiveHealing(finalHeal, in context);
            }
        }

        public (bool show, string message, SubclassSummaryType summaryType) GetSubclassSummary()
        {
            return (false, string.Empty, SubclassSummaryType.None);
        }

        public void ValidateOrThrow(string parentContext)
        {
            if (string.IsNullOrWhiteSpace(_effectName))
            {
                throw new InvalidOperationException($"[{parentContext}] EffectName cannot be null or whitespace on DirectHealEffect!");
            }

            if (_baseHealing <= 0f)
            {
                throw new InvalidOperationException($"[{parentContext}] Base healing must be greater than zero for '{_effectName}'!");
            }
        }
    }
}
