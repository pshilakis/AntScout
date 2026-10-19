using System;

namespace AntScout.Characters.Brains
{
    /// <summary>
    /// Fail-fast validation logic for <see cref="AiStrategyProfile"/>.
    /// Strictly complies with the Zero-Magic-Numbers and Fail-Fast standards (SRP).
    /// </summary>
    public partial class AiStrategyProfile
    {
        public void Validate()
        {
            if (_behavior == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile requires a non-null IAiBehavior assigned!");
            }

            if (_minGlandRatioToEmbark <= 0f || _minGlandRatioToEmbark > 1.0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile MinGlandRatioToEmbark ({_minGlandRatioToEmbark}) must be in range (0.0, 1.0]!");
            }

            if (_retreatHealthRatio < 0f || _retreatHealthRatio >= 1.0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile RetreatHealthRatio ({_retreatHealthRatio}) must be in range [0.0, 1.0)!");
            }

            if (_leashRadius <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile LeashRadius ({_leashRadius}) must be greater than zero!");
            }

            if (_scanInterval <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile ScanInterval ({_scanInterval}) must be greater than zero!");
            }

            if (_perceptionMultiplier <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile PerceptionMultiplier ({_perceptionMultiplier}) must be greater than zero!");
            }

            if (_arrivalDistance <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{name}] AiStrategyProfile ArrivalDistance ({_arrivalDistance}) must be greater than zero!");
            }
        }
    }
}
