using System;
using PGS.Stats;

namespace AntScout.Core.Stats
{
    /// <summary>
    /// Partial class containing fail-fast runtime validation and initialization checks
    /// for <see cref="ColonyVitals"/> (Single Responsibility Principle).
    /// </summary>
    public sealed partial class ColonyVitals
    {
        private void EnsureInitialized()
        {
            if (!_isInitialized || _stats == null || _maxHealthStat == null)
            {
                throw new InvalidOperationException("[ColonyVitals] Vitals accessed before Initialize() was called with valid configuration!");
            }
        }

        private static void ValidateInitializationArgs(
            StatsController statsController,
            in StatsController.InitialStatConfig maxHealthConfig,
            in StatsController.InitialStatConfig defenseConfig,
            in StatsController.InitialStatConfig attackConfig)
        {
            if (statsController == null)
            {
                throw new InvalidOperationException("[ColonyVitals] Missing required StatsController reference!");
            }

            if (maxHealthConfig.StatType == null)
            {
                throw new InvalidOperationException(
                    $"[{statsController.name}] Missing required MaxHealth StatTypeSO in Definition!");
            }

            if (maxHealthConfig.BaseValue <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{statsController.name}] MaxHealth BaseValue ({maxHealthConfig.BaseValue}) in Definition must be greater than zero!");
            }

            if (defenseConfig.StatType == null)
            {
                throw new InvalidOperationException(
                    $"[{statsController.name}] Missing required Defense StatTypeSO in Definition!");
            }

            if (defenseConfig.BaseValue < 0f)
            {
                throw new InvalidOperationException(
                    $"[{statsController.name}] Defense BaseValue ({defenseConfig.BaseValue}) in Definition cannot be negative!");
            }

            if (attackConfig.StatType != null && attackConfig.BaseValue <= 0f)
            {
                throw new InvalidOperationException(
                    $"[{statsController.name}] Attack BaseValue ({attackConfig.BaseValue}) in Definition must be greater than zero!");
            }
        }
    }
}
