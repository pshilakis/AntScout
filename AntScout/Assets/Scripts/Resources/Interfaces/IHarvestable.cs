using AntScout.Resources.Enums;
using UnityEngine;

namespace AntScout.Resources.Interfaces
{
    /// <summary>
    /// Contract for harvestable food entities.
    /// Grounded in Interface Segregation Principle (ISP) and Open/Closed Principle (OCP)
    /// to decouple swarm foraging behavior from concrete food types.
    /// </summary>
    public interface IHarvestable
    {
        ResourceType Type { get; }
        int RemainingUnits { get; }
        bool CanHarvest { get; }
        Vector3 WorldPosition { get; }

        /// <summary>
        /// Attempts to harvest a specified amount of food units from the source.
        /// </summary>
        /// <param name="amountRequested">Desired amount to harvest.</param>
        /// <param name="amountHarvested">Actual amount harvested based on remaining supply.</param>
        /// <returns>True if at least 1 unit was harvested; false otherwise.</returns>
        bool TryHarvest(int amountRequested, out int amountHarvested);
    }
}
