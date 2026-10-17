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
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    $"[{name}] FoodSource: Missing injected ResourceDefinition! " +
                    $"FoodSource prefabs must be spawned and initialized dynamically via Initialize(ResourceDefinition, Vector3, int).");
            }

            if (_units.Max <= 0)
            {
                throw new InvalidOperationException(
                    $"[{name}] FoodSource: Uninitialized capacity units for '{_definition.DisplayName}'! Total units must be greater than 0.");
            }
        }
    }
}
