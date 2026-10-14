using System;

namespace AntScout.Resources.Components
{
    /// <summary>
    /// Partial class containing fail-fast configuration validation for <see cref="FoodSource"/>
    /// (Single Responsibility Principle).
    /// </summary>
    public partial class FoodSource
    {
        private void ValidateConfiguration()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(
                    $"[FoodSource] Missing required ResourceConfigSO on GameObject '{gameObject.name}'. " +
                    $"Please assign a valid ResourceConfigSO in the Inspector.");
            }
        }
    }
}
