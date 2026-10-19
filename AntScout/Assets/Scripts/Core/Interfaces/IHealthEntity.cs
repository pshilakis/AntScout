using System;
using PGS.Effects;

namespace AntScout.Core.Interfaces
{
    /// <summary>
    /// Pure domain contract for any entity possessing vitals, health status, and damage notifications.
    /// Grounded in Interface Segregation Principle (ISP) and Dependency Inversion Principle (DIP):
    /// Decouples visual presentations (such as diegetic overhead healthbars) from concrete gameplay coordinators.
    /// </summary>
    public interface IHealthEntity
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        float NormalizedHealth { get; }
        bool IsAlive { get; }

        event Action<float, float> OnHealthChanged;
        event Action<DamageResult, HitContext> OnDamaged;
    }
}
