using System;

namespace AntScout.Core.Interfaces
{
    /// <summary>
    /// Pure domain contract for any entity possessing secondary energy, chemical reserves, or stamina.
    /// Grounded in Interface Segregation Principle (ISP) and Dependency Inversion Principle (DIP):
    /// Decouples visual presentations (such as diegetic overhead resource bars) from concrete chemical simulation coordinators.
    /// </summary>
    public interface IResourceEntity
    {
        float CurrentResource { get; }
        float MaxResource { get; }
        float NormalizedResource { get; }
        bool IsDepleted { get; }
        bool IsDraining { get; }

        event Action<float, float> OnResourceChanged;
    }
}
