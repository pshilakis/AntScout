using AntScout.FogOfWar.Core;
using AntScout.FogOfWar.Interfaces;
using UnityEngine;

namespace AntScout.Swarm.Nest
{
    /// <summary>
    /// Partial class implementing IVisionRevealer for ColonyNest structures.
    /// The home nest projects broad vision over the base yard.
    /// </summary>
    public partial class ColonyNest : IVisionRevealer
    {
        public Vector3 RevealerPosition => SpawnPosition;

        public float VisionRadius => FogOfWarManager.Instance != null && FogOfWarManager.Instance.Config != null
            ? FogOfWarManager.Instance.Config.NestVisionRadius
            : 15.0f;

        public bool IsRevealerActive => IsStructureAlive && gameObject.activeInHierarchy;
    }
}
