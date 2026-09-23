namespace AntScout.Core.Enums
{
    /// <summary>
    /// Categorizes chemical pheromone scents emitted by scouts or colonies.
    /// </summary>
    public enum PheromoneType
    {
        Recruitment = 0, // Directs workers and soldiers toward dynamic resource bonanzas
        Alarm = 1,       // Alerts allied soldiers to concentrate combat focus on hostiles or heavy prey
        Repellent = 2    // Marks environmental hazards or tactical retreat zones
    }
}
